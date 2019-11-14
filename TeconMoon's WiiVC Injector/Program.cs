using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TeconMoon_s_WiiVC_Injector.Utils;

namespace TeconMoon_s_WiiVC_Injector
{
    static class Program
    {
        private static int _ModVersion = 3;

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
                return "3.0.1mod" + ModVersion;
            }
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
