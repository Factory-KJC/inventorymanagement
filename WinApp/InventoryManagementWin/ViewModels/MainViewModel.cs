using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Windows;
using System.Windows.Input;
using InventoryClient.Models;
using InventoryClient.Views;
using InventoryClient.ViewModels;
using InventoryManagementWin.Models;
using Newtonsoft.Json;
using System.Configuration;

namespace InventoryClient.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private static readonly HttpClient client = new HttpClient();
        private string jwtToken;

        public ObservableCollection<InventoryItem> InventoryItems { get; set; } = new ObservableCollection<InventoryItem>();

        public ICommand LoginCommand { get; }
        public ICommand LoadInventoryCommand { get; }

        public MainViewModel()
        {
            LoginCommand = new RelayCommand(Login);
            LoadInventoryCommand = new RelayCommand(LoadInventory);
        }
        private async void Login()
        {
            var loginWindow = new LoginWindow();
            var loginViewModel = loginWindow.DataContext as LoginViewModel;

            if (loginViewModel != null)
            {
                // ログイン完了イベントをウィンドウを表示する前に購読
                loginViewModel.LoginCompleted += async (sender, e) =>
                {
                    var loginData = new
                    {
                        username = e.Username,
                        password = e.Password
                    };

                    // サーバーにログインデータを送信
                    var content = new StringContent(JsonConvert.SerializeObject(loginData), Encoding.UTF8, "application/json");
                    var response = await client.PostAsync("https://localhost:44394/api/auth/login", content);

                    loginWindow.Close(); // ウィンドウを閉じる

                    if (response.IsSuccessStatusCode)
                    {
                        var result = JsonConvert.DeserializeObject<Dictionary<string, string>>(await response.Content.ReadAsStringAsync());
                        jwtToken = result["token"];
                        MessageBox.Show("ログイン成功");
                    }
                    else
                    {
                        MessageBox.Show("ログイン失敗");
                    }
                };

                // ログインウィンドウを表示
                loginWindow.ShowDialog();
            }
        }


        /// <summary>
        /// 在庫一覧を更新
        /// </summary>
        private async void LoadInventory()
        {
            if (string.IsNullOrEmpty(jwtToken))
            {
                MessageBox.Show("先にログインしてください");
                return;
            }

            try
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
                var response = await client.GetAsync("https://localhost:44394/api/inventory");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var items = JsonConvert.DeserializeObject<List<InventoryItem>>(json);

                    InventoryItems.Clear();
                    foreach (var item in items)
                    {
                        InventoryItems.Add(item);
                    }
                }
                else
                {
                    MessageBox.Show("在庫取得に失敗しました");
                }
            }
            catch (HttpRequestException ex)
            {
                MessageBox.Show($"接続エラー: {ex.Message}");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}