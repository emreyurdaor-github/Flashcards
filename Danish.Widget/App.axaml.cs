using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Platform;
using System;
using System.Linq;
using Avalonia.Markup.Xaml;
using Danish.Widget.ViewModels;
using Danish.Widget.Views;
using Microsoft.Win32;

namespace Danish.Widget;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private TrayIcon? _trayIcon;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            _desktop = desktop;
            _mainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
            desktop.MainWindow = _mainWindow;
        }

        base.OnFrameworkInitializationCompleted();

        BuildTrayIcon();
    }

    private void BuildTrayIcon()
    {
        // Build menu items
        var restoreItem = new NativeMenuItem("Restore");
        restoreItem.Click += (_, _) =>
        {
            _mainWindow?.Show();
            if (_mainWindow != null) _mainWindow.WindowState = WindowState.Normal;
            _mainWindow?.Activate();
        };

        var startupItem = new NativeMenuItem("Run on Windows Startup");
        startupItem.Click += (_, _) => ToggleWindowsStartup();

        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) => _desktop?.Shutdown();

        // Build menu
        var menu = new NativeMenu();
        menu.Add(restoreItem);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(startupItem);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(exitItem);

        // Build tray icon
        _trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Danish.Widget/Assets/app.ico"))),
            ToolTipText = "Danish.Widget Widget",
            Menu = menu,
            IsVisible = true,
        };

        _trayIcon.Clicked += (_, _) =>
        {
            if (_mainWindow == null) return;
            if (!_mainWindow.IsVisible || _mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.Show();
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Activate();
            }
            else
            {
                _mainWindow.Hide();
            }
        };
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();
        foreach (var plugin in dataValidationPluginsToRemove)
            BindingPlugins.DataValidators.Remove(plugin);
    }

#pragma warning disable CA1416
    private void ToggleWindowsStartup()
    {
        try
        {
            const string runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            const string appName = "Danish.Widget";

            using var key = Registry.CurrentUser.OpenSubKey(runKey, writable: true);
            if (key == null) return;

            if (key.GetValue(appName) != null)
            {
                key.DeleteValue(appName);
            }
            else
            {
                var appPath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                key.SetValue(appName, appPath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ToggleWindowsStartup] Error: {ex.Message}");
        }
    }
#pragma warning restore CA1416
}