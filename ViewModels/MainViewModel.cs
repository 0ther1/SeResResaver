using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Controls;
using System.Windows.Shell;

namespace SeResResaver.ViewModels
{
    /// <summary>
    /// View model for MainWindow
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private Page? currentPage = null;

        [ObservableProperty]
        private double progressValue = 0.0;

        [ObservableProperty]
        private TaskbarItemProgressState progressState = TaskbarItemProgressState.None;

        public void SetProgressState(TaskbarItemProgressState state)
        {
            ProgressState = state;
        }

        public void SetProgressValue(double value)
        {
            ProgressValue = value;
        }
    }
}
