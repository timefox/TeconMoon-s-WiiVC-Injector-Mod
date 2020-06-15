using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using TeconMoon_WiiVC_Injector_Jam.Utils;

namespace TeconMoon_WiiVC_Injector_Jam
{
    static class Program
    {
        public static int ModVersion { get; } = 12;

        public static string Version => System.Reflection.Assembly.GetExecutingAssembly()
                    .GetName().Version.ToString() + " mod " + ModVersion;

        public static List<string> BatchBuildList { get; } = new List<string>();

        public static bool AppendBatchBuildList(string item)
        {
            if (File.Exists(item))
            {
                string fileExtension = new FileInfo(item).Extension;

                if (fileExtension.Equals(".iso", StringComparison.OrdinalIgnoreCase)
                    || fileExtension.Equals(".wbfs", StringComparison.OrdinalIgnoreCase))
                {
                    BatchBuildList.Add(item);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Check for if .Net v3.5 component is installed.
        /// </summary>
        private static bool CheckForNet35()
        {
            if (Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\NET Framework Setup\\NDP\\v3.5") == null)
            {
                MessageBox.Show(
                    Trt.Tr(".NET Framework 3.5 was not detected on your machine, which is required by programs used during the build process.\n\nYou should be able to enable this in \"Programs and Features\" under \"Turn Windows features on or off\", or download it from Microsoft.\n\nClick OK to close the injector and open \"Programs and Features\"..."),
                    Trt.Tr(".NET Framework v3.5 not found..."));

                Process.Start("appwiz.cpl");
                return false;
            }

            return true;
        }

        private static bool CheckRuntimeEnvironment()
        {
            if (!CheckForNet35())
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            foreach (string arg in args)
            {
                if (!arg.StartsWith("-") && !arg.StartsWith("/"))
                {
                    AppendBatchBuildList(arg);
                    continue;
                }

                if (arg.Substring(1).Equals("langtemplate", StringComparison.OrdinalIgnoreCase))
                {
                    TranslationTemplate translationTemplate = TranslationTemplate.CreateTemplate(
                        Application.StartupPath + @"\language.lang",
                        "TeconMoon-s-WiiVC-Injector-Mod-Language",
                        "English(en-us)",
                        Version,
                        "robin");
                    translationTemplate.AppendFormTranslation(new WiiVC_Injector());
                    translationTemplate.AppendFormTranslation(new SDCardMenu());
                    translationTemplate.AppendStringResourceTranslation(
                        Properties.Resources.ResourceManager.GetResourceSet(
                            CultureInfo.InvariantCulture, false, false));
                    return;
                }
            }

            if (!CheckRuntimeEnvironment())
            {
                return;
            }

            Application.Run(new WiiVC_Injector());
        }
    }
}
