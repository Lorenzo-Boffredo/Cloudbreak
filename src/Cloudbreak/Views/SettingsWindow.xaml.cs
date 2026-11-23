using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Cloudbreak.Core;

namespace Cloudbreak.Views;

public partial class SettingsWindow : Window
{
    private readonly AppController _c;
    private Settings S => _c.S;

    public SettingsWindow(AppController c)
    {
        _c = c;
        InitializeComponent();
        Height = Math.Min(Height, SystemParameters.WorkArea.Height - 40);
        SourceInitialized += (_, _) => Native.StyleWindow(this, Theme.IsDark);
        Loc.I.PropertyChanged += OnLanguageChanged;
        ShowPage(_lastPage);
        Load();
    }

    private static int _lastPage;

    private bool _switching;

    private void ShowPage(int i)
    {
        if (_switching) return;
        _switching = true;
        _lastPage = i;
        NavTimer.IsChecked = i == 0;
        NavAlerts.IsChecked = i == 1;
        NavGeneral.IsChecked = i == 2;
        PageTimer.Visibility = i == 0 ? Visibility.Visible : Visibility.Collapsed;
        PageAlerts.Visibility = i == 1 ? Visibility.Visible : Visibility.Collapsed;
        PageGeneral.Visibility = i == 2 ? Visibility.Visible : Visibility.Collapsed;
        _switching = false;
    }

    private void Nav_Click(object s, RoutedEventArgs e) =>
        ShowPage(ReferenceEquals(s, NavAlerts) ? 1 : ReferenceEquals(s, NavGeneral) ? 2 : 0);
    private void OnLanguageChanged(object? s, PropertyChangedEventArgs e) => Load();

    protected override void OnClosed(EventArgs e)
    {
        Loc.I.PropertyChanged -= OnLanguageChanged;
        base.OnClosed(e);
    }

    public void ApplyTheme()
    {
        Native.StyleWindow(this, Theme.IsDark);
        Load();
    }

    /// <summary>Shows settings changed elsewhere (the tray menu).</summary>
    public void Reload() => Load();

    private void Load()
    {
        var L = Loc.I;
        HoldVal.Text = $"{S.HoldMinutes} {L["min"]}";
        BreakVal.Text = $"{S.BreakMinutes} {L["min"]}";
        RhythmHold.Width = new GridLength(S.HoldMinutes, GridUnitType.Star);
        RhythmBreak.Width = new GridLength(S.BreakMinutes, GridUnitType.Star);
        RoundLen.Text = L.F("roundLen", S.HoldMinutes + S.BreakMinutes);
        foreach (RadioButton r in Presets.Children)
            r.IsChecked = (string)r.Tag == $"{S.HoldMinutes},{S.BreakMinutes}";

        ActiveOn.IsChecked = S.ActiveHoursEnabled;
        StartVal.Text = S.ActiveStart;
        EndVal.Text = S.ActiveEnd;
        HoursRow.Opacity = S.ActiveHoursEnabled ? 1 : 0.45;
        HoursRow.IsEnabled = S.ActiveHoursEnabled;
        HoursNote.Text = S.ActiveHoursEnabled ? L.F("hoursOn", S.ActiveStart) : L["hoursOff"];

        NotifyOn.IsChecked = S.Notifications;
        PositionBlock.Opacity = S.Notifications ? 1 : 0.45;
        PositionBlock.IsEnabled = S.Notifications;
        CornerTL.IsChecked = S.NotificationCorner == "tl";
        CornerTR.IsChecked = S.NotificationCorner == "tr";
        CornerBL.IsChecked = S.NotificationCorner == "bl";
        CornerBR.IsChecked = S.NotificationCorner == "br";
        PosName.Text = L["corner_" + S.NotificationCorner];
        // Tiles and color options have no text of their own: give screen readers a name.
        System.Windows.Automation.AutomationProperties.SetName(CornerTL, L["corner_tl"]);
        System.Windows.Automation.AutomationProperties.SetName(CornerTR, L["corner_tr"]);
        System.Windows.Automation.AutomationProperties.SetName(CornerBL, L["corner_bl"]);
        System.Windows.Automation.AutomationProperties.SetName(CornerBR, L["corner_br"]);
        System.Windows.Automation.AutomationProperties.SetName(AccViolet, L["accViolet"]);
        System.Windows.Automation.AutomationProperties.SetName(AccWindows, L["accWin"]);

        SoundOn.IsChecked = S.Sound;
        FogOn.IsChecked = S.Fog;
        LoginOn.IsChecked = S.StartWithWindows;
        MiniOn.IsChecked = S.MiniTimer;

        AccViolet.IsChecked = S.Accent == "violet";
        AccWindows.IsChecked = S.Accent == "windows";
        AccentNote.Text = S.Accent == "windows" ? L["accentNoteW"] : L["accentNoteV"];
        var probe = new Settings { Accent = "violet" };
        VioletDot.Fill = new SolidColorBrush(Theme.AccentFor(probe, Theme.IsDark));
        probe.Accent = "windows";
        WindowsDot.Fill = new SolidColorBrush(Theme.AccentFor(probe, Theme.IsDark));

        LangAuto.IsChecked = S.Language == "auto";
        LangEn.IsChecked = S.Language == "en";
        LangIt.IsChecked = S.Language == "it";
    }

    private void Commit(SettingsChange what)
    {
        S.Clamp();
        _c.ApplySettings(what);
        Load();
    }

    // ---------- Rhythm ----------
    private void HoldMinus(object s, RoutedEventArgs e) { S.HoldMinutes -= 5; Commit(SettingsChange.Timing); }
    private void HoldPlus(object s, RoutedEventArgs e) { S.HoldMinutes += 5; Commit(SettingsChange.Timing); }
    private void BreakMinus(object s, RoutedEventArgs e) { S.BreakMinutes -= 1; Commit(SettingsChange.Timing); }
    private void BreakPlus(object s, RoutedEventArgs e) { S.BreakMinutes += 1; Commit(SettingsChange.Timing); }

    private void Preset_Click(object s, RoutedEventArgs e)
    {
        var parts = ((string)((FrameworkElement)s).Tag).Split(',');
        S.HoldMinutes = int.Parse(parts[0]);
        S.BreakMinutes = int.Parse(parts[1]);
        Commit(SettingsChange.Timing);
    }

    // ---------- Active hours ----------
    private void Active_Click(object s, RoutedEventArgs e) { S.ActiveHoursEnabled = ActiveOn.IsChecked == true; Commit(SettingsChange.Timing); }

    private static string Shift(TimeSpan t, int minutes)
    {
        var total = ((int)t.TotalMinutes + minutes) % 1440;
        if (total < 0) total += 1440;
        return Settings.FormatTime(TimeSpan.FromMinutes(total));
    }

    private void StartMinus(object s, RoutedEventArgs e) { S.ActiveStart = Shift(S.ActiveStartTime, -30); Commit(SettingsChange.Timing); }
    private void StartPlus(object s, RoutedEventArgs e) { S.ActiveStart = Shift(S.ActiveStartTime, 30); Commit(SettingsChange.Timing); }
    private void EndMinus(object s, RoutedEventArgs e) { S.ActiveEnd = Shift(S.ActiveEndTime, -30); Commit(SettingsChange.Timing); }
    private void EndPlus(object s, RoutedEventArgs e) { S.ActiveEnd = Shift(S.ActiveEndTime, 30); Commit(SettingsChange.Timing); }

    // ---------- Alerts and startup ----------
    private void Toggle_Click(object s, RoutedEventArgs e)
    {
        S.Notifications = NotifyOn.IsChecked == true;
        S.Sound = SoundOn.IsChecked == true;
        S.Fog = FogOn.IsChecked == true;
        S.StartWithWindows = LoginOn.IsChecked == true;
        S.MiniTimer = MiniOn.IsChecked == true;
        Commit(SettingsChange.Behavior);
    }

    private void Corner_Click(object s, RoutedEventArgs e)
    {
        S.NotificationCorner = (string)((FrameworkElement)s).Tag;
        Commit(SettingsChange.Corner);
    }

    // ---------- Appearance ----------
    private void Accent_Click(object s, RoutedEventArgs e)
    {
        S.Accent = (string)((FrameworkElement)s).Tag;
        Commit(SettingsChange.Look);
    }

    private void Lang_Click(object s, RoutedEventArgs e)
    {
        S.Language = (string)((FrameworkElement)s).Tag;
        Commit(SettingsChange.Look);
    }

    private void Close_Click(object s, RoutedEventArgs e) => Close();
}
