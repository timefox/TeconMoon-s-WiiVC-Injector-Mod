using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TeconMoon_s_WiiVC_Injector.Utils;

namespace TeconMoon_s_WiiVC_Injector
{
    static class Program
    {
        private static int _ModVersion = 8;

        public static int ModVersion
        {
            get
            {
                return _ModVersion;
            }
        }

        public static string Version
        {
            get
            {
                return System.Reflection.Assembly.GetExecutingAssembly()
                    .GetName().Version.ToString() + "mod" + ModVersion;
            }
        }

        static private List<string> _AutoBuildList = new List<string>();

        static public List<string> AutoBuildList
        {
            get
            {
                return _AutoBuildList;
            }
        }

        static public bool AppendAutoBuildList(string item)
        {
            if (File.Exists(item))
            {
                string fileExtension = new FileInfo(item).Extension;

                if (fileExtension.Equals(".iso", StringComparison.OrdinalIgnoreCase)
                    || fileExtension.Equals(".wbfs", StringComparison.OrdinalIgnoreCase))
                {
                    AutoBuildList.Add(item);
                    return true;
                }
            }

            return false;
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
                    AppendAutoBuildList(arg);
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
                            CultureInfo.CurrentCulture, false, false));
                    return;
                }
            }

            Application.Run(new WiiVC_Injector());
        }
    }
}
