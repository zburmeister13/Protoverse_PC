using System;
using System.Windows;
using ProtoVerseApp.ViewModels;

namespace ProtoVerseApp.Views
{
    public partial class MainWindow : Window
    {
        private AccountViewModel? _subscribedAccount;

        public MainWindow()
        {
            InitializeComponent();

            // Subscribing from Loaded rather than straight after InitializeComponent:
            // the DataContext is declared in XAML, and reading it in the constructor
            // proved unreliable here - the handler silently didn't attach, so clicking
            // "Sign in" did nothing at all (no dialog, no error, since the view model
            // just raises an event nobody was listening to). Loaded is guaranteed to
            // run after the DataContext is in place. DataContextChanged covers the case
            // of it being swapped later.
            Loaded += (_, _) => SubscribeToAccount();
            DataContextChanged += (_, _) => SubscribeToAccount();
        }

        private void SubscribeToAccount()
        {
            if (DataContext is not MainViewModel main || ReferenceEquals(_subscribedAccount, main.Account))
                return;

            if (_subscribedAccount != null)
                _subscribedAccount.SignInRequested -= ShowSignInDialog;

            main.Account.SignInRequested += ShowSignInDialog;
            _subscribedAccount = main.Account;
        }

        /// <summary>Showing a dialog is a view concern, so the account view model raises
        /// an event rather than newing up a Window itself.</summary>
        private void ShowSignInDialog()
        {
            if (DataContext is not MainViewModel main)
                return;

            var dialog = new SignInWindow(main.Account.Store) { Owner = this };
            dialog.ShowDialog();
            // No result handling needed: the dialog writes through AccountStore, whose
            // Changed event already repaints the header and the Library.
        }

        /// <summary>Keeps the port list current without a dedicated Refresh button -
        /// re-reads available ports the moment the dropdown opens, so it's never
        /// showing stale data by the time the user is choosing from it.</summary>
        private void PortComboBox_DropDownOpened(object sender, EventArgs e)
        {
            if (DataContext is MainViewModel main)
                main.RefreshPortsCommand.Execute(null);
        }
    }
}
