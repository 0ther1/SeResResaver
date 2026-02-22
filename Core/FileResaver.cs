using System.Collections.Concurrent;
using System.IO;

namespace SeResResaver.Core
{
    /// <summary>
    /// Stream parameters used for creating a new stream.
    /// </summary>
    public class StreamParameters
    {
        private static readonly HashSet<string> PLAIN_FILE_EXTENSIONS = new()
        {
            ".wav",
            ".ogg",
        };

        /// <summary>
        /// Signed stream parameters.
        /// </summary>
        public class SignedStreamParameters
        {
            public int Version { get; set; }
            public byte[]? Key { get; set; }
        }

        /// <summary>
        /// Signed stream parameters.
        /// </summary>
        public SignedStreamParameters? SignedStream { get; set; }
        /// <summary>
        /// Is stream wrecker required.
        /// </summary>
        public bool UseStreamWrecker { get; set; }

        /// <summary>
        /// Creates a new stream using current parameters.
        /// </summary>
        /// <param name="path">File path.</param>
        /// <returns>A write-only stream.</returns>
        public Stream CreateStream(string path)
        {
            Stream s = File.Create(path);

            string ext = Path.GetExtension(path).ToLower();

            if (SignedStream != null && !PLAIN_FILE_EXTENSIONS.Contains(ext))
                s = StreamFactory.AddSignedStream(s, SignedStream.Version, SignedStream.Key!);
            if (UseStreamWrecker && ext == ".wld")
                s = StreamFactory.AddStreamWrecker(s);

            return s;
        }

        /// <summary>
        /// Serious Sam 2 stream parameters (no signed stream, no stream wrecker).
        /// </summary>
        public static readonly StreamParameters SS2 = new StreamParameters
        {
            SignedStream = null,
            UseStreamWrecker = false,
        };

        /// <summary>
        /// Serious Sam HD stream parameters (signed stream, no stream wrecker).
        /// </summary>
        public static readonly StreamParameters SSHD = new StreamParameters
        {
            SignedStream = new SignedStreamParameters
            {
                Version = 4,
                Key = Keys.SSHD_EDITOR_PRIVATE,
            },
            UseStreamWrecker = false,
        };

        /// <summary>
        /// Serious Sam 3 stream parameters (signed stream, stream wrecker).
        /// </summary>
        public static readonly StreamParameters SS3 = new StreamParameters
        {
            SignedStream = new SignedStreamParameters
            {
                Version = 5,
                Key = Keys.SS3_EDITOR_PRIVATE,
            },
            UseStreamWrecker = true,
        };

        /// <summary>
        /// Serious Sam Fusion 2017 stream parameters (signed stream, stream wrecker).
        /// </summary>
        public static readonly StreamParameters Fusion = new StreamParameters
        {
            SignedStream = new SignedStreamParameters
            {
                Version = 5,
                Key = Keys.FUSION_EDITOR_PRIVATE,
            },
            UseStreamWrecker = true,
        };

        /// <summary>
        /// Serious Sam 4 stream parameters (signed stream, stream wrecker).
        /// </summary>
        public static readonly StreamParameters SS4 = new StreamParameters
        {
            SignedStream = new SignedStreamParameters
            {
                Version = 5,
                Key = Keys.SS4_EDITOR_PRIVATE,
            },
            UseStreamWrecker = true,
        };
    }

    /// <summary>
    /// File resaver helper.
    /// </summary>
    public class FileResaver
    {
        /// <summary>
        /// File to be resaved with a new name.
        /// </summary>
        public class ResaveFile
        {
            /// <summary>
            /// File path (relative to game root) before resave.
            /// </summary>
            public string OldPath { get; set; } = string.Empty;
            /// <summary>
            /// File path (relative to game root) after resave.
            /// </summary>
            public string NewPath { get; set; } = string.Empty;
            /// <summary>
            /// Deletes old file after resave.
            /// </summary>
            public bool DeleteOld { get; set; }
        }

        /// <summary>
        /// Files for resaving.
        /// </summary>
        public List<ResaveFile> ResaveFiles { get; set; } = new();

        /// <summary>
        /// Files for updating references to resaved files.
        /// </summary>
        public List<string> UpdateReferencesInFiles { get; set; } = new();

        /// <summary>
        /// Errors occurred during file resaving.
        /// </summary>
        public ConcurrentDictionary<ResaveFile, Exception> ResaveErrors { get; private set; } = new();

        /// <summary>
        /// Errors occurred during reference updating.
        /// </summary>
        public ConcurrentDictionary<string, Exception> UpdateReferencesErrors { get; private set; } = new();

        /// <summary>
        /// Stream parameters used to create files during resaving.
        /// </summary>
        public StreamParameters StreamParameters { get; set; } = StreamParameters.SS2;
        public string GameDir 
        {
            get => gameDir;
            set
            {
                gameDir = value.Replace('\\', '/');
                if (!gameDir.EndsWith('/'))
                    gameDir += "/";
            }
        }

        private string gameDir = string.Empty;

        /// <summary>
        /// Event fired when resave process is finished.
        /// </summary>
        public event EventHandler? Finished;

        /// <summary>
        /// Event fired when another file resaved.
        /// </summary>
        public event EventHandler? ResaveProgressUpdated;

        /// <summary>
        /// Event fired when another file updated references.
        /// </summary>
        public event EventHandler? UpdateReferencesProgressUpdated;

        /// <summary>
        /// Reset resaver state.
        /// </summary>
        public void Reset()
        {
            GameDir = string.Empty;
            ResaveFiles.Clear();
            UpdateReferencesInFiles.Clear();
        }

        /// <summary>
        /// Begin resaving files.
        /// </summary>
        /// <param name="cancellationToken">Token for cancelling resave process.</param>
        public void Resave(CancellationToken cancellationToken)
        {
            ResaveErrors.Clear();
            UpdateReferencesErrors.Clear();

            Dictionary<string, string> renames = new();
            foreach (var rf in ResaveFiles)
                renames.Add(rf.OldPath, rf.NewPath);

            var resaveFile = (ResaveFile rf) =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                string inPath = Path.Combine(GameDir, rf.OldPath);
                string outPath = Path.Combine(GameDir, rf.NewPath);

                string? outDir = Path.GetDirectoryName(outPath);
                if (outDir != null && !Path.Exists(outDir))
                    Directory.CreateDirectory(outDir);

                try
                {
                    using (Stream inStream = StreamFactory.OpenRead(inPath))
                    {
                        IResaver resaver = ResaverFactory.GetResaver(inStream, inPath);
                        Stream outStream = StreamParameters.CreateStream(outPath);

                        resaver.Resave(inStream, outStream, renames, rf.NewPath);
                    }
                }
                catch (Exception ex)
                {
                    ResaveErrors.TryAdd(rf, ex);
                    if (File.Exists(outPath))
                        File.Delete(outPath);
                    return;
                }
                finally
                {
                    ResaveProgressUpdated?.Invoke(this, EventArgs.Empty);
                }
            };

            var updateReferences = (string path) =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                string tempPath = Path.GetTempFileName() + Path.GetExtension(path);
                try
                {
                    using (Stream inStream = StreamFactory.OpenRead(path))
                    {
                        IResaver resaver = ResaverFactory.GetResaver(inStream, path);

                        Stream outStream = StreamParameters.CreateStream(tempPath);

                        resaver.Resave(inStream, outStream, renames);
                    }

                    File.Replace(tempPath, path, null);
                }
                catch (Exception ex)
                {
                    UpdateReferencesErrors.TryAdd(path, ex);
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                    return;
                }
                finally
                {
                    UpdateReferencesProgressUpdated?.Invoke(this, EventArgs.Empty);
                }
            };

            try
            {
                Parallel.ForEach(ResaveFiles, new ParallelOptions { CancellationToken = cancellationToken }, resaveFile);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                Parallel.ForEach(UpdateReferencesInFiles, new ParallelOptions { CancellationToken = cancellationToken }, updateReferences);
            }
            catch (OperationCanceledException)
            {
                return;
            }


            foreach (var rf in ResaveFiles)
            {
                if (rf.DeleteOld)
                {
                    try
                    {
                        File.Delete(Path.Combine(GameDir, rf.OldPath));
                    }
                    catch { }
                }
            }

            Finished?.Invoke(this, EventArgs.Empty);
        }
    }
}
