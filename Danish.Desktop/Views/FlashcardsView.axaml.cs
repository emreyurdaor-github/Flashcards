using Avalonia.Controls;
using Danish.Desktop.ViewModels;

namespace Danish.Desktop.Views;

public partial class FlashcardsView : UserControl
{
    public FlashcardsView()
    {
        InitializeComponent();
        DataContext = new FlashcardsViewModel();
    }
}