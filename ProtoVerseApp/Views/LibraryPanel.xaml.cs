using System.Windows.Controls;

namespace ProtoVerseApp.Views
{
    public partial class LibraryPanel : UserControl
    {
        public LibraryPanel()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Scrolls the selected card into view. Selection is the view model's job
        /// (clicking a card, or following another card's "leads into" link, both just
        /// set LibraryViewModel.SelectedEntry) - but bringing a container into view is
        /// pure view concern with no MVVM-friendly binding, so it lives here.
        ///
        /// Following a link from a card at the top of the list to one further down is
        /// the case this exists for: without it the highlight ring would appear on a
        /// card the user can't see.
        /// </summary>
        private void OnCardSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CardList.SelectedItem != null)
                CardList.ScrollIntoView(CardList.SelectedItem);
        }
    }
}
