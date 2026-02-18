using SeResResaver.Core;
using System.Collections.Concurrent;
using System.IO;
using System.Windows;

namespace SeResResaver.Views.Dialogs
{
    /// <summary>
    /// Interaction logic for ParseReferencesDialog.xaml
    /// </summary>
    public partial class ParseReferencesDialog : Window
    {
        public List<string> Result { get; private set; } = new();

        private CancellationTokenSource? cancellationTokenSource = new();

        public ParseReferencesDialog(string gameDir, List<FileResaver.ResaveFile> resaveFiles, List<string> files)
        {
            InitializeComponent();

            cancellationTokenSource.Token.Register( () => { Finished(false); });

            Task.Run(() => Parse(gameDir, resaveFiles, files));
        }

        private void Finished(bool success)
        {
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;
            Dispatcher.Invoke(() => DialogResult = success);
        }

        private void Parse(string gameDir, List<FileResaver.ResaveFile> resaveFiles, List<string> files)
        {
            HashSet<string> references = new();
            HashSet<string> replaces = new();
            foreach (var rf in resaveFiles)
            {
                references.Add(rf.OldPath);
                replaces.Add(rf.NewPath);
            }

            ConcurrentBag<string> added = new();

            var addFile = (string path) =>
            {
                if (added.Contains(path)) return;

                string relPath = path.Replace('\\', '/');
                if (relPath.StartsWith(gameDir))
                    relPath = relPath.Substring(gameDir.Length);

                if (references.Contains(relPath) || replaces.Contains(relPath)) return;

                try
                {
                    using (Stream s = StreamFactory.OpenRead(path))
                    {
                        IReferenceParser? parser = ReferenceParserFactory.GetParser(s, path);
                        if (parser == null) return;

                        if (!parser.HasReferences(s, references)) return;
                    }
                }
                catch (Exception)
                {
                    return;
                }

                cancellationTokenSource!.Token.ThrowIfCancellationRequested();

                added.Add(path);
            };

            try
            {
                Parallel.ForEach(files, new ParallelOptions { CancellationToken = cancellationTokenSource!.Token }, addFile);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            Result = new(added);
            Finished(true);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }
    }
}
