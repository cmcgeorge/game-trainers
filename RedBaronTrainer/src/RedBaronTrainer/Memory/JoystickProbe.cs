using System.Runtime.InteropServices;

namespace RedBaronTrainer.Memory;

/// <summary>One <c>winmm</c> joystick slot, whether or not anything is plugged into it.</summary>
public sealed record HostJoystick(int Id, bool Present, string Name, int Axes, int Buttons, int X, int Y)
{
    public override string ToString() => Present
        ? $"ID {Id}: {Name} - {Axes} axes, {Buttons} buttons, X={X} Y={Y}"
        : $"ID {Id}: (empty)";
}

/// <summary>
/// Enumerates the host's joysticks through <c>winmm</c>'s legacy joystick API.
///
/// <para><b>Why winmm and not XInput.</b> This is the API SDL 1.2 uses on Windows, and SDL 1.2 is
/// what DOSBox and the SDL1 build of DOSBox-X are linked against. Asking the same API the emulator
/// asks is the only way to see what the emulator sees — an XInput pad that Windows is perfectly
/// happy with can still be invisible or, more often, sitting on a slot the emulator does not
/// look at.</para>
///
/// <para>The interesting failure this exposes is a gap: <c>joyGetDevCaps(0)</c> returning
/// <c>JOYERR_UNPLUGGED</c> while the pad answers on ID 1. Windows assigns these IDs by device
/// arrival and does not compact them, so a controller that has been unplugged and replugged, or a
/// second one that was connected first, leaves slot 0 empty.</para>
/// </summary>
public static class JoystickProbe
{
    private const int MaxIds = 16;
    private const uint JOYERR_NOERROR = 0;
    private const uint JOY_RETURNALL = 0x000000FF;

    [DllImport("winmm.dll")]
    private static extern uint joyGetNumDevs();

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern uint joyGetDevCapsW(UIntPtr id, ref JOYCAPSW caps, uint size);

    [DllImport("winmm.dll")]
    private static extern uint joyGetPosEx(uint id, ref JOYINFOEX info);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct JOYCAPSW
    {
        public ushort wMid, wPid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szPname;
        public uint wXmin, wXmax, wYmin, wYmax, wZmin, wZmax, wNumButtons, wPeriodMin, wPeriodMax;
        public uint wRmin, wRmax, wUmin, wUmax, wVmin, wVmax, wCaps, wMaxAxes, wNumAxes, wMaxButtons;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szRegKey;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szOEMVxD;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOYINFOEX
    {
        public uint dwSize, dwFlags, dwXpos, dwYpos, dwZpos, dwRpos, dwUpos, dwVpos;
        public uint dwButtons, dwButtonNumber, dwPOV, dwReserved1, dwReserved2;
    }

    /// <summary>Number of joystick slots the driver exposes (not the number of connected sticks).</summary>
    public static int SlotCount
    {
        get
        {
            try { return (int)Math.Min(joyGetNumDevs(), MaxIds); }
            catch (DllNotFoundException) { return 0; }
            catch (EntryPointNotFoundException) { return 0; }
        }
    }

    /// <summary>Reads every slot, present or not, in ID order.</summary>
    public static IReadOnlyList<HostJoystick> Enumerate()
    {
        var list = new List<HostJoystick>();
        int slots = SlotCount;
        for (int id = 0; id < slots; id++)
        {
            var caps = new JOYCAPSW();
            var info = new JOYINFOEX
            {
                dwSize = (uint)Marshal.SizeOf<JOYINFOEX>(),
                dwFlags = JOY_RETURNALL,
            };
            uint capsResult, posResult;
            try
            {
                capsResult = joyGetDevCapsW((UIntPtr)(uint)id, ref caps, (uint)Marshal.SizeOf<JOYCAPSW>());
                posResult = joyGetPosEx((uint)id, ref info);
            }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
            {
                break;
            }

            bool present = capsResult == JOYERR_NOERROR && posResult == JOYERR_NOERROR;
            list.Add(new HostJoystick(id, present,
                present ? (caps.szPname ?? "").Trim() : "",
                present ? (int)caps.wNumAxes : 0,
                present ? (int)caps.wNumButtons : 0,
                present ? (int)info.dwXpos : 0,
                present ? (int)info.dwYpos : 0));
        }
        return list;
    }

    /// <summary>
    /// The lowest slot with a stick in it, or -1. Anything other than 0 is the problem case: DOSBox
    /// binds emulated joystick 1 to the first stick SDL reports, and a gap at ID 0 is exactly what
    /// makes an attached pad look absent.
    /// </summary>
    public static int FirstPresentId(IReadOnlyList<HostJoystick> sticks)
    {
        ArgumentNullException.ThrowIfNull(sticks);
        foreach (var s in sticks)
            if (s.Present) return s.Id;
        return -1;
    }
}
