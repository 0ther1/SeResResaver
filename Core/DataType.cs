namespace SeResResaver.Core
{
    /// <summary>
    /// Data type kinds
    /// </summary>
    public enum DataTypeKind
    {
        Simple,
        ValueField,
        Pointer,
        Reference,
        Array,
        Struct,
        CStaticArray,
        CStaticStackArray,
        CDynamicContainer,
        Function,
        Void,
        SmartPointer,
        Handle,
        Typedef,
        UniquePointer,
        ScriptState,
        ScriptLatent,
        Unknown,
    }

    /// <summary>
    /// Struct member
    /// </summary>
    public class StructMember
    {
        public string Name { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public DataType? DataType { get; set; }
    }

    /// <summary>
    /// Serious Engine meta data type
    /// </summary>
    public class DataType
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public DataTypeKind Kind { get; set; }
        public int Format { get; set; }
        public int? Size { get; set; }
        public DataType? Pointer { get; set; }
        public int? ArraySize { get; set; }
        public string? Template { get; set; }
        public StructMember[]? Members { get; set; }

        public bool? HasResourceLink { get; private set; }

        private Action<DataType, BinaryMetaParser>? skipFunc;
        private Func<DataType, BinaryMetaParser, IEnumerable<bool>>? skipToResourceLinkFunc;
        private bool sizeIsSet;

        /// <summary>
        /// Skips one object in stream.
        /// </summary>
        /// <param name="parser">Parser.</param>
        public void Skip(BinaryMetaParser parser)
        {
            skipFunc!(this, parser);
        }

        /// <summary>
        /// Skips one struct in stream and returns control when encounters specified struct members.
        /// </summary>
        /// <param name="parser">Parser.</param>
        /// <param name="members">Struct members to not skip.</param>
        /// <returns>Encountered struct member.</returns>
        public IEnumerable<StructMember> SkipToMember(BinaryMetaParser parser, StructMember[] members)
        {
            HashSet<StructMember> hashSet = new(members);

            foreach (var mem in Members!)
            {
                if (hashSet.Contains(mem))
                {
                    yield return mem;
                }
                else
                {
                    mem.DataType!.Skip(parser);
                }
            }
        }

        /// <summary>
        /// Skips one object in stream and returns control when encounters ResourceLink.
        /// </summary>
        /// <param name="parser">Parser.</param>
        /// <returns><c>true</c> if encountered ResourceLink.</returns>
        public IEnumerable<bool> SkipToResourceLink(BinaryMetaParser parser)
        {
            if ((bool)HasResourceLink!)
            {
                foreach (var res in skipToResourceLinkFunc!(this, parser))
                    yield return res;
            }
            else
            {
                skipFunc!(this, parser);
            }
        }

        private static readonly Dictionary<string, int> SIMPLE_SIZES = new()
        {
            { "SBYTE", 1 },
            { "UBYTE", 1 },
            { "SWORD", 2 },
            { "UWORD", 2 },
            { "SLONG", 4 },
            { "ULONG", 4 },
            { "SQUAD", 8 },
            { "UQUAD", 8 },
            { "FLOAT", 4 },
            { "DOUBLE", 8 },
            { "IDENT", 4 },
        };

        public void SetSize()
        {
            if (sizeIsSet) return;

            sizeIsSet = true;

            switch (Kind)
            {
                case DataTypeKind.Simple:
                    {
                        int size;
                        if (SIMPLE_SIZES.TryGetValue(Name, out size))
                            Size = size;
                    }
                    break;
                case DataTypeKind.Array:
                    Pointer!.SetSize();
                    if (Pointer!.Size != null)
                        Size = Pointer!.Size * ArraySize!;
                    break;
                case DataTypeKind.Pointer:
                case DataTypeKind.Reference:
                case DataTypeKind.SmartPointer:
                case DataTypeKind.Handle:
                    Size = 4;
                    break;
                case DataTypeKind.Struct:
                    {
                        int size = 0;
                        if (Pointer != null)
                        {
                            Pointer.SetSize();
                            if (Pointer.Size == null)
                                return;
                            size = (int)Pointer.Size;
                        }

                        foreach (var mem in Members!)
                        {
                            mem.DataType!.SetSize();
                            if (mem.DataType.Size == null)
                                return;
                            size += (int)mem.DataType.Size;
                        }
                    }
                    break;
                case DataTypeKind.Typedef:
                    Pointer!.SetSize();
                    if (Pointer!.Size != null)
                        Size = Pointer!.Size;
                    break;
                case DataTypeKind.Unknown:
                    {
                        int size;
                        if (SIMPLE_SIZES.TryGetValue(Name, out size))
                            Size = size;
                    }
                    break;
                case DataTypeKind.UniquePointer:
                    switch (Template!)
                    {
                        case "UniquePtr":
                            Size = 4;
                            break;
                        case "Synced":
                            Pointer!.SetSize();
                            if (Pointer!.Size != null)
                                Size = Pointer!.Size;
                            break;
                    }
                    break;
            }
        }

        public void SetHasResourceLink()
        {
            if (HasResourceLink != null) return;
            HasResourceLink = false;

            switch (Kind)
            {
                case DataTypeKind.Array:
                case DataTypeKind.CStaticArray:
                case DataTypeKind.CStaticStackArray:
                case DataTypeKind.Typedef:
                    Pointer!.SetHasResourceLink();
                    HasResourceLink = Pointer!.HasResourceLink;
                    break;
                case DataTypeKind.UniquePointer:
                    switch (Template!)
                    {
                        case "Synced":
                            Pointer!.SetHasResourceLink();
                            HasResourceLink = Pointer!.HasResourceLink;
                            break;
                        case "ResourceLink":
                            HasResourceLink = true;
                            break;
                    }
                    break;
                case DataTypeKind.Struct:
                    if (Pointer != null)
                    {
                        Pointer.SetHasResourceLink();
                        if ((bool)Pointer.HasResourceLink!)
                        {
                            HasResourceLink = true;
                            return;
                        }
                    }
                    foreach (var mem in Members!)
                    {
                        mem.DataType!.SetHasResourceLink();
                        if ((bool)mem.DataType.HasResourceLink!)
                        {
                            HasResourceLink = true;
                            return;
                        }
                    }
                    break;

            }
        }

        public void SetFunctions()
        {
            if (DataTypeFunctions.SPECIAL_SKIP_FUNCTIONS.TryGetValue(Name, out var func))
            {
                skipFunc = func;
            }
            else if (Size != null)
            {
                skipFunc = DataTypeFunctions.Skip_Sized;
            }
            else
            {
                switch (Kind)
                {
                    case DataTypeKind.Array:
                        skipFunc = DataTypeFunctions.Skip_Array;
                        break;
                    case DataTypeKind.CStaticArray:
                    case DataTypeKind.CStaticStackArray:
                        skipFunc = DataTypeFunctions.Skip_CArray;
                        break;
                    case DataTypeKind.CDynamicContainer:
                        skipFunc = DataTypeFunctions.Skip_DynamicContainer;
                        break;
                    case DataTypeKind.Struct:
                        skipFunc = DataTypeFunctions.Skip_Struct;
                        break;
                    case DataTypeKind.Typedef:
                        skipFunc = DataTypeFunctions.Skip_Typedef;
                        break;
                    case DataTypeKind.UniquePointer:
                        switch (Template!)
                        {
                            case "ResourceLink":
                                skipFunc = DataTypeFunctions.Skip_String;
                                break;
                            case "Synced":
                                skipFunc = DataTypeFunctions.Skip_Typedef;
                                break;
                            case "CStaticArray2D":
                                skipFunc = DataTypeFunctions.Skip_CStaticArray2D;
                                break;
                        }
                        break;
                }
            }

            switch (Kind)
            {
                case DataTypeKind.Array:
                    skipToResourceLinkFunc = DataTypeFunctions.SkipToResourceLink_Array;
                    break;
                case DataTypeKind.CStaticArray:
                case DataTypeKind.CStaticStackArray:
                    skipToResourceLinkFunc = DataTypeFunctions.SkipToResourceLink_CArray;
                    break;
                case DataTypeKind.Struct:
                    skipToResourceLinkFunc = DataTypeFunctions.SkipToResourceLink_Struct;
                    break;
                case DataTypeKind.Typedef:
                    skipToResourceLinkFunc = DataTypeFunctions.SkipToResourceLink_Typedef;
                    break;
                case DataTypeKind.UniquePointer:
                    switch (Template!)
                    {
                        case "ResourceLink":
                            skipToResourceLinkFunc = DataTypeFunctions.SkipToResourceLink_ResourceLink;
                            break;
                        case "Synced":
                            skipToResourceLinkFunc = DataTypeFunctions.SkipToResourceLink_Typedef;
                            break;
                        case "CStaticArray2D":
                            skipToResourceLinkFunc = DataTypeFunctions.SkipToResourceLink_CStaticArray2D;
                            break;
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Functions for data types.
    /// </summary>
    public class DataTypeFunctions
    {
        /// <summary>
        /// Special cases skip functions.
        /// </summary>
        public static readonly Dictionary<string, Action<DataType, BinaryMetaParser>> SPECIAL_SKIP_FUNCTIONS = new()
        {
            { "CString", Skip_String },
            { "CMetaPointer", Skip_4bytes },
            { "CMetaHandle", Skip_4bytes },
            { "CSyncedSLONG", Skip_4bytes },
            { "CTransString", Skip_TransString },
            { "CBaseTexture", Skip_CBaseTexture },
        };

        public static void Skip_Sized(DataType dt, BinaryMetaParser parser)
        {
            parser.Skip((int)dt.Size!);
        }

        public static void Skip_String(DataType dt, BinaryMetaParser parser)
        {
            parser.SkipString();
        }

        public static void Skip_4bytes(DataType dt, BinaryMetaParser parser)
        {
            parser.Skip(4);
        }

        public static void Skip_Array(DataType dt, BinaryMetaParser parser)
        {
            if (dt.Pointer!.Size != null) parser.Skip((int)dt.ArraySize! * (int)dt.Pointer.Size);
            else
            {
                for (int i = 0; i < dt.ArraySize!; i++)
                    dt.Pointer!.Skip(parser);
            }
        }

        public static IEnumerable<bool> SkipToResourceLink_Array(DataType dt, BinaryMetaParser parser)
        {
            for (int i = 0; i < dt.ArraySize!; i++)
            {
                foreach (var res in dt.Pointer!.SkipToResourceLink(parser))
                {
                    yield return res;
                }
            }
        }

        public static void Skip_CArray(DataType dt, BinaryMetaParser parser)
        {
            parser.Skip(4);
            int count = parser.ReadInt();
            if (dt.Pointer!.Size != null) parser.Skip(count * (int)dt.Pointer.Size);
            else
            {
                for (int i = 0; i < count; i++)
                    dt.Pointer!.Skip(parser);
            }
        }

        public static IEnumerable<bool> SkipToResourceLink_CArray(DataType dt, BinaryMetaParser parser)
        {
            parser.Skip(4);
            int count = parser.ReadInt();

            for (int i = 0; i < count; i++)
            {
                foreach (var res in dt.Pointer!.SkipToResourceLink(parser))
                {
                    yield return res;
                }
            }
        }

        public static void Skip_DynamicContainer(DataType dt, BinaryMetaParser parser)
        {
            parser.Skip(4);
            int count = parser.ReadInt();
            parser.Skip(count * 4);
        }

        public static void Skip_Struct(DataType dt, BinaryMetaParser parser)
        {
            if (dt.Pointer != null) dt.Pointer.Skip(parser);

            foreach (var member in dt.Members!)
                member.DataType!.Skip(parser);
        }

        public static IEnumerable<bool> SkipToResourceLink_Struct(DataType dt, BinaryMetaParser parser)
        {
            if (dt.Pointer != null)
            {
                if ((bool)dt.Pointer.HasResourceLink!)
                {
                    foreach (var res in dt.Pointer.SkipToResourceLink(parser))
                        yield return res;
                }
                else
                {
                    dt.Pointer.Skip(parser);
                }
            }

            foreach (var mem in dt.Members!)
            {
                if ((bool)mem.DataType!.HasResourceLink!)
                {
                    foreach (var res in mem.DataType.SkipToResourceLink(parser))
                        yield return res;
                }
                else
                    mem.DataType.Skip(parser);
            }
        }

        public static void Skip_Typedef(DataType dt, BinaryMetaParser parser)
        {
            dt.Pointer!.Skip(parser);
        }

        public static IEnumerable<bool> SkipToResourceLink_Typedef(DataType dt, BinaryMetaParser parser)
        {
            foreach (var res in dt.Pointer!.SkipToResourceLink(parser))
                yield return res;
        }

        public static void Skip_TransString(DataType dt, BinaryMetaParser parser)
        {
            parser.Skip(4);
            parser.SkipString();
            parser.SkipString();
        }

        public static void Skip_CBaseTexture(DataType dt, BinaryMetaParser parser)
        {
            Skip_Struct(dt, parser);

            if (dt.Format > 26)
            {
                parser.Skip(2);
                int size = parser.ReadInt();
                parser.Skip(size);
            }
        }

        public static IEnumerable<bool> SkipToResourceLink_ResourceLink(DataType dt, BinaryMetaParser parser)
        {
            yield return true;
        }

        public static void Skip_CStaticArray2D(DataType dt, BinaryMetaParser parser)
        {
            parser.Skip(8);
            Skip_CArray(dt, parser);
        }

        public static IEnumerable<bool> SkipToResourceLink_CStaticArray2D(DataType dt, BinaryMetaParser parser)
        {
            parser.Skip(8);
            foreach (var res in SkipToResourceLink_CArray(dt, parser))
                yield return res;
        }
    }
}
