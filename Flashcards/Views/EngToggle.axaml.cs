using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Flashcards.Views;

// partial is required - Avalonia's source generator always emits a matching partial class
// from EngToggle.axaml that provides InitializeComponent() and the x:Name fields.
// The "unresolved symbol" warnings in Rider are cosmetic and disappear after the first build.
public partial class EngToggle : UserControl
{
    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<EngToggle, bool>(
            nameof(IsChecked),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<double> TrackWidthProperty =
        AvaloniaProperty.Register<EngToggle, double>(nameof(TrackWidth), 68);

    public static readonly StyledProperty<double> TrackHeightProperty =
        AvaloniaProperty.Register<EngToggle, double>(nameof(TrackHeight), 20);

    public static readonly StyledProperty<double> LabelFontSizeProperty =
        AvaloniaProperty.Register<EngToggle, double>(nameof(LabelFontSize), 12);

    public static readonly StyledProperty<double> SegmentFontSizeProperty =
        AvaloniaProperty.Register<EngToggle, double>(nameof(SegmentFontSize), 10);

    public bool IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public double TrackWidth
    {
        get => GetValue(TrackWidthProperty);
        set => SetValue(TrackWidthProperty, value);
    }

    public double TrackHeight
    {
        get => GetValue(TrackHeightProperty);
        set => SetValue(TrackHeightProperty, value);
    }

    public double LabelFontSize
    {
        get => GetValue(LabelFontSizeProperty);
        set => SetValue(LabelFontSizeProperty, value);
    }

    public double SegmentFontSize
    {
        get => GetValue(SegmentFontSizeProperty);
        set => SetValue(SegmentFontSizeProperty, value);
    }

    // Cached control references populated after XAML load
    private TextBlock? _labelText;
    private Border?    _track;
    private Border?    _offSegment;
    private TextBlock? _offText;
    private Border?    _onSegment;
    private TextBlock? _onText;

    static EngToggle()
    {
        IsCheckedProperty.Changed.AddClassHandler<EngToggle>((x, _) => x.UpdateVisualState());
    }

    public EngToggle()
    {
        InitializeComponent();

        // Grab references from the source-generated named fields
        _labelText  = LabelText;
        _track      = Track;
        _offSegment = OffSegment;
        _offText    = OffText;
        _onSegment  = OnSegment;
        _onText     = OnText;

        Cursor = new Cursor(StandardCursorType.Hand);
        UpdateVisualState();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TrackWidthProperty && _track is not null)
            _track.Width = (double)change.NewValue!;
        else if (change.Property == TrackHeightProperty && _track is not null)
            _track.Height = (double)change.NewValue!;
        else if (change.Property == LabelFontSizeProperty && _labelText is not null)
            _labelText.FontSize = (double)change.NewValue!;
        else if (change.Property == SegmentFontSizeProperty)
        {
            var size = (double)change.NewValue!;
            if (_offText is not null) _offText.FontSize = size;
            if (_onText is not null)  _onText.FontSize  = size;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        IsChecked = !IsChecked;
        e.Handled = true;
    }

    private void UpdateVisualState()
    {
        if (_offSegment is null || _onSegment is null) return;

        if (IsChecked)
        {
            // ON active = bright green
            _onSegment.Background  = new SolidColorBrush(Color.Parse("#1A4731"));
            _onText!.Foreground    = new SolidColorBrush(Color.Parse("#22C55E"));
            _offSegment.Background = new SolidColorBrush(Color.Parse("#3C3F41"));
            _offText!.Foreground   = new SolidColorBrush(Color.Parse("#6B7280"));
        }
        else
        {
            // OFF active = bright red
            _offSegment.Background = new SolidColorBrush(Color.Parse("#4B1C1C"));
            _offText!.Foreground   = new SolidColorBrush(Color.Parse("#EF4444"));
            _onSegment.Background  = new SolidColorBrush(Color.Parse("#3C3F41"));
            _onText!.Foreground    = new SolidColorBrush(Color.Parse("#6B7280"));
        }
    }
}