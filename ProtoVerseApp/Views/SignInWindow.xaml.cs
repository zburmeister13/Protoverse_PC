using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ProtoVerseApp.Models;
using ProtoVerseApp.Services;

namespace ProtoVerseApp.Views
{
    /// <summary>
    /// Profile picker. Small enough that its logic lives in code-behind rather than
    /// getting its own view model - it does one thing, closes, and holds no state the
    /// rest of the app binds to. All persistence goes through
    /// <see cref="AccountStore"/>.
    /// </summary>
    public partial class SignInWindow : Window
    {
        private readonly AccountStore _store;

        public SignInWindow(AccountStore store)
        {
            _store = store;
            InitializeComponent();
            RefreshList();
            NewNameBox.Focus();
        }

        private void RefreshList()
        {
            // Rebind rather than mutate: the store owns the list, and re-reading it is
            // cheap at the scale of "people sharing one PC".
            AccountList.ItemsSource = _store.Accounts.ToList();
            AccountList.SelectedItem = _store.Accounts.FirstOrDefault(a => a.Id == _store.ActiveAccount?.Id);

            bool any = _store.Accounts.Count > 0;
            EmptyHint.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
            SignInButton.IsEnabled = any;
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void OnCreateAccount(object sender, RoutedEventArgs e)
        {
            var name = NewNameBox.Text.Trim();
            if (name.Length == 0)
            {
                ShowError("Enter a name for the new profile.");
                return;
            }

            if (_store.NameExists(name))
            {
                // Names aren't the identity key (the id is), but a picker with two
                // identical rows is unusable, so duplicates are refused.
                ShowError($"There's already a profile called \"{name}\".");
                return;
            }

            // CreateAccount signs into the new profile, which is what someone typing a
            // name and pressing Create is asking for - no second click needed.
            if (_store.CreateAccount(name) == null)
            {
                ShowError("Couldn't create that profile.");
                return;
            }

            DialogResult = true;
            Close();
        }

        private void OnNewNameKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                OnCreateAccount(sender, e);
        }

        private void OnSignIn(object sender, RoutedEventArgs e)
        {
            if (AccountList.SelectedItem is not UserAccount account)
            {
                ShowError("Pick a profile, or create a new one.");
                return;
            }

            _store.SignIn(account.Id);
            DialogResult = true;
            Close();
        }

        private void OnAccountDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (AccountList.SelectedItem is UserAccount)
                OnSignIn(sender, e);
        }

        private void OnDeleteAccount(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string accountId })
                return;

            var account = _store.Accounts.FirstOrDefault(a => a.Id == accountId);
            if (account == null)
                return;

            // Deleting a profile throws away everything it tracked and there's no undo,
            // so this confirms - the one place in this dialog that does.
            var confirm = MessageBox.Show(
                this,
                $"Delete \"{account.DisplayName}\" and everything it tracked?\n\nThis can't be undone.",
                "Delete profile",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.OK)
                return;

            _store.DeleteAccount(accountId);
            RefreshList();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
