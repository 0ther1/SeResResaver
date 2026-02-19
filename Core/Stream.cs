using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace SeResResaver.Core
{
    /// <summary>
    /// Useful functions for Stream, BinaryReader and BinaryWriter
    /// </summary>
    public class StreamUtils
    {
        /// <summary>
        /// Read string from the stream in Serious Engine format.
        /// </summary>
        /// <param name="r">Reader.</param>
        /// <returns>A string from the stream.</returns>
        public static string ReadString(BinaryReader r)
        {
            int length = r.ReadInt32();
            if (length < 1) return "";

            byte[] buffer = new byte[length];
            r.Read(buffer, 0, length);

            return Encoding.UTF8.GetString(buffer);
        }

        /// <summary>
        /// Write string to the stream in Serious Engine format.
        /// </summary>
        /// <param name="w">Writer.</param>
        /// <param name="s">String.</param>
        public static void WriteString(BinaryWriter w, string s)
        {
            w.Write(s.Length);
            byte[] raw = Encoding.UTF8.GetBytes(s);
            w.Write(raw);
        }

        /// <summary>
        /// Expect next uint from the stream to be equal to expected value.
        /// </summary>
        /// <param name="r">Reader.</param>
        /// <param name="expected">Expected value.</param>
        /// <exception cref="InvalidDataException"></exception>
        public static void Expect(BinaryReader r, uint expected)
        {
            uint got = r.ReadUInt32();
            if (expected != got)
                throw new InvalidDataException($"Expected {expected} but got {got}");
        }

        /// <summary>
        /// Expect next ulong from the stream to be equal to expected value.
        /// </summary>
        /// <param name="r">Reader.</param>
        /// <param name="expected">Expected value.</param>
        /// <exception cref="InvalidDataException"></exception>
        public static void Expect(BinaryReader r, ulong expected)
        {
            ulong got = r.ReadUInt64();
            if (expected != got)
                throw new InvalidDataException($"Expected {expected} but got {got}");
        }

        /// <summary>
        /// Read from the stream without advancing position.
        /// </summary>
        /// <param name="s">Stream.</param>
        /// <param name="buffer">Buffer.</param>
        /// <param name="offset">Buffer offset.</param>
        /// <param name="count">Byte count.</param>
        /// <returns>The total number of bytes read.</returns>
        public static int Peek(Stream s, byte[] buffer, int offset, int count)
        {
            var pos = s.Position;
            int result = s.Read(buffer, offset, count);
            s.Seek(pos, SeekOrigin.Begin);
            return result;
        }

        /// <summary>
        /// Skip bytes from the stream.
        /// </summary>
        /// <param name="s">Stream.</param>
        /// <param name="count">Byte count.</param>
        /// <exception cref="EndOfStreamException"></exception>
        public static void Skip(Stream s, int count)
        {
            if (s.CanSeek)
            {
                s.Seek(count, SeekOrigin.Current);
            }
            else
            {
                byte[] buffer = new byte[Math.Min(count, 8192)];

                int remaining = count;
                while (remaining > 0)
                {
                    int toRead = Math.Min(buffer.Length, remaining);
                    int read = s.Read(buffer, 0, toRead);

                    if (read == 0)
                        throw new EndOfStreamException("Skipping past end of stream");

                    remaining -= read;
                }
            }
        }

        /// <summary>
        /// Skip a string from the stream.
        /// </summary>
        /// <param name="r">Reader.</param>
        public static void SkipString(BinaryReader r)
        {
            int length = r.ReadInt32();
            Skip(r.BaseStream, length);
        }
    }

    /// <summary>
    /// Functions for opening streams.
    /// </summary>
    public class StreamFactory
    {
        private static readonly byte[] SIGNED_STREAM_MAGIC = { 0x53, 0x49, 0x47, 0x53, 0x54, 0x52, 0x4D, 0x31 };
        private static readonly byte[] STREAM_WRECKER_MAGIC = { 0x57, 0x52, 0x4B, 0x53, 0x54, 0x52, 0x4D, 0x31 };
        private static readonly byte[] INFO_STREAM_MAGIC = { 0x49, 0x4E, 0x46, 0x53, 0x54, 0x52, 0x4D, 0x31 };

        /// <summary>
        /// Opens an existing file for reading.
        /// </summary>
        /// <param name="path">The file to be opened for reading.</param>
        /// <returns>A read-only stream.</returns>
        public static Stream OpenRead(string path)
        {
            Stream stream = File.OpenRead(path);

            byte[] magic = new byte[8];
            while (true)
            {
                if (StreamUtils.Peek(stream, magic, 0, 8) != 8) break;

                if (magic.SequenceEqual(SIGNED_STREAM_MAGIC))
                {
                    StreamUtils.Skip(stream, 8);
                    stream = new SignedStream(stream);
                }
                else if (magic.SequenceEqual(STREAM_WRECKER_MAGIC))
                {
                    StreamUtils.Skip(stream, 8);
                    stream = new StreamWrecker(stream, true);
                }
                else if (magic.SequenceEqual(INFO_STREAM_MAGIC))
                {
                    StreamUtils.Skip(stream, 8);
                    using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true))
                    {
                        StreamUtils.SkipString(reader);
                    }
                }
                else
                {
                    break;
                }
            }

            return stream;
        }

        /// <summary>
        /// Opens a signed stream on top of another stream.
        /// </summary>
        /// <param name="stream">Base stream.</param>
        /// <param name="version">Signed stream version.</param>
        /// <param name="key">DER-encoded RSA private key.</param>
        /// <returns>Signed stream instance.</returns>
        public static Stream AddSignedStream(Stream stream, int version, byte[] key)
        {
            stream.Write(SIGNED_STREAM_MAGIC, 0, 8);
            return new SignedStream(stream, version, key);
        }

        /// <summary>
        /// Opens a stream wrecker on top of another stream.
        /// </summary>
        /// <param name="stream">Base stream.</param>
        /// <returns>Stream wrecker instance.</returns>
        public static Stream AddStreamWrecker(Stream stream)
        {
            stream.Write(STREAM_WRECKER_MAGIC, 0, 8);
            return new StreamWrecker(stream, false);
        }
    }

    /// <summary>
    /// Serious Engine signed stream.
    /// </summary>
    public class SignedStream : Stream
    {
        private const uint MAGIC = 0x53494732;
        private const int LATEST_VERSION = 5;
        private const string SIGNKEY_EDITOR = "Signkey.EditorSignature";

        private readonly Stream baseStream;
        private readonly bool readable;

        private long dataStart;
        private int curBlock;
        private int blockCount;
        private int blockSize;
        private int digestSize;
        private int signatureSize;
        private int nonce;

        private MemoryStream block;
        private RsaPssSigner? signer;

        public override bool CanRead => readable;

        public override bool CanSeek => true;

        public override bool CanWrite => !readable;

        public override long Length => baseStream.Length - dataStart - (digestSize + signatureSize) * blockCount;

        public override long Position 
        {
            get => blockSize * curBlock + block.Position;
            set => Seek(value, SeekOrigin.Begin);
        }

        public override void Flush()
        {
            FlushBlock();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!CanRead)
                throw new NotSupportedException("Signed stream is not readable");
            if (curBlock >= blockCount - 1 && block.Position >= block.Length)
                return 0;

            int bytesRead = 0;
            while (bytesRead < count)
            {
                long canRead = block.Length - block.Position;
                int toRead = (int)Math.Min(count-bytesRead, canRead);

                block.Read(buffer, offset+bytesRead, toRead);
                bytesRead += toRead;

                if (block.Position >= block.Length)
                {
                    if (curBlock >= blockCount - 1)
                        break;
                    ReadBlock(curBlock + 1);
                }
            }

            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.Current)
            {
                offset += blockSize * curBlock + block.Position;
            }
            else if (origin == SeekOrigin.End)
            {
                if (offset > 0)
                    offset = 0;

                offset += Length;
            }

            int newBlock = (int)(offset / blockSize);
            if (curBlock != newBlock)
                ReadBlock(newBlock);

            block.Position = offset % blockSize;

            int signedBlockSize = blockSize + digestSize + signatureSize;
            baseStream.Seek(dataStart + signedBlockSize * curBlock + block.Position, SeekOrigin.Begin);

            return Position;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!CanWrite)
                throw new NotSupportedException("Signed stream is not writeable");

            int bytesWritten = 0;
            while (bytesWritten < count)
            {
                long canWrite = blockSize - block.Position;
                int toWrite = (int)Math.Min(count - bytesWritten, canWrite);

                block.Write(buffer, offset + bytesWritten, toWrite);
                bytesWritten += toWrite;

                if (block.Position >= blockSize)
                    FlushBlock();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (CanWrite)
                    FlushBlock();
                baseStream.Dispose();
                block?.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Opens a new signed stream for reading.
        /// </summary>
        /// <param name="stream">Base stream.</param>
        public SignedStream(Stream stream)
        {
            baseStream = stream;
            readable = true;
            ReadHeader();

            long length = baseStream.Length - dataStart;
            long signedBlockSize = blockSize + digestSize + signatureSize;

            blockCount = (int)(length / signedBlockSize);
            if (length % signedBlockSize > 0)
                blockCount++;

            block = new MemoryStream(blockSize);
            curBlock = -1;

            ReadBlock(0);
        }

        /// <summary>
        /// Opens a new signed stream for writing. Should not be used manually - use StreamFactory instead.
        /// </summary>
        /// <param name="stream">Base stream.</param>
        /// <param name="version">Stream version.</param>
        /// <param name="key">DER-encoded RSA private key.</param>
        public SignedStream(Stream stream, int version, byte[] key)
        {
            baseStream = stream;
            readable = false;
            blockSize = 0x10000;
            signatureSize = 0x100;
            signer = new RsaPssSigner(RsaPssSigner.HashMethod.SHA1, key);
            nonce = RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);
            block = new MemoryStream(blockSize);
            blockCount = 1;
            WriteHeader(version);
        }

        private void ReadHeader()
        {
            using (var reader = new BinaryReader(baseStream, Encoding.UTF8, true))
            {
                StreamUtils.Expect(reader, MAGIC);

                int version = reader.ReadInt32();
                if (version > LATEST_VERSION)
                    throw new InvalidDataException($"Unexpected signed stream version {version}");

                blockSize = reader.ReadInt32();
                blockSize = Math.Min(Math.Max(0, blockSize), 0x80000);

                StreamUtils.Skip(baseStream, 4);

                digestSize = reader.ReadInt32();
                digestSize = Math.Min(Math.Max(0, digestSize), 0x1000);

                StreamUtils.Skip(baseStream, 4);

                if (version > 1)
                    StreamUtils.Skip(baseStream, 4);

                if (version > 2)
                    StreamUtils.Skip(baseStream, 4);

                if (version > 4)
                    StreamUtils.SkipString(reader);

                signatureSize = reader.ReadInt32();

                if (signatureSize > 0)
                    StreamUtils.SkipString(reader);
            }

            StreamUtils.Skip(baseStream, signatureSize+digestSize);

            dataStart = baseStream.Position;
        }

        private void WriteHeader(int version)
        {
            byte[] signature;

            using (MemoryStream headerStream = new MemoryStream(128))
            using (BinaryWriter writer = new BinaryWriter(headerStream))
            {
                writer.Write(version);
                writer.Write(blockSize);
                writer.Write((int)RsaPssSigner.HashMethod.SHA1);
                writer.Write(digestSize);
                writer.Write(nonce);

                if (version > 1)
                    writer.Write(0);

                if (version > 2)
                    writer.Write(0);

                writer.Write(signatureSize);
                writer.Write(Encoding.UTF8.GetBytes(SIGNKEY_EDITOR));

                byte[] buffer = headerStream.GetBuffer();
                signature = signer!.Sign(buffer, 0, (int)headerStream.Length);
            }

            using (BinaryWriter writer = new BinaryWriter(baseStream, Encoding.UTF8, true))
            {
                writer.Write(MAGIC);
                writer.Write(version);
                writer.Write(blockSize);
                writer.Write((int)RsaPssSigner.HashMethod.SHA1);
                writer.Write(digestSize);
                writer.Write(nonce);

                if (version > 1)
                    writer.Write(0);

                if (version > 2)
                    writer.Write(0);

                if (version > 4)
                    writer.Write(0);

                writer.Write(signatureSize);
                StreamUtils.WriteString(writer, SIGNKEY_EDITOR);
            }

            baseStream.Write(signature);
        }

        private void ReadBlock(int blockNum)
        {
            curBlock = blockNum;
            block.Position = 0;

            baseStream.Seek(dataStart + (blockSize + digestSize + signatureSize) * blockNum, SeekOrigin.Begin);

            long length = baseStream.Length;
            long canRead = length - baseStream.Position - digestSize - signatureSize;
            long toRead = (int)Math.Min(blockSize, canRead);

            if (canRead < 0)
                throw new Exception("No block data in signed stream");

            byte[] buffer = new byte[toRead];
            baseStream.Read(buffer, 0, (int)toRead);

            block?.Dispose();
            block = new MemoryStream(buffer);

            StreamUtils.Skip(baseStream, digestSize + signatureSize);
        }

        private void FlushBlock()
        {
            if (block.Length < 1) return;

            int salt = nonce ^ (curBlock + 0x0B1B);
            byte[] saltBytes = BitConverter.GetBytes(salt);
            signer!.Update(saltBytes);

            byte[] buffer = block.GetBuffer();
            byte[] signature = signer!.Sign(buffer, 0, (int)block.Length);
            baseStream.Write(buffer, 0, (int)block.Length);
            baseStream.Write(signature);

            block.SetLength(0);
            block.Position = 0;
            curBlock++;
            blockCount++;
        }
    }

    /// <summary>
    /// Serious Engine stream wrecker.
    /// </summary>
    public class StreamWrecker : Stream
    {
        private const uint MAGIC = 0x6C720D60;
        private static uint NUM1 = 0x12345678;
        private static uint NUM2 = 0x87654321;

        private readonly Stream baseStream;
        private readonly bool readable;

        private long dataStart;
        private int blockPos;
        private int curBlock;
        private List<int> blockSizes = new();

        public override bool CanRead => readable;

        public override bool CanSeek => true;

        public override bool CanWrite => !readable;

        public override long Length => baseStream.Length - dataStart - 8 * (blockSizes.Count - 1);

        public override long Position
        { 
            get
            {
                long offset = 0;
                for (int i = 0; i < curBlock; i++)
                    offset += blockSizes[i];
                return offset + blockPos;
            }
            set => Seek(value, SeekOrigin.Begin); 
        }

        public override void Flush()
        {
            baseStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!CanRead)
                throw new NotSupportedException("Stream wrecker is not readable");
            if (curBlock >= blockSizes.Count - 1 && blockPos >= blockSizes[blockSizes.Count - 1])
                return 0;

            int bytesRead = 0;
            while (bytesRead < count)
            {
                int blockSize = blockSizes[curBlock];
                long canRead = blockSize - blockPos;
                int toRead = (int)Math.Min(count - bytesRead, canRead);

                baseStream.Read(buffer, offset + bytesRead, toRead);
                bytesRead += toRead;
                blockPos += toRead;

                if (blockPos >= blockSize)
                {
                    if (curBlock >= blockSizes.Count - 1)
                        break;
                    StreamUtils.Skip(baseStream, 8);
                    blockPos = 0;
                    curBlock++;
                }
            }

            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.Current)
            {
                for (int i = 0; i < curBlock; i++)
                    offset += blockSizes[i];
                offset += blockPos;
            }
            else if (origin == SeekOrigin.End)
            {
                if (offset > 0)
                    offset = 0;

                offset += Length;
            }

            long streamSize = 0;
            int blockSize;
            curBlock = -1;

            for (int i = 0; i < blockSizes.Count; i++)
            {
                blockSize = blockSizes[i];

                if (offset < streamSize + blockSize)
                {
                    curBlock = i;
                    blockPos = (int)(offset - streamSize);
                    break;
                }

                streamSize += blockSize;
            }

            if (curBlock == -1)
            {
                curBlock = blockSizes.Count-1;
                blockPos = blockSizes[curBlock];
            }

            baseStream.Seek(dataStart + offset + 8 * curBlock, SeekOrigin.Begin);

            return Position;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!CanWrite)
                throw new NotSupportedException("Stream wrecker is not writeable");

            int blockSize = blockSizes[curBlock];
            int bytesWritten = 0;
            while (bytesWritten < count)
            {
                long canWrite = blockSize - blockPos;
                int toWrite = (int)Math.Min(count - bytesWritten, canWrite);

                baseStream.Write(buffer, offset + bytesWritten, toWrite);
                bytesWritten += toWrite;
                blockPos += toWrite;

                if (blockPos >= blockSize)
                {
                    curBlock++;
                    blockPos = 0;
                    blockSize = GenerateBlockSize();
                    blockSizes.Add(blockSize);
                    WriteBlockSize(blockSize);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (CanWrite)
                    Flush();
                baseStream.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Opens a new stream wrecker. Should not be used manually - use StreamFactory instead.
        /// </summary>
        /// <param name="stream">Base stream.</param>
        /// <param name="read">true to open the stream for reading; otherwise opens the stream for writing.</param>
        public StreamWrecker(Stream stream, bool read)
        {
            baseStream = stream;
            readable = read;

            if (read)
                ReadHeader();
            else
                WriteHeader();
        }

        private void ReadHeader()
        {
            using (BinaryReader reader = new BinaryReader(baseStream, Encoding.UTF8, true))
            {
                StreamUtils.Expect(reader, MAGIC);

                int blockSize = ReadBlockSize();
                dataStart = baseStream.Position;

                blockSizes.Add(blockSize);

                long streamLength = baseStream.Length;
                while (true)
                {
                    long pos = baseStream.Position;
                    if (pos + blockSize >= streamLength)
                    {
                        blockSizes[blockSizes.Count - 1] = (int)(streamLength - pos);
                        break;
                    }

                    StreamUtils.Skip(baseStream, blockSize);

                    blockSize = ReadBlockSize();
                    blockSizes.Add(blockSize);
                }
            }

            baseStream.Seek(dataStart, SeekOrigin.Begin);
        }

        private void WriteHeader()
        {
            using (BinaryWriter writer = new BinaryWriter(baseStream, Encoding.UTF8, true))
            {
                writer.Write(MAGIC);

                int blockSize = GenerateBlockSize();
                blockSizes.Add(blockSize);

                WriteBlockSize(blockSize);
            }

            dataStart = baseStream.Position;
        }

        private int GenerateBlockSize()
        {
            NUM1 = (NUM1 >> 1) | (((NUM1 ^ (8 * NUM1)) & 0xFFFFFFF8) << 28);
            NUM2 *= 1220703125;

            return (int)(float)((float)((float)((float)((float)(NUM1 ^ NUM2) * 2.3283064e-10) * 1048576.0) + 1048576.0) + 5242880.0);
        }

        private int ReadBlockSize()
        {
            StreamUtils.Skip(baseStream, 4);

            uint packed;

            using (BinaryReader reader = new BinaryReader(baseStream, Encoding.UTF8, true))
            {
                packed = reader.ReadUInt32();
            }

            return (int)(BitOperations.RotateLeft(packed, 4) / 0x5e8 + 5242880);
        }

        private void WriteBlockSize(int blockSize)
        {
            NUM1 = (NUM1 >> 1) | (((NUM1 ^ (8 * NUM1)) & 0xFFFFFFF8) << 28);
            NUM2 *= 1220703125;

            using (BinaryWriter writer = new BinaryWriter(baseStream, Encoding.UTF8, true))
            {
                writer.Write(NUM1 ^ NUM2);
                uint packed = BitOperations.RotateRight(1512 * (uint)blockSize + 662700032, 4);
                writer.Write(packed);
            }
        }
    }
}
