using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Windows;
using Newtonsoft.Json;
using InventoryManagementWin.Models;

namespace InventoryClient
{
    public partial class MainWindow : Window
    {
        private static readonly HttpClient client = new HttpClient();
        private string jwtToken = "";

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var loginWindow = new LoginWindow { Owner = this };
            if (loginWindow.ShowDialog() == true)
            {
                var loginData = new
                {
                    username = loginWindow.Username,
                    password = loginWindow.Password
                };
                var content = new StringContent(JsonConvert.SerializeObject(loginData), Encoding.UTF8, "application/json");
                var response = await client.PostAsync("https://localhost:44394/api/auth/login", content);
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
            }
        }

        private async void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(jwtToken))
            {
                MessageBox.Show("先にログインしてください");
                return;
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
            var response = await client.GetAsync("https://localhost:44394/api/inventory");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var items = JsonConvert.DeserializeObject<List<InventoryItem>>(json);
                InventoryGrid.ItemsSource = items;
            }
            else
            {
                MessageBox.Show("在庫取得失敗");
            }
        }
    }
}
