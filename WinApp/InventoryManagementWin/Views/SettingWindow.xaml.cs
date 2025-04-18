﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace InventoryClient.Views
{
    public partial class SettingWindow : Window
    {
        public SettingWindow()
        {
            InitializeComponent();
            var vm = new SettingViewModel();
            DataContext = vm;
            PasswordBox.Password = vm.DefaultPassword;
        }
        
        public void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingViewModel vm)
            {
                vm.DefaultPassword = PasswordBox.Password;
            }
        }
    }
}
