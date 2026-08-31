using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProtoVerseApp.Services;

namespace ProtoVerseApp.ViewModels
{
    /// <summary>
    /// Backs the sign-in control in the window's top-right corner. Thin by design -
    /// all state lives in <see cref="AccountStore"/>; this just exposes it for binding
    /// and raises <see cref="SignInRequested"/> when the user wants the picker, since
    /// showing a Window is the view's job, not a view model's.
    ///
    /// These profiles are not a security feature - see <see cref="AccountStore"/>.
    /// </summary>
    public partial class AccountViewModel : ObservableObject
    {
        public AccountStore Store { get; }

        /// <summary>Raised when the user clicks Sign in / Switch. MainWindow handles it
        /// by showing the picker dialog.</summary>
        public event Action? SignInRequested;

        public bool IsSignedIn => Store.IsSignedIn;

        public string DisplayName => Store.ActiveAccount?.DisplayName ?? "Not signed in";

        public string StatusLine => Store.IsSignedIn
            ? "Signed in"
            : "Sign in to track your kit";

        public AccountViewModel(AccountStore store)
        {
            Store = store;
            Store.Changed += Refresh;
        }

        private void Refresh()
        {
            OnPropertyChanged(nameof(IsSignedIn));
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(StatusLine));
        }

        [RelayCommand]
        private void SignIn() => SignInRequested?.Invoke();

        [RelayCommand]
        private void SignOut() => Store.SignOut();
    }
}
