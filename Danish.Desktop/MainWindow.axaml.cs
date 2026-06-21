using Avalonia.Controls;
using Danish.Desktop.ViewModels;

namespace Danish.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}