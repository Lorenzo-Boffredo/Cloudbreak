<div align="center">

<img src="docs/icon.png" width="96" alt="Cloudbreak icon">

# Cloudbreak

**Twenty-five minutes without. Five minutes with.**

A pomodoro timer for vaping less, for Windows.<br>
Start it once and it loops on its own: hold, break, hold again, until you stop it.

<a href="../../releases/latest"><img src="https://img.shields.io/badge/Download_for_Windows-7A64DA?style=for-the-badge" alt="Download for Windows"></a>

<img src="https://img.shields.io/badge/Windows-10%20%7C%2011-2A2E37?style=flat-square" alt="Windows 10 and 11">
<img src="https://img.shields.io/badge/.NET-10-2A2E37?style=flat-square" alt=".NET 10">
<img src="https://img.shields.io/badge/works-offline-2A2E37?style=flat-square" alt="Works offline">
<img src="https://img.shields.io/badge/languages-EN%20%7C%20IT-2A2E37?style=flat-square" alt="English and Italian">
<a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-2A2E37?style=flat-square" alt="MIT license"></a>

<br><br>

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/screens/main-break-dark.png">
  <img src="docs/screens/main-break-light.png" width="430" alt="Cloudbreak during a break: the window turns violet and shows the time left">
</picture>

</div>

## How it works

The day is a row of rounds. Each round is a **hold**, time without vaping, followed by a **break**, time you can vape. When one ends, the next one starts and you get a quiet notification.

<table>
  <tr>
    <td align="center" width="50%">
      <picture>
        <source media="(prefers-color-scheme: dark)" srcset="docs/screens/main-hold-dark.png">
        <img src="docs/screens/main-hold-light.png" alt="Hold: black and white, with the time until the next break">
      </picture>
      <br><b>Hold</b><br>Black and white. It tells you when you can vape next.
    </td>
    <td align="center" width="50%">
      <picture>
        <source media="(prefers-color-scheme: dark)" srcset="docs/screens/main-break-dark.png">
        <img src="docs/screens/main-break-light.png" alt="Break: the window turns violet">
      </picture>
      <br><b>Break</b><br>The only color in the app. Skip it if you don't need it.
    </td>
  </tr>
</table>

The bar under **Today** shows every hold and break of the day, so you can see how it is going at a glance.

## Features

- **Your rhythm.** Pick the hold and break lengths, or use a preset: 25/5, 45/5, 60/10, 90/10.
- **Active hours.** Outside them Cloudbreak waits, then starts again on its own.
- **Starts with Windows** and lives next to the clock. The tray ring fills as the round goes by.
- **Notifications** in the screen corner you choose. Sound on or off, separately.
- **Mini timer.** A small always-on-top chip with the time left. Drag it anywhere.
- **Today's numbers.** Rounds completed, vape-free time, breaks skipped. Paused time never counts.
- **Fits your Windows.** Light or dark theme, violet or your Windows accent color, English or Italian.

## Settings

<table>
  <tr>
    <td width="50%">
      <picture>
        <source media="(prefers-color-scheme: dark)" srcset="docs/screens/settings-timer-dark.png">
        <img src="docs/screens/settings-timer-light.png" alt="Timer settings: hold and break length, presets, active hours">
      </picture>
    </td>
    <td width="50%">
      <picture>
        <source media="(prefers-color-scheme: dark)" srcset="docs/screens/settings-alerts-dark.png">
        <img src="docs/screens/settings-alerts-light.png" alt="Alert settings: notifications and their position, sound, mini timer">
      </picture>
    </td>
  </tr>
</table>

## Install

1. Download the **Cloudbreak-Setup** installer from the [latest release](../../releases/latest).
2. Run it. Windows may show *"Windows protected your PC"* because the installer isn't signed: click **More info**, then **Run anyway**.
3. Choose **Just for me** (no admin password needed), pick a folder, and finish.

Needs Windows 10 or 11 (64-bit) and the free [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0). If it's missing, the installer tells you and opens the download page.

**Uninstall** from *Settings → Apps → Installed apps → Cloudbreak*. It removes everything it added: the app, its settings and stats, the start-with-Windows entry, the shortcuts and the tray icon record.

## Light on your PC

- Ticks once a second only while something is on screen. Otherwise it sleeps until the next phase or the next minute.
- No redraws while the screen is off or the PC is locked.
- Runs in Windows efficiency mode when no window is open.
- About 20 MB of memory and a fraction of a percent of one CPU core.

## Privacy

Cloudbreak never connects to the internet. No accounts, no telemetry, no updates in the background. Settings and today's numbers stay on your PC, in `%APPDATA%\Cloudbreak`.

## Build from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and [Inno Setup 6](https://jrsoftware.org/isinfo.php).

```powershell
powershell -ExecutionPolicy Bypass -File build\build.ps1
```

The installer is written to `Setup\`.

| Folder | What's inside |
|---|---|
| `src/Cloudbreak/` | The app: C#, WPF, .NET 10 |
| `build/` | Build script, installer script (Inno Setup), icon generator |
| `assets/` | App icon |
| `docs/` | Images for this page |
| `design/` | Interactive design mockup (HTML) |

## License

[MIT](LICENSE) © 2026 [Lorenzo-Boffredo](https://github.com/Lorenzo-Boffredo). Free to use, change and share; keep the copyright notice.

<br>

<details>
<summary><b>Italiano</b></summary>

<br>

**Venticinque minuti senza. Cinque minuti con.**

Cloudbreak è un timer pomodoro per svapare meno, per Windows. Lo avvii una volta e va avanti da solo: **resisti** (niente svapo, 25 minuti di base), poi **pausa** (puoi svapare, 5 minuti), e si ripete finché non lo fermi.

**Installazione**

1. Scarica il setup **Cloudbreak-Setup** dall'[ultima release](../../releases/latest).
2. Aprilo. Se Windows mostra *"Windows ha protetto il PC"*, clicca **Ulteriori informazioni** e poi **Esegui comunque**: il setup non è firmato.
3. Scegli **Solo per me** (non serve la password di amministratore), la cartella, e finisci.

Serve Windows 10 o 11 a 64 bit e il [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0), gratuito. Se manca, il setup te lo dice.

**Disinstallazione** da *Impostazioni → App → App installate → Cloudbreak*. Toglie tutto quello che ha aggiunto, senza lasciare file.

L'app è in italiano e in inglese e segue la lingua di Windows. Non si collega mai a internet: impostazioni e statistiche restano sul tuo PC.

</details>
