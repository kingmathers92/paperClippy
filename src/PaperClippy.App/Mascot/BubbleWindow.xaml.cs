using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using PaperClippy.App.Interop;
using PaperClippy.Core.Geometry;
using PaperClippy.Core.Speech;

namespace PaperClippy.App.Mascot;

/// <summary>
/// Gem's speech bubble: a retro cream card with a pointer toward Gem, typewriter text, optional buttons or a menu list,
/// a dismiss button and timed dismissal. One instance is reused for every bubble.
/// </summary>
/// <remarks>
/// Call <see cref="Prepare"/> to measure a bubble (this yields its size in pixels), let
/// <see cref="BubblePlacement"/> choose where it goes, then call <see cref="Present"/>. The window raises events only
/// for things the user (or its own timer) did; closing it from code via <see cref="HideBubble"/> raises nothing.
/// </remarks>
public partial class BubbleWindow : Window
{
    private const double MaxContentWidth = 270;
    private const double MinBodyWidth = 150;
    private const double Pad = 14;
    private const double PointerLength = 14;
    private const double PointerBase = 18;
    private const double ShadowSize = 4;
    private const double CornerRadius = 10;

    private readonly DispatcherTimer _typeTimer = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly DispatcherTimer _dismissTimer = new(DispatcherPriority.Normal);
    private readonly DispatcherTimer _proximityTimer = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly Stopwatch _typeClock = new();
    private readonly Stopwatch _dismissClock = new();

    private BubbleContent? _content;
    private StackPanel? _panel;
    private TextBlock? _typedText;
    private Size _bodyDip;
    private double _dpiScale = 1;
    private TimeSpan _remaining;
    private bool _typingDone;
    private bool _noActivate;
    private DateTime? _awaySince;

    public BubbleWindow()
    {
        InitializeComponent();
        _typeTimer.Tick += OnTypeTick;
        _dismissTimer.Tick += (_, _) => Dismiss(BubbleDismissReason.Timeout);
        _proximityTimer.Tick += OnProximityTick;
        MouseEnter += (_, _) => PauseDismissal();
        MouseLeave += (_, _) => ResumeDismissal();
    }

    public event Action<string>? ChoiceInvoked;

    public event Action<string>? ActionInvoked;

    public event Action<BubbleDismissReason>? Dismissed;

    /// <summary>Where Gem is (physical pixels, a little padded). A menu closes itself if the pointer stays away from both this and the bubble.</summary>
    public RectD NearArea { get; set; }

    public bool IsShowingMenu => _content?.IsMenu == true && IsVisible;

    public void SetNoActivate(bool noActivate)
    {
        _noActivate = noActivate;
        NativeMethods.SetDesktopWidgetStyle(new WindowInteropHelper(this).EnsureHandle(), noActivate);
    }

    /// <summary>Builds the content, measures it, and returns the bubble body's size in physical pixels.</summary>
    public BubbleSpec Prepare(BubbleContent content, double dpiScale)
    {
        _content = content;
        _dpiScale = dpiScale;

        _panel = BuildContent(content);
        _panel.Measure(new Size(MaxContentWidth, double.PositiveInfinity));

        var contentWidth = Math.Max(MinBodyWidth - Pad * 2, _panel.DesiredSize.Width);
        _panel.Width = contentWidth;
        _panel.Measure(new Size(contentWidth, double.PositiveInfinity));

        _bodyDip = new Size(contentWidth + Pad * 2, _panel.DesiredSize.Height + Pad * 2);
        return new BubbleSpec(
            _bodyDip.Width * dpiScale,
            _bodyDip.Height * dpiScale,
            PointerLength * dpiScale,
            PointerBase * dpiScale,
            ShadowSize * dpiScale,
            CornerRadius * dpiScale);
    }

    /// <summary>Lays out the prepared content in the chosen position and shows it.</summary>
    public void Present(BubbleLayout layout)
    {
        if (_content is null || _panel is null)
            return;

        var s = _dpiScale;
        var windowWidth = layout.Window.Width / s;
        var windowHeight = layout.Window.Height / s;
        var bodyX = (layout.Body.X - layout.Window.X) / s;
        var bodyY = (layout.Body.Y - layout.Window.Y) / s;
        var body = new RectD(bodyX, bodyY, _bodyDip.Width, _bodyDip.Height);

        var outline = Geometry.Parse(BubbleGeometry.PathData(body, layout.Side, layout.PointerOffset / s, PointerLength, PointerBase, CornerRadius));
        outline.Freeze();

        Layer.Children.Clear();
        Layer.Width = windowWidth;
        Layer.Height = windowHeight;

        // hard offset shadow (retro), then the cream card with its dark outline, then a dotted "stitch" inside
        Layer.Children.Add(new Path
        {
            Data = outline,
            Fill = new SolidColorBrush(Color.FromArgb(0x59, 0, 0, 0)),
            RenderTransform = new TranslateTransform(3.5, 3.5),
            IsHitTestVisible = false,
        });

        var card = new Path { Data = outline, StrokeThickness = 1.6, StrokeLineJoin = PenLineJoin.Round };
        card.SetResourceReference(Shape.FillProperty, "Brush.Surface");
        card.SetResourceReference(Shape.StrokeProperty, "Brush.Ink");
        Layer.Children.Add(card);

        var stitch = new Rectangle
        {
            Width = body.Width - 10,
            Height = body.Height - 10,
            RadiusX = CornerRadius - 3,
            RadiusY = CornerRadius - 3,
            StrokeThickness = 1,
            StrokeDashArray = [1.5, 2.5],
            IsHitTestVisible = false,
        };
        stitch.SetResourceReference(Shape.StrokeProperty, "Brush.Line");
        Canvas.SetLeft(stitch, body.X + 5);
        Canvas.SetTop(stitch, body.Y + 5);
        Layer.Children.Add(stitch);

        Canvas.SetLeft(_panel, body.X + Pad);
        Canvas.SetTop(_panel, body.Y + Pad);
        Layer.Children.Add(_panel);

        var close = new Button { Style = (Style)FindResource("Retro.CloseButton") };
        close.Click += (_, _) => Dismiss(BubbleDismissReason.Closed);
        Canvas.SetLeft(close, body.Right - 22);
        Canvas.SetTop(close, body.Y + 4);
        Layer.Children.Add(close);

        Width = windowWidth;
        Height = windowHeight;

        var handle = new WindowInteropHelper(this).EnsureHandle();
        NativeMethods.SetDesktopWidgetStyle(handle, _noActivate);
        NativeMethods.SetWindowPos(handle, IntPtr.Zero, (int)Math.Round(layout.Window.X), (int)Math.Round(layout.Window.Y), 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);

        StopTimers();
        _typingDone = false;
        _remaining = _content.AutoDismiss ?? TimeSpan.Zero;
        if (!IsVisible)
            Show();

        if (_content.Typewriter && !_content.IsMenu && _typedText is not null && _typedText.Tag is string full && full.Length > 0)
        {
            _typedText.Text = string.Empty;
            _typeClock.Restart();
            _typeTimer.Start();
        }
        else
        {
            FinishTyping();
        }

        if (_content.IsMenu)
        {
            _awaySince = null;
            _proximityTimer.Start();
        }
    }

    /// <summary>Hides the bubble without raising any event (used when the brain closes it).</summary>
    public void HideBubble()
    {
        StopTimers();
        _content = null;
        if (IsVisible)
            Hide();
    }

    // ------------------------------------------------------------------ content

    private StackPanel BuildContent(BubbleContent content)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical };
        _typedText = null;

        if (content.IsMenu)
        {
            panel.Children.Add(MakeText(content.Text, bold: true, rightMargin: 16));
            panel.Children.Add(new Border { Height = 6 });

            foreach (var choice in content.Choices!)
            {
                var row = new Button
                {
                    Style = (Style)FindResource("Retro.Choice"),
                    Content = (choice.Checked ? "✓  " : "•  ") + choice.Label,
                    Tag = choice.Id,
                };
                row.Click += (_, _) => ChoiceInvoked?.Invoke(choice.Id);
                panel.Children.Add(row);
            }

            return panel;
        }

        // Spoken text: an invisible copy reserves the final size while a visible copy "types" itself.
        var grid = new Grid();
        var ghost = MakeText(content.Text, bold: false, rightMargin: 16);
        ghost.Opacity = 0;
        var typed = MakeText(content.Text, bold: false, rightMargin: 16);
        typed.Tag = content.Text;
        typed.Text = content.Typewriter ? string.Empty : content.Text;
        grid.Children.Add(ghost);
        grid.Children.Add(typed);
        panel.Children.Add(grid);
        _typedText = typed;

        if (content.Actions is { Count: > 0 })
        {
            var row = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            foreach (var action in content.Actions)
            {
                var button = new Button { Style = (Style)FindResource("Retro.Button"), Content = action.Label, Margin = new Thickness(0, 0, 6, 4) };
                button.Click += (_, _) => ActionInvoked?.Invoke(action.Id);
                row.Children.Add(button);
            }

            panel.Children.Add(row);
        }

        return panel;
    }

    private TextBlock MakeText(string text, bool bold, double rightMargin)
    {
        var block = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = MaxContentWidth,
            Margin = new Thickness(0, 0, rightMargin, 0),
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Ink");
        return block;
    }

    // ------------------------------------------------------------------ typing and timing

    private void OnTypeTick(object? sender, EventArgs e)
    {
        if (_typedText?.Tag is not string full)
        {
            FinishTyping();
            return;
        }

        var visible = Typewriter.VisibleLength(full, _typeClock.Elapsed.TotalSeconds);
        if (visible >= full.Length)
            FinishTyping();
        else if (_typedText.Text.Length != visible)
            _typedText.Text = full[..visible];
    }

    private void FinishTyping()
    {
        _typeTimer.Stop();
        if (_typedText?.Tag is string full)
            _typedText.Text = full;

        if (_typingDone)
            return;

        _typingDone = true;
        if (_remaining > TimeSpan.Zero)
            StartDismissTimer(_remaining);
    }

    private void StartDismissTimer(TimeSpan span)
    {
        _dismissTimer.Stop();
        _dismissTimer.Interval = span;
        _dismissClock.Restart();
        _dismissTimer.Start();
    }

    // Reading takes time: hovering over a bubble pauses its countdown, and leaving gives at least two more seconds.
    private void PauseDismissal()
    {
        if (!_dismissTimer.IsEnabled)
            return;

        _dismissTimer.Stop();
        _remaining = Max(_remaining - _dismissClock.Elapsed, TimeSpan.FromSeconds(2));
    }

    private void ResumeDismissal()
    {
        if (_typingDone && _content is not null && _remaining > TimeSpan.Zero && IsVisible)
            StartDismissTimer(_remaining);
    }

    private void OnProximityTick(object? sender, EventArgs e)
    {
        if (!IsShowingMenu || !NativeMethods.GetCursorPos(out var cursor))
            return;

        var near = (TryGetBounds(out var mine) && Contains(mine, cursor)) || Contains(NearArea, cursor);
        if (near)
        {
            _awaySince = null;
            return;
        }

        _awaySince ??= DateTime.UtcNow;
        if (DateTime.UtcNow - _awaySince.Value >= TimeSpan.FromSeconds(6))
            Dismiss(BubbleDismissReason.Timeout);
    }

    private bool TryGetBounds(out RectD rect)
    {
        rect = default;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero || !NativeMethods.GetWindowRect(handle, out var r))
            return false;

        rect = new RectD(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
        return true;
    }

    private static bool Contains(RectD rect, NativeMethods.POINT p)
        => p.X >= rect.X && p.X <= rect.Right && p.Y >= rect.Y && p.Y <= rect.Bottom;

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    private void StopTimers()
    {
        _typeTimer.Stop();
        _dismissTimer.Stop();
        _proximityTimer.Stop();
    }

    private void Dismiss(BubbleDismissReason reason)
    {
        HideBubble();
        Dismissed?.Invoke(reason);
    }

    /// <summary>Clicking a bubble that is still typing shows the rest at once.</summary>
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_typeTimer.IsEnabled)
            FinishTyping();
    }
}
