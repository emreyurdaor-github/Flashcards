using System;
using System.ComponentModel;
using Avalonia;
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

    // Swipe gesture tracking
    private Point _swipeStart;
    private bool _isSwiping;
    private const double SwipeThreshold = 60.0;   // minimum horizontal distance (dp)
    private const double SwipeMaxVertical = 80.0;  // maximum vertical drift allowed

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

        // Swipe gesture support
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _swipeStart = e.GetPosition(this);
        _isSwiping = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isSwiping) return;
        _isSwiping = false;

        if (DataContext is not MainWindowViewModel vm) return;

        var end = e.GetPosition(this);
        var deltaX = end.X - _swipeStart.X;
        var deltaY = end.Y - _swipeStart.Y;

        // Only trigger if horizontal movement exceeds threshold and vertical drift is small
        if (Math.Abs(deltaX) < SwipeThreshold || Math.Abs(deltaY) > SwipeMaxVertical)
            return;

        bool swipedLeft = deltaX < 0;   // swipe left → go to next
        bool swipedRight = deltaX > 0;  // swipe right → go to previous

        if (vm.SelectedTabIndex == 0 && vm.IsFlashcardPage && vm.CanNavigateFlashcards)
        {
            if (swipedLeft)
                vm.NextCardCommand.Execute(null);
            else if (swipedRight)
                vm.PreviousCardCommand.Execute(null);
        }
        else if (vm.SelectedTabIndex == 2 && vm.IsWritingPage)
        {
            if (swipedLeft)
                vm.NextWritingCommand.Execute(null);
            else if (swipedRight)
                vm.PreviousWritingCommand.Execute(null);
        }
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