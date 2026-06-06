using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
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
                RefreshSpeakingTopicInlines(vm);
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

            // Apply initial layout based on current size
            ApplyWritingLayout(Bounds.Width, Bounds.Height);
        };

        // React to size/orientation changes
        SizeChanged += (_, e) => ApplyWritingLayout(e.NewSize.Width, e.NewSize.Height);

        // Swipe gesture support
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
    }

    /// <summary>
    /// Switches the writing grid between side-by-side (landscape) and stacked (portrait).
    /// </summary>
    private void ApplyWritingLayout(double width, double height)
    {
        var grid = this.FindControl<Grid>("WritingGrid");
        var danishBorder = this.FindControl<Border>("DanishWritingBorder");
        var englishBorder = this.FindControl<Border>("EnglishWritingBorder");

        if (grid == null || danishBorder == null || englishBorder == null) return;

        bool isLandscape = width > height;

        if (isLandscape)
        {
            // Side-by-side: two equal columns, one row
            grid.ColumnDefinitions = new ColumnDefinitions("*,*");
            grid.RowDefinitions = new RowDefinitions("*");

            Grid.SetRow(danishBorder, 0);
            Grid.SetColumn(danishBorder, 0);
            danishBorder.Margin = new Thickness(0, 6, 4, 0);

            Grid.SetRow(englishBorder, 0);
            Grid.SetColumn(englishBorder, 1);
            englishBorder.Margin = new Thickness(4, 6, 0, 0);
        }
        else
        {
            // Stacked: one column, two equal rows
            grid.ColumnDefinitions = new ColumnDefinitions("*");
            grid.RowDefinitions = new RowDefinitions("*,*");

            Grid.SetRow(danishBorder, 0);
            Grid.SetColumn(danishBorder, 0);
            danishBorder.Margin = new Thickness(0, 6, 0, 5);

            Grid.SetRow(englishBorder, 1);
            Grid.SetColumn(englishBorder, 0);
            englishBorder.Margin = new Thickness(0, 5, 0, 0);
        }
    }

    // ...existing code...


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
        if (sender is not MainWindowViewModel vm) return;

        if (e.PropertyName == nameof(MainWindowViewModel.CurrentDanishWritingSegments))
            RefreshDanishWritingInlines(vm);

        if (e.PropertyName == nameof(MainWindowViewModel.CurrentSpeakingTopicSegments))
        {
            RefreshSpeakingTopicInlines(vm);
            ScrollSpeakingTopicToWord(vm);
        }

        if (e.PropertyName == nameof(MainWindowViewModel.SpeakingShowTopic))
            ApplySpeakingTopicVisibility(vm.SpeakingShowTopic);
    }

    private void ApplySpeakingTopicVisibility(bool showNotes)
    {
        var grid = this.FindControl<Grid>("SpeakingPageGrid");
        if (grid == null) return;
        grid.RowDefinitions = showNotes
            ? new RowDefinitions("*,*")
            : new RowDefinitions("*,0");
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

    private void RefreshSpeakingTopicInlines(MainWindowViewModel vm)
    {
        var tb = this.FindControl<TextBlock>("SpeakingTopicTextBlock");
        if (tb == null) return;

        tb.Inlines ??= new InlineCollection();
        tb.Inlines.Clear();

        var greenBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80));
        var whiteBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xCB, 0xD5, 0xE1));
        foreach (var seg in vm.CurrentSpeakingTopicSegments)
        {
            tb.Inlines.Add(new Run
            {
                Text = seg.Text,
                Foreground = seg.IsHighlighted ? greenBrush : whiteBrush
            });
        }
    }

    private void ScrollSpeakingTopicToWord(MainWindowViewModel vm)
    {
        var scroller = this.FindControl<ScrollViewer>("SpeakingTopicScroller");
        var tb = this.FindControl<TextBlock>("SpeakingTopicTextBlock");
        if (scroller == null || tb == null) return;

        double progress = vm.SpeakingWordProgress;
        if (progress < 0) { scroller.Offset = new Avalonia.Vector(0, 0); return; }

        double contentHeight = tb.Bounds.Height;
        double viewportHeight = scroller.Viewport.Height;
        double targetOffset = progress * contentHeight - viewportHeight / 2;
        scroller.Offset = new Avalonia.Vector(0, Math.Max(0, targetOffset));
    }
}