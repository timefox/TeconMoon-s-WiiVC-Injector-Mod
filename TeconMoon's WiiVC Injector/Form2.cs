using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Net;
using System.Diagnostics;
using TeconMoon_s_WiiVC_Injector.Utils;

namespace TeconMoon_s_WiiVC_Injector
{
    public partial class SDCardMenu : Form
    {
        private TranslationTemplate tr = TranslationTemplate.LoadTemplate(
            Application.StartupPath + @"\language.lang");

        public SDCardMenu()
        {
            InitializeComponent();

            if (tr.IsValidate)
            {
                tr.TranslateForm(this);
            }
        }
        string SelectedDriveLetter;
        bool DriveSpecified;

        //Load Drives and set drive variable on load
        private void SDCardMenu_Load(object sender, EventArgs e)
        {
            ReloadDriveList();
            SpecifyDrive();
            MemcardBlocks.SelectedIndex = 0;
            VideoForceMode.SelectedIndex = 0;
            VideoTypeMode.SelectedIndex = 0;
            LanguageBox.SelectedIndex = 0;
            NintendontOptions.SetItemChecked(9, true);
        }

        //Callable voids for commands
        public void SpecifyDrive()
        {
            if (DriveBox.SelectedValue != null)
            {
                SelectedDriveLetter = DriveBox.SelectedValue.ToString().Substring(0, 3);
                DriveSpecified = true;
            }
            else
            {
                SelectedDriveLetter = "";
                DriveSpecified = false;
            }
        }
        public void ReloadDriveList()
        {
            DriveBox.DataSource = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Removable).Select(d => d.Name + " (" + d.VolumeLabel + ")").ToList();
        }
        public void CheckForBoxes()
        {
            if (NintendontOptions.GetItemChecked(1))
            {
                MemcardText.Enabled = true;
                MemcardBlocks.Enabled = true;
                MemcardMulti.Enabled = true;
            }
            else
            {
                MemcardText.Enabled = false;
                MemcardBlocks.Enabled = false;
                MemcardMulti.Checked = false;
                MemcardMulti.Enabled = false;
            }
            if (NintendontOptions.GetItemChecked(9))
            {
                VideoWidth.Enabled = false;
                VideoWidthText.Enabled = false;
                WidthNumber.Text = tr.Tr("Auto");
            }
            else
            {
                VideoWidth.Enabled = true;
                VideoWidthText.Enabled = true;
                WidthNumber.Text = VideoWidth.Value.ToString();
            }
        }
        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length).Where(x => x % 2 == 0).Select(x => Convert.ToByte(hex.Substring(x, 2), 16)).ToArray();
        }
        public static bool CheckForInternetConnection()
        {
            try
            {
                using (var client = new WebClient())
                {
                    using (client.OpenRead("http://clients3.google.com/generate_204"))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        //Reload Drives when selected
        private void ReloadDrives_Click(object sender, EventArgs e)
        {
            ReloadDriveList();
            SpecifyDrive();
        }
        //Specify Drive variable when a drive is selected
        private void DriveBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            SpecifyDrive();
        }
        

        //Changing config options
        public void NintendontOptions_SelectedIndexChanged(object sender, EventArgs e)
        {
            CheckForBoxes();
        }
        private void NintendontOptions_DoubleClick(object sender, EventArgs e)
        {
            CheckForBoxes();
        }
        private void VideoForceMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (VideoForceMode.SelectedIndex == 0)
            {
                VideoTypeMode.SelectedIndex = 0;
                VideoTypeMode.Enabled = false;
            }
            else if (VideoForceMode.SelectedIndex == 3)
            {
                VideoTypeMode.SelectedIndex = 0;
                VideoTypeMode.Enabled = false;
            }
            else
            {
                VideoTypeMode.SelectedIndex = 1;
                VideoTypeMode.Enabled = true;
            }
        }
        private void VideoWidth_Scroll(object sender, EventArgs e)
        {
            WidthNumber.Text = VideoWidth.Value.ToString();
        }
        private void VideoTypeMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (VideoForceMode.SelectedIndex != 0 & VideoForceMode.SelectedIndex != 3 & VideoTypeMode.SelectedIndex == 0)
            {
                VideoTypeMode.SelectedIndex = 1;
            }
        }

        //Buttons that make changes to SD Card
        private void NintendontUpdate_Click(object sender, EventArgs e)
        {
            if (DriveSpecified)
            {
                if (CheckForInternetConnection() == false)
                {
                    DialogResult dialogResult = MessageBox.Show(
                        tr.Tr("Your internet connection could not be verified, do you wish to try and download Nintendont anyways?"),
                        tr.Tr("Internet Connection Verification Failed"), MessageBoxButtons.YesNo);
                    if (dialogResult == DialogResult.No)
                    {
                        goto skipnintendontdownload;
                    }
                }
                ActionStatus.Text = tr.Tr("Downloading...");
                ActionStatus.Refresh();
                Directory.CreateDirectory(SelectedDriveLetter + "apps\\nintendont");
                var client = new WebClient();
                client.DownloadFile("https://raw.githubusercontent.com/FIX94/Nintendont/master/loader/loader.dol", SelectedDriveLetter + "apps\\nintendont\\boot.dol");
                client.DownloadFile("https://raw.githubusercontent.com/FIX94/Nintendont/master/nintendont/meta.xml", SelectedDriveLetter + "apps\\nintendont\\meta.xml");
                client.DownloadFile("https://raw.githubusercontent.com/FIX94/Nintendont/master/nintendont/icon.png", SelectedDriveLetter + "apps\\nintendont\\icon.png");
                ActionStatus.Text = "";
                MessageBox.Show(tr.Tr("Download complete."));
            }
            else
            {
                MessageBox.Show(tr.Tr("Drive not specified, nowhere to download contents"));
            }
            skipnintendontdownload:;
        }
        private void GenerateConfig_Click(object sender, EventArgs e)
        {
            if (DriveSpecified == false)
            {
                MessageBox.Show(tr.Tr("Drive not specified, nowhere to place generated config"));
                goto SkipGeneration;
            }
            int DecOffset09 = 0;
            int DecOffset0A_0C = 0;
            int DecOffset0D = 0;
            int DecOffset0F = 0;
            uint DecOffset10_13 = 0;
            int DecOffset21C = 0;
            int DecOffset21D = 0;
            string HexOffset09;
            string HexOffset0A_0C;
            string HexOffset0D;
            string HexOffset0F;
            string HexOffset10_13;
            string HexOffset21C;
            string HexOffset21D;

            //09 Offsets
            //TRI Arcade Mode
            if (NintendontOptions.GetItemChecked(11))
            {
                DecOffset09 = DecOffset09 + 1;
            }
            //Wiimote CC Rumble
            if (NintendontOptions.GetItemChecked(12))
            {
                DecOffset09 = DecOffset09 + 2;
            }
            //Skip IPL
            if (NintendontOptions.GetItemChecked(13))
            {
                DecOffset09 = DecOffset09 + 4;
            }
            //convert to hex string
            HexOffset09 = DecOffset09.ToString("X");
            HexOffset09 = HexOffset09.PadLeft(2, '0');

            //0A-0C Offsets
            //Cheats
            if (NintendontOptions.GetItemChecked(0))
            {
                DecOffset0A_0C = DecOffset0A_0C + 256;
            }
            //Memcard Emu
            if (NintendontOptions.GetItemChecked(1))
            {
                DecOffset0A_0C = DecOffset0A_0C + 2049;
            }
            //Memcard Multi
            if (MemcardMulti.Checked)
            {
                DecOffset0A_0C = DecOffset0A_0C + 2097152;
            }
            //Cheat Path
            if (NintendontOptions.GetItemChecked(2))
            {
                DecOffset0A_0C = DecOffset0A_0C + 4097;
            }
            //Force Widescreen
            if (NintendontOptions.GetItemChecked(3))
            {
                DecOffset0A_0C = DecOffset0A_0C + 8193;
            }
            //Force Progressive
            if (NintendontOptions.GetItemChecked(4))
            {
                DecOffset0A_0C = DecOffset0A_0C + 16387;
            }
            //Unlock Read Speed
            if (NintendontOptions.GetItemChecked(5))
            {
                DecOffset0A_0C = DecOffset0A_0C + 65540;
            }
            //OSReport
            if (NintendontOptions.GetItemChecked(6))
            {
                DecOffset0A_0C = DecOffset0A_0C + 131076;
            }
            //WiiU Widescreen
            if (NintendontOptions.GetItemChecked(7))
            {
                DecOffset0A_0C = DecOffset0A_0C + 8388612;
            }
            //Log
            if (NintendontOptions.GetItemChecked(8))
            {
                DecOffset0A_0C = DecOffset0A_0C + 1048594;
            }
            //convert to hex string
            HexOffset0A_0C = DecOffset0A_0C.ToString("X");
            HexOffset0A_0C = HexOffset0A_0C.PadLeft(6, '0');

            //0D Offsets
            //Video Force Options
            //Auto
            if (VideoForceMode.SelectedIndex == 0)
            {
                DecOffset0D = 0;
            }
            //Force
            if (VideoForceMode.SelectedIndex == 1)
            {
                DecOffset0D = 1;
            }
            //Force (Deflicker)
            if (VideoForceMode.SelectedIndex == 2)
            {
                DecOffset0D = 5;
            }
            //None
            if (VideoForceMode.SelectedIndex == 3)
            {
                DecOffset0D = 2;
            }
            //convert to hex string
            HexOffset0D = DecOffset0D.ToString("X");
            HexOffset0D = HexOffset0D.PadLeft(2, '0');

            //0F Offsets
            //Video Mode Options
            //None
            if (VideoTypeMode.SelectedIndex == 0)
            {
                DecOffset0F = 16;
            }
            //NTSC
            if (VideoTypeMode.SelectedIndex == 1)
            {
                DecOffset0F = 20;
            }
            //MPAL
            if (VideoTypeMode.SelectedIndex == 2)
            {
                DecOffset0F = 24;
            }
            //PAL50
            if (VideoTypeMode.SelectedIndex == 3)
            {
                DecOffset0F = 17;
            }
            //PAL60
            if (VideoTypeMode.SelectedIndex == 4)
            {
                DecOffset0F = 18;
            }
            //Patch PAL50
            if (NintendontOptions.GetItemChecked(10))
            {
                DecOffset0F = DecOffset0F + 32;
            }
            //convert to hex string
            HexOffset0F = DecOffset0F.ToString("X");
            HexOffset0F = HexOffset0F.PadLeft(2, '0');

            //10-13 Offsets
            //Language Selection
            //Automatic
            if (LanguageBox.SelectedIndex == 0)
            {
                DecOffset10_13 = 4294967295;
            }
            //English
            if (LanguageBox.SelectedIndex == 1)
            {
                DecOffset10_13 = 0;
            }
            //German
            if (LanguageBox.SelectedIndex == 2)
            {
                DecOffset10_13 = 1;
            }
            //French
            if (LanguageBox.SelectedIndex == 3)
            {
                DecOffset10_13 = 2;
            }
            //Spanish
            if (LanguageBox.SelectedIndex == 4)
            {
                DecOffset10_13 = 3;
            }
            //Italian
            if (LanguageBox.SelectedIndex == 5)
            {
                DecOffset10_13 = 4;
            }
            //Dutch
            if (LanguageBox.SelectedIndex == 6)
            {
                DecOffset10_13 = 5;
            }
            //convert to hex string
            HexOffset10_13 = DecOffset10_13.ToString("X");
            HexOffset10_13 = HexOffset10_13.PadLeft(8, '0');

            //21C Offsets
            //Memcard Blocks
            //59
            if (MemcardBlocks.SelectedIndex == 0)
            {
                DecOffset21C = 0;
            }
            //123
            if (MemcardBlocks.SelectedIndex == 1)
            {
                DecOffset21C = 1;
            }
            //251
            if (MemcardBlocks.SelectedIndex == 2)
            {
                DecOffset21C = 2;
            }
            //507
            if (MemcardBlocks.SelectedIndex == 3)
            {
                DecOffset21C = 3;
            }
            //1019
            if (MemcardBlocks.SelectedIndex == 4)
            {
                DecOffset21C = 4;
            }
            //2043
            if (MemcardBlocks.SelectedIndex == 5)
            {
                DecOffset21C = 5;
            }
            //convert to hex string
            HexOffset21C = DecOffset21C.ToString("X");
            HexOffset21C = HexOffset21C.PadLeft(2, '0');

            //21D Offsets
            //Video Width
            if (NintendontOptions.GetItemChecked(9))
            {
                DecOffset21D = 0;
            }
            else
            {
                DecOffset21D = VideoWidth.Value - 600;
            }
            //convert to hex string
            HexOffset21D = DecOffset21D.ToString("X");
            HexOffset21D = HexOffset21D.PadLeft(2, '0');

            //Output Hex File
            string config = "01070CF60000000800" + HexOffset09 + HexOffset0A_0C + HexOffset0D + "00" + HexOffset0F + HexOffset10_13 + "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" + HexOffset21C + HexOffset21D + "0000";
            File.WriteAllBytes(SelectedDriveLetter + "nincfg.bin", StringToByteArray(config));

            MessageBox.Show(tr.Tr("Config generation complete."));
            SkipGeneration:;
        }

        private void Format_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://www.ridgecrop.demon.co.uk/index.htm?guiformat.htm");
            Process.Start("http://www.ridgecrop.demon.co.uk/guiformat.exe");
        }
    }
}
