using System.Runtime.InteropServices;

namespace TeconMoon_s_WiiVC_Injector
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
    }
}
