using SeResResaver.Core;
using SeResResaver.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace SeResResaver.Views.Pages
{
    /// <summary>
    /// Interaction logic for UpdateReferencesInFilesPage.xaml
    /// </summary>
    public partial class UpdateReferencesInFilesPage : Page
    {
        private FileResaver resaver;
        private UpdateReferencesInFilesViewModel viewModel;

        public event EventHandler? NextClicked;
        public event EventHandler? BackClicked;

        public UpdateReferencesInFilesPage(FileResaver resaver)
        {
            InitializeComponent();
            this.resaver = resaver;
            viewModel = new UpdateReferencesInFilesViewModel(resaver);
            DataContext = viewModel;
        }

        public void Reset()
        {
            viewModel.AllFiles.Clear();
            viewModel.SearchText = string.Empty;
        }

        private void OnNextClick(object sender, RoutedEventArgs e)
        {
            resaver.UpdateReferencesInFiles = new(viewModel.AllFiles);
            NextClicked?.Invoke(this, EventArgs.Empty);
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            BackClicked?.Invoke(this, EventArgs.Empty);
        }
    }
}
