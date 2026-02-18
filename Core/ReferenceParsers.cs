using System.IO;
using System.Text.RegularExpressions;

namespace SeResResaver.Core
{
    /// <summary>
    /// Interface for searching reference files.
    /// </summary>
    public interface IReferenceParser
    {
        /// <summary>
        /// Searches for file references in the stream.
        /// </summary>
        /// <param name="stream">The stream to search for references.</param>
        /// <param name="references">The file paths to search for.</param>
        /// <returns><c>true</c> if at least one reference is found; otherwise <c>false</c>.</returns>
        bool HasReferences(Stream stream, HashSet<string> references);
    }

    /// <summary>
    /// Functions for creating parsers.
    /// </summary>
    public class ReferenceParserFactory
    {
        /// <summary>
        /// Creates a parser appropriate for the given stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="path">File path used to open file stream.</param>
        /// <returns>A reference parser or <c>null</c> if no parser for this stream.</returns>
        public static IReferenceParser? GetParser(Stream stream, string path)
        {
            byte[] buffer = new byte[8];
            StreamUtils.Peek(stream, buffer, 0, buffer.Length);

            if (buffer.SequenceEqual(BitConverter.GetBytes(BinaryMetaParser.MAGIC)))
                return new BinaryMetaReferenceParser();
            if (buffer.SequenceEqual(TextMetaResaver.MAGIC))
                return new TextMetaReferenceParser();

            int offset = 0;
            if (buffer.AsSpan(0, 3).SequenceEqual(NfoResaver.BOM))
                offset = 3;

            if (buffer.AsSpan(offset, NfoResaver.MAGIC.Length).SequenceEqual(NfoResaver.MAGIC))
                return new NfoReferenceParser();

            if (path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                return new LuaReferenceParser();

            return null;
        }
    }

    /// <summary>
    /// Reference parser for binary meta files.
    /// </summary>
    public class BinaryMetaReferenceParser : IReferenceParser
    {
        public bool HasReferences(Stream stream, HashSet<string> references)
        {
            using (BinaryMetaParser parser = new BinaryMetaParser(stream))
            {
                if (parser.Version > 9)
                {
                    parser.BeginBlock(BinaryMetaParser.BLOCK_MESSAGES);
                    parser.SkipString();
                }

                parser.BeginBlock(BinaryMetaParser.BLOCK_INFO);
                int skipLength = 16;
                if (parser.Version > 7)
                    skipLength += 4;
                parser.Skip(skipLength);

                int referencedFileCount = parser.BeginList(BinaryMetaParser.BLOCK_EXTERNAL_FILES);
                for (int i = 0; i < referencedFileCount; i++)
                {
                    parser.Skip(8);
                    string path = parser.ReadString();

                    if (references.Contains(path)) return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Reference parser for text meta files.
    /// </summary>
    public class TextMetaReferenceParser : IReferenceParser
    {
        public bool HasReferences(Stream stream, HashSet<string> references)
        {
            using (StreamReader reader = new StreamReader(stream))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] split = line.Split('=', 2);
                    if (split.Length == 2)
                    {
                        if (split[1].Contains("@'"))
                        {
                            int start = split[1].IndexOf('\'') + 1;
                            int end = split[1].IndexOf('\'', start);
                            string path = split[1].Substring(start, end - start);

                            if (references.Contains(path)) return true;
                        }
                    }
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Reference parser for *.nfo files.
    /// </summary>
    public class NfoReferenceParser : IReferenceParser
    {
        public bool HasReferences(Stream stream, HashSet<string> references)
        {
            byte[] buffer = new byte[3];
            StreamUtils.Peek(stream, buffer, 0, 3);
            if (buffer.SequenceEqual(NfoResaver.BOM))
                StreamUtils.Skip(stream, 3);

            using (StreamReader reader = new StreamReader(stream))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] split = line.Split('=', 2);
                    if (split.Length == 2)
                    {
                        if (NfoResaver.KEYS.Contains(split[0]))
                        {
                            int start = split[1].IndexOf('"') + 1;
                            int end = split[1].IndexOf('"', start);
                            string path = split[1].Substring(start, end - start);

                            if (references.Contains(path)) return true;
                        }
                    }
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Reference parser for *.lua files.
    /// </summary>
    public class LuaReferenceParser : IReferenceParser
    {
        public bool HasReferences(Stream stream, HashSet<string> references)
        {
            byte[] buffer = new byte[3];
            StreamUtils.Peek(stream, buffer, 0, 3);
            if (buffer.SequenceEqual(NfoResaver.BOM))
                StreamUtils.Skip(stream, 3);

            using (StreamReader reader = new StreamReader(stream))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    var matches = LuaResaver.FuncPattern().Matches(line);
                    foreach (Match match in matches)
                    {
                        if (match.Groups.Count > 1)
                        {
                            string path = match.Groups["path"].Value.Trim();

                            if (references.Contains(path)) return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
