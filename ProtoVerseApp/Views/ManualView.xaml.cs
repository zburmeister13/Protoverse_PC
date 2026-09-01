using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProtoVerseApp.ViewModels;

namespace ProtoVerseApp.Views
{
    public partial class ManualView : UserControl
    {
        private ManualViewModel? _viewModel;

        /// <summary>Guards the two-way relationship between scrolling and selection.
        /// The table of contents scrolls the body when you click it, and the body
        /// updates the table of contents as you scroll - without this flag each would
        /// retrigger the other and the manual would fight the mouse wheel.</summary>
        private bool _syncing;

        public ManualView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

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
            if (_syncing || e.PropertyName != nameof(ManualViewModel.SelectedSection) || _viewModel?.SelectedSection == null)
                return;

            var target = _viewModel.SelectedSection;

            // The section may not be realised yet if it's far down the list, so defer
            // to a lower dispatcher priority and let layout run first.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var element = FindSectionElement(ManualScroller, target);
                element?.BringIntoView();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Keeps the table of contents pointing at whatever section is currently being
        /// read, so it doubles as a position indicator rather than only a jump menu.
        ///
        /// "Currently being read" is the last section whose heading has passed the top
        /// of the viewport - matching how a reader thinks about it, and meaning the
        /// highlight advances when a heading scrolls off the top rather than when the
        /// previous section merely ends.
        /// </summary>
        private void OnManualScrolled(object sender, ScrollChangedEventArgs e)
        {
            if (_viewModel == null || Math.Abs(e.VerticalChange) < 0.5 && e.ExtentHeightChange == 0)
                return;

            ManualSectionViewModel? current = null;

            foreach (var section in _viewModel.Sections)
            {
                var element = FindSectionElement(ManualScroller, section);
                if (element == null || !element.IsVisible)
                    continue;

                var top = element.TransformToAncestor(ManualScroller).Transform(new Point(0, 0)).Y;

                // A small tolerance so a heading sitting exactly at the top counts as
                // the current one rather than the previous.
                if (top <= 12)
                    current = section;
                else
                    break; // sections are in document order; nothing later can qualify
            }

            // Before the first heading passes the top, treat the first section as
            // current rather than leaving the table of contents blank.
            current ??= _viewModel.Sections.FirstOrDefault();

            if (current == null || ReferenceEquals(current, _viewModel.SelectedSection))
                return;

            _syncing = true;
            try { _viewModel.SelectedSection = current; }
            finally { _syncing = false; }
        }

        /// <summary>Walks the visual tree for the FrameworkElement whose DataContext is
        /// the target section. Cheap enough at a dozen sections; results aren't cached
        /// because the containers are regenerated whenever the manual is rebound.</summary>
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
