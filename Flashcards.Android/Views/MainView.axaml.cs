using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using Flashcards.Models;
using Flashcards.ViewModels;

namespace Flashcards.Views;

public partial class MainView : UserControl
{
    private bool _syncingScroll;

    public MainView()
    {
        InitializeComponent();

        // Find and configure the AutoCompleteBox
        var autoCompleteBox = this.FindControl<AutoCompleteBox>("SearchBox");
        if (autoCompleteBox != null)
        {
            autoCompleteBox.SelectionChanged += (sender, e) =>
            {
                if (e.AddedItems.Count > 0 && e.AddedItems[0] is FlashcardEntry flashcard)
                {
                    if (DataContext is MainWindowViewModel viewModel)
                        viewModel.SelectSearchResultCommand.Execute(flashcard);
                    try { autoCompleteBox.IsDropDownOpen = false; } catch { }
                }
            };
        }

        Loaded += (_, _) =>
        {
            var danish = this.FindControl<ScrollViewer>("DanishWritingScroller");
            var english = this.FindControl<ScrollViewer>("EnglishWritingScroller");

            if (danish == null || english == null) return;

            danish.ScrollChanged += (_, _) =>
            {
                if (_syncingScroll) return;
                _syncingScroll = true;
                english.Offset = danish.Offset;
                _syncingScroll = false;
            };

            english.ScrollChanged += (_, _) =>
            {
                if (_syncingScroll) return;
                _syncingScroll = true;
                danish.Offset = english.Offset;
                _syncingScroll = false;
            };
        };
    }
}
