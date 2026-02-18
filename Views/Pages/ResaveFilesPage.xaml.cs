using SeResResaver.Core;
using SeResResaver.Resources;
using SeResResaver.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace SeResResaver.Views.Pages
{
    /// <summary>
    /// Interaction logic for ResaveFilesPage.xaml
    /// </summary>
    public partial class ResaveFilesPage : Page
    {
        private ResaveFilesViewModel viewModel;
        private FileResaver resaver;

        public event EventHandler? NextClicked;

        public ResaveFilesPage(FileResaver resaver)
        {
            InitializeComponent();
            viewModel = new ResaveFilesViewModel(dataGridFiles);
            this.resaver = resaver;
            DataContext = viewModel;
        }

        public void Reset()
        {
            viewModel.AllFiles.Clear();
            viewModel.SearchText = string.Empty;
        }

        private void OnNextClick(object sender, RoutedEventArgs e)
        {
            string? errorMessage = null;

            if (viewModel.AllFiles.Count < 1)
                errorMessage = Strings.ResaveFilesPage_NoFilesError;

            List<FileResaver.ResaveFile> files = new(viewModel.AllFiles.Count);

            foreach (var rf in viewModel.AllFiles)
            {
                if (string.IsNullOrWhiteSpace(rf.NewPath))
                {
                    errorMessage = string.Format(Strings.ResaveFilesPage_EmptyNewNameError, rf.OldPath);
                    break;
                }
                else if (rf.NewPath == rf.OldPath)
                {
                    errorMessage = string.Format(Strings.ResaveFilesPage_NewNameEqualsToOldNameError, rf.OldPath);
                    break;
                }

                files.Add(rf.ResaveFile);
            }

            if (errorMessage != null)
            {
                MessageBox.Show(
                    Window.GetWindow(this),
                    errorMessage,
                    Strings.Common_Error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return;
            }

            resaver.GameDir = viewModel.GameDir;
            resaver.ResaveFiles = files;

            NextClicked?.Invoke(this, EventArgs.Empty);
        }
    }

    
}
