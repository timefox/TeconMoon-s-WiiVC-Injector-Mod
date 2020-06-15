using System;
using System.Runtime.InteropServices;

namespace TeconMoon_WiiVC_Injector_Jam
{
    class Win32Native
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern bool WritePrivateProfileString(
            string lpAppName, string lpKeyName, string lpString, string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern uint GetPrivateProfileString(
            string lpAppName, string lpKeyName, string lpDefault, string lpReturnedString,
            uint nSize, string lpFileName);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern IntPtr LockWindowUpdate(IntPtr Handle);
    }
}
