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
                        "3.0.0.1mod2",
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
