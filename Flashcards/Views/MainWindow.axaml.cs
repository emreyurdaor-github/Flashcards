using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Flashcards.Models;
using Flashcards.ViewModels;

namespace Flashcards.Views;

public partial class MainWindow : Window
{
    private bool _syncingScroll;
    private bool _isDoubledSize;
    private const double BaseWidth = 520;
    private const double BaseHeight = 680;

    public MainWindow()
    {
        InitializeComponent();
        PointerPressed += OnWidgetPointerPressed;
        Closed += OnClosed;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var danish = this.FindControl<ScrollViewer>("DanishWritingScroller");
        var english = this.FindControl<ScrollViewer>("EnglishWritingScroller");

        if (danish != null && english != null)
        {
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
        }

        var topic = this.FindControl<ScrollViewer>("SpeakingTopicScroller");
        var notes = this.FindControl<ScrollViewer>("SpeakingNotesScroller");

        if (topic != null && notes != null)
        {
            topic.ScrollChanged += (_, _) =>
            {
                if (_syncingScroll) return;
                _syncingScroll = true;
                notes.Offset = topic.Offset;
                _syncingScroll = false;
            };

            notes.ScrollChanged += (_, _) =>
            {
                if (_syncingScroll) return;
                _syncingScroll = true;
                topic.Offset = notes.Offset;
                _syncingScroll = false;
            };
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        
        if (DataContext is MainWindowViewModel viewModel)
        {
            // Replace the MinimizeToTrayCommand with one that handles window minimize
            viewModel.MinimizeToTrayCommand = new RelayCommand(() =>
            {
                WindowState = WindowState.Minimized;
                ShowInTaskbar = true;
            });

            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            RefreshDanishWritingInlines(viewModel);
            RefreshSpeakingTopicInlines(viewModel);

            // Find and configure the AutoCompleteBox
            var autoCompleteBox = this.FindControl<AutoCompleteBox>("SearchBox");
            if (autoCompleteBox != null)
            {
                autoCompleteBox.SelectionChanged += (sender, e) =>
                {
                    if (e.AddedItems.Count > 0 && e.AddedItems[0] is FlashcardEntry flashcard)
                    {
                        // Execute the command to select the flashcard
                        viewModel.SelectSearchResultCommand.Execute(flashcard);
                        // Close the dropdown to prevent issues
                        try
                        {
                            autoCompleteBox.IsDropDownOpen = false;
                        }
                        catch
                        {
                            // Ignore errors when closing dropdown
                        }
                    }
                };
            }
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MainWindowViewModel vm) return;

        if (e.PropertyName == nameof(MainWindowViewModel.CurrentDanishWritingSegments))
            RefreshDanishWritingInlines(vm);

        if (e.PropertyName == nameof(MainWindowViewModel.CurrentSpeakingTopicSegments))
            RefreshSpeakingTopicInlines(vm);
    }

    private void RefreshDanishWritingInlines(MainWindowViewModel vm)
    {
        var tb = this.FindControl<TextBlock>("DanishWritingTextBlock");
        if (tb == null) return;

        tb.Inlines ??= new InlineCollection();
        tb.Inlines.Clear();

        var greenBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80)); // readable green
        var whiteBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xCB, 0xD5, 0xE1)); // #FFCBD5E1
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

    private void OnWidgetPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            // Don't steal the click from interactive controls (ComboBox, Button, TextBox, etc.)
            if (IsInteractiveControl(e.Source as Visual))
                return;
            BeginMoveDrag(e);
        }
    }

    private static bool IsInteractiveControl(Visual? visual)
    {
        while (visual != null)
        {
            if (visual is Button or ComboBox or TextBox or AutoCompleteBox
                      or CheckBox or RadioButton or Slider or ScrollBar)
                return true;
            visual = visual.Parent as Visual;
        }
        return false;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.Dispose();
        }
    }

    private void OnResizeButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _isDoubledSize = !_isDoubledSize;
        Width = _isDoubledSize ? BaseWidth * 2 : BaseWidth;
        Height = _isDoubledSize ? BaseHeight * 2 : BaseHeight;
        if (sender is Button btn)
            btn.Content = _isDoubledSize ? "⤡" : "⤢";
    }
}
