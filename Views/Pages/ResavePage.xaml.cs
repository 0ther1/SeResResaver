using SeResResaver.Core;
using SeResResaver.Resources;
using SeResResaver.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shell;

namespace SeResResaver.Views.Pages
{
    /// <summary>
    /// Interaction logic for ResavePage.xaml
    /// </summary>
    public partial class ResavePage : Page
    {
        private ResaveViewModel viewModel;
        private FileResaver resaver;

        public event EventHandler? BackClicked;
        public event EventHandler? BackToStartClicked;

        public ResavePage(FileResaver resaver, Action<double> setTaskbarProgressValue, Action<TaskbarItemProgressState> setTaskbarProgressState)
        {
            InitializeComponent();
            viewModel = new ResaveViewModel(resaver, setTaskbarProgressValue, setTaskbarProgressState);
            DataContext = viewModel;

            this.resaver = resaver;
        }

        public void Reset()
        {
            viewModel.Status = Strings.ResavePage_StatusStart;

            if (resaver.GameDir.Contains("Serious Sam 2"))
                viewModel.GameTitleIndex = 0;
            else if (resaver.GameDir.Contains("Serious Sam HD"))
                viewModel.GameTitleIndex = 1;
            else if (resaver.GameDir.Contains("Serious Sam 3"))
                viewModel.GameTitleIndex = 2;
            else if (resaver.GameDir.Contains("Serious Sam Fusion 2017"))
                viewModel.GameTitleIndex = 3;
            else if (resaver.GameDir.Contains("Serious Sam 4"))
                viewModel.GameTitleIndex = 4;

            viewModel.IsWorking = false;
            viewModel.Progress = 0;
            viewModel.MaxProgress = 1;
            viewModel.IsFinished = false;
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            viewModel.Abort();
            BackClicked?.Invoke(this, EventArgs.Empty);
        }

        private void OnBackToStartClick(object sender, RoutedEventArgs e)
        {
            viewModel.Abort();
            BackToStartClicked?.Invoke(this, EventArgs.Empty);
        }
    }
}
