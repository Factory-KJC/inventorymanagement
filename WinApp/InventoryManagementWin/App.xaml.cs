using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace InventoryManagementWin
{
    /// <summary>
    /// プロパティクラス
    /// </summary>
    public class Property
    {
        public string Host { get; set; }
        public string DefaultUser { get; set; }
        public string EncryptedDefaultPass { get; set; }

        /// <summary>
        /// App.configに保存された値を読み込む
        /// </summary>
        public void Load()
        {
            Host = ConfigurationManager.AppSettings["Host"];
            DefaultUser = ConfigurationManager.AppSettings["Username"];
            EncryptedDefaultPass = ConfigurationManager.AppSettings["EncryptedPassword"];
        }
        /// <summary>
        /// App.configに設定を保存
        /// </summary>
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
