using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Danish.Widget.Models;
using Danish.Widget.ViewModels;

namespace Danish.Widget.Views;

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
            RefreshSpeakingTitleInlines(viewModel);
            RefreshSpeakingNotesTitleInlines(viewModel);
            RefreshSpeakingNotesInlines(viewModel);

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
        {
            RefreshSpeakingTopicInlines(vm);
            ScrollSpeakingTopicToWord(vm);
        }

        if (e.PropertyName == nameof(MainWindowViewModel.CurrentSpeakingTitleSegments))
            RefreshSpeakingTitleInlines(vm);

        if (e.PropertyName == nameof(MainWindowViewModel.CurrentSpeakingNotesTitleSegments))
            RefreshSpeakingNotesTitleInlines(vm);

        if (e.PropertyName == nameof(MainWindowViewModel.CurrentSpeakingNotesSegments))
            RefreshSpeakingNotesInlines(vm);

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
            tb.Inlines.Add(BuildSpeakingInline(seg,
                normalFore: seg.IsHighlighted ? greenBrush : whiteBrush,
                normalWeight: FontWeight.Normal,
                onHoverPlayDanish: seg.Tooltip is not null
                    ? word => vm.PlayVocabWord(word)
                    : null));
    }

    private void RefreshSpeakingTitleInlines(MainWindowViewModel vm)
    {
        var tb = this.FindControl<TextBlock>("Emne");
        if (tb == null) return;

        tb.Inlines ??= new InlineCollection();
        tb.Inlines.Clear();

        var yellowBrush = new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24));
        foreach (var seg in vm.CurrentSpeakingTitleSegments)
            tb.Inlines.Add(BuildSpeakingInline(seg,
                normalFore: yellowBrush,
                normalWeight: FontWeight.SemiBold,
                onHoverPlayDanish: seg.Tooltip is not null
                    ? word => vm.PlayVocabWord(word)
                    : null));
    }

    private void RefreshSpeakingNotesTitleInlines(MainWindowViewModel vm)
    {
        var tb = this.FindControl<TextBlock>("SpeakingNotesTitleTextBlock");
        if (tb == null) return;

        tb.Inlines ??= new InlineCollection();
        tb.Inlines.Clear();

        var yellowBrush = new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24));
        foreach (var seg in vm.CurrentSpeakingNotesTitleSegments)
            tb.Inlines.Add(BuildSpeakingInline(seg,
                normalFore: yellowBrush,
                normalWeight: FontWeight.SemiBold));
    }

    private void RefreshSpeakingNotesInlines(MainWindowViewModel vm)
    {
        var tb = this.FindControl<TextBlock>("SpeakingNotesTextBlock");
        if (tb == null) return;

        tb.Inlines ??= new InlineCollection();
        tb.Inlines.Clear();

        var whiteBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xCB, 0xD5, 0xE1));
        foreach (var seg in vm.CurrentSpeakingNotesSegments)
            tb.Inlines.Add(BuildSpeakingInline(seg,
                normalFore: whiteBrush,
                normalWeight: FontWeight.Normal));
    }

    /// <summary>
    /// Builds an Avalonia inline for a speaking segment.
    /// Bold+italic+underline key-word matches use InlineUIContainer so a ToolTip and
    /// click-to-play handler can be attached. Plain or value-only matches use Span/Run.
    /// </summary>
    private static Inline BuildSpeakingInline(
        Models.WritingSegment seg, IBrush normalFore, FontWeight normalWeight,
        Action<string>? onHoverPlayDanish = null)
    {
        if (!seg.IsBoldItalic)
            return new Run { Text = seg.Text, Foreground = normalFore, FontWeight = normalWeight };

        var underline = TextDecorations.Underline;

        if (seg.Tooltip is not null || onHoverPlayDanish is not null)
        {
            // Use an embedded TextBlock so we can attach ToolTip and hover-to-play
            var inner = new TextBlock
            {
                Text = seg.Text,
                Foreground = normalFore,
                FontWeight = FontWeight.Bold,
                FontStyle = FontStyle.Italic,
                TextDecorations = underline,
                Padding = new Thickness(0),
            };
            if (seg.Tooltip is not null)
                ToolTip.SetTip(inner, seg.Tooltip);
            if (onHoverPlayDanish is not null)
            {
                inner.Cursor = new Cursor(StandardCursorType.Hand);
                CancellationTokenSource? holdCts = null;
                inner.PointerEntered += (_, _) =>
                {
                    holdCts?.Cancel();
                    holdCts = new CancellationTokenSource();
                    var token = holdCts.Token;
                    Task.Delay(1000, token).ContinueWith(t =>
                    {
                        if (!t.IsCanceled)
                            onHoverPlayDanish(seg.Text);
                    }, TaskScheduler.Default);
                };
                inner.PointerExited += (_, _) =>
                {
                    holdCts?.Cancel();
                    holdCts = null;
                };
            }
            return new InlineUIContainer { Child = inner };
        }

        // No tooltip / no play — a Span with a nested Run is sufficient
        return new Span
        {
            Foreground = normalFore,
            FontWeight = FontWeight.Bold,
            FontStyle = FontStyle.Italic,
            TextDecorations = underline,
            Inlines = { new Run { Text = seg.Text } },
        };
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
