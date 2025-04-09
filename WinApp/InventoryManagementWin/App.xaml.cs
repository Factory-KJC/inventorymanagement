using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Configuration;

namespace InventoryManagementWin
{
    public class property
    {
        public string Host { get; set; }
        public string DefaultUser { get; set; }
        public string EncryptedDefaultPass {  get; set; }

        public void Load()
        {
            Host = ConfigurationManager.AppSettings["Host"];
        }
        public void Save()
        {
            Properties.Settings.Default["Host"] = Host;
            Properties.Settings.Default["Username"] = DefaultUser;
            Properties.Settings.Default["EncryptedPassword"] = EncryptedDefaultPass;

            Properties.Settings.Default.Save();
        }
    }

    /// <summary>
    /// App.xaml の相互作用ロジック
    /// </summary>
    public partial class App : Application
    {
    }
}
