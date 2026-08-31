using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProtoVerseApp.ViewModels;

namespace ProtoVerseApp.Views
{
    public partial class ManualView : UserControl
    {
        public ManualView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private ManualViewModel? _viewModel;

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_viewModel != null)
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

            _viewModel = DataContext as ManualViewModel;

            if (_viewModel != null)
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        /// <summary>
        /// Scrolls to the section picked in the table of contents. The manual stays one
        /// continuous scroll rather than switching pages - a learner mid-task flips
        /// between the setup steps and the observations constantly, and paging would
        /// make that a navigation act each time.
        ///
        /// Done in code-behind because bringing a generated container into view has no
        /// MVVM-friendly binding: the view model says *which* section, the view works
        /// out where that ended up on screen.
        /// </summary>
        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ManualViewModel.SelectedSection) || _viewModel?.SelectedSection == null)
                return;

            var target = _viewModel.SelectedSection;

            // The section may not be realised yet if it's far down the list, so defer
            // to a lower dispatcher priority and let layout run first.
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                var element = FindSectionElement(ManualScroller, target);
                element?.BringIntoView();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>Walks the visual tree for the FrameworkElement whose DataContext is
        /// the target section. Cheaper than it looks - a manual is a dozen sections, and
        /// this only runs on an explicit TOC click.</summary>
        private static FrameworkElement? FindSectionElement(DependencyObject root, ManualSectionViewModel target)
        {
            if (root is FrameworkElement element && ReferenceEquals(element.DataContext, target)
                && element is not ListBoxItem)
            {
                return element;
            }

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var found = FindSectionElement(VisualTreeHelper.GetChild(root, i), target);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
