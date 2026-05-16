using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
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

        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.PropertyChanged += OnViewModelPropertyChanged;
                RefreshDanishWritingInlines(vm);
            }
        };

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

            if (DataContext is MainWindowViewModel vm)
                RefreshDanishWritingInlines(vm);
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.CurrentDanishWritingSegments) &&
            sender is MainWindowViewModel vm)
        {
            RefreshDanishWritingInlines(vm);
        }
    }

    private void RefreshDanishWritingInlines(MainWindowViewModel vm)
    {
        var tb = this.FindControl<TextBlock>("DanishWritingTextBlock");
        if (tb == null) return;

        tb.Inlines ??= new InlineCollection();
        tb.Inlines.Clear();

        var greenBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80));
        var whiteBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xCB, 0xD5, 0xE1));
        foreach (var seg in vm.CurrentDanishWritingSegments)
        {
            tb.Inlines.Add(new Run
            {
                Text = seg.Text,
                Foreground = seg.IsHighlighted ? greenBrush : whiteBrush
            });
        }
    }
}