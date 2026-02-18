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
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SeResResaver.ViewModels
{
    /// <summary>
    /// View model for ResaveFilesPage
    /// </summary>
    public partial class ResaveFilesViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasGameDir))]
        [NotifyCanExecuteChangedFor(nameof(AddFilesCommand))]
        [NotifyCanExecuteChangedFor(nameof(AddFolderCommand))]
        private string gameDir = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelectedItems))]
        [NotifyCanExecuteChangedFor(nameof(RemoveFilesCommand))]
        [NotifyCanExecuteChangedFor(nameof(RenameFilesCommand))]
        [NotifyCanExecuteChangedFor(nameof(CheckAllCommand))]
        [NotifyCanExecuteChangedFor(nameof(UncheckAllCommand))]
        private ObservableCollection<object> selectedItems = new();

        public ObservableCollection<ResaveFileItemViewModel> AllFiles { get; private set; } = new();
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

        public bool HasGameDir => !string.IsNullOrEmpty(GameDir);
        public bool HasSearchText => !string.IsNullOrEmpty(SearchText);
        public bool HasSelectedItems => SelectedItems?.Count > 0;

        private DataGrid dataGrid;

        public ResaveFilesViewModel(DataGrid dataGrid)
        {
            filteredFiles = CollectionViewSource.GetDefaultView(AllFiles);
            filteredFiles.Filter = FilterFiles;
            this.dataGrid = dataGrid;
        }

        partial void OnSearchTextChanged(string value)
        {
            RefreshFilter();
        }

        public void NotifySelectionChanged()
        {
            RemoveFilesCommand.NotifyCanExecuteChanged();
            RenameFilesCommand.NotifyCanExecuteChanged();
            CheckAllCommand.NotifyCanExecuteChanged();
            UncheckAllCommand.NotifyCanExecuteChanged();
        }

        public void RefreshFilter()
        {
            dataGrid.CommitEdit(DataGridEditingUnit.Row, true);

            Files.Refresh();
        }

        private bool FilterFiles(object obj)
        {
            if (obj is not ResaveFileItemViewModel file)
                return false;

            if (string.IsNullOrWhiteSpace(SearchText))
                return true;

            var searchLower = SearchText.ToLower();

            return file.OldPath.ToLower().Contains(searchLower) || file.NewPath.ToLower().Contains(searchLower);
        }

        [RelayCommand(CanExecute = nameof(HasSearchText))]
        private void ClearFilter()
        {
            SearchText = string.Empty;
        }

        [RelayCommand]
        private void BrowseGameDir()
        {
            OpenFolderDialog dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() != true) return;

            string? dir = dialog.FolderName;
            while (dir != null && !Directory.Exists(Path.Combine(dir, "Content")))
            {
                dir = Path.GetDirectoryName(dir);
            }

            if (dir == null)
            {
                MessageBox.Show(
                    Strings.ResaveFilesPage_InvalidGameDirError,
                    Strings.Common_Error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return;
            }

            GameDir = dir.Replace('\\', '/');
        }

        [RelayCommand(CanExecute = nameof(HasGameDir))]
        private void AddFiles()
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Multiselect = true,
                Filter = Strings.Common_FilterAllFiles,
                InitialDirectory = GameDir.Replace('/', '\\'),
            };
            if (ofd.ShowDialog() != true) return;

            HashSet<string> current = new();
            foreach (var rf in AllFiles)
                current.Add(rf.OldPath);

            foreach (string path in ofd.FileNames)
            {
                string p = path.Replace('\\', '/');
                if (!p.StartsWith(GameDir)) continue;

                p = p.Substring(GameDir.Length + 1);

                if (current.Contains(p)) continue;

                AllFiles.Add(new ResaveFileItemViewModel(new FileResaver.ResaveFile
                {
                    OldPath = p,
                }));
            }

            Files.Refresh();
        }

        [RelayCommand(CanExecute = nameof(HasGameDir))]
        private void AddFolder()
        {
            OpenFolderDialog ofd = new OpenFolderDialog
            {
                Multiselect = true,
                InitialDirectory = GameDir.Replace('/', '\\'),
            };
            if (ofd.ShowDialog() != true) return;

            HashSet<string> current = new();
            foreach (var rf in AllFiles)
                current.Add(rf.OldPath);

            foreach (string folder in ofd.FolderNames)
            {
                foreach (string path in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
                {
                    string p = path.Replace('\\', '/');
                    if (!p.StartsWith(GameDir)) continue;

                    p = p.Substring(GameDir.Length + 1);

                    if (current.Contains(p)) continue;

                    AllFiles.Add(new ResaveFileItemViewModel(new FileResaver.ResaveFile
                    {
                        OldPath = p,
                    }));
                }
            }

            Files.Refresh();
        }

        [RelayCommand(CanExecute = nameof(HasSelectedItems))]
        private void RemoveFiles()
        {
            List<ResaveFileItemViewModel> toRemove = new(SelectedItems.Count);
            foreach (var obj in SelectedItems)
            {
                if (obj is not ResaveFileItemViewModel file) continue;
                toRemove.Add(file);
            }

            AllFiles.RemoveMany(toRemove);
        }

        [RelayCommand(CanExecute = nameof(HasSelectedItems))]
        private void RenameFiles()
        {
            RenameFilesDialog dialog = new RenameFilesDialog();
            if (dialog.ShowDialog() != true) return;

            var rules = dialog.Rules;
            foreach (var obj in SelectedItems)
            {
                if (obj is not ResaveFileItemViewModel file) continue;
                string p = file.OldPath;
                foreach (var rule in rules)
                {
                    if (rule.IsRegex)
                        p = Regex.Replace(p, rule.Substring, rule.Replacement, rule.IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
                    else
                        p = p.Replace(rule.Substring, rule.Replacement, rule.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
                }

                file.NewPath = p;
            }
        }

        [RelayCommand(CanExecute = nameof(HasSelectedItems))]
        private void CheckAll()
        {
            foreach (var obj in SelectedItems)
            {
                if (obj is not ResaveFileItemViewModel file) continue;
                file.DeleteOld = true;
            }
        }

        [RelayCommand(CanExecute = nameof(HasSelectedItems))]
        private void UncheckAll()
        {
            foreach (var obj in SelectedItems)
            {
                if (obj is not ResaveFileItemViewModel file) continue;
                file.DeleteOld = false;
            }
        }
    }
}
