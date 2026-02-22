using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SeResResaver.Core
{
    /// <summary>
    /// Interface for resaving files.
    /// </summary>
    public interface IResaver
    {
        /// <summary>
        /// Resaves a file.
        /// </summary>
        /// <param name="from">Source file.</param>
        /// <param name="to">Destination file.</param>
        /// <param name="renames">A dictionary with renamed files.
        /// <list type="bullet">
        /// <item><description><c>key</c>: File path before rename.</description></item>
        /// <item><description><c>value</c>: File path after rename.</description></item>
        /// </list>
        /// </param>
        /// <param name="newAssetFN">Optional new asset filename. Use this if file being renamed.</param>
        void Resave(Stream from, Stream to, Dictionary<string, string> renames, string? newAssetFN=null);
    }

    /// <summary>
    /// Functions for creating resavers.
    /// </summary>
    public class ResaverFactory
    {
        /// <summary>
        /// Creates a resaver appropriate for the given stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="path">File path used to open file stream.</param>
        /// <returns>A resaver.</returns>
        public static IResaver GetResaver(Stream stream, string path)
        {
            byte[] buffer = new byte[8];
            StreamUtils.Peek(stream, buffer, 0, buffer.Length);

            if (buffer.SequenceEqual(BitConverter.GetBytes(BinaryMetaParser.MAGIC)))
                return new BinaryMetaResaver();
            if (buffer.SequenceEqual(TextMetaResaver.MAGIC))
                return new TextMetaResaver();

            int offset = 0;
            if (buffer.AsSpan(0, 3).SequenceEqual(NfoResaver.BOM))
                offset = 3;

            if (buffer.AsSpan(offset, NfoResaver.MAGIC.Length).SequenceEqual(NfoResaver.MAGIC))
                return new NfoResaver();

            if (path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                return new LuaResaver();

            return new PlainFileResaver();
        }
    }

    /// <summary>
    /// Resaver for binary meta files.
    /// </summary>
    public class BinaryMetaResaver : IResaver
    {
        private long lastFlushPos;

        public void Resave(Stream from, Stream to, Dictionary<string, string> renames, string? newAssetFN = null)
        {
            lastFlushPos = 0;

            Action<BinaryWriter, int> writeIntFunc = WriteIntLE;

            using (BinaryMetaParser parser = new BinaryMetaParser(from))
            using (BinaryWriter writer = new BinaryWriter(to))
            {
                if (parser.BigEndian) writeIntFunc = WriteIntBE;

                if (parser.Version > 9)
                {
                    parser.BeginBlock(BinaryMetaParser.BLOCK_MESSAGES);
                    parser.SkipString();
                }

                parser.BeginBlock(BinaryMetaParser.BLOCK_INFO);
                int skipLength = 8;
                if (parser.Version > 7)
                    skipLength += 4;
                parser.Skip(skipLength);
                int typeCount = parser.ReadInt();
                parser.Skip(4);

                DataType[] dataTypes = new DataType[typeCount];
                for (int i = 0; i < typeCount; i++)
                    dataTypes[i] = new DataType();

                int referencedFileCount = parser.BeginList(BinaryMetaParser.BLOCK_EXTERNAL_FILES);
                for (int i = 0; i < referencedFileCount; i++)
                {
                    parser.Skip(8);
                    long pos = from.Position;

                    string path = parser.ReadString();

                    string? newPath;
                    if (renames.TryGetValue(path, out newPath))
                    {
                        from.Position = pos;
                        Flush(from, to);
                        byte[] buffer = Encoding.UTF8.GetBytes(newPath);
                        writeIntFunc(writer, buffer.Length);

                        to.Write(buffer, 0, buffer.Length);

                        lastFlushPos = from.Position = lastFlushPos + path.Length + 4;
                    }
                }

                int idCount = parser.BeginList(BinaryMetaParser.BLOCK_IDS);
                for (int i = 0; i < idCount; i++)
                {
                    parser.Skip(4);
                    parser.SkipString();
                }

                int externalTypeCount = parser.BeginList(BinaryMetaParser.BLOCK_EXTERNAL_TYPES);
                for (int i = 0; i < externalTypeCount; i++)
                {
                    int index = parser.ReadInt();
                    DataType dt = dataTypes[index];
                    dt.Index = index;
                    dt.Name = parser.ReadString();
                    dt.Kind = DataTypeKind.Unknown;
                }

                List<StructMember> resourceFileMembers = new();
                bool hasResourceLink = false;
                int internalTypeCount = parser.BeginList(BinaryMetaParser.BLOCK_INTERNAL_TYPES);
                for (int i = 0; i < internalTypeCount; i++)
                {
                    parser.BeginBlock(BinaryMetaParser.BLOCK_DATA_TYPE);
                    int index = parser.ReadInt();
                    DataType dt = dataTypes[index];
                    dt.Index = index;
                    dt.Name = parser.ReadString();
                    dt.Format = parser.ReadInt();
                    dt.Kind = (DataTypeKind)parser.ReadInt();

                    switch (dt.Kind)
                    {
                        case DataTypeKind.Simple:
                            parser.Skip(4);
                            dt.Size = parser.ReadInt();
                            break;
                        case DataTypeKind.ValueField:
                            dt.Size = parser.ReadInt();
                            break;
                        case DataTypeKind.Pointer:
                        case DataTypeKind.Reference:
                        case DataTypeKind.CStaticArray:
                        case DataTypeKind.CStaticStackArray:
                        case DataTypeKind.CDynamicContainer:
                        case DataTypeKind.SmartPointer:
                        case DataTypeKind.Handle:
                        case DataTypeKind.Typedef:
                            dt.Pointer = dataTypes[parser.ReadInt()];
                            break;
                        case DataTypeKind.Array:
                            dt.Pointer = dataTypes[parser.ReadInt()];
                            parser.Skip(8);
                            dt.ArraySize = parser.ReadInt();
                            break;
                        case DataTypeKind.Struct:
                            {
                                int baseIndex = parser.ReadInt();
                                int memberCount = parser.BeginList(BinaryMetaParser.BLOCK_STRUCT_MEMBERS);
                                if (baseIndex != -1)
                                    dt.Pointer = dataTypes[baseIndex];
                                dt.Members = new StructMember[memberCount];

                                for (int j = 0; j < memberCount; j++)
                                {
                                    StructMember member = new StructMember();
                                    dt.Members[j] = member;

                                    if (parser.Version < 11)
                                    {
                                        if (parser.Version < 5)
                                            member.Id = parser.ReadString();
                                        member.Name = parser.ReadString();
                                    }
                                    else
                                    {
                                        member.Id = parser.ReadInt().ToString();
                                    }

                                    member.DataType = dataTypes[parser.ReadInt()];

                                    if (dt.Name == "CResourceFile" && (member.Id == "14" || member.Name == "14" || member.Id == "7" || member.Name == "7"))
                                    {
                                        resourceFileMembers.Add(member);
                                    }

                                        
                                }
                            }
                            break;
                        case DataTypeKind.UniquePointer:
                            dt.Template = parser.ReadString();
                            dt.Pointer = dataTypes[parser.ReadInt()];
                            hasResourceLink = hasResourceLink || dt.Template == "ResourceLink";
                            break;
                        default:
                            throw new Exception($"Unexpected data type kind {dt.Kind}");
                    }
                }

                if (resourceFileMembers.Count < 1 && !hasResourceLink)
                {
                    from.Seek(0, SeekOrigin.End);
                    Flush(from, to);
                    return;
                }

                foreach (var dt in dataTypes)
                {
                    dt.SetSize();
                    dt.SetHasResourceLink();
                }
                foreach (var dt in dataTypes)
                    dt.SetFunctions();

                int externalObjectCount = parser.BeginList(BinaryMetaParser.BLOCK_EXTERNAL_OBJECTS);
                for (int i = 0; i < externalObjectCount; i++)
                {
                    parser.Skip(8);
                    if (parser.Version > 8)
                    {
                        int obtainType = parser.ReadInt();
                        switch (obtainType)
                        {
                            case 0:
                                parser.Skip(4);
                                break;
                            case 1:
                                parser.SkipString();
                                break;
                            default:
                                throw new Exception($"Unexpected obtain type {obtainType}");
                        }
                    }
                    else
                    {
                        parser.Skip(4);
                    }

                    parser.Skip(4);
                }

                int internalObjectTypesCount = parser.BeginList(BinaryMetaParser.BLOCK_INTERNAL_OBJECT_TYPES);
                for (int i = 0; i < internalObjectTypesCount; i++)
                {
                    parser.Skip(8);
                }

                int editObjectTypesCount = parser.BeginList(BinaryMetaParser.BLOCK_EDIT_OBJECT_TYPES);
                for (int i = 0; i < editObjectTypesCount; i++)
                {
                    parser.Skip(8);
                }

                int internalObjectCount = parser.BeginList(BinaryMetaParser.BLOCK_INTERNAL_OBJECTS);

                if (internalObjectCount > 0)
                {
                    parser.Skip(4);
                    DataType type = dataTypes[parser.ReadInt()];
                    if (resourceFileMembers.Count > 0 && newAssetFN != null)
                    {
                        foreach (var mem in type.SkipToMember(parser, resourceFileMembers.ToArray()))
                        {
                            Flush(from, to);

                            if (mem.Id == "14" || mem.Name == "14")
                            {
                                parser.SkipString();
                                byte[] buffer = Encoding.UTF8.GetBytes(newAssetFN);
                                writeIntFunc(writer, buffer.Length);
                                to.Write(buffer);
                            }
                            else if (mem.Id == "7" || mem.Name == "7")
                            {
                                uint newId = (uint)Random.Shared.NextInt64(0, uint.MaxValue);
                                parser.Skip(4);
                                writeIntFunc(writer, (int)newId);
                            }

                            lastFlushPos = from.Position;
                        }
                    }
                    else
                    {
                        type.Skip(parser);
                    }
                }

                if (!hasResourceLink)
                {
                    from.Seek(0, SeekOrigin.End);
                    Flush(from, to);
                    return;
                }

                for (int i = 1; i < internalObjectCount; i++)
                {
                    parser.Skip(4);
                    DataType type = dataTypes[parser.ReadInt()];
                    if ((bool)type.HasResourceLink!)
                    {
                        foreach (var _ in type.SkipToResourceLink(parser))
                        {
                            long pos = from.Position;

                            string path = parser.ReadString();

                            string? newPath;
                            if (renames.TryGetValue(path, out newPath))
                            {
                                from.Position = pos;
                                Flush(from, to);
                                byte[] buffer = Encoding.UTF8.GetBytes(newPath);
                                writeIntFunc(writer, buffer.Length);

                                to.Write(buffer, 0, buffer.Length);

                                lastFlushPos = from.Position = lastFlushPos + path.Length + 4;
                            }
                        }
                    }
                    else
                    {
                        type.Skip(parser);
                        DataType? baseTexture = type.GetStructBase("CBaseTexture");
                        if (baseTexture?.Format > 26)
                        {
                            parser.Skip(2);
                            int size = parser.ReadInt();
                            parser.Skip(size);
                        }
                    }
                }

                int editObjectCount = parser.BeginList(BinaryMetaParser.BLOCK_EDIT_OBJECTS);
                for (int i = 0; i < editObjectCount; i++)
                {
                    parser.Skip(4);
                    DataType type = dataTypes[parser.ReadInt()];
                    if ((bool)type.HasResourceLink!)
                    {
                        foreach (var _ in type.SkipToResourceLink(parser))
                        {
                            long pos = from.Position;

                            string path = parser.ReadString();

                            string? newPath;
                            if (renames.TryGetValue(path, out newPath))
                            {
                                from.Position = pos;
                                Flush(from, to);
                                byte[] buffer = Encoding.UTF8.GetBytes(newPath);
                                writeIntFunc(writer, buffer.Length);

                                to.Write(buffer, 0, buffer.Length);

                                lastFlushPos = from.Position = lastFlushPos + path.Length + 4;
                            }
                        }
                    }
                    else
                    {
                        type.Skip(parser);
                    }
                }

                from.Seek(0, SeekOrigin.End);

                Flush(from, to);
            }
        }

        private void Flush(Stream from, Stream to)
        {
            long toFlush = from.Position - lastFlushPos;
            if (toFlush < 1) return;

            from.Position = lastFlushPos;

            int bufferSize = (int)Math.Min(81920, toFlush);
            byte[] buffer = new byte[bufferSize];

            while (toFlush > 0)
            {
                int toRead = (int)Math.Min(bufferSize, toFlush);
                int bytesRead = from.Read(buffer, 0, toRead);

                if (bytesRead == 0)
                    break;

                to.Write(buffer, 0, bytesRead);
                toFlush -= bytesRead;
            }

            lastFlushPos = from.Position;
        }

        private static void WriteIntLE(BinaryWriter writer, int value)
        {
            writer.Write(value);
        }

        private static void WriteIntBE(BinaryWriter writer, int value)
        {
            writer.Write(BinaryPrimitives.ReverseEndianness(value));
        }
    }

    /// <summary>
    /// Resaver for text meta files.
    /// </summary>
    public class TextMetaResaver : IResaver
    {
        public static readonly byte[] MAGIC = { 0x4D, 0x65, 0x74, 0x61, 0x54, 0x65, 0x78, 0x74 };

        public void Resave(Stream from, Stream to, Dictionary<string, string> renames, string? newAssetFN = null)
        {
            using (StreamReader reader = new StreamReader(from))
            using (StreamWriter writer = new StreamWriter(to))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] split = line.Split('=', 2);
                    if (split.Length == 2)
                    {
                        if (newAssetFN != null)
                        {
                            if (split[0].Contains("rf_strAssetFN"))
                            {
                                writer.WriteLine($"{split[0]}= \"{newAssetFN}\";");
                                continue;
                            }
                            else if (split[0].Contains("rf_ulAssetUID"))
                            {
                                uint newId = (uint)Random.Shared.NextInt64(0, uint.MaxValue);
                                writer.WriteLine($"{split[0]}= {newId};");
                                continue;
                            }
                        }

                        if (split[1].Contains("@'"))
                        {
                            int start = split[1].IndexOf('\'') + 1;
                            int end = split[1].IndexOf('\'', start);
                            string path = split[1].Substring(start, end - start);

                            string? newPath;
                            if (renames.TryGetValue(path, out newPath))
                            {
                                writer.WriteLine($"{split[0]}= @'{newPath}';");
                                continue;
                            }
                        }
                    }

                    writer.WriteLine(line);
                }
            }
        }
    }

    /// <summary>
    /// Resaver for *.nfo files.
    /// </summary>
    public class NfoResaver : IResaver
    {
        public static readonly byte[] BOM = { 0xEF, 0xBB, 0xBF };
        public static readonly byte[] MAGIC = { 0x4C, 0x45, 0x56, 0x45, 0x4C };

        public static readonly HashSet<string> KEYS = new()
        {
            "LOADING_SCREEN",
            "THUMBNAIL",
            "INTRO_CUTSCENE_WORLD",
            "NETRICSA"
        };

        public void Resave(Stream from, Stream to, Dictionary<string, string> renames, string? newAssetFN = null)
        {
            byte[] buffer = new byte[3];
            StreamUtils.Peek(from, buffer, 0, 3);
            if (buffer.SequenceEqual(BOM))
            {
                StreamUtils.Skip(from, 3);
                to.Write(buffer, 0, 3);
            }

            using (StreamReader reader = new StreamReader(from))
            using (StreamWriter writer = new StreamWriter(to))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] split = line.Split('=', 2);
                    if (split.Length == 2)
                    {
                        if (KEYS.Contains(split[0]))
                        {
                            int start = split[1].IndexOf('"') + 1;
                            int end = split[1].IndexOf('"', start);
                            string path = split[1].Substring(start, end - start);

                            string? newPath;
                            if (renames.TryGetValue(path, out newPath))
                            {
                                writer.WriteLine($"{split[0]}=\"{newPath}\"");
                                continue;
                            }
                        }
                    }

                    writer.WriteLine(line);
                }
            }
        }
    }

    /// <summary>
    /// Resaver for *.lua files.
    /// </summary>
    public partial class LuaResaver : IResaver
    {
        [GeneratedRegex(@"(?<function>LoadResource|dofile)\s*\(\s*[""']?(?<path>[^""')]+)[""']?\s*\)")]
        public static partial Regex FuncPattern();

        public void Resave(Stream from, Stream to, Dictionary<string, string> renames, string? newAssetFN = null)
        {
            byte[] buffer = new byte[3];
            StreamUtils.Peek(from, buffer, 0, 3);
            if (buffer.SequenceEqual(NfoResaver.BOM))
            {
                StreamUtils.Skip(from, 3);
                to.Write(buffer, 0, 3);
            }

            using (StreamReader reader = new StreamReader(from))
            using (StreamWriter writer = new StreamWriter(to))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    var matches = FuncPattern().Matches(line);
                    bool renamed = false;
                    foreach (Match match in matches)
                    {
                        if (match.Groups.Count > 1)
                        {
                            string path = match.Groups["path"].Value.Trim();

                            string? newPath;
                            if (renames.TryGetValue(path, out newPath))
                            {
                                string func = match.Groups["function"].Value;
                                string prefix = line.Substring(0, match.Index);

                                int suffixStart = match.Index + match.Length;
                                string suffix = line.Substring(suffixStart, line.Length - suffixStart);

                                writer.WriteLine($"{prefix}{func}(\"{newPath}\"){suffix}");
                                renamed = true;
                            }
                        }
                    }

                    if (renamed) continue;

                    writer.WriteLine(line);
                }
            }
        }
    }

    /// <summary>
    /// Resaver for plain files.
    /// </summary>
    public class PlainFileResaver : IResaver
    {
        public void Resave(Stream from, Stream to, Dictionary<string, string> renames, string? newAssetFN = null)
        {
            using (from)
            using (to)
            {
                from.CopyTo(to);
            }
        }
    }
}
