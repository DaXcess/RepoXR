using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace RepoXR;

internal static class Native
{
    public static readonly IntPtr HKEY_LOCAL_MACHINE = (IntPtr)0x80000002;

    private const int SECURITY_MAX_SID_SIZE = 68;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder strText, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern void AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentProcessId();

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("Advapi32.dll", EntryPoint = "RegOpenKeyExA", CharSet = CharSet.Ansi)]
    public static extern int RegOpenKeyEx(IntPtr hKey, [In] string lpSubKey, int ulOptions, int samDesired,
        out IntPtr phkResult);

    [DllImport("advapi32.dll", CharSet = CharSet.Ansi)]
    public static extern int RegQueryValueEx(IntPtr hKey, string lpValueName, int lpReserved, out uint lpType,
        StringBuilder? lpData, ref uint lpcbData);

    [DllImport("advapi32.dll", CharSet = CharSet.Ansi)]
    public static extern int RegQueryInfoKey(IntPtr hKey, StringBuilder? lpClass, IntPtr lpcbClass, IntPtr lpReserved,
        out uint lpcSubKeys, out uint lpcbMaxSubKeyLen, out uint lpcbMaxClassLen, out uint lpcValues,
        out uint lpcbMaxValueNameLen, out uint lpcbMaxValueLen, IntPtr lpSecurityDescriptor, IntPtr lpftLastWriteTime);

    [DllImport("advapi32.dll", EntryPoint = "RegEnumValueA", CharSet = CharSet.Ansi)]
    public static extern int RegEnumValue(IntPtr hKey, uint dwIndex, StringBuilder lpValueName, ref uint lpcchValueName,
        IntPtr lpReserved, IntPtr lpType, IntPtr lpData, IntPtr lpcbData);

    [DllImport("advapi32.dll")]
    public static extern int RegCloseKey(IntPtr hKey);

    [DllImport("Shlwapi.dll", CharSet = CharSet.Ansi)]
    public static extern int ShellMessageBox(IntPtr hAppInst, IntPtr hWnd, string lpcText, string lpcTitle,
        uint fuStyle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr hProcess, uint dwAccess, out IntPtr hToken);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr hToken, uint tokenInformationClass, in byte lpData,
        int tokenInformationLength, out uint returnLength);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern IntPtr GetSidSubAuthorityCount(IntPtr pSid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern IntPtr GetSidSubAuthority(IntPtr pSid, int nSubAuthority);

    [DllImport("kernel32.dll", SetLastError = true)]
    [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
    [SuppressUnmanagedCodeSecurity]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct SID_AND_ATTRIBUTES
    {
        public IntPtr Sid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_MANDATORY_LABEL
    {
        public SID_AND_ATTRIBUTES Label;
    }

    public static bool RegOpenSubKey(ref IntPtr hKey, string lpSubKey, int samDesired)
    {
        var result = RegOpenKeyEx(hKey, lpSubKey, 0, samDesired, out var hNewKey) == 0;
        if (!result)
            return false;

        RegCloseKey(hKey);
        hKey = hNewKey;

        return true;
    }

    public static unsafe bool IsHighIntegrityLevel()
    {
        var hToken = IntPtr.Zero;

        try
        {
            if (!OpenProcessToken(GetCurrentProcess(), 0x0018, out hToken))
                return false;

            Span<byte> buffer = stackalloc byte[SECURITY_MAX_SID_SIZE + sizeof(uint)];

            if (!GetTokenInformation(hToken, 25, MemoryMarshal.GetReference(buffer), buffer.Length, out _))
                return false;

            var label = MemoryMarshal.Cast<byte, TOKEN_MANDATORY_LABEL>(buffer)[0];
            var pSid = label.Label.Sid;
            if (pSid == IntPtr.Zero)
                return false;

            var subAuthCount = Marshal.ReadByte(GetSidSubAuthorityCount(pSid));
            var integrityLevel = Marshal.ReadInt32(GetSidSubAuthority(pSid, subAuthCount - 1));

            return integrityLevel > 0x2000;
        }
        finally
        {
            if (hToken != IntPtr.Zero)
                CloseHandle(hToken);
        }
    }

    public static void BringGameWindowToFront()
    {
        var currentPid = GetCurrentProcessId();

        var gameWindows = FindWindows(delegate(IntPtr hWnd, IntPtr _)
        {
            GetWindowThreadProcessId(hWnd, out var pid);

            if (pid != currentPid)
                return false;

            // You might think that the window title is "R.E.P.O.", however during startup it is actually called "REPO"
            // and is eventually changed by the game's WindowManager to "R.E.P.O."
            return GetWindowText(hWnd) == "REPO";
        }).ToArray();

        if (gameWindows.Length > 1)
            Logger.LogWarning(
                "Multiple game windows called 'R.E.P.O.' detected. Bringing only the first one to the front.");

        var targetWindow = gameWindows[0];

        // Little hack to make BringWindowToTop work properly
        var foregroundPid = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        var currentThreadId = GetCurrentThreadId();

        AttachThreadInput(foregroundPid, currentThreadId, true);
        BringWindowToTop(targetWindow);
        AttachThreadInput(foregroundPid, currentThreadId, false);
    }

    private static string GetWindowText(IntPtr hWnd)
    {
        var size = GetWindowTextLength(hWnd);
        if (size <= 0)
            return string.Empty;

        var builder = new StringBuilder(size + 1);
        GetWindowText(hWnd, builder, builder.Capacity);

        return builder.ToString();
    }

    private static IEnumerable<IntPtr> FindWindows(EnumWindowsProc filter)
    {
        var windows = new List<IntPtr>();

        EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
        {
            if (filter(hWnd, lParam))
                windows.Add(hWnd);

            return true;
        }, IntPtr.Zero);

        return windows;
    }
}