using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Linq;
using System.Resources;
using System.Globalization;

namespace TeconMoon_s_WiiVC_Injector
{
    namespace Utils
    {
        class Draw
        {
            // Get a adjusted font which is fit to specified width and height.
            // Using Graphics's method.
            // Modified from MSDN: https://msdn.microsoft.com/en-us/library/bb986765.aspx
            static public Font GetGraphicAdjustedFont(
                Graphics g,
                string graphicString,
                Font originalFont,
                int containerWidth,
                int containerHeight,
                int maxFontSize,
                int minFontSize,
                StringFormat stringFormat,
                bool smallestOnFail
                )
            {
                Font testFont = null;
                // We utilize MeasureString which we get via a control instance           
                for (int adjustedSize = maxFontSize; adjustedSize >= minFontSize; adjustedSize--)
                {
                    testFont = new Font(originalFont.Name, adjustedSize, originalFont.Style);

                    // Test the string with the new size
                    SizeF adjustedSizeNew = g.MeasureString(
                        graphicString,
                        testFont,
                        containerWidth,
                        stringFormat);

                    if (containerWidth > Convert.ToInt32(adjustedSizeNew.Width) &&
                        containerHeight > Convert.ToInt32(adjustedSizeNew.Height))
                    {
                        // Good font, return it
                        return testFont;
                    }
                }

                // If you get here there was no fontsize that worked
                // return minimumSize or original?
                if (smallestOnFail)
                {
                    return testFont;
                }
                else
                {
                    return originalFont;
                }
            }

            // Get a adjusted font which is fit to specified width and height.
            // Using TextRenderer's method.
            static public Font GetTextRendererAdjustedFont(
                Graphics g,
                string text,
                Font originalFont,
                int containerWidth,
                int containerHeight,
                int maxFontSize,
                int minFontSize,
                TextFormatFlags flags,
                bool smallestOnFail
                )
            {
                Font testFont = null;
                // We utilize MeasureText which we get via a control instance           
                for (int adjustedSize = maxFontSize; adjustedSize >= minFontSize; adjustedSize--)
                {
                    testFont = new Font(originalFont.Name, adjustedSize, originalFont.Style);

                    // Test the string with the new size
                    Size adjustedSizeNew = TextRenderer.MeasureText(
                        g,
                        text,
                        testFont,
                        new Size(containerWidth, containerHeight),
                        flags);

                    if (containerWidth > adjustedSizeNew.Width &&
                        containerHeight > adjustedSizeNew.Height)
                    {
                        // Good font, return it
                        return testFont;
                    }
                }

                // If you get here there was no fontsize that worked
                // return minimumSize or original?
                if (smallestOnFail)
                {
                    return testFont;
                }
                else
                {
                    return originalFont;
                }
            }

            // Draw a string in a specified rectangle with
            // a specified font with max font size that can
            // fit to the rectangle.
            static public void ImageDrawString(
                ref Bitmap bitmap,
                string s,
                Rectangle rectangle,
                Font font,
                Color foreColor,
                bool adjustedFontByTextRenderer,
                bool drawStringByTextRenderer
                )
            {
                StringFormat stringFormat = StringFormat.GenericDefault;

                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    TextFormatFlags flags = TextFormatFlags.HorizontalCenter
                        | TextFormatFlags.VerticalCenter
                        | TextFormatFlags.WordBreak;

                    if (!adjustedFontByTextRenderer)
                    {
                        font = GetGraphicAdjustedFont(
                            graphics,
                            s,
                            font,
                            rectangle.Width,
                            rectangle.Height,
                            100, 8,
                            stringFormat,
                            true);
                    }
                    else
                    {
                        // Can't get the correct word break output
                        // if we use GetGraphicAdjustedFont.
                        // But it's really more slower than 
                        // GetGraphicAdjustedFont.
                        font = GetTextRendererAdjustedFont(
                            graphics,
                            s,
                            font,
                            rectangle.Width,
                            rectangle.Height,
                            64, 8,
                            flags,
                            true);
                    }

                    if (!drawStringByTextRenderer)
                    {
                        SizeF sizeF = graphics.MeasureString(s, font, rectangle.Width);

                        RectangleF rectF = new RectangleF(
                            rectangle.X + (rectangle.Width - sizeF.Width) / 2,
                            rectangle.Y + (rectangle.Height - sizeF.Height) / 2,
                            sizeF.Width,
                            sizeF.Height);

                        graphics.DrawString(
                            s,
                            font,
                            new SolidBrush(foreColor),
                            rectF,
                            stringFormat);
                    }
                    else
                    {
                        // Poor draw performance, both for speed and output result.
                        Size size = TextRenderer.MeasureText(
                            graphics,
                            s,
                            font,
                            new Size(rectangle.Width, rectangle.Height),
                            flags);

                        Rectangle rect = new Rectangle(
                            rectangle.X + (rectangle.Width - size.Width) / 2,
                            rectangle.Y + (rectangle.Height - size.Height) / 2,
                            size.Width,
                            size.Height);

                        TextRenderer.DrawText(
                            graphics,
                            s,
                            font,
                            rect,
                            foreColor,
                            flags);
                    }
                }
            }
        }

        public static class RichTextBoxExtensions
        {
            public static void AppendText(this RichTextBox box, string text, Color color)
            {
                AppendText(box, text, color, null);
            }

            public static void AppendText(this RichTextBox box, string text, Color color, Font font)
            {
                Win32Native.LockWindowUpdate(box.Handle);
                int savedSelectionStart = box.SelectionStart;
                int savedSelectionLength = box.SelectionLength;
                box.SelectionStart = box.TextLength;
                box.SelectionLength = 0;

                box.SelectionColor = color;
                if (font != null)
                {
                    box.SelectionFont = font;
                }                
                
                box.AppendText(text);
                box.SelectionColor = box.ForeColor;
                box.SelectionFont = box.Font;
                box.SelectionStart = savedSelectionStart;
                box.SelectionLength = savedSelectionLength;
                Win32Native.LockWindowUpdate(IntPtr.Zero);
            }
        }

        class StringEx
        {
            // Check if the input byte array is GB2312 encoded.
            static public bool IsGB2312EncodingArray(byte[] b)
            {
                int i = 0;
                while (i < b.Length)
                {
                    if (b[i] <= 127)
                    {
                        ++i;
                        continue;
                    }

                    if (b[i] >= 176 && b[i] <= 247)
                    {
                        if (i == b.Length - 1)
                        {
                            return false;
                        }
                        ++i;

                        if (b[i] < 160 || b[i] > 254)
                        {
                            return false;
                        }

                        ++i;
                    }
                    else
                    {
                        return false;
                    }
                }

                return true;
            }

            // Get a probably encoding object for input array.
            static public Encoding GetArrayEncoding(byte[] b)
            {
                if (IsGB2312EncodingArray(b))
                {
                    return Encoding.GetEncoding("GB2312");
                }

                // We assume it is utf8 by default.
                return Encoding.UTF8;
            }

            // Read a string from a binary stream.
            static public string ReadStringFromBinaryStream(BinaryReader reader, long position, bool peek = false)
            {
                long oldPosition = 0;

                if (peek)
                {
                    oldPosition = reader.BaseStream.Position;
                }

                reader.BaseStream.Position = position;
                ArrayList readBuffer = new ArrayList();
                byte b;
                while ((b = reader.ReadByte()) != 0)
                {
                    readBuffer.Add(b);
                }

                if (peek)
                {
                    reader.BaseStream.Position = oldPosition;
                }

                byte[] readBytes = readBuffer.OfType<byte>().ToArray();
                return Encoding.Default.GetString(Encoding.Convert(
                    GetArrayEncoding(readBytes),
                    Encoding.Default,
                    readBytes));
            }
        }

        class TranslationTemplate
        {
            protected const string SectionResource = "@Resource";
            protected const string KeyFormTitle = "@Title";
            protected const string KeyLanguage = "language";
            protected const string KeyVerion = "verion";
            protected const string KeyAuthor = "author";

            private IniFile TemplateFile
            {
                get;
                set;
            }

            public bool IsValidate
            {
                get
                {
                    if (String.IsNullOrWhiteSpace(TemplateFileName))
                    {
                        return false;
                    }

                    return File.Exists(TemplateFileName);
                }
            }

            public string TemplateFileName
            {
                get
                {
                    return TemplateFile != null ? TemplateFile.FileName : "";
                }
            }

            private TranslationTemplate(string templateFile)
            {
                TemplateFile = new IniFile(templateFile);
            }

            static public TranslationTemplate LoadTemplate(string templateFilePath)
            {
                return new TranslationTemplate(templateFilePath);
            }

            static public TranslationTemplate CreateTemplate(
                string templateFilePath,
                string appName,
                string defaultLanguageName,
                string version,
                string author
                )
            {
                TranslationTemplate template = new TranslationTemplate(templateFilePath);

                template.TemplateFile.CurrentSection = appName;
                template.TemplateFile.WriteStringValue(KeyLanguage, defaultLanguageName);
                template.TemplateFile.WriteStringValue(KeyVerion, version);
                template.TemplateFile.WriteStringValue(KeyAuthor, author);

                return template;
            }

            public void AppendFormTranslation(Form form)
            {
                TemplateFile.CurrentSection = form.Name;
                TemplateFile.WriteStringValue(KeyFormTitle, form.Text);

                foreach (Control control in form.Controls)
                {
                    AppendControlTranslation(control);
                }
            }

            private void AppendControlTranslation(Control control)
            {
                if (!String.IsNullOrEmpty(control.Text))
                {
                    TemplateFile.WriteStringValue(control.Name, control.Text);
                }

                foreach (Control subControl in control.Controls)
                {
                    AppendControlTranslation(subControl);
                }

                ToolStrip toolStrip = control as ToolStrip;
                if (toolStrip != null)
                {
                    foreach (ToolStripItem item in toolStrip.Items)
                    {
                        AppendToolStripItemTranslation(item);
                    }
                }
            }

            private void AppendToolStripItemTranslation(ToolStripItem item)
            {
                if (!String.IsNullOrEmpty(item.Text))
                {
                    TemplateFile.WriteStringValue(item.Name, item.Text);
                }
            }

            public void AppendStringResourceTranslation(ResourceSet resourceSet)
            {
                TemplateFile.CurrentSection = SectionResource;

                foreach (DictionaryEntry dictionaryEntry in resourceSet)
                {
                    if (dictionaryEntry.Value is string)
                    {
                        TemplateFile.WriteStringValue(
                            dictionaryEntry.Key.ToString(),
                            dictionaryEntry.Value.ToString());
                    }
                }
            }

            public void TranslationForm(Form form)
            {
                TemplateFile.CurrentSection = form.Name;
                _TranslateControl(form, KeyFormTitle);

                foreach (Control control in form.Controls)
                {
                    TranslateControl(control);
                }
            }

            private void TranslateControl(Control control)
            {
                _TranslateControl(control);

                foreach (Control subControl in control.Controls)
                {
                    TranslateControl(subControl);
                }

                ToolStrip toolStrip = control as ToolStrip;
                if (toolStrip != null)
                {
                    foreach (ToolStripItem item in toolStrip.Items)
                    {
                        TranslateToolStripItem(item);
                    }
                }
            }

            private void _TranslateControl(Control control)
            {
                _TranslateControl(control, control.Name);
            }

            private void _TranslateControl(Control control, string id)
            {
                string translation = TemplateFile.ReadStringValue(id, 1024);

                if (!String.IsNullOrEmpty(translation))
                {
                    control.Text = translation;
                }
            }

            private void TranslateToolStripItem(ToolStripItem item)
            {
                TranslateToolStripItem(item, item.Name);
            }

            private void TranslateToolStripItem(ToolStripItem item, string id)
            {
                string translation = TemplateFile.ReadStringValue(id, 1024);

                if (!String.IsNullOrEmpty(translation))
                {
                    item.Text = translation;
                }
            }

            public string Tr(string s)
            {
                if (String.IsNullOrEmpty(s) || !IsValidate)
                {
                    return s;
                }

                foreach (DictionaryEntry dictionaryEntry 
                    in Properties.Resources.ResourceManager.GetResourceSet(
                        CultureInfo.CurrentCulture, false, false))
                {
                    if (dictionaryEntry.Value is string)
                    {
                        if (s.Equals(dictionaryEntry.Value))
                        {
                            return TrId(dictionaryEntry.Key.ToString());
                        }
                    }
                }

                return s;
            }

            public string TrId(string id)
            {
                string s = TemplateFile.ReadStringValue(SectionResource, id, 1024);

                if (String.IsNullOrEmpty(s))
                {
                    return Properties.Resources.ResourceManager.GetString(id);
                }

                return s;
            }
        }

        class IniFile
        {
            public string FileName
            {
                get;
                protected set;
            }

            public string CurrentSection
            {
                get;
                set;
            }

            public IniFile(string iniFile)
            {
                FileName = iniFile;
            }

            public bool WriteStringValue(string key, string value)
            {
                return WriteStringValue(CurrentSection, key, value);
            }

            public bool WriteStringValue(string section, string key, string value)
            {
                return Win32Native.WritePrivateProfileString(
                    section, 
                    key, 
                    value.Replace("\\r", "\r").Replace("\\n", "\n"), 
                    FileName);
            }

            public string ReadStringValue(string key, int maxLength, string defaultValue = "")
            {
                return ReadStringValue(CurrentSection, key, maxLength, defaultValue);
            }

            public string ReadStringValue(string section, string key, int maxLength, string defaultValue = "")
            {
                StringBuilder buffer = new StringBuilder(maxLength);
                buffer.Length = maxLength;
                string value = buffer.ToString(0, maxLength);
                int length = (int)Win32Native.GetPrivateProfileString(
                    section,
                    key,
                    defaultValue,
                    value,
                    (uint)maxLength,
                    FileName);
                return value.Substring(0, length)
                    .Replace("\\r", "\r")
                    .Replace("\\n", "\n");                
            }

            public string[] GetSections()
            {
                string value = new StringBuilder(65535).ToString(0, 65535);
                if (Win32Native.GetPrivateProfileString(null, null, "", value, 65535, FileName) != 0)
                {
                    return StringsFromMultiString(value);
                }

                return new string[0];
            }

            private static string[] StringsFromMultiString(string s)
            {
                string[] raw = s.Split('\0');
                return raw.Take(raw.Length - 2).ToArray();
             }
        }
    }
}
