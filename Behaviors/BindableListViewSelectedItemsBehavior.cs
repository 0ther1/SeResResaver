using Microsoft.Xaml.Behaviors;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace SeResResaver.Behaviors
{
    /// <summary>
    /// Behavior for binding to SelectedItems in ListView
    /// </summary>
    public class BindableListViewSelectedItemsBehavior : Behavior<ListView>
    {
        public static readonly DependencyProperty SelectedItemsProperty =
        DependencyProperty.Register(nameof(SelectedItems),
            typeof(ObservableCollection<object>),
            typeof(BindableListViewSelectedItemsBehavior),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public ObservableCollection<object> SelectedItems
        {
            get => (ObservableCollection<object>)GetValue(SelectedItemsProperty);
            set => SetValue(SelectedItemsProperty, value);
        }

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(nameof(ViewModel),
                typeof(INotifyPropertyChanged),
                typeof(BindableListViewSelectedItemsBehavior));

        public INotifyPropertyChanged ViewModel
        {
            get => (INotifyPropertyChanged)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.SelectionChanged += OnSelectionChanged;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.SelectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AssociatedObject.SelectedItems != null && SelectedItems != null)
            {
                SelectedItems.Clear();
                foreach (var item in AssociatedObject.SelectedItems)
                {
                    SelectedItems.Add(item);
                }

                if (ViewModel != null)
                {
                    var method = ViewModel.GetType().GetMethod("NotifySelectionChanged");
                    method?.Invoke(ViewModel, null);
                }
            }
        }
    }
}
