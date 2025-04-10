using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using InventoryClient;
using InventoryClient.Models;
using InventoryClient.ViewModels;

public class SettingViewModel : ViewModelBase
{
    private string _hostName;
    private string _defaultUsername;
    private string _defaultPassword;

    public string HostName
    {
        get => _hostName;
        set => SetProperty(ref _hostName, value);
    }

    public string DefaultUsername
    {
        get => _defaultUsername;
        set => SetProperty(ref _defaultUsername, value);
    }

    public string DefaultPassword
    {
        get => _defaultPassword;
        set => SetProperty(ref _defaultPassword, value);
    }

    public RelayCommand SaveCommand { get; }

    public SettingViewModel()
    {
        LoadSettings();
        SaveCommand = new RelayCommand(SaveSettings);
    }

    /// <summary>
    /// 設定ファイルから設定を読み込む
    /// </summary>
    private void LoadSettings()
    {
        if (File.Exists(Property.settingFileName))
        {
            var json = File.ReadAllText(Property.settingFileName);
            var setting = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);

            HostName = setting.Host;
            DefaultUsername = setting.DefaultUsername;
            DefaultPassword = setting.DecodedPassword(); // Base64復号
        }
    }

    /// <summary>
    /// 設定ファイルに設定を保存
    /// </summary>
    private void SaveSettings()
    {
        var setting = new AppSettings
        {
            Host = HostName,
            DefaultUsername = DefaultUsername,
            EncodedPassword = Convert.ToBase64String(Encoding.UTF8.GetBytes(DefaultPassword))
        };

        var json = System.Text.Json.JsonSerializer.Serialize(setting, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Property.settingFileName, json);

        MessageBox.Show("設定を保存しました", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
