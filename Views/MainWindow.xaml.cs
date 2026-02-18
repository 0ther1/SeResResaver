using SeResResaver.Core;
using SeResResaver.ViewModels;
using SeResResaver.Views.Pages;
using System.Windows;

namespace SeResResaver.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        FileResaver resaver;
        MainViewModel viewModel;

        ResaveFilesPage resaveFilesPage;
        UpdateReferencesInFilesPage updateReferencesInFilesPage;
        ResavePage resavePage;

        public MainWindow()
        {
            InitializeComponent();
            resaver = new FileResaver();

            viewModel = new MainViewModel();

            resaveFilesPage = new ResaveFilesPage(resaver);
            updateReferencesInFilesPage = new UpdateReferencesInFilesPage(resaver);
            resavePage = new ResavePage(resaver, viewModel.SetProgressValue, viewModel.SetProgressState);

            resaveFilesPage.NextClicked += (o, a) => viewModel.CurrentPage = updateReferencesInFilesPage;
            updateReferencesInFilesPage.BackClicked += (o, a) => viewModel.CurrentPage = resaveFilesPage;
            updateReferencesInFilesPage.NextClicked += (o, a) =>
            {
                viewModel.CurrentPage = resavePage;
                resavePage.Reset();
            };
            resavePage.BackClicked += (o, a) => viewModel.CurrentPage = updateReferencesInFilesPage;
            resavePage.BackToStartClicked += (o, a) =>
            {
                resaver.Reset();
                resaveFilesPage.Reset();
                updateReferencesInFilesPage.Reset();
                viewModel.CurrentPage = resaveFilesPage;
            };

            viewModel.CurrentPage = resaveFilesPage;

            DataContext = viewModel;
        }
    }

    
}