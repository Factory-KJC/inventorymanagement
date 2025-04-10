using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace InventoryClient
{
    /// <summary>
    /// プロパティクラス
    /// </summary>
    public static class Property
    {
        public static string settingFileName = ConfigurationManager.AppSettings["UserSettingFileName"]; // ユーザー設定ファイル名
    }

    /// <summary>
    /// App.xaml の相互作用ロジック
    /// </summary>
    public partial class App : Application
    {
    }
}
