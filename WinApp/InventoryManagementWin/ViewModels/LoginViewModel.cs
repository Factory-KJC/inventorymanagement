using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace InventoryClient.ViewModels
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        private string _username;
        private string _password;

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        public string Password
        {
            get => _password;
            set { _password = value; OnPropertyChanged(); }
        }

        public ICommand SubmitCommand { get; }
        public Action CloseAction { get; set; }

        public event EventHandler<LoginEventArgs> LoginCompleted;

        public LoginViewModel()
        {
            SubmitCommand = new RelayCommand(OnSubmit);
        }

        
        private void OnSubmit()
        {
            LoginCompleted?.Invoke(this, new LoginEventArgs
            {
                Username = this.Username,
                Password = this.Password
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class LoginEventArgs : EventArgs
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

    }
}
