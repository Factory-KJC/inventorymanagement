using System.Windows;
using InventoryClient.ViewModels;

namespace InventoryClient.Views
{
    /// <summary>
    /// AddInventoryWIndow.xaml の相互作用ロジック
    /// </summary>
    public partial class AddInventoryWindow : Window
    {
        public AddInventoryWindow()
        {
            InitializeComponent();
            var vm = new AddInventoryViewModel();
            DataContext = vm;
        }
    }
}
