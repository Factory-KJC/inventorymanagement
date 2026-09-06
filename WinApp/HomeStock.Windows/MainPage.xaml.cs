using HomeStock.Windows.ViewModels;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace HomeStock.Windows;

/// <summary>MAUI画面をWinUI 3のUSB-HIDキー入力へ接続します。</summary>
public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;
    private Microsoft.UI.Xaml.FrameworkElement? _nativeRoot;

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        AttachKeyboardHandler();
        await _viewModel.InitializeAsync();
    }

    private void OnUnloaded(object? sender, EventArgs e) => DetachKeyboardHandler();

    private void AttachKeyboardHandler()
    {
        if (Handler?.PlatformView is not Microsoft.UI.Xaml.FrameworkElement root || ReferenceEquals(root, _nativeRoot))
        {
            return;
        }

        DetachKeyboardHandler();
        _nativeRoot = root;
        _nativeRoot.AddHandler(Microsoft.UI.Xaml.UIElement.KeyDownEvent, new KeyEventHandler(OnNativeKeyDown), true);
    }

    private void DetachKeyboardHandler()
    {
        if (_nativeRoot is null)
        {
            return;
        }

        _nativeRoot.RemoveHandler(Microsoft.UI.Xaml.UIElement.KeyDownEvent, new KeyEventHandler(OnNativeKeyDown));
        _nativeRoot = null;
    }

    private async void OnNativeKeyDown(object sender, KeyRoutedEventArgs e)
    {
        char? character = e.Key switch
        {
            >= VirtualKey.Number0 and <= VirtualKey.Number9 => (char)('0' + (int)e.Key - (int)VirtualKey.Number0),
            >= VirtualKey.NumberPad0 and <= VirtualKey.NumberPad9 => (char)('0' + (int)e.Key - (int)VirtualKey.NumberPad0),
            VirtualKey.Enter => '\r',
            _ => null,
        };
        if (character.HasValue)
        {
            await _viewModel.PushScannerCharacterAsync(character.Value);
        }
    }
}
