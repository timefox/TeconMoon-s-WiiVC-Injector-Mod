using System.Windows.Forms;
using TeconMoon_s_WiiVC_Injector.Utils;

namespace TeconMoon_s_WiiVC_Injector
{
    static class Trt
    {
        private static TranslationTemplate Tt { get; } = TranslationTemplate.LoadTemplate(
            Application.StartupPath + @"\language.lang", true);

        public static bool IsValidate
        {
            get
            {                
                return Tt.IsValidate;
            }
        }

        static public void TranslateForm(Form form)
        {
            Tt.TranslateForm(form);
        }

        static public string Tr(string s)
        {
            return Tt.Tr(s);
        }
    }
}
