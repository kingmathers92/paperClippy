using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using PaperClippy.Core.Mascot;
using PaperClippy.Core.Mascot.Animation;

namespace PaperClippy.App.Mascot;

/// <summary>
/// Draws Gem. It contains no behaviour at all: <see cref="Apply"/> maps a <see cref="Pose"/> (plain numbers from the
/// animator) onto transforms, opacities and which mouth is visible.
/// </summary>
public partial class GemView : UserControl
{
    private static readonly Color[] ConfettiColors =
    [
        Color.FromRgb(0xFF, 0x6F, 0xA5), Color.FromRgb(0xFF, 0xC9, 0x3C), Color.FromRgb(0x4D, 0xB6, 0xFF),
        Color.FromRgb(0x8B, 0xE0, 0x4E), Color.FromRgb(0xB0, 0x7C, 0xFF), Color.FromRgb(0xFF, 0x8A, 0x5C),
    ];

    private readonly Dictionary<MouthShape, UIElement> _mouths;
    private readonly Rectangle[] _confetti = new Rectangle[Confetti.Count];
    private readonly RotateTransform[] _confettiRotate = new RotateTransform[Confetti.Count];
    private readonly TranslateTransform[] _confettiShift = new TranslateTransform[Confetti.Count];
    private readonly TextBlock[] _zzz;
    private readonly Ellipse[] _thoughtDots;
    private readonly Dictionary<Accessory, UIElement> _accessories;

    private MouthShape _shownMouth = MouthShape.Smile;

    public GemView()
    {
        InitializeComponent();

        _mouths = new Dictionary<MouthShape, UIElement>
        {
            [MouthShape.Smile] = MouthSmile,
            [MouthShape.Grin] = MouthGrin,
            [MouthShape.Neutral] = MouthNeutral,
            [MouthShape.Open] = MouthOpen,
            [MouthShape.TalkSmall] = MouthTalkSmall,
            [MouthShape.TalkWide] = MouthTalkWide,
            [MouthShape.Wobbly] = MouthWobbly,
            [MouthShape.Smirk] = MouthSmirk,
        };

        _accessories = new Dictionary<Accessory, UIElement>
        {
            [Accessory.Glasses] = AccGlasses,
            [Accessory.PartyHat] = AccPartyHat,
            [Accessory.BowTie] = AccBowTie,
        };

        _zzz = [Zzz0, Zzz1, Zzz2];
        _thoughtDots = [ThoughtDot0, ThoughtDot1, ThoughtDot2];
        BuildConfetti();
        Apply(Pose.Neutral);
    }

    /// <summary>The canvas that defines Gem's design space; mouse positions are measured in it.</summary>
    public Canvas DesignCanvas => Root;

    public void ApplyPalette(GemPalette palette)
    {
        ((SolidColorBrush)Resources["WireEdge"]).Color = (Color)ColorConverter.ConvertFromString(palette.Edge);
        ((SolidColorBrush)Resources["WireBody"]).Color = (Color)ColorConverter.ConvertFromString(palette.Body);
        var shine = (Color)ColorConverter.ConvertFromString(palette.Shine);
        ((SolidColorBrush)Resources["WireShine"]).Color = Color.FromArgb(0xCC, shine.R, shine.G, shine.B);
    }

    public void SetAccessory(Accessory accessory)
    {
        foreach (var (kind, element) in _accessories)
            element.Visibility = kind == accessory ? Visibility.Visible : Visibility.Collapsed;
    }

    public void Apply(in Pose p)
    {
        // body: squash/stretch and tilt about the feet, then translate
        BodyScale.ScaleX = p.ScaleX;
        BodyScale.ScaleY = p.ScaleY;
        BodyRotate.Angle = p.Rotation;
        BodyShift.X = p.OffsetX;
        BodyShift.Y = p.OffsetY;

        // eyes: openness squashes the eyeball; pupils slide with the gaze; a curved lid line fades in as the eye closes
        EyeScaleL.ScaleY = Math.Max(0.06, p.EyeOpenL);
        EyeScaleR.ScaleY = Math.Max(0.06, p.EyeOpenR);
        PupilShiftL.X = PupilShiftR.X = p.GazeX * 4.2;
        PupilShiftL.Y = PupilShiftR.Y = p.GazeY * 5.2;
        LidL.Opacity = Math.Clamp((0.35 - p.EyeOpenL) / 0.2, 0, 1);
        LidR.Opacity = Math.Clamp((0.35 - p.EyeOpenR) / 0.2, 0, 1);

        // cheeks
        var cheek = Math.Min(1, 0.6 + 0.35 * p.CheekGlow);
        CheekL.Opacity = CheekR.Opacity = cheek;
        CheekScaleL.ScaleX = CheekScaleR.ScaleX = 1 + 0.18 * p.CheekGlow;
        CheekScaleL.ScaleY = CheekScaleR.ScaleY = 1 + 0.12 * p.CheekGlow;

        // mouth: exactly one shape visible
        if (p.Mouth != _shownMouth)
        {
            _mouths[_shownMouth].Visibility = Visibility.Collapsed;
            _mouths[p.Mouth].Visibility = Visibility.Visible;
            _shownMouth = p.Mouth;
        }

        // eyebrows
        var browsVisible = p.BrowOpacity > 0.01;
        BrowL.Visibility = BrowR.Visibility = browsVisible ? Visibility.Visible : Visibility.Collapsed;
        if (browsVisible)
        {
            BrowL.Opacity = BrowR.Opacity = Math.Min(1, p.BrowOpacity);
            BrowShiftL.Y = 76 - p.BrowLiftL * 7;
            BrowShiftR.Y = 76 - p.BrowLiftR * 7;
            BrowRotateL.Angle = -p.BrowTiltL * 18;
            BrowRotateR.Angle = p.BrowTiltR * 18;
        }

        ApplyOverlays(p);
    }

    private void ApplyOverlays(in Pose p)
    {
        // question mark
        Show(QuestionMark, p.QuestionMark);
        if (p.QuestionMark > 0.01)
        {
            var wobble = Math.Sin(p.ThoughtPhase * 2 * Math.PI);
            QuestionShift.Y = -4 * wobble;
            QuestionRotate.Angle = 10 * wobble;
        }

        // thought cloud: three dots pulse in turn
        Show(ThoughtGroup, p.Thought);
        if (p.Thought > 0.01)
        {
            for (var i = 0; i < _thoughtDots.Length; i++)
                _thoughtDots[i].Opacity = 0.25 + 0.75 * Math.Max(0, Math.Sin((p.ThoughtPhase * 3 - i) * Math.PI / 1.5));
        }

        // sleeping Zs drift up and fade
        var zzzVisible = p.Zzz > 0.01;
        ZzzGroup.Visibility = zzzVisible ? Visibility.Visible : Visibility.Collapsed;
        if (zzzVisible)
        {
            for (var i = 0; i < _zzz.Length; i++)
            {
                var t = (p.ZzzPhase + i / 3.0) % 1.0;
                var size = 14 + 14 * t;
                _zzz[i].FontSize = size;
                _zzz[i].Opacity = p.Zzz * Math.Sin(Math.PI * t);
                Canvas.SetLeft(_zzz[i], 132 + 16 * t);
                Canvas.SetTop(_zzz[i], 62 - 44 * t - size * 0.85);
            }
        }

        // working indicator: a spinning dashed ring
        Show(WorkGroup, p.Working);
        if (p.Working > 0.01)
            WorkRotate.Angle = p.WorkSpin;

        // confetti
        var confettiVisible = p.ConfettiTime >= 0;
        ConfettiLayer.Visibility = confettiVisible ? Visibility.Visible : Visibility.Collapsed;
        if (confettiVisible)
        {
            for (var i = 0; i < Confetti.Count; i++)
            {
                var c = Confetti.Evaluate(i, p.ConfettiTime);
                _confetti[i].Opacity = c.Opacity;
                _confettiShift[i].X = c.X - c.Size / 2;
                _confettiShift[i].Y = c.Y - c.Size / 4;
                _confettiRotate[i].Angle = c.Rotation;
            }
        }
    }

    private static void Show(UIElement element, double opacity)
    {
        element.Visibility = opacity > 0.01 ? Visibility.Visible : Visibility.Collapsed;
        element.Opacity = Math.Clamp(opacity, 0, 1);
    }

    private void BuildConfetti()
    {
        for (var i = 0; i < Confetti.Count; i++)
        {
            var particle = Confetti.Evaluate(i, 0); // size and colour never change over a particle's life
            var rotate = new RotateTransform { CenterX = particle.Size / 2, CenterY = particle.Size / 4 };
            var shift = new TranslateTransform();
            var rect = new Rectangle
            {
                Width = particle.Size,
                Height = particle.Size / 2,
                Fill = new SolidColorBrush(ConfettiColors[particle.ColorIndex]),
                RenderTransform = new TransformGroup { Children = { rotate, shift } },
                IsHitTestVisible = false,
            };

            _confetti[i] = rect;
            _confettiRotate[i] = rotate;
            _confettiShift[i] = shift;
            ConfettiLayer.Children.Add(rect);
        }
    }
}
