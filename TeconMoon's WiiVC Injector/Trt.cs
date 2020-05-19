using System.Windows.Forms;
using TeconMoon_s_WiiVC_Injector.Utils;

namespace TeconMoon_s_WiiVC_Injector
{
    static class Trt
    {
        private static TranslationTemplate _tt = TranslationTemplate.LoadTemplate(
            Application.StartupPath + @"\language.lang");

        private static TranslationTemplate tt
        {
            get
            {
                return _tt;
            }
        }

        public static bool IsValidate
        {
            get
            {                
                return tt.IsValidate;
            }
        }

        static public void TranslateForm(Form form)
        {
            tt.TranslateForm(form);
        }

        static public string Tr(string s)
        {
            return tt.Tr(s);
        }
    }
}
