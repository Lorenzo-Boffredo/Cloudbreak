using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace Cloudbreak.Core;

/// <summary>English and Italian strings. "auto" follows the Windows display language.</summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc I { get; } = new();

    private Dictionary<string, string> _t = En;
    public string Code { get; private set; } = "en";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key] => _t.TryGetValue(key, out var v) ? v : En.TryGetValue(key, out var e) ? e : key;

    public string F(string key, params object[] args) => string.Format(CultureInfo.InvariantCulture, this[key], args);

    public void Set(string choice)
    {
        string code = choice switch
        {
            "it" => "it",
            "en" => "en",
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "it" ? "it" : "en",
        };
        Code = code;
        _t = code == "it" ? It : En;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
    }

    private static readonly Dictionary<string, string> En = new()
    {
        ["hold"] = "Hold",
        ["break"] = "Break",
        ["paused"] = "Paused",
        ["ready"] = "Ready",
        ["offHours"] = "Off hours",
        ["round"] = "Round {0}",
        ["hintHold"] = "No vaping until {0}.",
        ["hintBreak"] = "Vape if you want to. Next hold at {0}.",
        ["hintPaused"] = "Paused. Resume to keep counting.",
        ["hintIdle"] = "Start a {0}-minute hold. It loops until you stop it.",
        ["hintOutside"] = "Outside your active hours. Back at {0}.",
        ["start"] = "Start",
        ["pause"] = "Pause",
        ["resume"] = "Resume",
        ["skipBreak"] = "Skip break",
        ["stop"] = "Stop",
        ["today"] = "Today",
        ["allDay"] = "All day",
        ["holdsDone"] = "Holds done",
        ["vapeFree"] = "Vape-free",
        ["skipped"] = "Breaks skipped",

        ["settingsTitle"] = "Cloudbreak settings",
        ["navTimer"] = "Timer",
        ["navAlerts"] = "Alerts",
        ["navGeneral"] = "General",
        ["roundLen"] = "One round lasts {0} minutes, then it starts again.",
        ["notifDesc"] = "A message on screen when the phase changes",
        ["soundDesc"] = "A soft chime when the phase changes. Works with or without notifications.",
        ["settings"] = "Settings",
        ["holdDesc"] = "Time without vaping",
        ["breakDesc"] = "Time you can vape",
        ["min"] = "min",
        ["activeHours"] = "Active hours",
        ["activeDesc"] = "Only run between these times",
        ["hoursOn"] = "Outside these hours Cloudbreak waits, then starts again at {0}.",
        ["hoursOff"] = "Cloudbreak runs all day until you stop it.",
        ["notif"] = "Notifications",
        ["posLabel"] = "Position",
        ["corner_tl"] = "Top left",
        ["corner_tr"] = "Top right",
        ["corner_bl"] = "Bottom left",
        ["corner_br"] = "Bottom right",
        ["sound"] = "Sound",
        ["fog"] = "Break fog",
        ["fogDesc"] = "A little violet mist in the corners of every screen when a break starts",
        ["login"] = "Start with Windows",
        ["loginDesc"] = "Opens in the tray when you turn on the PC",
        ["mini"] = "Mini timer",
        ["miniDesc"] = "Small always-on-top timer",
        ["accent"] = "Accent color",
        ["accViolet"] = "Violet",
        ["accWin"] = "Match Windows",
        ["accentNoteV"] = "Cloudbreak violet, used for breaks.",
        ["accentNoteW"] = "Uses your Windows accent color and follows it when you change it.",
        ["language"] = "Language",
        ["langDesc"] = "Automatic follows your Windows language",
        ["langAuto"] = "Automatic",
        ["applyNow"] = "Changes apply right away.",

        ["tBreakTitle"] = "Break time",
        ["tHoldTitle"] = "Break's over",
        ["tStartTitle"] = "Cloudbreak is on",
        ["tBreak"] = "You can vape for {0} minutes. Next hold starts at {1}.",
        ["tBreakOne"] = "You can vape for 1 minute. Next hold starts at {0}.",
        ["tHold"] = "Hold for {0} minutes, until {1}.",
        ["dismiss"] = "Dismiss",

        ["open"] = "Open Cloudbreak",
        ["quit"] = "Quit",
        ["tipHold"] = "Cloudbreak: hold until {0}",
        ["tipBreak"] = "Cloudbreak: break until {0}",
        ["tipPaused"] = "Cloudbreak: paused",
        ["tipIdle"] = "Cloudbreak: stopped",
        ["tipOff"] = "Cloudbreak: off hours, back at {0}",
        ["pin"] = "Keep on top",
        ["minimize"] = "Minimize",
        ["close"] = "Close",
    };

    private static readonly Dictionary<string, string> It = new()
    {
        ["hold"] = "Resisti",
        ["break"] = "Pausa",
        ["paused"] = "Sospeso",
        ["ready"] = "Pronto",
        ["offHours"] = "Fuori orario",
        ["round"] = "Giro {0}",
        ["hintHold"] = "Niente svapo fino alle {0}.",
        ["hintBreak"] = "Puoi svapare. Si riprende alle {0}.",
        ["hintPaused"] = "Sospeso. Riprendi per continuare il conteggio.",
        ["hintIdle"] = "Avvia {0} minuti senza svapo. Si ripete finché non lo fermi.",
        ["hintOutside"] = "Fuori dagli orari attivi. Riparte alle {0}.",
        ["start"] = "Avvia",
        ["pause"] = "Sospendi",
        ["resume"] = "Riprendi",
        ["skipBreak"] = "Salta la pausa",
        ["stop"] = "Ferma",
        ["today"] = "Oggi",
        ["allDay"] = "Tutto il giorno",
        ["holdsDone"] = "Giri completati",
        ["vapeFree"] = "Senza svapo",
        ["skipped"] = "Pause saltate",

        ["settingsTitle"] = "Impostazioni di Cloudbreak",
        ["navTimer"] = "Timer",
        ["navAlerts"] = "Avvisi",
        ["navGeneral"] = "Generale",
        ["roundLen"] = "Un giro dura {0} minuti, poi ricomincia.",
        ["notifDesc"] = "Un avviso a schermo quando cambia la fase",
        ["soundDesc"] = "Un rintocco quando cambia la fase. Funziona con o senza avvisi.",
        ["settings"] = "Impostazioni",
        ["holdDesc"] = "Tempo senza svapare",
        ["breakDesc"] = "Tempo in cui puoi svapare",
        ["min"] = "min",
        ["activeHours"] = "Orari attivi",
        ["activeDesc"] = "Funziona solo in questa fascia oraria",
        ["hoursOn"] = "Fuori orario Cloudbreak aspetta e riparte da solo alle {0}.",
        ["hoursOff"] = "Cloudbreak funziona tutto il giorno finché non lo fermi.",
        ["notif"] = "Notifiche",
        ["posLabel"] = "Posizione",
        ["corner_tl"] = "In alto a sinistra",
        ["corner_tr"] = "In alto a destra",
        ["corner_bl"] = "In basso a sinistra",
        ["corner_br"] = "In basso a destra",
        ["sound"] = "Suono",
        ["fog"] = "Nebbia della pausa",
        ["fogDesc"] = "Una nebbiolina viola agli angoli di ogni schermo quando inizia la pausa",
        ["login"] = "Avvia con Windows",
        ["loginDesc"] = "Si apre nella barra quando accendi il PC",
        ["mini"] = "Mini timer",
        ["miniDesc"] = "Timer piccolo sempre in primo piano",
        ["accent"] = "Colore principale",
        ["accViolet"] = "Viola",
        ["accWin"] = "Come Windows",
        ["accentNoteV"] = "Il viola di Cloudbreak, usato per le pause.",
        ["accentNoteW"] = "Usa il colore principale di Windows e lo segue quando lo cambi.",
        ["language"] = "Lingua",
        ["langDesc"] = "Automatica segue la lingua di Windows",
        ["langAuto"] = "Automatica",
        ["applyNow"] = "Le modifiche si applicano subito.",

        ["tBreakTitle"] = "È ora della pausa",
        ["tHoldTitle"] = "Pausa finita",
        ["tStartTitle"] = "Cloudbreak è attivo",
        ["tBreak"] = "Puoi svapare per {0} minuti. Si riprende alle {1}.",
        ["tBreakOne"] = "Puoi svapare per 1 minuto. Si riprende alle {0}.",
        ["tHold"] = "Resisti {0} minuti, fino alle {1}.",
        ["dismiss"] = "Chiudi",

        ["open"] = "Apri Cloudbreak",
        ["quit"] = "Esci",
        ["tipHold"] = "Cloudbreak: resisti fino alle {0}",
        ["tipBreak"] = "Cloudbreak: pausa fino alle {0}",
        ["tipPaused"] = "Cloudbreak: sospeso",
        ["tipIdle"] = "Cloudbreak: fermo",
        ["tipOff"] = "Cloudbreak: fuori orario, riparte alle {0}",
        ["pin"] = "Sempre in primo piano",
        ["minimize"] = "Riduci a icona",
        ["close"] = "Chiudi",
    };
}

/// <summary>XAML: Text="{c:T hold}" binds to the current language.</summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class TExtension : MarkupExtension
{
    public TExtension() { }
    public TExtension(string key) { Key = key; }

    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Loc.I, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
