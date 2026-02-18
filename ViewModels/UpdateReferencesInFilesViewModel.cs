using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SeResResaver.Core;
using SeResResaver.Extensions;
using SeResResaver.Resources;
using SeResResaver.Views.Dialogs;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;

namespace SeResResaver.ViewModels
{
    /// <summary>
    /// View model for UpdateReferencesInFilesPage
    /// </summary>
    public partial class UpdateReferencesInFilesViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelectedItems))]
        [NotifyCanExecuteChangedFor(nameof(RemoveFilesCommand))]
        private ObservableCollection<object> selectedItems = new();

        public ObservableCollection<string> AllFiles { get; private set; } = new();
        private ICollectionView filteredFiles;

        public ICollectionView Files
        {
            get => filteredFiles;
            private set => SetProperty(ref filteredFiles, value);
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSearchText))]
        [NotifyCanExecuteChangedFor(nameof(ClearFilterCommand))]
        private string searchText = string.Empty;

        public bool HasSearchText => !string.IsNullOrEmpty(SearchText);
        public bool HasSelectedItems => SelectedItems?.Count > 0;

        private FileResaver resaver;
        private string gameDir;

        public UpdateReferencesInFilesViewModel(FileResaver resaver)
        {
            filteredFiles = CollectionViewSource.GetDefaultView(AllFiles);
            filteredFiles.Filter = FilterFiles;
            this.resaver = resaver;
            gameDir = resaver.GameDir.Replace('/', '\\');
        }

        partial void OnSearchTextChanged(string value)
        {
            Files.Refresh();
        }

        public void NotifySelectionChanged()
        {
            RemoveFilesCommand.NotifyCanExecuteChanged();
        }

        private bool FilterFiles(object obj)
        {
            if (obj is not string path)
                return false;

            if (string.IsNullOrWhiteSpace(SearchText))
                return true;

            var searchLower = SearchText.ToLower();

            return path.ToLower().Contains(searchLower);
        }

        [RelayCommand(CanExecute = nameof(HasSearchText))]
        private void ClearFilter()
        {
            SearchText = string.Empty;
        }

        [RelayCommand]
        private void AddFiles()
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Multiselect = true,
                Filter = Strings.Common_FilterAllFiles,
                InitialDirectory = gameDir,
            };
            if (ofd.ShowDialog() != true) return;

            HashSet<string> current = new(AllFiles);
            List<string> filesToParse = new();

            foreach (string path in ofd.FileNames)
            {
                if (!path.StartsWith(gameDir)) continue;

                if (current.Contains(path)) continue;

                filesToParse.Add(path);
            }

            ParseReferencesDialog dialog = new ParseReferencesDialog(resaver.GameDir, resaver.ResaveFiles, filesToParse);
            if (dialog.ShowDialog() != true) return;

            foreach (var path in dialog.Result)
                AllFiles.Add(path);

            Files.Refresh();
        }

        [RelayCommand]
        private void AddFolder()
        {
            OpenFolderDialog ofd = new OpenFolderDialog
            {
                Multiselect = true,
                InitialDirectory = gameDir,
            };
            if (ofd.ShowDialog() != true) return;

            HashSet<string> current = new(AllFiles);
            List<string> filesToParse = new();

            foreach (string folder in ofd.FolderNames)
            {
                foreach (string path in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
                {
                    if (!path.StartsWith(gameDir)) continue;

                    if (current.Contains(path)) continue;

                    filesToParse.Add(path);
                }
            }

            ParseReferencesDialog dialog = new ParseReferencesDialog(resaver.GameDir, resaver.ResaveFiles, filesToParse);
            if (dialog.ShowDialog() != true) return;

            foreach (var path in dialog.Result)
                AllFiles.Add(path);

            Files.Refresh();
        }

        [RelayCommand(CanExecute = nameof(HasSelectedItems))]
        private void RemoveFiles()
        {
            List<string> toRemove = new(SelectedItems.Count);
            foreach (var obj in SelectedItems)
            {
                if (obj is not string path) continue;
                toRemove.Add(path);
            }

            AllFiles.RemoveMany(toRemove);
        }
    }
}
