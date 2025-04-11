using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using InventoryClient.ViewModels;
using InventoryClient;
using InventoryClient.Models;
using System.Collections.Specialized;
using System.ComponentModel;
using static MaterialDesignThemes.Wpf.Theme.ToolBar;
using System.Runtime.CompilerServices;
using System.Configuration;

namespace InventoryClient.ViewModels
{
    public class AddInventoryVIewModel : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public string Barcode { get; set; }
        public string Supplier { get; set; }
        public string Storage_Location { get; set; }
        public DateTime _entry_Date { get; set; } = DateTime.Today;
        public DateTime Entry_Date
        {
            get => _entry_Date;
            set { _entry_Date = value; OnPropertyChanged(); }
        }
        public DateTime _expiration_Date { get; set; }
        public DateTime Expiration_Date
        {
            get => _expiration_Date;
            set { _expiration_Date = value; OnPropertyChanged(); }
        }
        public string Notes { get; set; }

        public ICommand AddInventoryCommand { get; }

        public AddInventoryVIewModel()
        {
            AddInventoryCommand = new RelayCommand(AddInventory);
        }

        private async void AddInventory()
        {
            var item = new InventoryItem
            {
                Name = this.Name,
                Category = this.Category,
                Quantity = this.Quantity,
                Price = this.Price,
                Barcode = this.Barcode,
                Supplier = this.Supplier,
                Storage_Location = this.Storage_Location,
                Entry_Date = this.Entry_Date,
                Expiration_Date = this.Expiration_Date,
                Notes = this.Notes
            };

            var json = JsonSerializer.Serialize(item);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var client = new HttpClient();
            //client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TokenManager.JwtToken);

            var response = await client.PostAsync("https://localhost:44394/api/inventory", content);

            if (response.IsSuccessStatusCode)
            {
                MessageBox.Show("在庫が登録されました。");
                Application.Current.Windows[Application.Current.Windows.Count - 1]?.Close();
            }
            else
            {
                MessageBox.Show("登録に失敗しました。");
            }
            client.Dispose();
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}