using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace SeResResaver.Core
{
    /// <summary>
    /// Binary meta file parser.
    /// </summary>
    public class BinaryMetaParser : IDisposable
    {
        public const ulong MAGIC = 0x4154454d45535443;
        public const uint BLOCK_MESSAGES = 0x5347534D;
        public const uint BLOCK_INFO = 0x4f464e49;
        public const uint BLOCK_EXTERNAL_FILES = 0x4c494652;
        public const uint BLOCK_IDS = 0x544E4449;
        public const uint BLOCK_EXTERNAL_TYPES = 0x59545845;
        public const uint BLOCK_INTERNAL_TYPES = 0x59544E49;
        public const uint BLOCK_DATA_TYPE = 0x59545444;
        public const uint BLOCK_STRUCT_MEMBERS = 0x424D5453;
        public const uint BLOCK_EXTERNAL_OBJECTS = 0x424F5845;
        public const uint BLOCK_INTERNAL_OBJECT_TYPES = 0x5954424F;
        public const uint BLOCK_EDIT_OBJECT_TYPES = 0x59544445;
        public const uint BLOCK_INTERNAL_OBJECTS = 0x534A424F;
        public const uint BLOCK_EDIT_OBJECTS = 0x424F4445;

        private delegate int ReadIntFunc(BinaryReader reader);

        private readonly BinaryReader reader;
        private bool disposedValue;
        private ReadIntFunc readIntFunc;
        
        public int Version { get; private set; }
        public bool BigEndian => readIntFunc == ReadIntBE;
        public BinaryReader Reader => reader;

        /// <summary>
        /// Creates a new parser from the stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <exception cref="Exception"></exception>
        public BinaryMetaParser(Stream stream)
        {
            reader = new BinaryReader(stream);

            StreamUtils.Expect(reader, MAGIC);

            readIntFunc = ReadIntLE;

            uint cookie = reader.ReadUInt32();
            if (cookie == 0xCDAB3412)
                readIntFunc = ReadIntBE;
            else if (cookie != 0x1234ABCD)
                throw new Exception($"Invalid endianness cookie {cookie}");

            Version = ReadInt();

            if (Version > 1)
                ReadString();
        }

        /// <summary>
        /// Reads a 4-byte integer.
        /// </summary>
        /// <returns>4-byte integer.</returns>
        public int ReadInt()
        {
            return readIntFunc(reader);
        }

        /// <summary>
        /// Reads a string.
        /// </summary>
        /// <returns>A string.</returns>
        public string ReadString()
        {
            int length = ReadInt();
            if (length < 1) return "";

            byte[] buffer = new byte[length];
            reader.Read(buffer, 0, length);
            return Encoding.UTF8.GetString(buffer);
        }

        /// <summary>
        /// Skips bytes.
        /// </summary>
        /// <param name="count">Amount of bytes to skip.</param>
        public void Skip(int count)
        {
            StreamUtils.Skip(reader.BaseStream, count);
        }

        /// <summary>
        /// Skips a string.
        /// </summary>
        public void SkipString()
        {
            int length = ReadInt();
            StreamUtils.Skip(reader.BaseStream, length);
        }

        /// <summary>
        /// Begins given block.
        /// </summary>
        /// <param name="magic">4-byte magic number.</param>
        public void BeginBlock(uint magic)
        {
            StreamUtils.Expect(reader, magic);
        }

        /// <summary>
        /// Begins given list.
        /// </summary>
        /// <param name="magic">4-byte magic number.</param>
        /// <returns>List length.</returns>
        public int BeginList(uint magic)
        {
            BeginBlock(magic);
            return ReadInt();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    reader.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private static int ReadIntLE(BinaryReader reader)
        {
            return reader.ReadInt32();
        }

        private static int ReadIntBE(BinaryReader reader)
        {
            return BinaryPrimitives.ReverseEndianness(reader.ReadInt32());
        }
    }
}
