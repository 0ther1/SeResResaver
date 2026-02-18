using CommunityToolkit.Mvvm.ComponentModel;
using SeResResaver.Core;

namespace SeResResaver.ViewModels
{
    /// <summary>
    /// View model for ResaveFile
    /// </summary>
    public partial class ResaveFileItemViewModel : ObservableObject
    {
        public FileResaver.ResaveFile ResaveFile { get; private set; }

        public string OldPath => ResaveFile.OldPath;
        public string NewPath
        {
            get => ResaveFile.NewPath;
            set
            {
                ResaveFile.NewPath = value;
                OnPropertyChanged();
            }
        }
        public bool DeleteOld
        {
            get => ResaveFile.DeleteOld;
            set
            {
                ResaveFile.DeleteOld = value;
                OnPropertyChanged();
            }
        }

        public ResaveFileItemViewModel(FileResaver.ResaveFile resaveFile)
        {
            ResaveFile = resaveFile;
        }
    }
}
