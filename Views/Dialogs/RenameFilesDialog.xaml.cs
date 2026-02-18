using SeResResaver.Resources;
using System.Windows;

namespace SeResResaver.Views.Dialogs
{
    /// <summary>
    /// Interaction logic for RenameFilesDialog.xaml
    /// </summary>
    public partial class RenameFilesDialog : Window
    {
        public class ReplacementRule
        {
            public string Substring { get; set; } = string.Empty;
            public string Replacement { get; set; } = string.Empty;
            public bool IsRegex { get; set; }
            public bool IgnoreCase { get; set; }
        }

        public List<ReplacementRule> Rules { get; private set; } = new();

        public RenameFilesDialog()
        {
            InitializeComponent();

            dataGridRules.ItemsSource = Rules;
        }

        private void OnOKClick(object sender, RoutedEventArgs e)
        {
            string? errorText = null;

            if (Rules.Count < 1)
                errorText = Strings.RenameFilesDialog_NoRulesError;
            
            for (int i = 0; i < Rules.Count; i++)
            {
                var rule = Rules[i];
                if (string.IsNullOrEmpty(rule.Substring))
                {
                    errorText = string.Format(Strings.RenameFilesDialog_EmptySubstringError, i);
                    break;
                }
            }

            if (errorText != null)
            {
                MessageBox.Show(
                    this,
                    errorText,
                    Strings.Common_Error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return;
            }

            DialogResult = true;
        }
    }
}
