using System.Windows;

namespace InventoryClient
{
    public partial class LoginWindow : Window
    {
        public string Username { get; private set; }
        public string Password { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
        }
        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            Username = UsernameBox.Text;
            Password = PasswordBox.Password;
            DialogResult = true;
            Close();
        }
    }
}
