using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Cloudbreak.Core;

/// <summary>
/// Notification-area icon through Shell_NotifyIcon, without WinForms.
/// Its hidden window also receives system broadcasts (theme, power, time).
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private const int WM_TRAY = 0x8001;
    private const int WM_SETTINGCHANGE = 0x001A;
    private const int WM_TIMECHANGE = 0x001E;
    private const int WM_POWERBROADCAST = 0x0218;
    private const int WM_DWMCOLORIZATIONCOLORCHANGED = 0x0320;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONUP = 0x0205;

    private const int WM_WTSSESSION_CHANGE = 0x02B1;
    private const int WTS_SESSION_LOCK = 0x7, WTS_SESSION_UNLOCK = 0x8;
    private const int PBT_POWERSETTINGCHANGE = 0x8013;

    private readonly HwndSource _src;
    private readonly uint _taskbarCreated;
    private readonly IntPtr _displayNotify;
    private IntPtr _icon;
    private string _tip = "Cloudbreak";
    private bool _added;

    public event Action? LeftClick;
    public event Action? RightClick;
    public event Action? SystemThemeChanged;
    public event Action? ClockJumped;
    /// <summary>true = the screen is off or the session is locked: nobody can see the timer.</summary>
    public event Action<bool>? DisplayOff;
    public event Action<bool>? SessionLocked;

    public TrayIcon()
    {
        var p = new HwndSourceParameters("CloudbreakTray")
        {
            Width = 0,
            Height = 0,
            WindowStyle = unchecked((int)0x80000000), // WS_POPUP, never shown
            ExtendedWindowStyle = 0x80, // WS_EX_TOOLWINDOW
        };
        _src = new HwndSource(p);
        _src.AddHook(WndProc);
        _taskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");
        _displayNotify = Native.RegisterPowerSettingNotification(_src.Handle, ref Native.GUID_CONSOLE_DISPLAY_STATE, 0);
        Native.WTSRegisterSessionNotification(_src.Handle, 0);
    }

    public IntPtr Handle => _src.Handle;

    public void Update(IntPtr icon, string tip)
    {
        var old = _icon;
        _icon = icon;
        _tip = tip.Length > 127 ? tip[..127] : tip;
        // At logon the shell may not be ready yet: only count the icon as added when Windows says so,
        // and add it again if a modify fails (TaskbarCreated also re-adds it once Explorer starts).
        if (!_added) _added = Send(Native.NIM_ADD);
        else if (!Send(Native.NIM_MODIFY)) _added = Send(Native.NIM_ADD);
        if (old != IntPtr.Zero && old != icon) Native.DestroyIcon(old);
    }

    public bool IsShown => _added;

    private bool Send(uint msg)
    {
        var d = new Native.NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<Native.NOTIFYICONDATA>(),
            hWnd = Handle,
            uID = 1,
            uFlags = Native.NIF_MESSAGE | Native.NIF_ICON | Native.NIF_TIP,
            uCallbackMessage = WM_TRAY,
            hIcon = _icon,
            szTip = _tip,
            szInfo = "",
            szInfoTitle = "",
        };
        return Native.Shell_NotifyIcon(msg, ref d);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_TRAY:
                int m = (int)(lParam.ToInt64() & 0xFFFF);
                if (m == WM_LBUTTONUP) LeftClick?.Invoke();
                else if (m == WM_RBUTTONUP) RightClick?.Invoke();
                handled = true;
                break;
            case WM_SETTINGCHANGE:
            case WM_DWMCOLORIZATIONCOLORCHANGED:
                SystemThemeChanged?.Invoke();
                break;
            case WM_TIMECHANGE:
                ClockJumped?.Invoke();
                break;
            case WM_POWERBROADCAST:
                long w = wParam.ToInt64();
                if (w == 0x12 || w == 0x7) ClockJumped?.Invoke(); // resume from sleep
                else if (w == PBT_POWERSETTINGCHANGE && lParam != IntPtr.Zero)
                {
                    // POWERBROADCAST_SETTING: GUID (16) + DataLength (4) + Data. Display: 0 off, 1 on, 2 dimmed.
                    // Only trust it if it is the setting we registered for and carries a 4-byte value.
                    var guid = Marshal.PtrToStructure<Guid>(lParam);
                    if (guid == Native.GUID_CONSOLE_DISPLAY_STATE && Marshal.ReadInt32(lParam, 16) >= 4)
                        DisplayOff?.Invoke(Marshal.ReadInt32(lParam, 20) == 0);
                }
                break;
            case WM_WTSSESSION_CHANGE:
                long ev = wParam.ToInt64();
                if (ev == WTS_SESSION_LOCK) SessionLocked?.Invoke(true);
                else if (ev == WTS_SESSION_UNLOCK) SessionLocked?.Invoke(false);
                break;
            default:
                if (_taskbarCreated != 0 && msg == (int)_taskbarCreated)
                {
                    // Explorer restarted: add the icon again.
                    _added = _icon != IntPtr.Zero && Send(Native.NIM_ADD);
                }
                break;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_added) Send(Native.NIM_DELETE);
        _added = false;
        if (_icon != IntPtr.Zero) Native.DestroyIcon(_icon);
        _icon = IntPtr.Zero;
        if (_displayNotify != IntPtr.Zero) Native.UnregisterPowerSettingNotification(_displayNotify);
        Native.WTSUnRegisterSessionNotification(_src.Handle);
        _src.Dispose();
    }
}
