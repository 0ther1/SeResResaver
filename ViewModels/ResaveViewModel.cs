using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeResResaver.Core;
using SeResResaver.Resources;
using System.Collections.ObjectModel;
using System.Media;
using System.Windows;
using System.Windows.Shell;

namespace SeResResaver.ViewModels
{
    /// <summary>
    /// View model for ResavePage
    /// </summary>
    public partial class ResaveViewModel : ObservableObject
    {
        public class FileError
        {
            public string File { get; set; } = string.Empty;
            public string ErrorMessage { get; set; } = string.Empty;
        };

        private FileResaver resaver;
        private StreamParameters[] DEFAULT_PARAMETERS =
        {
            StreamParameters.SS2,
            StreamParameters.SSHD,
            StreamParameters.SS3,
            StreamParameters.Fusion,
            StreamParameters.SS4,
        };

        public int GameTitleIndex
        {
            get => Array.IndexOf(DEFAULT_PARAMETERS, resaver.StreamParameters);
            set
            {
                resaver.StreamParameters = DEFAULT_PARAMETERS[value];
                OnPropertyChanged();
            }
        }

        public ObservableCollection<FileError> Errors { get; private set; } = new();

        [ObservableProperty]
        private string status = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ButtonText))]
        [NotifyPropertyChangedFor(nameof(CanGoBack))]
        private bool isWorking = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotFinished))]
        [NotifyPropertyChangedFor(nameof(CanGoBack))]
        [NotifyCanExecuteChangedFor(nameof(StartStopRenameCommand))]
        private bool isFinished = false;

        [ObservableProperty]
        private int progress = 0;

        [ObservableProperty]
        private int maxProgress = 0;

        public string ButtonText => IsWorking ? Strings.ResavePage_AbortButton : Strings.ResavePage_StartButton;

        private CancellationTokenSource? cancellationTokenSource;
        private Action<double> setTaskbarProgressValue;
        private Action<TaskbarItemProgressState> setTaskbarProgressState;

        public bool CanGoBack => !IsWorking && !IsFinished;

        public ResaveViewModel(FileResaver resaver, Action<double> setTaskbarProgressValue, Action<TaskbarItemProgressState> setTaskbarProgressState)
        {
            this.resaver = resaver;
            resaver.ResaveProgressUpdated += Resaver_ResaveProgressUpdated;
            resaver.UpdateReferencesProgressUpdated += Resaver_UpdateReferencesProgressUpdated;
            resaver.Finished += Resaver_Finished;

            this.setTaskbarProgressState = setTaskbarProgressState;
            this.setTaskbarProgressValue = setTaskbarProgressValue;
        }

        public void Abort()
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;

            setTaskbarProgressState(TaskbarItemProgressState.None);

            Status = Strings.ResavePage_StatusAborted;
        }

        private void Resaver_Finished(object? sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsWorking = false;
                IsFinished = true;
                setTaskbarProgressState(TaskbarItemProgressState.None);

                cancellationTokenSource?.Dispose();
                cancellationTokenSource = null;

                SystemSounds.Beep.Play();

                if (resaver.ResaveErrors.Count > 0 || resaver.UpdateReferencesErrors.Count > 0)
                    Status = Strings.ResavePage_StatusFinishedWithErrors;
                else
                    Status = Strings.ResavePage_StatusFinished;

                foreach (var kv in resaver.ResaveErrors)
                    Errors.Add(new FileError { File = kv.Key.OldPath, ErrorMessage = kv.Value.Message });

                foreach (var kv in resaver.UpdateReferencesErrors)
                    Errors.Add(new FileError { File = kv.Key, ErrorMessage = kv.Value.Message });
            });
        }

        private void Resaver_ResaveProgressUpdated(object? sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() => {
                if (!IsWorking) return;

                Progress++;
                setTaskbarProgressValue((double)Progress / MaxProgress);
                if (Progress == MaxProgress && resaver.UpdateReferencesInFiles.Count > 0)
                {
                    MaxProgress = resaver.UpdateReferencesInFiles.Count;
                    Progress = 0;
                }
                else
                    Status = string.Format(Strings.ResavePage_StatusResaving, Progress + 1, MaxProgress);
            });
        }

        private void Resaver_UpdateReferencesProgressUpdated(object? sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() => {
                if (!IsWorking) return;

                Progress++;
                setTaskbarProgressValue((double)Progress / MaxProgress);
                Status = string.Format(Strings.ResavePage_StatusUpdatingReferences, Progress + 1, MaxProgress);
            });
        }

        private bool IsNotFinished => !IsFinished;

        [RelayCommand(CanExecute = nameof(IsNotFinished))]
        private void StartStopRename()
        {
            if (IsWorking)
            {
                Abort();
            }
            else
            {
                cancellationTokenSource?.Cancel();
                cancellationTokenSource?.Dispose();
                cancellationTokenSource = new();

                setTaskbarProgressValue(0);
                setTaskbarProgressState(TaskbarItemProgressState.Normal);

                Progress = 0;
                MaxProgress = resaver.ResaveFiles.Count;
                Errors.Clear();

                Task.Run(() => resaver.Resave(cancellationTokenSource.Token));
            }

            IsWorking = !IsWorking;
        }

    }
}
