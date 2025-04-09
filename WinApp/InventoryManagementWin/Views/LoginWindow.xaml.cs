using System.Windows;
using InventoryClient.ViewModels;

namespace InventoryClient.Views
{
    public partial class LoginWindow : Window
    {
        public LoginViewModel ViewModel => DataContext as LoginViewModel;
        public LoginWindow()
        {
            InitializeComponent();
            var vm = new LoginViewModel();
            vm.CloseAction = () => this.DialogResult = true;
            this.DataContext = vm;
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel vm)
            {
                vm.Password = PasswordBox.Password;
            }
        }
    }
}
