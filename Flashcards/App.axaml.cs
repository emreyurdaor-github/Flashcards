using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System;
using System.Linq;
using Avalonia.Markup.Xaml;
using Flashcards.ViewModels;
using Flashcards.Views;

namespace Flashcards;

public partial class App : Application
{
    private TrayIcon? _trayIcon;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            
            var mainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
            
            desktop.MainWindow = mainWindow;
            
            // Add system tray icon
            SetupTrayIcon(mainWindow, desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon(MainWindow mainWindow, IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            _trayIcon = new TrayIcon
            {
                Icon = new WindowIcon("avares://Flashcards/Assets/app.ico"),
                IsVisible = true,
                Menu = new NativeMenu()
            };

            var showMenuItem = new NativeMenuItem { Header = "Show" };
            showMenuItem.Click += (sender, e) =>
            {
                mainWindow.Show();
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Activate();
            };

            var exitMenuItem = new NativeMenuItem { Header = "Exit" };
            exitMenuItem.Click += (sender, e) =>
            {
                desktop.Shutdown();
            };

            _trayIcon.Menu?.Items?.Add(showMenuItem);
            _trayIcon.Menu?.Items?.Add(exitMenuItem);

            // Handle tray icon click to toggle window
            _trayIcon.Clicked += (sender, e) =>
            {
                if (mainWindow.WindowState == WindowState.Minimized || !mainWindow.IsVisible)
                {
                    mainWindow.Show();
                    mainWindow.WindowState = WindowState.Normal;
                    mainWindow.Activate();
                }
                else
                {
                    mainWindow.Hide();
                }
            };

            System.Diagnostics.Debug.WriteLine("Tray icon setup completed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to setup tray icon: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}