using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using PaperClippy.Core.Mascot;
using PaperClippy.Core.Personality;

namespace PaperClippy.App.Mascot;

public sealed record SleepOption(int Minutes, string Label);

public sealed record AccessoryOption(Accessory Value, string Label);

/// <summary>
/// The "Gem Settings" dialog's state. Every change is applied immediately (there is no OK/Apply step), by calling the
/// callbacks supplied by the host; <see cref="Reload"/> refreshes the dialog when something else changes a setting.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    public static IReadOnlyList<SleepOption> SleepOptions { get; } =
    [
        new(0, "Never"), new(2, "After 2 minutes"), new(5, "After 5 minutes"),
        new(10, "After 10 minutes"), new(30, "After 30 minutes"), new(60, "After an hour"),
    ];

    public static IReadOnlyList<AccessoryOption> AccessoryOptions { get; } =
    [
        new(Accessory.None, "Nothing"), new(Accessory.Glasses, "Glasses"),
        new(Accessory.PartyHat, "Party hat"), new(Accessory.BowTie, "Bow tie"),
    ];

    private readonly Action<MascotSettings> _applySettings;
    private readonly Action<bool> _setVisible;
    private readonly Action<double> _setScale;
    private MascotSettings _current;
    private bool _loading;

    public SettingsViewModel(
        MascotSettings settings,
        bool visible,
        double scale,
        Action<MascotSettings> applySettings,
        Action<bool> setVisible,
        Action<double> setScale)
    {
        _applySettings = applySettings;
        _setVisible = setVisible;
        _setScale = setScale;
        _current = settings;
        Reload(settings, visible, scale);
    }

    [ObservableProperty] private bool _showGem;
    [ObservableProperty] private double _sizePercent = 100;
    [ObservableProperty] private AnimationIntensity _animation;
    [ObservableProperty] private PersonalityLevel _personality;
    [ObservableProperty] private ChatterFrequency _chatter;
    [ObservableProperty] private double _bubbleSeconds = 8;
    [ObservableProperty] private bool _quietMode;
    [ObservableProperty] private bool _quietHoursEnabled;
    [ObservableProperty] private string _quietFrom = "22:00";
    [ObservableProperty] private string _quietTo = "08:00";
    [ObservableProperty] private int _sleepAfterMinutes;
    [ObservableProperty] private bool _followPointer;
    [ObservableProperty] private bool _keepFocus;
    [ObservableProperty] private Accessory _accessory;

    /// <summary>Loads values into the dialog without applying them (they are already in effect).</summary>
    public void Reload(MascotSettings settings, bool visible, double scale)
    {
        _loading = true;
        try
        {
            _current = settings;
            ShowGem = visible;
            SizePercent = Math.Round(scale * 100);
            Animation = settings.Animation;
            Personality = settings.Personality;
            Chatter = settings.Chatter;
            BubbleSeconds = settings.BubbleSeconds;
            QuietMode = settings.QuietMode;
            QuietHoursEnabled = settings.QuietHoursEnabled;
            QuietFrom = Format(settings.EffectiveQuietFrom);
            QuietTo = Format(settings.EffectiveQuietTo);
            SleepAfterMinutes = SleepOptions.OrderBy(o => Math.Abs(o.Minutes - settings.SleepAfterMinutes)).First().Minutes;
            FollowPointer = settings.FollowPointer;
            KeepFocus = settings.KeepFocus;
            Accessory = settings.Accessory;
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>Puts back the last valid quiet-hours times (used when a half-typed time loses focus).</summary>
    public void RevertTimes()
    {
        _loading = true;
        QuietFrom = Format(_current.EffectiveQuietFrom);
        QuietTo = Format(_current.EffectiveQuietTo);
        _loading = false;
    }

    partial void OnShowGemChanged(bool value) { if (!_loading) _setVisible(value); }

    partial void OnSizePercentChanged(double value) { if (!_loading) _setScale(Math.Clamp(value, 50, 200) / 100.0); }

    partial void OnAnimationChanged(AnimationIntensity value) => Push(c => c with { Animation = value });

    partial void OnPersonalityChanged(PersonalityLevel value) => Push(c => c with { Personality = value });

    partial void OnChatterChanged(ChatterFrequency value) => Push(c => c with { Chatter = value });

    partial void OnBubbleSecondsChanged(double value) => Push(c => c with { BubbleSeconds = (int)Math.Round(value) });

    partial void OnQuietModeChanged(bool value) => Push(c => c with { QuietMode = value });

    partial void OnQuietHoursEnabledChanged(bool value) => Push(c => c with { QuietHoursEnabled = value });

    partial void OnQuietFromChanged(string value)
    {
        if (TryParse(value, out var time))
            Push(c => c with { QuietFrom = time });
    }

    partial void OnQuietToChanged(string value)
    {
        if (TryParse(value, out var time))
            Push(c => c with { QuietTo = time });
    }

    partial void OnSleepAfterMinutesChanged(int value) => Push(c => c with { SleepAfterMinutes = value });

    partial void OnFollowPointerChanged(bool value) => Push(c => c with { FollowPointer = value });

    partial void OnKeepFocusChanged(bool value) => Push(c => c with { KeepFocus = value });

    partial void OnAccessoryChanged(Accessory value) => Push(c => c with { Accessory = value });

    private void Push(Func<MascotSettings, MascotSettings> change)
    {
        if (_loading)
            return;

        _current = change(_current).Sanitized();
        _applySettings(_current);
    }

    private static string Format(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static bool TryParse(string text, out TimeOnly time)
        => TimeOnly.TryParseExact(text.Trim(), new[] { "H:mm", "HH:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
}
