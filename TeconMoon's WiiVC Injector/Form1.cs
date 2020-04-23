using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;
using StackOverflow;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Media;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using TeconMoon_s_WiiVC_Injector.Utils;

namespace TeconMoon_s_WiiVC_Injector
{
    public partial class WiiVC_Injector : Form
    {
        public WiiVC_Injector()
        {
            InitializeComponent();
            //Check for if .Net v3.5 component is installed
            CheckForNet35();

            if (!ExtractToolChainsToTemp())
            {
                MessageBox.Show(
                    tr.Tr("Create temporary directory failed, it may be caused by "
                    + "low space on hard drive, permission denied or invalid path name."),
                    tr.Tr("Error"));

                Environment.Exit(0);
            }

            ApplyTranslation();

            this.Text += " - [" + Program.Version + "]";

#if !DEBUG
            this.DebugButton.Visible = false;
#endif

            LoadSettings();

            // 
            // Initlize actions for build thread.
            //
            ActBuildStatus = new Action<string>((s) =>
            {
                BuildStatus.Text = s;
                BuildStatus.Refresh();
            });

            ActBuildProgress = new Action<int>((progress) =>
            {
                BuildProgress.Value = progress;
            });

            ActBuildOutput = new Action<BuildOutputItem>((item) =>
            {
                AppendBuildOutput(item);
            });

            // Process any pending build requests.
            AutoBuildWiiRetail();
        }

        bool ExtractToolChainsToTemp()
        {
            try
            {
                //Delete Temporary Root Folder if it exists
                if (Directory.Exists(TempRootPath))
                {
                    Directory.Delete(TempRootPath, true);
                }
                Directory.CreateDirectory(TempRootPath);
                //Extract Tools to temp folder
                File.WriteAllBytes(TempRootPath + "TOOLDIR.zip", Properties.Resources.TOOLDIR);
                ZipFile.ExtractToDirectory(TempRootPath + "TOOLDIR.zip", TempRootPath);
                File.Delete(TempRootPath + "TOOLDIR.zip");
                //Create Source and Build directories
                Directory.CreateDirectory(TempSourcePath);
                Directory.CreateDirectory(TempBuildPath);
            }
            catch (Exception)
            {
                if (!TempRootPath.Equals(DefaultTempRootPath, StringComparison.OrdinalIgnoreCase))
                {
                    Registry.CurrentUser.CreateSubKey("WiiVCInjector").DeleteValue("TemporaryDirectory");
                    TempRootPath = DefaultTempRootPath;
                    UpdateTempDirs();
                    return ExtractToolChainsToTemp();
                }

                return false;
            }

            return true;
        }

        void UpdateTempDirs()
        {
            TempSourcePath = TempRootPath + "SOURCETEMP\\";
            TempBuildPath = TempRootPath + "BUILDDIR\\";
            TempToolsPath = TempRootPath + "TOOLDIR\\";
            TempIconPath = TempRootPath + "SOURCETEMP\\iconTex.png";
            TempBannerPath = TempRootPath + "SOURCETEMP\\bootTvTex.png";
            TempDrcPath = TempRootPath + "SOURCETEMP\\bootDrcTex.png";
            TempLogoPath = TempRootPath + "SOURCETEMP\\bootLogoTex.png";
            TempSoundPath = TempRootPath + "SOURCETEMP\\bootSound.wav";
        }

        void CleanupBuildSourceTemp()
        {
            string[] tempImages =
            {
                "iconTex.png",
                "bootTvTex.png",
                "bootDrcTex.png",
                "bootLogoTex.png"
            };

            foreach (string tempImage in tempImages)
            {
                string tempImagePath = TempSourcePath + "\\" + tempImage;
                string tempPath = TempRootPath + "\\" + tempImage;

                if (File.Exists(tempImagePath))
                {
                    File.Move(tempImagePath, tempPath);
                }
            }

            string[] cleanPaths =
            {
                TempSourcePath,
                TempBuildPath
            };

            foreach (string path in cleanPaths)
            {
                if (Directory.Exists(path))
                {
                    try
                    {
                        Directory.Delete(path, true);
                    }
                    catch (Exception)
                    {

                    }

                    // Recreate Source and Build directories
                    try
                    {
                        Directory.CreateDirectory(path);
                    }
                    catch (Exception)
                    {

                    }
                }
            }

            foreach (string tempImage in tempImages)
            {
                string tempImagePath = TempSourcePath + "\\" + tempImage;
                string tempPath = TempRootPath + "\\" + tempImage;

                if (File.Exists(tempImagePath))
                {
                    File.Move(tempPath, tempImagePath);
                }
            }
        }

        private void LoadSettings()
        {
            //Initialize Registry values if they don't exist and pull values from them if they do
            RegistryKey appKey = Registry.CurrentUser.CreateSubKey("WiiVCInjector");
            if (appKey.GetValue("WiiUCommonKey") == null)
            {
                appKey.SetValue("WiiUCommonKey", "00000000000000000000000000000000");
            }
            WiiUCommonKey.Text = appKey.GetValue("WiiUCommonKey").ToString();
            if (appKey.GetValue("TitleKey") == null)
            {
                appKey.SetValue("TitleKey", "00000000000000000000000000000000");
            }
            TitleKey.Text = appKey.GetValue("TitleKey").ToString();
            OutputDirectory.Text = appKey.GetValue("OutputDirectory") as string;
            TemporaryDirectory.Text = GetTempRootPath(false);
            appKey.Close();
        }

        void ApplyTranslation()
        {
            if (tr.IsValidate)
            {
                tr.TranslationForm(this);
            }

            // 
            // OpenIcon
            // 
            this.OpenIcon.FileName = "iconTex";
            this.OpenIcon.Title = tr.Tr("Your image will be converted to PNG (Lossless) to be later processed.");
            // 
            // OpenBanner
            // 
            this.OpenBanner.FileName = "bootTvTex";
            this.OpenBanner.Title = tr.Tr("Your image will be converted to PNG (Lossless) to be later processed.");
            // 
            // OpenGame
            // 
            this.OpenGame.FileName = "game";
            this.OpenGame.Filter = tr.Tr("Wii Dumps (*.iso,*.wbfs)|*.iso;*.wbfs");
            this.OpenGame.Title = tr.Tr("Specify your game file");
            // 
            // OpenDrc
            // 
            this.OpenDrc.FileName = "bootDrcTex";
            this.OpenDrc.Title = tr.Tr("Your image will be converted to PNG (Lossless) to be later processed.");
            // 
            // OpenLogo
            // 
            this.OpenLogo.FileName = "bootLogoTex";
            this.OpenLogo.Title = tr.Tr("Your image will be converted to PNG (Lossless) to be later processed.");
            // 
            // OpenBootSound
            // 
            this.OpenBootSound.FileName = "BootSound";
            this.OpenBootSound.Filter = tr.Tr("Supported Sound Files (*.wav)|*wav");
            this.OpenBootSound.Title = tr.Tr("Specify your boot sound");
            // 
            // OpenMainDol
            // 
            this.OpenMainDol.FileName = "main";
            this.OpenMainDol.Filter = tr.Tr("Nintendont Forwarder (*.dol)|*.dol");
            this.OpenMainDol.Title = tr.Tr("Specify your replacement Nintendont Forwarder");
            // 
            // OpenGC2
            // 
            this.OpenGC2.FileName = "disc2";
            this.OpenGC2.Filter = tr.Tr("GameCube Disk 2 (*.gcm,*.iso)|*.gcm;*.iso");
            this.OpenGC2.Title = tr.Tr("Specify your GameCube game\'s 2nd disc");
        }

        private void EnableControl(Control control, bool enabled)
        {
            if (ControlEnabledStatus.ContainsKey(control.Name))
            {
                ControlEnabledStatus[control.Name] = control.Enabled;
            }
            else
            {
                ControlEnabledStatus.Add(control.Name, control.Enabled);
            }

            foreach (Control subControl in control.Controls)
            {
                EnableControl(subControl, enabled);
            }

            control.Enabled = enabled;
        }

        private void RestoreContorlEnabled(Control control)
        {
            if (ControlEnabledStatus.ContainsKey(control.Name))
            {
                control.Enabled = ControlEnabledStatus[control.Name];
            }

            foreach (Control subControl in control.Controls)
            {
                RestoreContorlEnabled(subControl);
            }
        }

        private void EnsureEnableControl(Control control, bool enabled)
        {
            control.Enabled = enabled;

            if (enabled)
            {
                Control parent = control.Parent;
                while (parent != null)
                {
                    parent.Enabled = true;
                    parent = parent.Parent;
                }
            }
        }

        private void FreezeFormBuild(bool freeze)
        {
            if (freeze)
            {
                foreach (Control control in Controls)
                {
                    EnableControl(control, false);
                }

                EnsureEnableControl(TheBigOneTM, true);
                EnsureEnableControl(BuildOutput, true);
                EnsureEnableControl(BuildOutputToolStrip, true);
            }
            else
            {
                foreach (Control control in Controls)
                {
                    RestoreContorlEnabled(control);
                }

                EnableSystemSelection(MainTabs.SelectedTab == SourceFilesTab);
            }
        }

        void AutoBuildWiiRetail()
        {
            if (Program.AutoBuildList.Count == 0)
            {
                return;
            }

            if (IsBuilding)
            {
                Program.AutoBuildList.Clear();
                return;
            }

            AutoBuildSucceedList.Clear();
            AutoBuildFailedList.Clear();
            AutoBuildSkippedList.Clear();

            BuildCompletedEx += WiiVC_Injector_BuildCompletedEx;

            WiiRetail.PerformClick();

            PropmtForSucceed = false;
            AutoBuildNext();
        }

        void AutoBuildNext()
        {
            if (Program.AutoBuildList.Count() != 0)
            {
                string game = Program.AutoBuildList[0];

                if (SelectGameSource(game, true))
                {
                    BuildCurrentWiiRetail();
                }
            }
            else
            {
                BuildCompletedEx -= WiiVC_Injector_BuildCompletedEx;

                if (!InClosing)
                {
                    string s = String.Format(
                        tr.Tr("All conversions have been completed.\nSucceed: {0}.\nFailed: {1}."),
                        AutoBuildSucceedList.Count,
                        AutoBuildFailedList.Count);

                    MessageBox.Show(s);
                }
            }
        }

        private void WiiVC_Injector_BuildCompletedEx(object sender, bool e)
        {
            if (e)
            {
                AutoBuildSucceedList.Add(Program.AutoBuildList[0]);
            }
            else
            {
                AutoBuildFailedList.Add(Program.AutoBuildList[0]);
            }

            Program.AutoBuildList.RemoveAt(0);

            if (LastBuildCancelled)
            {
                AutoBuildSkippedList.AddRange(Program.AutoBuildList);
                Program.AutoBuildList.Clear();
            }

            AutoBuildNext();
        }

        bool BuildCurrentWiiRetail()
        {
            // Switch to Source Files Tab.
            MainTabs.SelectedIndex = MainTabs.TabPages.IndexOfKey("SourceFilesTab");

            // Auto generate images.
            GenerateImage.PerformClick();

            // Switch to Build Tab.
            MainTabs.SelectedIndex = MainTabs.TabPages.IndexOfKey("BuildTab");

            // Check if everything is ready.
            if (TheBigOneTM.Enabled && !IsBuilding)
            {
                // Ready to rumble. :)
                return BuildAnsync();
            }

            return false;
        }

        private string NormalizeCmdlineArg(string arg)
        {
            return String.Format("\"{0}\"", arg);
        }

        private void AppendBuildOutput(BuildOutputItem item)
        {
            Color color = BuildOutput.ForeColor;
            Font font = null;

            switch (item.buildOutputType)
            {
                case BuildOutputType.botSucceed:
                    color = Color.Green;
                    font = new Font(BuildOutput.Font, FontStyle.Bold);
                    break;
                case BuildOutputType.botError:
                    color = Color.DarkRed;
                    font = new Font(BuildOutput.Font, FontStyle.Bold);
                    break;
                case BuildOutputType.botStep:
                    color = Color.AliceBlue;
                    font = new Font(BuildOutput.Font.FontFamily, BuildOutput.Font.Size + 1, FontStyle.Bold);
                    break;
                case BuildOutputType.botExec:
                    color = Color.Blue;
                    font = new Font(BuildOutput.Font, FontStyle.Bold);
                    break;
                case BuildOutputType.botNormal:
                default:
                    break;
            }
            BuildOutput.AppendText(item.s, color, font);

            if (AutoScrollBuildOutput.Checked)
            {
                BuildOutput.SelectionStart = BuildOutput.TextLength;
                BuildOutput.ScrollToCaret();
            }
        }

        //Specify public variables for later use (ASK ALAN)
        string SystemType = "wii";
        string TitleIDHex;
        string TitleIDText;
        string InternalGameName;
        bool FlagWBFS;
        bool FlagGameSpecified;
        bool FlagGC2Specified;
        bool FlagIconSpecified;
        bool FlagBannerSpecified;
        bool FlagDrcSpecified;
        bool FlagLogoSpecified;
        bool FlagBootSoundSpecified;
        bool BuildFlagSource;
        bool BuildFlagMeta;
        bool BuildFlagAdvance = true;
        bool BuildFlagKeys;
        bool CommonKeyGood;
        bool TitleKeyGood;
        bool AncastKeyGood;
        bool FlagRepo;
        bool HideProcess = true;
        bool ThrowProcessException = false;
        bool LastBuildCancelled = false;
        bool InClosing = false;
        bool PropmtForSucceed = true;
        int TitleIDInt;
        long GameType;
        string CucholixRepoID = "";
        string sSourceData;
        byte[] tmpSource;
        byte[] tmpHash;
        string AncastKeyHash;
        string WiiUCommonKeyHash;
        string TitleKeyHash;
        string DRCUSE = "1";
        string pngtemppath;
        string LoopString = " -noLoop";
        string nfspatchflag = "";
        string passpatch = " -passthrough";
        ProcessStartInfo Launcher;
        string LauncherExeFile;
        string LauncherExeArgs;
        string JNUSToolDownloads = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + "\\JNUSToolDownloads\\";
        static string DefaultTempRootPath = Path.GetTempPath() + "WiiVCInjector\\";
        static string TempRootPath = GetTempRootPath() + "WiiVCInjector\\";
        string TempSourcePath = TempRootPath + "SOURCETEMP\\";
        string TempBuildPath = TempRootPath + "BUILDDIR\\";
        string TempToolsPath = TempRootPath + "TOOLDIR\\";
        string TempIconPath = TempRootPath + "SOURCETEMP\\iconTex.png";
        string TempBannerPath = TempRootPath + "SOURCETEMP\\bootTvTex.png";
        string TempDrcPath = TempRootPath + "SOURCETEMP\\bootDrcTex.png";
        string TempLogoPath = TempRootPath + "SOURCETEMP\\bootLogoTex.png";
        string TempSoundPath = TempRootPath + "SOURCETEMP\\bootSound.wav";

        enum BuildOutputType
        {
            botNormal,
            botSucceed,
            botError,
            botStep,
            botExec,
        };

        struct BuildOutputItem
        {
            public string s;
            public BuildOutputType buildOutputType;
        };

        string GameIso;
        List<string> AutoBuildSucceedList = new List<string>();
        List<string> AutoBuildFailedList = new List<string>();
        List<string> AutoBuildSkippedList = new List<string>();
        Dictionary<String, bool> ControlEnabledStatus = new Dictionary<String, bool>();
        Thread BuilderThread;
        TranslationTemplate tr = TranslationTemplate.LoadTemplate(
                Application.StartupPath + @"\language.lang");
        Action<string> ActBuildStatus;
        Action<int> ActBuildProgress;
        Action<BuildOutputItem> ActBuildOutput;
        Stopwatch BuildStopwatch = new Stopwatch();

        private bool IsBuilding
        {
            get
            {
                return BuilderThread != null;
            }
        }

        private delegate bool BuildStep();

        private event EventHandler<bool> BuildCompletedEx;

        private static string GetTempRootPath(bool hasBackslash = true)
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey("WiiVCInjector");
            string path = Path.GetTempPath();

            if (key != null)
            {
                path = key.GetValue("TemporaryDirectory", "") as string;
                if (String.IsNullOrWhiteSpace(path))
                {
                    path = Path.GetTempPath();
                }
            }

            if (hasBackslash)
            {
                if (!path.EndsWith("\\"))
                {
                    path += "\\";
                }
            }
            else
            {
                path = path.TrimEnd(new char[] { '\\' });
            }

            return path;
        }

        //call options
        public bool LaunchProgram()
        {
            Launcher = new ProcessStartInfo(LauncherExeFile, LauncherExeArgs);

            if (HideProcess)
            {
                Launcher.CreateNoWindow = true;
                Launcher.WindowStyle = ProcessWindowStyle.Hidden;
            }

            Launcher.RedirectStandardOutput = true;
            Launcher.RedirectStandardError = true;
            Launcher.UseShellExecute = false;

            bool exitNormally = true;

            BeginInvoke(ActBuildOutput, new BuildOutputItem()
            {
                s = tr.Tr("Executing:") + ' ' + LauncherExeFile + '\n' 
                  + tr.Tr("Args:") + ' ' + LauncherExeArgs + '\n',
                buildOutputType = BuildOutputType.botExec
            });


            try
            {
                Process process = Process.Start(Launcher);

                AsyncStreamReader standardOutput = new AsyncStreamReader(process.StandardOutput);
                AsyncStreamReader standardError = new AsyncStreamReader(process.StandardError);

                string OutputReceived = "";

                System.Timers.Timer OutputPumpTimer = new System.Timers.Timer();
                OutputPumpTimer.Interval = 100;
                OutputPumpTimer.Elapsed += (sender, e) =>
                {
                    if (!String.IsNullOrEmpty(OutputReceived))
                    {
                        BeginInvoke(ActBuildOutput, new BuildOutputItem()
                        {
                            s = OutputReceived,
                            buildOutputType = BuildOutputType.botNormal
                        });

                        OutputReceived = "";
                    }
                };
                OutputPumpTimer.Start();

                standardOutput.DataReceived += (sender, data) =>
                {
                    if (String.IsNullOrEmpty(data))
                    {
                        return;
                    }
                    
                    OutputReceived += data.Replace("\0", "");
                };

                standardError.DataReceived += (sender, data) =>
                {
                    if (String.IsNullOrEmpty(data))
                    {
                        return;
                    }

                    if (!String.IsNullOrEmpty(OutputReceived))
                    {
                        BeginInvoke(ActBuildOutput, new BuildOutputItem()
                        {
                            s = OutputReceived,
                            buildOutputType = BuildOutputType.botNormal
                        });

                        OutputReceived = "";
                    }

                    BeginInvoke(ActBuildOutput, new BuildOutputItem()
                    {
                        s = data.Replace("\0", ""),
                        buildOutputType = BuildOutputType.botError
                    });
                };

                standardOutput.Start();
                standardError.Start();

                while (!process.WaitForExit(500))
                {
                    if (LastBuildCancelled)
                    {
                        process.CloseMainWindow();
                        if (!process.WaitForExit(100))
                        {
                            process.Kill();
                        }

                        exitNormally = false;
                    }
                }

                OutputPumpTimer.Stop();

                if (!String.IsNullOrEmpty(OutputReceived))
                {
                    BeginInvoke(ActBuildOutput, new BuildOutputItem()
                    {
                        s = OutputReceived,
                        buildOutputType = BuildOutputType.botNormal
                    });

                    OutputReceived = "";
                }

                process.Close();
            }
            catch (Exception ex)
            {
                Console.Write("err(LaunchProgram): " + ex.Message);

                if (ThrowProcessException)
                {
                    throw ex;
                }

                exitNormally = false;
            }

            if (!exitNormally && ThrowProcessException)
            {
                throw new Exception(NormalizeCmdlineArg(LauncherExeFile) 
                    + " does not exit normally.");
            }

            return exitNormally;
        }
        public static bool CheckForInternetConnection()
        {
            try
            {
                using (var client = new WebClient())
                {
                    string testUrl = "http://clients3.google.com/generate_204";

                    if (Thread.CurrentThread.CurrentCulture.Name == "zh-CN")
                    {
                        testUrl = "https://www.baidu.com";
                    }

                    using (client.OpenRead(testUrl))
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
        public static string GetFullPath(string fileName)
        {
            var values = Environment.GetEnvironmentVariable("PATH");
            foreach (var path in values.Split(';'))
            {
                var fullPath = Path.Combine(path, fileName);
                if (File.Exists(fullPath))
                    return fullPath;
            }
            return null;
        }
        public void DownloadFromRepo()
        {
            var client = new WebClient();
            IconPreviewBox.Load("https://raw.githubusercontent.com/cucholix/wiivc-bis/master/" + SystemType + "/image/" + CucholixRepoID + "/iconTex.png");
            if (File.Exists(Path.GetTempPath() + "WiiVCInjector\\SOURCETEMP\\iconTex.png")) { File.Delete(Path.GetTempPath() + "WiiVCInjector\\SOURCETEMP\\iconTex.png"); }
            client.DownloadFile(IconPreviewBox.ImageLocation, Path.GetTempPath() + "WiiVCInjector\\SOURCETEMP\\iconTex.png");
            IconSourceDirectory.Text = tr.Tr("iconTex.png downloaded from Cucholix's Repo");
            IconSourceDirectory.ForeColor = Color.Black;
            FlagIconSpecified = true;
            BannerPreviewBox.Load("https://raw.githubusercontent.com/cucholix/wiivc-bis/master/" + SystemType + "/image/" + CucholixRepoID + "/bootTvTex.png");
            if (File.Exists(Path.GetTempPath() + "WiiVCInjector\\SOURCETEMP\\bootTvTex.png")) { File.Delete(Path.GetTempPath() + "WiiVCInjector\\SOURCETEMP\\bootTvTex.png"); }
            client.DownloadFile(BannerPreviewBox.ImageLocation, Path.GetTempPath() + "WiiVCInjector\\SOURCETEMP\\bootTvTex.png");
            BannerSourceDirectory.Text = tr.Tr("bootTvTex.png downloaded from Cucholix's Repo");
            BannerSourceDirectory.ForeColor = Color.Black;
            FlagBannerSpecified = true;
            FlagRepo = true;
        }
        //Called from RepoDownload_Click to check if files exist before downloading
        private bool RemoteFileExists(string url)
        {          
            HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
            request.Method = "HEAD";
            HttpWebResponse response = request.GetResponse() as HttpWebResponse;
            response.Close();
            return (response.StatusCode == HttpStatusCode.OK);
        }
        private void CheckForNet35()
        {
            if (Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\NET Framework Setup\\NDP\\v3.5") == null)
            {
                MessageBox.Show(
                    tr.Tr(".NET Framework 3.5 was not detected on your machine, which is required by programs used during the build process.\n\nYou should be able to enable this in \"Programs and Features\" under \"Turn Windows features on or off\", or download it from Microsoft.\n\nClick OK to close the injector and open \"Programs and Features\"..."),
                    tr.Tr(".NET Framework v3.5 not found..."));
                HideProcess = false;
                LauncherExeFile = "appwiz.cpl";
                LauncherExeArgs = "";
                LaunchProgram();
                Environment.Exit(0);
            }
        }

        private void DeleteTempDir()
        {
            try
            {
                Directory.Delete(TempRootPath, true);
            }
            catch (Exception)
            {

            }
        }

        //Cleanup when program is closed
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            if (InClosing)
            {
                return;
            }

            if (e.CloseReason == CloseReason.WindowsShutDown)
            {
                InClosing = true;

                if (IsBuilding)
                {
                    BuildCompletedEx = null;
                    BuildCompletedEx += ((s, a) =>
                    {
                        DeleteTempDir();
                        Close();
                    });

                    CancelBuild();

                    int nWaitLoop = 0;
                    while (IsBuilding)
                    {                       
                        if (nWaitLoop++ < 10)
                        {
                            Thread.Sleep(100);
                            continue;
                        }

                        try
                        {
                            BuilderThread.Abort();
                        }
                        catch (Exception)
                        {

                        }

                        DeleteTempDir();

                        break;
                    }
                }
                else
                {
                    DeleteTempDir();
                }

                return;
            }

            // Confirm user wants to close
            switch (MessageBox.Show(this, tr.Tr("Are you sure you want to close?"), 
                tr.Tr("Closing"), MessageBoxButtons.YesNo))
            {
                case DialogResult.No:
                    e.Cancel = true;
                    break;
                default:
                    InClosing = true;

                    if (IsBuilding)
                    {
                        BuildCompletedEx = null;
                        BuildCompletedEx += ((s, a) =>
                        {
                            DeleteTempDir();
                            InClosing = true;
                            Close();
                        });

                        CancelBuild();
                        e.Cancel = true;
                        break;
                    }

                    DeleteTempDir();
                    break;
            }
        }

        //Radio Buttons for desired injection type (Check with Alan on having one command to clear variables instead of specifying them all 4 times)
        private void WiiRetail_CheckedChanged(object sender, EventArgs e)
        {
            if (WiiRetail.Checked)
            {
                WiiVMC.Enabled = true;
                RepoDownload.Enabled = true;
                GameSourceButton.Enabled = true;
                GameSourceButton.Text = tr.Tr("Game...");
                OpenGame.FileName = tr.Tr("game");
                OpenGame.Filter = tr.Tr("Wii Dumps (*.iso,*.wbfs)|*.iso;*.wbfs");
                GameSourceDirectory.Text = tr.Tr("Game file has not been specified");
                GameSourceDirectory.ForeColor = Color.Red;
                FlagGameSpecified = false;
                SystemType = "wii";
                GameNameLabel.Text = "";
                TitleIDLabel.Text = "";
                TitleIDInt = 0;
                TitleIDHex = "";
                GameType = 0;
                CucholixRepoID = "";
                PackedTitleLine1.Text = "";
                PackedTitleIDLine.Text = "";
                GC2SourceButton.Enabled = false;
                GC2SourceDirectory.Text = tr.Tr("2nd GameCube Disc Image has not been specified");
                GC2SourceDirectory.ForeColor = Color.Red;
                FlagGC2Specified = false;
                if (NoGamePadEmu.Checked == false & CCEmu.Checked == false & HorWiiMote.Checked == false & VerWiiMote.Checked == false & ForceCC.Checked == false & ForceNoCC.Checked == false)
                {
                    NoGamePadEmu.Checked = true;
                    GamePadEmuLayout.Enabled = true;
                    DRCUSE = "1";
                }
                Force43NINTENDONT.Checked = false;
                Force43NINTENDONT.Enabled = false;
                CustomMainDol.Checked = false;
                CustomMainDol.Enabled = false;
                DisableNintendontAutoboot.Checked = false;
                DisableNintendontAutoboot.Enabled = false;
                DisablePassthrough.Checked = false;
                DisablePassthrough.Enabled = false;
                DisableGamePad.Checked = false;
                DisableGamePad.Enabled = false;
                C2WPatchFlag.Checked = false;
                C2WPatchFlag.Enabled = false;
                if (ForceCC.Checked) { DisableTrimming.Checked = false; DisableTrimming.Enabled = false; } else { DisableTrimming.Enabled = true; }
                Force43NAND.Checked = false;
                Force43NAND.Enabled = false;
            }
        }
        private void WiiHomebrew_CheckedChanged(object sender, EventArgs e)
        {
            if (WiiHomebrew.Checked)
            {
                WiiVMC.Checked = false;
                WiiVMC.Enabled = false;
                RepoDownload.Enabled = false;
                GameSourceButton.Enabled = true;
                GameSourceButton.Text = tr.Tr("Game...");
                OpenGame.FileName = "boot.dol";
                OpenGame.Filter = tr.Tr("DOL Files (*.dol)|*.dol");
                GameSourceDirectory.Text = tr.Tr("Game file has not been specified");
                GameSourceDirectory.ForeColor = Color.Red;
                FlagGameSpecified = false;
                SystemType = "dol";
                GameNameLabel.Text = "";
                TitleIDLabel.Text = "";
                TitleIDInt = 0;
                TitleIDHex = "";
                GameType = 0;
                CucholixRepoID = "";
                PackedTitleLine1.Text = "";
                PackedTitleIDLine.Text = "";
                DRCUSE = "65537";
                GC2SourceButton.Enabled = false;
                GC2SourceDirectory.Text = tr.Tr("2nd GameCube Disc Image has not been specified");
                GC2SourceDirectory.ForeColor = Color.Red;
                FlagGC2Specified = false;
                NoGamePadEmu.Checked = false;
                CCEmu.Checked = false;
                HorWiiMote.Checked = false;
                VerWiiMote.Checked = false;
                ForceCC.Checked = false;
                ForceNoCC.Checked = false;
                GamePadEmuLayout.Enabled = false;
                LRPatch.Checked = false;
                LRPatch.Enabled = false;
                Force43NINTENDONT.Checked = false;
                Force43NINTENDONT.Enabled = false;
                CustomMainDol.Checked = false;
                CustomMainDol.Enabled = false;
                DisableNintendontAutoboot.Checked = false;
                DisableNintendontAutoboot.Enabled = false;
                DisablePassthrough.Enabled = true;
                DisableGamePad.Enabled = true;
                C2WPatchFlag.Enabled = true;
                DisableTrimming.Checked = false;
                DisableTrimming.Enabled = false;
                Force43NAND.Checked = false;
                Force43NAND.Enabled = false;
            }
        }
        private void WiiNAND_CheckedChanged(object sender, EventArgs e)
        {
            if (WiiNAND.Checked)
            {
                WiiNANDLoopback:
                WiiVMC.Checked = false;
                WiiVMC.Enabled = false;
                RepoDownload.Enabled = true;
                GameSourceButton.Enabled = false;
                GameSourceButton.Text = tr.Tr("TitleID...");
                OpenGame.FileName = tr.Tr("NULL");
                GameNameLabel.Text = "";
                TitleIDLabel.Text = "";
                TitleIDInt = 0;
                TitleIDHex = "";
                GameType = 0;
                CucholixRepoID = "";
                PackedTitleLine1.Text = "";
                PackedTitleIDLine.Text = "";
                GC2SourceButton.Enabled = false;
                GC2SourceDirectory.Text = tr.Tr("2nd GameCube Disc Image has not been specified");
                GC2SourceDirectory.ForeColor = Color.Red;
                FlagGC2Specified = false;
                Force43NINTENDONT.Checked = false;
                Force43NINTENDONT.Enabled = false;
                CustomMainDol.Checked = false;
                CustomMainDol.Enabled = false;
                DisableNintendontAutoboot.Checked = false;
                DisableNintendontAutoboot.Enabled = false;
                DisablePassthrough.Checked = false;
                DisablePassthrough.Enabled = false;
                DisableGamePad.Checked = false;
                DisableGamePad.Enabled = false;
                C2WPatchFlag.Checked = false;
                C2WPatchFlag.Enabled = false;
                DisableTrimming.Checked = false;
                DisableTrimming.Enabled = false;
                Force43NAND.Enabled = true;
                if (NoGamePadEmu.Checked == false & CCEmu.Checked == false & HorWiiMote.Checked == false & VerWiiMote.Checked == false & ForceCC.Checked == false & ForceNoCC.Checked == false)
                {
                    NoGamePadEmu.Checked = true;
                    GamePadEmuLayout.Enabled = true;
                    DRCUSE = "1";
                }
                GameSourceDirectory.Text = Microsoft.VisualBasic.Interaction.InputBox(
                    tr.Tr("Enter your installed Wii Channel's 4-letter Title ID. If you don't know it, open a WAD for the channel in something like ShowMiiWads to view it."),
                    tr.Tr("Enter your WAD's Title ID"), tr.Tr("XXXX"), 0, 0);
                if (GameSourceDirectory.Text == "")
                {
                    GameSourceDirectory.ForeColor = Color.Red;
                    GameSourceDirectory.Text = tr.Tr("Title ID specification cancelled, reselect vWii NAND Title Launcher to specify");
                    FlagGameSpecified = false;
                    goto skipWiiNandLoopback;
                }
                if (GameSourceDirectory.Text.Length == 4)
                {
                    GameSourceDirectory.Text = GameSourceDirectory.Text.ToUpper();
                    GameSourceDirectory.ForeColor = Color.Black;
                    FlagGameSpecified = true;
                    SystemType = "wiiware";
                    GameNameLabel.Text = tr.Tr("N/A");
                    TitleIDLabel.Text = tr.Tr("N/A");
                    TitleIDText = GameSourceDirectory.Text;
                    CucholixRepoID = GameSourceDirectory.Text;
                    char[] HexIDBuild = GameSourceDirectory.Text.ToCharArray();
                    StringBuilder stringBuilder = new StringBuilder();
                    foreach (char c in HexIDBuild)
                    {
                        stringBuilder.Append(((Int16)c).ToString("X"));
                    }
                    PackedTitleIDLine.Text = "00050002" + stringBuilder.ToString();
                }
                else
                {
                    GameSourceDirectory.ForeColor = Color.Red;
                    GameSourceDirectory.Text = tr.Tr("Invalid Title ID");
                    FlagGameSpecified = false;
                    MessageBox.Show(
                        tr.Tr("Only 4 characters can be used, try again. Example: The Star Fox 64 (USA) Channel's Title ID is NADE01, so you would specify NADE as the Title ID"));
                    goto WiiNANDLoopback;
                }
                skipWiiNandLoopback:;
            }
        }
        private void GCRetail_CheckedChanged(object sender, EventArgs e)
        {
            if (GCRetail.Checked)
            {
                WiiVMC.Checked = false;
                WiiVMC.Enabled = false;
                RepoDownload.Enabled = true;
                GameSourceButton.Enabled = true;
                GameSourceButton.Text = tr.Tr("Game...");
                OpenGame.FileName = tr.Tr("game");
                OpenGame.Filter = tr.Tr("GameCube Dumps (*.gcm,*.iso)|*.gcm;*.iso");
                GameSourceDirectory.Text = tr.Tr("Game file has not been specified");
                GameSourceDirectory.ForeColor = Color.Red;
                FlagGameSpecified = false;
                SystemType = "gcn";
                GameNameLabel.Text = "";
                TitleIDLabel.Text = "";
                TitleIDInt = 0;
                TitleIDHex = "";
                GameType = 0;
                CucholixRepoID = "";
                PackedTitleLine1.Text = "";
                PackedTitleIDLine.Text = "";
                DRCUSE = "65537";
                GC2SourceButton.Enabled = true;
                NoGamePadEmu.Checked = false;
                CCEmu.Checked = false;
                HorWiiMote.Checked = false;
                VerWiiMote.Checked = false;
                ForceCC.Checked = false;
                ForceNoCC.Checked = false;
                GamePadEmuLayout.Enabled = false;
                LRPatch.Checked = false;
                LRPatch.Enabled = false;
                Force43NINTENDONT.Enabled = true;
                CustomMainDol.Enabled = true;
                DisableNintendontAutoboot.Enabled = true;
                DisablePassthrough.Checked = false;
                DisablePassthrough.Enabled = false;
                DisableGamePad.Enabled = true;
                C2WPatchFlag.Checked = false;
                C2WPatchFlag.Enabled = false;
                DisableTrimming.Checked = false;
                DisableTrimming.Enabled = false;
                Force43NAND.Checked = false;
                Force43NAND.Enabled = false;
            }
        }
        private void SDCardStuff_Click(object sender, EventArgs e)
        {
            new SDCardMenu().Show();
        }

        void EnableSystemSelection(bool enabled)
        {
            WiiRetail.Enabled = enabled;
            WiiHomebrew.Enabled = enabled;
            WiiNAND.Enabled = enabled;
            GCRetail.Enabled = enabled;
        }

        void CheckBuildRequirements()
        {
            //Generate MD5 hashes for loaded keys and check them
            WiiUCommonKey.Text = WiiUCommonKey.Text.ToUpper();
            sSourceData = WiiUCommonKey.Text;
            tmpSource = ASCIIEncoding.ASCII.GetBytes(sSourceData);
            tmpHash = new MD5CryptoServiceProvider().ComputeHash(tmpSource);
            WiiUCommonKeyHash = BitConverter.ToString(tmpHash);
            if (WiiUCommonKeyHash == "35-AC-59-94-97-22-79-33-1D-97-09-4F-A2-FB-97-FC")
            {
                CommonKeyGood = true;
                WiiUCommonKey.ReadOnly = true;
                WiiUCommonKey.BackColor = Color.Lime;
            }
            else
            {
                CommonKeyGood = false;
                WiiUCommonKey.ReadOnly = false;
                WiiUCommonKey.BackColor = Color.White;
            }
            TitleKey.Text = TitleKey.Text.ToUpper();
            sSourceData = TitleKey.Text;
            tmpSource = ASCIIEncoding.ASCII.GetBytes(sSourceData);
            tmpHash = new MD5CryptoServiceProvider().ComputeHash(tmpSource);
            TitleKeyHash = BitConverter.ToString(tmpHash);
            if (TitleKeyHash == "F9-4B-D8-8E-BB-7A-A9-38-67-E6-30-61-5F-27-1C-9F")
            {
                TitleKeyGood = true;
                TitleKey.ReadOnly = true;
                TitleKey.BackColor = Color.Lime;
            }
            else
            {
                TitleKeyGood = false;
                TitleKey.ReadOnly = false;
                TitleKey.BackColor = Color.White;
            }
            AncastKey.Text = AncastKey.Text.ToUpper();
            sSourceData = AncastKey.Text;
            tmpSource = ASCIIEncoding.ASCII.GetBytes(sSourceData);
            tmpHash = new MD5CryptoServiceProvider().ComputeHash(tmpSource);
            AncastKeyHash = BitConverter.ToString(tmpHash);
            if (AncastKeyHash == "31-8D-1F-9D-98-FB-08-E7-7C-7F-E1-77-AA-49-05-43")
            {
                AncastKeyGood = true;
            }
            else
            {
                AncastKeyGood = false;
            }
            //Final check for if all requirements are good
            if (FlagGameSpecified & FlagIconSpecified & FlagBannerSpecified)
            {
                SourceCheck.ForeColor = Color.Green;
                BuildFlagSource = true;
            }
            else
            {
                SourceCheck.ForeColor = Color.Red;
                BuildFlagSource = false;
            }
            if (PackedTitleLine1.Text != "" & PackedTitleIDLine.TextLength == 16)
            {
                MetaCheck.ForeColor = Color.Green;
                BuildFlagMeta = true;
            }
            else
            {
                MetaCheck.ForeColor = Color.Red;
                BuildFlagMeta = false;
            }

            if (CustomMainDol.Checked == false)
            {
                AdvanceCheck.ForeColor = Color.Green;
                BuildFlagAdvance = true;
            }
            else
            {
                if (Path.GetExtension(OpenMainDol.FileName) == ".dol")
                {
                    AdvanceCheck.ForeColor = Color.Green;
                    BuildFlagAdvance = true;
                }
                else
                {
                    AdvanceCheck.ForeColor = Color.Red;
                    BuildFlagAdvance = false;
                }
            }

            //Skip Ancast Key if box not checked in advanced
            if (C2WPatchFlag.Checked == false)
            {
                if (CommonKeyGood & TitleKeyGood)
                {
                    KeysCheck.ForeColor = Color.Green;
                    BuildFlagKeys = true;
                }
                else
                {
                    KeysCheck.ForeColor = Color.Red;
                    BuildFlagKeys = false;
                }
            }
            else
            {
                if (CommonKeyGood & TitleKeyGood & AncastKeyGood)
                {
                    KeysCheck.ForeColor = Color.Green;
                    BuildFlagKeys = true;
                }
                else
                {
                    KeysCheck.ForeColor = Color.Red;
                    BuildFlagKeys = false;
                }
            }
            //Enable Build Button
            if (BuildFlagSource & BuildFlagMeta & BuildFlagAdvance & BuildFlagKeys)
            {
                TheBigOneTM.Enabled = true;
            }
            else
            {
                TheBigOneTM.Enabled = false;
            }
        }

        //Performs actions when switching tabs
        private void MainTabs_SelectedIndexChanged(object sender, EventArgs e)
        {
            //Disables Radio buttons when switching away from the main tab
            if (!IsBuilding)
            {
                EnableSystemSelection(MainTabs.SelectedTab == SourceFilesTab);
            }

            //Check for building requirements when switching to the Build tab
            if (MainTabs.SelectedTab == BuildTab)
            {
                CheckBuildRequirements();
            }
        }

        private bool SelectGameSource(string gameFilePath, bool silent)
        {
            OpenGame.FileName = gameFilePath;

            if (String.IsNullOrEmpty(gameFilePath))
            {
                GameSourceDirectory.Text = tr.Tr("Game file has not been specified");
                GameSourceDirectory.ForeColor = Color.Red;
                FlagGameSpecified = false;
                GameNameLabel.Text = "";
                TitleIDLabel.Text = "";
                TitleIDInt = 0;
                TitleIDHex = "";
                GameType = 0;
                CucholixRepoID = "";
                PackedTitleLine1.Text = "";
                PackedTitleIDLine.Text = "";
            }
            else
            {
                GameSourceDirectory.Text = gameFilePath;
                GameSourceDirectory.ForeColor = Color.Black;
                FlagGameSpecified = true;
                //Get values from game file
                using (var reader = new BinaryReader(File.OpenRead(gameFilePath)))
                {
                    reader.BaseStream.Position = 0x00;
                    TitleIDInt = reader.ReadInt32();
                    //WBFS Check
                    if (TitleIDInt == 1397113431 /*'SFBW'*/) //Performs actions if the header indicates a WBFS file
                    {
                        FlagWBFS = true;
                        reader.BaseStream.Position = 0x200;
                        TitleIDInt = reader.ReadInt32();
                        reader.BaseStream.Position = 0x218;
                        GameType = reader.ReadInt64();
                        InternalGameName = StringEx.ReadStringFromBinaryStream(reader, 0x220);
                        CucholixRepoID = StringEx.ReadStringFromBinaryStream(reader, 0x200);
                    }
                    else
                    {
                        if (TitleIDInt == 65536) //Performs actions if the header indicates a DOL file
                        {
                            reader.BaseStream.Position = 0x2A0;
                            TitleIDInt = reader.ReadInt32();
                            InternalGameName = tr.Tr("N/A");
                        }
                        else //Performs actions if the header indicates a normal Wii / GC iso
                        {
                            FlagWBFS = false;
                            reader.BaseStream.Position = 0x18;
                            GameType = reader.ReadInt64();
                            InternalGameName = StringEx.ReadStringFromBinaryStream(reader, 0x20);
                            CucholixRepoID = StringEx.ReadStringFromBinaryStream(reader, 0x00);
                        }
                    }
                }
                //Flag if GameType Int doesn't match current SystemType
                if (SystemType == "wii" && GameType != 2745048157)
                {
                    GameSourceDirectory.Text = tr.Tr("Game file has not been specified");
                    GameSourceDirectory.ForeColor = Color.Red;
                    FlagGameSpecified = false;
                    GameNameLabel.Text = "";
                    TitleIDLabel.Text = "";
                    TitleIDInt = 0;
                    TitleIDHex = "";
                    GameType = 0;
                    CucholixRepoID = "";
                    PackedTitleLine1.Text = "";
                    PackedTitleIDLine.Text = "";
                    if (!silent)
                    {
                        MessageBox.Show(tr.Tr("This is not a Wii image. It will not be loaded."));
                    }
                    return false;
                }
                if (SystemType == "gcn" && GameType != 4440324665927270400)
                {
                    GameSourceDirectory.Text = tr.Tr("Game file has not been specified");
                    GameSourceDirectory.ForeColor = Color.Red;
                    FlagGameSpecified = false;
                    GameNameLabel.Text = "";
                    TitleIDLabel.Text = "";
                    TitleIDInt = 0;
                    TitleIDHex = "";
                    GameType = 0;
                    CucholixRepoID = "";
                    PackedTitleLine1.Text = "";
                    PackedTitleIDLine.Text = "";
                    if (!silent)
                    {
                        MessageBox.Show(tr.Tr("This is not a GameCube image. It will not be loaded."));
                    }
                    return false;
                }

                // Setup game name labels.
                GameNameLabel.Text = InternalGameName;

                // Try to convert simplified Chinese string to traditional Chinese string
                // which will avoid probably missing chars at wii consolo.
                if (!StringEx.IsGB2312EncodingArray(InternalGameName.OfType<byte>().ToArray()))
                {
                    PackedTitleLine1.Text = InternalGameName;
                }
                else
                {
                    PackedTitleLine1.Text = Microsoft.VisualBasic.Strings.StrConv(
                        InternalGameName, Microsoft.VisualBasic.VbStrConv.TraditionalChinese, 0);
                }

                //Convert pulled Title ID Int to Hex for use with Wii U Title ID
                TitleIDHex = TitleIDInt.ToString("X");
                TitleIDHex = TitleIDHex.Substring(6, 2) + TitleIDHex.Substring(4, 2) + TitleIDHex.Substring(2, 2) + TitleIDHex.Substring(0, 2);
                if (SystemType == "dol")
                {
                    TitleIDLabel.Text = TitleIDHex;
                    PackedTitleIDLine.Text = ("00050002" + TitleIDHex);
                    TitleIDText = "BOOT";
                }
                else
                {
                    TitleIDText = string.Join("", System.Text.RegularExpressions.Regex.Split(TitleIDHex, "(?<=\\G..)(?!$)").Select(x => (char)Convert.ToByte(x, 16)));
                    TitleIDLabel.Text = (TitleIDText + " / " + TitleIDHex);
                    PackedTitleIDLine.Text = ("00050002" + TitleIDHex);
                }
            }

            return true;
        }

        //Events for the "Required Source Files" Tab
        private void GameSourceButton_Click(object sender, EventArgs e)
        {
            if (OpenGame.ShowDialog() == DialogResult.OK)
            {
                SelectGameSource(OpenGame.FileName, false);
            }
            else
            {
                SelectGameSource(null, false);
            }
        }

        private void SelectIconSource(string filePath)
        {
            if (String.IsNullOrEmpty(filePath))
            {
                IconPreviewBox.Image = null;
                IconSourceDirectory.Text = tr.Tr("Icon has not been specified");
                IconSourceDirectory.ForeColor = Color.Red;
                FlagIconSpecified = false;
                FlagRepo = false;
                pngtemppath = "";
            }
            else
            {
                if (Path.GetExtension(filePath) == ".tga")
                {
                    pngtemppath = Path.GetTempPath() + "WiiVCInjector\\SOURCETEMP\\iconTex.png";
                    if (File.Exists(pngtemppath)) { File.Delete(pngtemppath); }
                    LauncherExeFile = TempToolsPath + "IMG\\tga2pngcmd.exe";
                    LauncherExeArgs = "-i \"" + filePath + "\" -o \"" + Path.GetDirectoryName(pngtemppath) + "\"";
                    LaunchProgram();
                    File.Move(Path.GetDirectoryName(pngtemppath) + "\\" 
                        + Path.GetFileNameWithoutExtension(filePath) + ".png", 
                        pngtemppath);
                }
                else
                {
                    pngtemppath = Path.GetTempPath() + "WiiVCInjector\\SOURCETEMP\\iconTex.png";
                    if (File.Exists(pngtemppath)) { File.Delete(pngtemppath); }
                    Image.FromFile(filePath).Save(pngtemppath, System.Drawing.Imaging.ImageFormat.Png);
                }
                FileStream tempstream = new FileStream(pngtemppath, FileMode.Open);
                var tempimage = Image.FromStream(tempstream);
                IconPreviewBox.Image = tempimage;
                tempstream.Close();
                IconSourceDirectory.Text = filePath;
                IconSourceDirectory.ForeColor = Color.Black;
                FlagIconSpecified = true;
                FlagRepo = false;
            }
        }

        private void IconSourceButton_Click(object sender, EventArgs e)
        {
            if (FlagRepo)
            {
                IconPreviewBox.Image = null;
                BannerPreviewBox.Image = null;
                FlagIconSpecified = false;
                FlagBannerSpecified = false;
                FlagRepo = false;
                pngtemppath = "";
            }
            MessageBox.Show(tr.Tr("Make sure your icon is 128x128 (1:1) to prevent distortion"));
            if (OpenIcon.ShowDialog() == DialogResult.OK)
            {
                SelectIconSource(OpenIcon.FileName);
            }
            else
            {
                SelectIconSource(null);
            }
        }

        private void SelectBannerSource(string filePath)
        {
            if (String.IsNullOrEmpty(filePath))
            {
                BannerPreviewBox.Image = null;
                BannerSourceDirectory.Text = tr.Tr("Banner has not been specified");
                BannerSourceDirectory.ForeColor = Color.Red;
                FlagBannerSpecified = false;
                FlagRepo = false;
                pngtemppath = "";
            }
            else
            {
                if (Path.GetExtension(filePath) == ".tga")
                {
                    pngtemppath = Path.GetTempPath() + "WiiVCInjector\\SOURCETEMP\\bootTvTex.png";
                    if (File.Exists(pngtemppath)) { File.Delete(pngtemppath); }
                    LauncherExeFile = TempToolsPath + "IMG\\tga2pngcmd.exe";
                    LauncherExeArgs = "-i \"" + filePath + "\" -o \"" + Path.GetDirectoryName(pngtemppath) + "\"";
                    LaunchProgram();
                    File.Move(Path.GetDirectoryName(pngtemppath) + "\\" + Path.GetFileNameWithoutExtension(filePath) + ".png", pngtemppath);
                }
                else
                {
                    pngtemppath = Path.GetTempPath() + "WiiVCInjector\\SOURCETEMP\\bootTvTex.png";
                    if (File.Exists(pngtemppath)) { File.Delete(pngtemppath); }
                    Image.FromFile(filePath).Save(pngtemppath, System.Drawing.Imaging.ImageFormat.Png);
                }
                FileStream tempstream = new FileStream(pngtemppath, FileMode.Open);
                var tempimage = Image.FromStream(tempstream);
                BannerPreviewBox.Image = tempimage;
                tempstream.Close();
                BannerSourceDirectory.Text = filePath;
                BannerSourceDirectory.ForeColor = Color.Black;
                FlagBannerSpecified = true;
                FlagRepo = false;
            }
        }

        private void BannerSourceButton_Click(object sender, EventArgs e)
        {
            if (FlagRepo)
            {
                BannerPreviewBox.Image = null;
                BannerPreviewBox.Image = null;
                FlagBannerSpecified = false;
                FlagBannerSpecified = false;
                FlagRepo = false;
                pngtemppath = "";
            }
            MessageBox.Show(tr.Tr("Make sure your Banner is 1280x720 (16:9) to prevent distortion"));
            if (OpenBanner.ShowDialog() == DialogResult.OK)
            {
                SelectBannerSource(OpenBanner.FileName);
            }
            else
            {
                SelectBannerSource(null);
            }
        }
        private void RepoDownload_Click(object sender, EventArgs e)
        {
            if (String.IsNullOrWhiteSpace(CucholixRepoID))
            {
                MessageBox.Show(tr.Tr("Please select your game before using this option"));
                FlagRepo = false;
                return;
            }

            string AreaID = CucholixRepoID.Substring(0, 3);
            string SubID = CucholixRepoID.Substring(4, 2);

            string[] RepoId =
            {
                CucholixRepoID,
                AreaID + "E",
                AreaID + "P",
                AreaID + "J",
            };

            if (SystemType != "wiiware")
            {
                for (int i = 1; i < RepoId.Length; ++i)
                {
                    RepoId[i] += SubID;
                }
            }

            for (int i = 0; i < RepoId.Length; ++i)
            {
                string Url;

                Url = "https://raw.githubusercontent.com/cucholix/wiivc-bis/master/";
                Url += SystemType + "/image/" + RepoId[i] + "/iconTex.png";

                try
                {
                    if (RemoteFileExists(Url))
                    {
                        CucholixRepoID = RepoId[i];
                        DownloadFromRepo();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    bool NotFound = false;

                    if (ex is WebException)
                    {
                        WebException wex = ex as WebException;
                        HttpWebResponse response = wex.Response as HttpWebResponse;
                        if (response != null && response.StatusCode == HttpStatusCode.NotFound)
                        {
                            NotFound = true;
                        }
                    }
                    
                    if (!NotFound)
                    {
                        FlagRepo = false;
                        MessageBox.Show(
                            tr.Tr("Failed to connect to Cucholix's Repo."),
                            tr.Tr("Download Error"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return;
                    }
                }
            }

            FlagRepo = false;
            if (MessageBox.Show(
                tr.Tr("Cucholix's Repo does not have assets for your game. You will need to provide your own. Would you like to visit the GBAtemp request thread?"),
                tr.Tr("Game not found on Repo"), 
                MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk) 
                == DialogResult.Yes)
            {
                System.Diagnostics.Process.Start("https://gbatemp.net/threads/483080/");
            }
        }
        

        //Events for the "Optional Source Files" Tab
        private void GC2SourceButton_Click(object sender, EventArgs e)
        {
            if (OpenGC2.ShowDialog() == DialogResult.OK)
            {
                using (var reader = new BinaryReader(File.OpenRead(OpenGC2.FileName)))
                {
                    reader.BaseStream.Position = 0x18;
                    long GC2GameType = reader.ReadInt64();
                    if (GC2GameType != 4440324665927270400)
                    {
                        MessageBox.Show(tr.Tr("This is not a GameCube image. It will not be loaded."));
                        GC2SourceDirectory.Text = tr.Tr("2nd GameCube Disc Image has not been specified");
                        GC2SourceDirectory.ForeColor = Color.Red;
                        FlagGC2Specified = false;
                    }
                    else
                    {
                        GC2SourceDirectory.Text = OpenGC2.FileName;
                        GC2SourceDirectory.ForeColor = Color.Black;
                        FlagGC2Specified = true;
                    }
                }
            }
            else
            {
                GC2SourceDirectory.Text = tr.Tr("2nd GameCube Disc Image has not been specified");
                GC2SourceDirectory.ForeColor = Color.Red;
                FlagGC2Specified = false;
            }
        }

        private void SelectDrcSource(string filePath)
        {
            if (String.IsNullOrEmpty(filePath))
            {
                DrcPreviewBox.Image = null;
                DrcSourceDirectory.Text = tr.Tr("GamePad Banner has not been specified");
                DrcSourceDirectory.ForeColor = Color.Red;
                pngtemppath = "";
            }
            else
            {
                if (Path.GetExtension(filePath) == ".tga")
                {
                    pngtemppath = Path.GetTempPath() + "WiiVCInjector\\SOURCETEMP\\bootDrcTex.png";
                    if (File.Exists(pngtemppath)) { File.Delete(pngtemppath); }
                    LauncherExeFile = TempToolsPath + "IMG\\tga2pngcmd.exe";
                    LauncherExeArgs = "-i \"" + filePath + "\" -o \"" + Path.GetDirectoryName(pngtemppath) + "\"";
                    LaunchProgram();
                    File.Move(Path.GetDirectoryName(pngtemppath) + "\\" + Path.GetFileNameWithoutExtension(filePath) + ".png", pngtemppath);
                }
                else
                {
                    pngtemppath = Path.GetTempPath() + "WiiVCInjector\\SOURCETEMP\\bootDrcTex.png";
                    if (File.Exists(pngtemppath)) { File.Delete(pngtemppath); }
                    Image.FromFile(filePath).Save(pngtemppath, System.Drawing.Imaging.ImageFormat.Png);
                }
                FileStream tempstream = new FileStream(pngtemppath, FileMode.Open);
                var tempimage = Image.FromStream(tempstream);
                DrcPreviewBox.Image = tempimage;
                tempstream.Close();
                DrcSourceDirectory.Text = filePath;
                DrcSourceDirectory.ForeColor = Color.Black;
                FlagDrcSpecified = true;
                FlagRepo = false;
            }
        }

        private void DrcSourceButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show(tr.Tr("Make sure your GamePad Banner is 854x480 (16:9) to prevent distortion"));
            if (OpenDrc.ShowDialog() == DialogResult.OK)
            {
                SelectDrcSource(OpenDrc.FileName);
            }
            else
            {
                SelectDrcSource(null);
            }
        }

        private void SelectLogoSource(string filePath)
        {
            if (String.IsNullOrEmpty(filePath))
            {
                LogoPreviewBox.Image = null;
                LogoSourceDirectory.Text = tr.Tr("GamePad Banner has not been specified");
                LogoSourceDirectory.ForeColor = Color.Red;
                pngtemppath = "";
            }
            else
            {
                if (Path.GetExtension(filePath) == ".tga")
                {
                    pngtemppath = Path.GetTempPath() + "WiiVCInjector\\SOURCETEMP\\bootLogoTex.png";
                    if (File.Exists(pngtemppath)) { File.Delete(pngtemppath); }
                    LauncherExeFile = TempToolsPath + "IMG\\tga2pngcmd.exe";
                    LauncherExeArgs = "-i \"" + filePath + "\" -o \"" + Path.GetDirectoryName(pngtemppath) + "\"";
                    LaunchProgram();
                    File.Move(Path.GetDirectoryName(pngtemppath) + "\\" + Path.GetFileNameWithoutExtension(filePath) + ".png", pngtemppath);
                }
                else
                {
                    pngtemppath = Path.GetTempPath() + "WiiVCInjector\\SOURCETEMP\\bootLogoTex.png";
                    if (File.Exists(pngtemppath)) { File.Delete(pngtemppath); }
                    Image.FromFile(filePath).Save(pngtemppath, System.Drawing.Imaging.ImageFormat.Png);
                }
                FileStream tempstream = new FileStream(pngtemppath, FileMode.Open);
                var tempimage = Image.FromStream(tempstream);
                LogoPreviewBox.Image = tempimage;
                tempstream.Close();
                LogoSourceDirectory.Text = filePath;
                LogoSourceDirectory.ForeColor = Color.Black;
                FlagLogoSpecified = true;
                FlagRepo = false;
            }
        }

        private void LogoSourceButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show(tr.Tr("Make sure your Logo is 170x42 to prevent distortion"));
            if (OpenLogo.ShowDialog() == DialogResult.OK)
            {
                SelectLogoSource(OpenLogo.FileName);
            }
            else
            {
                SelectLogoSource(null);
            }
        }
        private void BootSoundButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show(tr.Tr("Your sound file will be cut off if it's longer than 6 seconds to prevent the Wii U from not loading it. When the Wii U plays the boot sound, it will fade out once it's done loading the game (usually after about 5 seconds). You can not change this."));
            if (OpenBootSound.ShowDialog() == DialogResult.OK)
            {
                using (var reader = new BinaryReader(File.OpenRead(OpenBootSound.FileName)))
                {
                    reader.BaseStream.Position = 0x00;
                    long WAVHeader1 = reader.ReadInt32();
                    reader.BaseStream.Position = 0x08;
                    long WAVHeader2 = reader.ReadInt32();
                    if (WAVHeader1 == 1179011410 & WAVHeader2 == 1163280727)
                    {
                        BootSoundDirectory.Text = OpenBootSound.FileName;
                        BootSoundDirectory.ForeColor = Color.Black;
                        BootSoundPreviewButton.Enabled = true;
                        FlagBootSoundSpecified = true;
                    }
                    else
                    {
                        MessageBox.Show(tr.Tr("This is not a valid WAV file. It will not be loaded. \nConsider converting it with something like Audacity."));
                        BootSoundDirectory.Text = tr.Tr("Boot Sound has not been specified");
                        BootSoundDirectory.ForeColor = Color.Red;
                        BootSoundPreviewButton.Enabled = false;
                        FlagBootSoundSpecified = false;
                    }
                }
            }
            else
            {
                if (BootSoundPreviewButton.Text != tr.Tr("Stop Sound"))
                {
                    BootSoundDirectory.Text = tr.Tr("Boot Sound has not been specified");
                    BootSoundDirectory.ForeColor = Color.Red;
                    BootSoundPreviewButton.Enabled = false;
                    FlagBootSoundSpecified = false;
                }
            }
        }
        private void ToggleBootSoundLoop_CheckedChanged(object sender, EventArgs e)
        {
            if (ToggleBootSoundLoop.Checked)
            {
                LoopString = "";
            }
            else
            {
                LoopString = " -noLoop";
            }
        }
        private void BootSoundPreviewButton_Click(object sender, EventArgs e)
        {
            var simpleSound = new SoundPlayer(OpenBootSound.FileName);
            if (BootSoundPreviewButton.Text == tr.Tr("Stop Sound"))
            {
                simpleSound.Stop();
                BootSoundPreviewButton.Text = tr.Tr("Play Sound");
            }
            else
            {
                if (ToggleBootSoundLoop.Checked)
                {
                    simpleSound.PlayLooping();
                    BootSoundPreviewButton.Text = tr.Tr("Stop Sound");
                }
                else
                {
                    simpleSound.Play();
                }
            }
        }

        //Events for the "GamePad/Meta Options" Tab
        private void EnablePackedLine2_CheckedChanged(object sender, EventArgs e)
        {
            if (EnablePackedLine2.Checked)
            {
                PackedTitleLine2.Text = "";
                PackedTitleLine2.BackColor = Color.White;
                PackedTitleLine2.ReadOnly = false;
            }
            else
            {
                PackedTitleLine2.Text = tr.Tr("(Optional) Line 2");
                PackedTitleLine2.BackColor = Color.Silver;
                PackedTitleLine2.ReadOnly = true;
            }

        }
        //Radio Buttons for GamePad Emulation Mode
        private void NoGamePadEmu_CheckedChanged(object sender, EventArgs e)
        {
            if (NoGamePadEmu.Checked)
            {
                DRCUSE = "1";
                nfspatchflag = "";
                LRPatch.Checked = false;
                LRPatch.Enabled = false;
            }
        }
        private void CCEmu_CheckedChanged(object sender, EventArgs e)
        {
            if (CCEmu.Checked)
            {
                DRCUSE = "65537";
                nfspatchflag = "";
                LRPatch.Enabled = true;
            }
        }
        private void HorWiiMote_CheckedChanged(object sender, EventArgs e)
        {
            if (HorWiiMote.Checked)
            {
                DRCUSE = "65537";
                nfspatchflag = " -horizontal";
                LRPatch.Checked = false;
                LRPatch.Enabled = false;
            }
        }
        private void VerWiiMote_CheckedChanged(object sender, EventArgs e)
        {
            if (VerWiiMote.Checked)
            {
                DRCUSE = "65537";
                nfspatchflag = " -wiimote";
                LRPatch.Checked = false;
                LRPatch.Enabled = false;
            }
        }
        private void ForceCC_CheckedChanged(object sender, EventArgs e)
        {
            if (ForceCC.Checked)
            {
                DRCUSE = "65537";
                nfspatchflag = " -instantcc";
                DisableTrimming.Checked = false;
                DisableTrimming.Enabled = false;
                LRPatch.Enabled = true;
            }
        }
        private void ForceWiiMote_CheckedChanged(object sender, EventArgs e)
        {
            if (ForceNoCC.Checked)
            {
                DRCUSE = "65537";
                nfspatchflag = " -nocc";
                LRPatch.Checked = false;
                LRPatch.Enabled = false;
            }
        }
        private void TutorialLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://www.google.com");
        }

        //Events for the Advanced Tab
        private void Force43NINTENDONT_CheckedChanged(object sender, EventArgs e)
        {
            if (Force43NINTENDONT.Checked)
            {
                CustomMainDol.Checked = false;
                CustomMainDol.Enabled = false;
                DisableNintendontAutoboot.Checked = false;
                DisableNintendontAutoboot.Enabled = false;
            }
            else
            {
                CustomMainDol.Enabled = true;
                DisableNintendontAutoboot.Enabled = true;
            }
        }
        private void CustomMainDol_CheckedChanged(object sender, EventArgs e)
        {
            if (CustomMainDol.Checked)
            {
                MainDolSourceButton.Enabled = true;
                Force43NINTENDONT.Checked = false;
                Force43NINTENDONT.Enabled = false;
                DisableNintendontAutoboot.Checked = false;
                DisableNintendontAutoboot.Enabled = false;

            }
            else
            {
                MainDolSourceButton.Enabled = false;
                MainDolLabel.Text = tr.Tr("<- Specify custom main.dol file");
                Force43NINTENDONT.Enabled = true;
                DisableNintendontAutoboot.Enabled = true;
                OpenMainDol.FileName = null;
            }
        }
        private void NintendontAutoboot_CheckedChanged(object sender, EventArgs e)
        {
            if (DisableNintendontAutoboot.Checked)
            {
                Force43NINTENDONT.Checked = false;
                Force43NINTENDONT.Enabled = false;
                CustomMainDol.Checked = false;
                CustomMainDol.Enabled = false;
            }
            else
            {
                Force43NINTENDONT.Enabled = true;
                CustomMainDol.Enabled = true;
            }
        }
        private void MainDolSourceButton_Click(object sender, EventArgs e)
        {
            if (OpenMainDol.ShowDialog() == DialogResult.OK)
            {
                MainDolLabel.Text = OpenMainDol.FileName;
            }
            else
            {
                MainDolLabel.Text = tr.Tr("<- Specify custom main.dol file");
            }
        }
        private void DisablePassthrough_CheckedChanged(object sender, EventArgs e)
        {
            if (DisablePassthrough.Checked)
            {
                passpatch = "";
            }
            else
            {
                passpatch = " -passthrough";
            }
        }
        private void DisableGamePad_CheckedChanged(object sender, EventArgs e)
        {
            if (DisableGamePad.Checked)
            {
                if (SystemType == "gcn")
                {
                    DRCUSE = "1";
                }
                else if (SystemType == "dol")
                {
                    DRCUSE = "1";
                }
            }
            else
            {
                if (SystemType == "gcn")
                {
                    DRCUSE = "65537";
                }
                else if (SystemType == "dol")
                {
                    DRCUSE = "65537";
                }
            }
        }
        private void C2WPatchFlag_CheckedChanged(object sender, EventArgs e)
        {
            if (C2WPatchFlag.Checked)
            {
                AncastKey.ReadOnly = false;
                AncastKey.BackColor = Color.White;
                SaveAncastKeyButton.Enabled = true;
                if (Registry.CurrentUser.CreateSubKey("WiiVCInjector").GetValue("AncastKey") == null)
                {
                    Registry.CurrentUser.CreateSubKey("WiiVCInjector").SetValue("AncastKey", "00000000000000000000000000000000");
                }
                AncastKey.Text = Registry.CurrentUser.OpenSubKey("WiiVCInjector").GetValue("AncastKey").ToString();
                Registry.CurrentUser.OpenSubKey("WiiVCInjector").Close();
                //If key is correct, lock text box for edits
                AncastKey.Text = AncastKey.Text.ToUpper();
                sSourceData = AncastKey.Text;
                tmpSource = ASCIIEncoding.ASCII.GetBytes(sSourceData);
                tmpHash = new MD5CryptoServiceProvider().ComputeHash(tmpSource);
                AncastKeyHash = BitConverter.ToString(tmpHash);
                if (AncastKeyHash == "31-8D-1F-9D-98-FB-08-E7-7C-7F-E1-77-AA-49-05-43")
                {
                    AncastKey.ReadOnly = true;
                    AncastKey.BackColor = Color.Lime;
                }
                else
                {
                    AncastKey.ReadOnly = false;
                    AncastKey.BackColor = Color.White;
                }
            }
            else
            {
                AncastKey.BackColor = Color.Silver;
                AncastKey.ReadOnly = true;
                SaveAncastKeyButton.Enabled = false;
            }
        }
        private void SaveAncastKeyButton_Click(object sender, EventArgs e)
        {
            //Verify Title Key MD5 Hash
            AncastKey.Text = AncastKey.Text.ToUpper();
            sSourceData = AncastKey.Text;
            tmpSource = ASCIIEncoding.ASCII.GetBytes(sSourceData);
            tmpHash = new MD5CryptoServiceProvider().ComputeHash(tmpSource);
            AncastKeyHash = BitConverter.ToString(tmpHash);
            if (AncastKeyHash == "31-8D-1F-9D-98-FB-08-E7-7C-7F-E1-77-AA-49-05-43")
            {
                Registry.CurrentUser.CreateSubKey("WiiVCInjector").SetValue("AncastKey", AncastKey.Text);
                Registry.CurrentUser.CreateSubKey("WiiVCInjector").Close();
                MessageBox.Show(tr.Tr("The Wii U Starbuck Ancast Key has been verified."));
                AncastKey.ReadOnly = true;
                AncastKey.BackColor = Color.Lime;
            }
            else
            {
                MessageBox.Show(tr.Tr("The Wii U Starbuck Ancast Key you have provided is incorrect" + "\n" + "(MD5 Hash verification failed)"));
            }
        }
        private void sign_c2w_patcher_link_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://github.com/FIX94/sign_c2w_patcher");
        }
        private void DisableTrimming_CheckedChanged(object sender, EventArgs e)
        {
            if (DisableTrimming.Checked)
            {
                WiiVMC.Checked = false;
                WiiVMC.Enabled = false;
            }
            else
            {
                if (SystemType == "wii")
                {
                    WiiVMC.Enabled = true;
                }
                else
                {
                    WiiVMC.Checked = false;
                    WiiVMC.Enabled = false;
                }
            }
        }

        //Events for the "Build Title" Tab
        private void SaveCommonKeyButton_Click(object sender, EventArgs e)
        {
            //Verify Wii U Common Key MD5 Hash
            WiiUCommonKey.Text = WiiUCommonKey.Text.ToUpper();
            sSourceData = WiiUCommonKey.Text;
            tmpSource = ASCIIEncoding.ASCII.GetBytes(sSourceData);
            tmpHash = new MD5CryptoServiceProvider().ComputeHash(tmpSource);
            WiiUCommonKeyHash = BitConverter.ToString(tmpHash);
            if (WiiUCommonKeyHash == "35-AC-59-94-97-22-79-33-1D-97-09-4F-A2-FB-97-FC")
            {
                Registry.CurrentUser.CreateSubKey("WiiVCInjector").SetValue("WiiUCommonKey", WiiUCommonKey.Text);
                MessageBox.Show(tr.Tr("The Wii U Common Key has been verified."));
                MainTabs.SelectedTab = AdvancedTab;
                MainTabs.SelectedTab = BuildTab;
            }
            else
            {
                MessageBox.Show(tr.Tr("The Wii U Common Key you have provided is incorrect" + "\n" + "(MD5 Hash verification failed)"));
            }
        }
        private void SaveTitleKeyButton_Click(object sender, EventArgs e)
        {
            //Verify Title Key MD5 Hash
            TitleKey.Text = TitleKey.Text.ToUpper();
            sSourceData = TitleKey.Text;
            tmpSource = ASCIIEncoding.ASCII.GetBytes(sSourceData);
            tmpHash = new MD5CryptoServiceProvider().ComputeHash(tmpSource);
            TitleKeyHash = BitConverter.ToString(tmpHash);
            if (TitleKeyHash == "F9-4B-D8-8E-BB-7A-A9-38-67-E6-30-61-5F-27-1C-9F")
            {
                Registry.CurrentUser.CreateSubKey("WiiVCInjector").SetValue("TitleKey", TitleKey.Text);
                MessageBox.Show(tr.Tr("The Title Key has been verified."));
                MainTabs.SelectedTab = AdvancedTab;
                MainTabs.SelectedTab = BuildTab;
            }
            else
            {
                MessageBox.Show(tr.Tr("The Title Key you have provided is incorrect" + "\n" + "(MD5 Hash verification failed)"));
            }
        }
        private void DebugButton_Click(object sender, EventArgs e)
        {
            // MessageBox.Show(ShortenPath(OpenGame.FileName));
        }

        //Events for the actual "Build" Button
        private void TheBigOneTM_Click(object sender, EventArgs e)
        {
            ToggleBuild();
        }

        private void BuildThread()
        {
            bool buildSucceed = false;

            try
            {
                buildSucceed = BuildPack();
            }
            catch (Exception ex)
            {
                Console.Write("BuildPack throws an exception: " + ex.Message);
            }

            BeginInvoke(new Action<bool>((Succeed) => {
                this.BuildCompleted(Succeed);
            }), buildSucceed);
        }

        private void ToggleBuild()
        {
            if (IsBuilding)
            {
                DialogResult dialogResult = MessageBox.Show(
                    tr.Tr("Are you sure to cancel the current build progress?"),
                    tr.Tr("Stop building"),
                    MessageBoxButtons.YesNo);

                if (dialogResult == DialogResult.Yes)
                {
                    CancelBuild();
                }
            }
            else
            {
                BuildAnsync();
            }
        }

        private bool BuildAnsync()
        {
            //
            // Disable form elements so navigation can't be 
            // attempted during build process
            //
            FreezeFormBuild(true);
            TheBigOneTM.Text = tr.Tr("STOP");

            // 
            // Reset build status ui indicators.
            //
            BuildStatus.Text = "";
            BuildStatus.ForeColor = Color.Black;

            //
            // Reset build output.
            //
            BuildOutput.ResetText();

            //
            // Reset user cancellation flag.
            //
            LastBuildCancelled = false;

            //
            // Allocate a new builder thread.
            //
            BuilderThread = new Thread(new ThreadStart(this.BuildThread));

            //
            // Start stopwatch for building.
            //
            BuildStopwatch.Restart();

            try
            {
                BuilderThread.Start();
            }
            catch (Exception ex)
            {
                BuildCompleted(false);
                BuildStatus.Text = ex.Message;
                BuildStatus.ForeColor = Color.Red;
                return false;
            }           

            return true;
        }

        private void BuildCompleted(bool succeed)
        {
            BuildStopwatch.Stop();

            if (succeed && PropmtForSucceed && !InClosing)
            {
                MessageBox.Show(tr.Tr("Conversion Complete! Your packed game can be found here: ")
                    + OutputDirectory.Text + "\\WUP-N-" + TitleIDText + "_" + PackedTitleIDLine.Text
                    + tr.Tr(".\n\nInstall your title using WUP Installer GX2 with signature patches enabled (CBHC, Haxchi, etc). Make sure you have signature patches enabled when launching your title.\n\n Click OK to continue..."),
                    PackedTitleLine1.Text + tr.Tr(" Conversion Complete..."));
            }

            if (BuilderThread != null)
            {
                BuilderThread.Join();
                BuilderThread = null;
            }

            try
            {
                CleanupBuildSourceTemp();
            }
            catch (Exception ex)
            {
                Console.Write("CleanupBuildSourceTemp thorws an exception: " + ex.Message);
            }

            FreezeFormBuild(false);
            TheBigOneTM.Text = tr.Tr("BUILD");
            BuildStatus.Text = "";
            BuildProgress.Value = 0;

            if (BuildCompletedEx != null)
            {
                BuildCompletedEx(this, succeed);
            }

            if (!InClosing)
            {
                BuildOutputItem buildResult = new BuildOutputItem();
                buildResult.s = "\n";

                if (succeed)
                {
                    buildResult.s += tr.Tr("Build succeed.");
                    buildResult.buildOutputType = BuildOutputType.botSucceed;
                }
                else
                {
                    if (LastBuildCancelled)
                    {
                        buildResult.s += tr.Tr("Build cancelled.");
                        buildResult.buildOutputType = BuildOutputType.botError;
                    }
                    else
                    {
                        buildResult.s += tr.Tr("Build failed.");
                        buildResult.buildOutputType = BuildOutputType.botError;
                    }
                }

                
                buildResult.s += String.Format("({0})", TimeSpan.FromMilliseconds(BuildStopwatch.ElapsedMilliseconds).Duration().ToString());

                AppendBuildOutput(buildResult);
            }
        }

        private void CancelBuild()
        {
            TheBigOneTM.Enabled = false;
            LastBuildCancelled = true;
        }

        private bool PrepareTemporaryDirectory()
        {
            TemporaryDirectory.Text = TemporaryDirectory.Text.Trim();

            string tempDir = TemporaryDirectory.Text;

            if (String.IsNullOrWhiteSpace(tempDir))
            {
                tempDir = GetTempRootPath();
            }

            if (!tempDir.EndsWith("\\"))
            {
                tempDir += "\\";
            }

            string newTempRootPath = tempDir + "WiiVCInjector\\";
            if (MoveTempDir(TempRootPath, newTempRootPath))
            {
                TempRootPath = newTempRootPath;
                UpdateTempDirs();
                Registry.CurrentUser.CreateSubKey("WiiVCInjector")
                    .SetValue("TemporaryDirectory", tempDir);
                return true;
            }

            MessageBox.Show(
                tr.Tr("Create temporary directory failed, it may be caused by "
                + "low space on hard drive, permission denied or invalid path name."),
                tr.Tr("Error"));

            //
            // Restore the default temp dir location on failed.
            //
            TemporaryDirectory.Text = Path.GetTempPath();
            Registry.CurrentUser.CreateSubKey("WiiVCInjector")
                .DeleteValue("TemporaryDirectory");

            return false;
        }

        private bool CheckFreeDiskSpaceForPack()
        {
            //
            // Check for free space
            //
            Dictionary<string, long> requiredFreespace = new Dictionary<string, long>();
            long gamesize = new FileInfo(GameSourceDirectory.Text).Length;
            requiredFreespace.Add("wii", gamesize * 2 + 5000000000);
            requiredFreespace.Add("dol", 6000000000);
            requiredFreespace.Add("wiiware", 6000000000);
            requiredFreespace.Add("gcn", gamesize * 2 + 6000000000);

            var drive = new DriveInfo(TempRootPath);
            long freeSpaceInBytes = drive.AvailableFreeSpace;

            if (freeSpaceInBytes < requiredFreespace[SystemType])
            {
                DialogResult dialogResult = MessageBox.Show(
                    tr.Tr("Your hard drive may be low on space. The conversion process involves temporary files that can amount to more than double the size of your game. If you continue without clearing some hard drive space, the conversion may fail. Do you want to continue anyways?"),
                    tr.Tr("Check your hard drive space"), MessageBoxButtons.YesNo);
                if (dialogResult != DialogResult.Yes)
                {
                    return false;
                }
            }

            return true;
        }

        private bool PrepareOutputDirectory()
        {
            // Specify Path Variables to be called later
            OutputDirectory.Text = OutputDirectory.Text.Trim();
            if (String.IsNullOrEmpty(OutputDirectory.Text))
            {
                FolderBrowserDialog OutputFolderSelect = new FolderBrowserDialog();
                if (OutputFolderSelect.ShowDialog() == DialogResult.Cancel)
                {
                    MessageBox.Show(tr.Tr("Output folder selection has been cancelled, conversion will not continue."));
                    return false;
                }
                Invoke(new Action<string>((s) => { OutputDirectory.Text = s; }), OutputFolderSelect.SelectedPath);
            }

            if (!Directory.Exists(OutputDirectory.Text))
            {
                try
                {
                    Directory.CreateDirectory(OutputDirectory.Text);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        tr.Tr("Can't create the specified output directory, conversion will not continue.\nAdditional error information:")
                        + ex.Message);

                    Invoke(new Action(() => { OutputDirectory.Text = ""; }));
                    
                    Registry.CurrentUser.CreateSubKey("WiiVCInjector")
                        .DeleteValue("OutputDirectory");

                    return false;
                }
            }

            Registry.CurrentUser.CreateSubKey("WiiVCInjector")
                .SetValue("OutputDirectory", OutputDirectory.Text);
            return true;
        }

        private bool PrepareJNUSStuffs()
        {
            if (!VerfiyJNUSStuffs())
            {
                return DownloadJNUSStuffs();
            }

            return true;
        }

        private bool VerfiyJNUSStuffs()
        {
            //
            // Check if JNUSTool exists?
            //
            if (!Directory.Exists(JNUSToolDownloads))
            {
                return false;
            }

            //
            // Check required files. What's better? We can verify their SHA1 or MD5 btw.
            //
            string[] JNUSToolFiles =
            {
                "0005001010004000\\code\\deint.txt",
                "0005001010004000\\code\\font.bin",
                "0005001010004001\\code\\c2w.img",
                "0005001010004001\\code\\boot.bin",
                "0005001010004001\\code\\dmcu.d.hex",
                "Rhythm Heaven Fever [VAKE01]\\code\\cos.xml",
                "Rhythm Heaven Fever [VAKE01]\\code\\frisbiiU.rpx",
                "Rhythm Heaven Fever [VAKE01]\\code\\fw.img",
                "Rhythm Heaven Fever [VAKE01]\\code\\fw.tmd",
                "Rhythm Heaven Fever [VAKE01]\\code\\htk.bin",
                "Rhythm Heaven Fever [VAKE01]\\code\\nn_hai_user.rpl",
                "Rhythm Heaven Fever [VAKE01]\\content\\assets\\shaders\\cafe\\banner.gsh",
                "Rhythm Heaven Fever [VAKE01]\\content\\assets\\shaders\\cafe\\fade.gsh",
                "Rhythm Heaven Fever [VAKE01]\\meta\\bootMovie.h264",
                "Rhythm Heaven Fever [VAKE01]\\meta\\bootLogoTex.tga",
                "Rhythm Heaven Fever [VAKE01]\\meta\\bootSound.btsnd",
            };

            foreach (string file in JNUSToolFiles)
            {
                if (!File.Exists(JNUSToolDownloads + file))
                {
                    return false;
                }
            }

            return true;
        }

        private struct JNUSStuffsDownloadItem
        {
            public string buildStatus;
            public string exeArgs;
            public int progress;
        };

        private bool DownloadJNUSStuffs()
        {
            //Download base files with JNUSTool, store them for future use
            if (!CheckForInternetConnection())
            {
                DialogResult dialogResult = MessageBox.Show(
                    tr.Tr("Your internet connection could not be verified, do you wish to try and download the necessary base files from Nintendo anyways? (This is a one-time download)"),
                    tr.Tr("Internet Connection Verification Failed"), MessageBoxButtons.YesNo);
                if (dialogResult == DialogResult.No)
                {
                    return false;
                }
            }

            if (LastBuildCancelled)
            {
                return false;
            }

            Invoke(
                ActBuildStatus, 
                tr.Tr("(One-Time Download) Downloading base files from Nintendo..."));

            string[] JNUSToolConfig = { "http://ccs.cdn.wup.shop.nintendo.net/ccs/download", WiiUCommonKey.Text };
            File.WriteAllLines(TempToolsPath + "JAR\\config", JNUSToolConfig);
            Directory.SetCurrentDirectory(TempToolsPath + "JAR");

            Invoke(ActBuildProgress, 10);

            JNUSStuffsDownloadItem[] downloadItems = new JNUSStuffsDownloadItem[]{
                new JNUSStuffsDownloadItem {
                    buildStatus = tr.Tr("(One-Time Download) Downloading base files from Nintendo... (deint.txt)"),
                    exeArgs = "0005001010004000 -file /code/deint.txt",
                    progress = 12,
                },
                new JNUSStuffsDownloadItem {
                    buildStatus = tr.Tr("(One-Time Download) Downloading base files from Nintendo... (font.bin)"),
                    exeArgs = "0005001010004000 -file /code/font.bin",
                    progress = 15,
                },
                new JNUSStuffsDownloadItem {
                    buildStatus = tr.Tr("(One-Time Download) Downloading base files from Nintendo... (c2w.img)"),
                    exeArgs = "0005001010004001 -file /code/c2w.img",
                    progress = 17,
                },
                new JNUSStuffsDownloadItem {
                    buildStatus = tr.Tr("(One-Time Download) Downloading base files from Nintendo... (boot.bin)"),
                    exeArgs = "0005001010004001 -file /code/boot.bin",
                    progress = 20,
                },
                new JNUSStuffsDownloadItem {
                    buildStatus = tr.Tr("(One-Time Download) Downloading base files from Nintendo... (dmcu.d.hex)"),
                    exeArgs = "0005001010004001 -file /code/dmcu.d.hex",
                    progress = 23,
                },
                new JNUSStuffsDownloadItem {
                    buildStatus = tr.Tr("(One-Time Download) Downloading base files from Nintendo... (cos.xml)"),
                    exeArgs = "00050000101b0700 " + TitleKey.Text + " -file /code/cos.xml",
                    progress = 25,
                },
                new JNUSStuffsDownloadItem {
                    buildStatus = tr.Tr("(One-Time Download) Downloading base files from Nintendo... (frisbiiU.rpx)"),
                    exeArgs = "00050000101b0700 " + TitleKey.Text + " -file /code/frisbiiU.rpx",
                    progress = 27,
                },
                new JNUSStuffsDownloadItem {
                    buildStatus = tr.Tr("(One-Time Download) Downloading base files from Nintendo... (fw.img)"),
                    exeArgs = "00050000101b0700 " + TitleKey.Text + " -file /code/fw.img",
                    progress = 30,
                },
                new JNUSStuffsDownloadItem {
                    buildStatus = tr.Tr("(One-Time Download) Downloading base files from Nintendo... (fw.tmd)"),
                    exeArgs = "00050000101b0700 " + TitleKey.Text + " -file /code/fw.tmd",
                    progress = 32,
                },
                new JNUSStuffsDownloadItem {
                    buildStatus = tr.Tr("(One-Time Download) Downloading base files from Nintendo... (htk.bin)"),
                    exeArgs = "00050000101b0700 " + TitleKey.Text + " -file /code/htk.bin",
                    progress = 35,
                },
                new JNUSStuffsDownloadItem {
                    buildStatus = tr.Tr("(One-Time Download) Downloading base files from Nintendo... (nn_hai_user.rpl)"),
                    exeArgs = "00050000101b0700 " + TitleKey.Text + " -file /code/nn_hai_user.rpl",
                    progress = 37,
                },
                new JNUSStuffsDownloadItem {
                    buildStatus = tr.Tr("(One-Time Download) Downloading base files from Nintendo... (banner.gsh / fade.gsh)"),
                    exeArgs = "00050000101b0700 " + TitleKey.Text + " -file /content/assets/.*",
                    progress = 40,
                },
                new JNUSStuffsDownloadItem {
                    buildStatus = tr.Tr("(One-Time Download) Downloading base files from Nintendo... (bootMovie.h264)"),
                    exeArgs = "00050000101b0700 " + TitleKey.Text + " -file /meta/bootMovie.h264",
                    progress = 42,
                },
                new JNUSStuffsDownloadItem {
                    buildStatus = tr.Tr("(One-Time Download) Downloading base files from Nintendo... (bootLogoTex.tga)"),
                    exeArgs = "00050000101b0700 " + TitleKey.Text + " -file /meta/bootLogoTex.tga",
                    progress = 45,
                },
                new JNUSStuffsDownloadItem {
                    buildStatus = tr.Tr("(One-Time Download) Downloading base files from Nintendo... (bootSound.btsnd)"),
                    exeArgs = "00050000101b0700 " + TitleKey.Text + " -file /meta/bootSound.btsnd",
                    progress = 47,
                },
            };

            LauncherExeFile = "JNUSTool.exe";

            foreach (JNUSStuffsDownloadItem downloadItem in downloadItems)
            {
                Invoke(ActBuildStatus, downloadItem.buildStatus);
                LauncherExeArgs = downloadItem.exeArgs;

                if (LastBuildCancelled)
                {
                    break;
                }

                if (!LaunchProgram())
                {
                    break;
                }

                Invoke(ActBuildProgress, downloadItem.progress);
            }

            bool downloadCompleted = !LastBuildCancelled;

            if (downloadCompleted)
            {
                try
                {
                    Directory.CreateDirectory(JNUSToolDownloads + "Rhythm Heaven Fever [VAKE01]");
                    Directory.CreateDirectory(JNUSToolDownloads + "0005001010004000");
                    Directory.CreateDirectory(JNUSToolDownloads + "0005001010004001");
                    FileSystem.CopyDirectory("Rhythm Heaven Fever [VAKE01]", JNUSToolDownloads + "Rhythm Heaven Fever [VAKE01]");
                    FileSystem.CopyDirectory("0005001010004000", JNUSToolDownloads + "0005001010004000");
                    FileSystem.CopyDirectory("0005001010004001", JNUSToolDownloads + "0005001010004001");
                    Directory.Delete("Rhythm Heaven Fever [VAKE01]", true);
                    Directory.Delete("0005001010004000", true);
                    Directory.Delete("0005001010004001", true);
                    File.Delete("config");
                }
                catch (Exception)
                {

                }

                //Check if files exist after they were supposed to be downloaded
                downloadCompleted = VerfiyJNUSStuffs();
                if (!downloadCompleted)
                {
                    MessageBox.Show(tr.Tr("Failed to download base files using JNUSTool, conversion will not continue"));
                }
            }

            //
            // Cleanup all files and directories if downloading hasn't 
            // been completed for any reasons.
            //
            if (!downloadCompleted)
            {
                try
                {
                    Directory.Delete(JNUSToolDownloads, true);
                    Directory.Delete("Rhythm Heaven Fever [VAKE01]", true);
                    Directory.Delete("0005001010004000", true);
                    Directory.Delete("0005001010004001", true);
                    File.Delete("config");
                }
                catch (Exception)
                {

                }
            }

            return downloadCompleted;
        }

        private bool PrepareBasicFilesForPack()
        {
            //
            // Copy downloaded files to the build directory
            //
            Invoke(ActBuildStatus, tr.Tr("Copying base files to temporary build directory..."));

            Directory.SetCurrentDirectory(TempRootPath);
            FileSystem.CopyDirectory(JNUSToolDownloads + "Rhythm Heaven Fever [VAKE01]", TempBuildPath);

            if (C2WPatchFlag.Checked)
            {
                FileSystem.CopyDirectory(JNUSToolDownloads + "0005001010004000", TempBuildPath);
                FileSystem.CopyDirectory(JNUSToolDownloads + "0005001010004001", TempBuildPath);
                string[] AncastKeyCopy = { AncastKey.Text };
                File.WriteAllLines(TempToolsPath + "C2W\\starbuck_key.txt", AncastKeyCopy);
                File.Copy(TempBuildPath + "code\\c2w.img", TempToolsPath + "C2W\\c2w.img");
                Directory.SetCurrentDirectory(TempToolsPath + "C2W");
                LauncherExeFile = "c2w_patcher.exe";
                LauncherExeArgs = "-nc";
                LaunchProgram();
                File.Delete(TempBuildPath + "code\\c2w.img");
                File.Copy(TempToolsPath + "C2W\\c2p.img", TempBuildPath + "code\\c2w.img", true);
                File.Delete(TempToolsPath + "C2W\\c2p.img");
                File.Delete(TempToolsPath + "C2W\\c2w.img");
                File.Delete(TempToolsPath + "C2W\\starbuck_key.txt");
            }

            Invoke(ActBuildProgress, 50);

            return true;
        }

        private bool GeneratePackXmls()
        {
            //
            // Generate app.xml & meta.xml
            //
            Invoke(ActBuildStatus, tr.Tr("Generating app.xml and meta.xml"));

            string[] AppXML = {
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>",
                "<app type=\"complex\" access=\"777\">",
                "  <version type=\"unsignedInt\" length=\"4\">16</version>",
                "  <os_version type=\"hexBinary\" length=\"8\">000500101000400A</os_version>",
                "  <title_id type=\"hexBinary\" length=\"8\">" + PackedTitleIDLine.Text + "</title_id>",
                "  <title_version type=\"hexBinary\" length=\"2\">0000</title_version>",
                "  <sdk_version type=\"unsignedInt\" length=\"4\">21204</sdk_version>",
                "  <app_type type=\"hexBinary\" length=\"4\">8000002E</app_type>",
                "  <group_id type=\"hexBinary\" length=\"4\">" + TitleIDHex + "</group_id>",
                "  <os_mask type=\"hexBinary\" length=\"32\">0000000000000000000000000000000000000000000000000000000000000000</os_mask>",
                "  <common_id type=\"hexBinary\" length=\"8\">0000000000000000</common_id>",
                "</app>" };
            File.WriteAllLines(TempBuildPath + "code\\app.xml", AppXML);

            string longname = PackedTitleLine1.Text;

            if (EnablePackedLine2.Checked && !String.IsNullOrWhiteSpace(PackedTitleLine2.Text))
            {
                longname += "&#x000A;" + PackedTitleLine2.Text;
            }

            string[] MetaXML = {
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>",
                "<menu type=\"complex\" access=\"777\">",
                "  <version type=\"unsignedInt\" length=\"4\">33</version>",
                "  <product_code type=\"string\" length=\"32\">WUP-N-" + TitleIDText + "</product_code>",
                "  <content_platform type=\"string\" length=\"32\">WUP</content_platform>",
                "  <company_code type=\"string\" length=\"8\">0001</company_code>",
                "  <mastering_date type=\"string\" length=\"32\"></mastering_date>",
                "  <logo_type type=\"unsignedInt\" length=\"4\">0</logo_type>",
                "  <app_launch_type type=\"hexBinary\" length=\"4\">00000000</app_launch_type>",
                "  <invisible_flag type=\"hexBinary\" length=\"4\">00000000</invisible_flag>",
                "  <no_managed_flag type=\"hexBinary\" length=\"4\">00000000</no_managed_flag>",
                "  <no_event_log type=\"hexBinary\" length=\"4\">00000002</no_event_log>",
                "  <no_icon_database type=\"hexBinary\" length=\"4\">00000000</no_icon_database>",
                "  <launching_flag type=\"hexBinary\" length=\"4\">00000004</launching_flag>",
                "  <install_flag type=\"hexBinary\" length=\"4\">00000000</install_flag>",
                "  <closing_msg type=\"unsignedInt\" length=\"4\">0</closing_msg>",
                "  <title_version type=\"unsignedInt\" length=\"4\">0</title_version>",
                "  <title_id type=\"hexBinary\" length=\"8\">" + PackedTitleIDLine.Text + "</title_id>",
                "  <group_id type=\"hexBinary\" length=\"4\">" + TitleIDHex + "</group_id>",
                "  <boss_id type=\"hexBinary\" length=\"8\">0000000000000000</boss_id>",
                "  <os_version type=\"hexBinary\" length=\"8\">000500101000400A</os_version>",
                "  <app_size type=\"hexBinary\" length=\"8\">0000000000000000</app_size>",
                "  <common_save_size type=\"hexBinary\" length=\"8\">0000000000000000</common_save_size>",
                "  <account_save_size type=\"hexBinary\" length=\"8\">0000000000000000</account_save_size>",
                "  <common_boss_size type=\"hexBinary\" length=\"8\">0000000000000000</common_boss_size>",
                "  <account_boss_size type=\"hexBinary\" length=\"8\">0000000000000000</account_boss_size>",
                "  <save_no_rollback type=\"unsignedInt\" length=\"4\">0</save_no_rollback>",
                "  <join_game_id type=\"hexBinary\" length=\"4\">00000000</join_game_id>",
                "  <join_game_mode_mask type=\"hexBinary\" length=\"8\">0000000000000000</join_game_mode_mask>",
                "  <bg_daemon_enable type=\"unsignedInt\" length=\"4\">0</bg_daemon_enable>",
                "  <olv_accesskey type=\"unsignedInt\" length=\"4\">3921400692</olv_accesskey>",
                "  <wood_tin type=\"unsignedInt\" length=\"4\">0</wood_tin>",
                "  <e_manual type=\"unsignedInt\" length=\"4\">0</e_manual>",
                "  <e_manual_version type=\"unsignedInt\" length=\"4\">0</e_manual_version>",
                "  <region type=\"hexBinary\" length=\"4\">00000002</region>",
                "  <pc_cero type=\"unsignedInt\" length=\"4\">128</pc_cero>",
                "  <pc_esrb type=\"unsignedInt\" length=\"4\">6</pc_esrb>",
                "  <pc_bbfc type=\"unsignedInt\" length=\"4\">192</pc_bbfc>",
                "  <pc_usk type=\"unsignedInt\" length=\"4\">128</pc_usk>",
                "  <pc_pegi_gen type=\"unsignedInt\" length=\"4\">128</pc_pegi_gen>",
                "  <pc_pegi_fin type=\"unsignedInt\" length=\"4\">192</pc_pegi_fin>",
                "  <pc_pegi_prt type=\"unsignedInt\" length=\"4\">128</pc_pegi_prt>",
                "  <pc_pegi_bbfc type=\"unsignedInt\" length=\"4\">128</pc_pegi_bbfc>",
                "  <pc_cob type=\"unsignedInt\" length=\"4\">128</pc_cob>",
                "  <pc_grb type=\"unsignedInt\" length=\"4\">128</pc_grb>",
                "  <pc_cgsrr type=\"unsignedInt\" length=\"4\">128</pc_cgsrr>",
                "  <pc_oflc type=\"unsignedInt\" length=\"4\">128</pc_oflc>",
                "  <pc_reserved0 type=\"unsignedInt\" length=\"4\">192</pc_reserved0>",
                "  <pc_reserved1 type=\"unsignedInt\" length=\"4\">192</pc_reserved1>",
                "  <pc_reserved2 type=\"unsignedInt\" length=\"4\">192</pc_reserved2>",
                "  <pc_reserved3 type=\"unsignedInt\" length=\"4\">192</pc_reserved3>",
                "  <ext_dev_nunchaku type=\"unsignedInt\" length=\"4\">0</ext_dev_nunchaku>",
                "  <ext_dev_classic type=\"unsignedInt\" length=\"4\">0</ext_dev_classic>",
                "  <ext_dev_urcc type=\"unsignedInt\" length=\"4\">0</ext_dev_urcc>",
                "  <ext_dev_board type=\"unsignedInt\" length=\"4\">0</ext_dev_board>",
                "  <ext_dev_usb_keyboard type=\"unsignedInt\" length=\"4\">0</ext_dev_usb_keyboard>",
                "  <ext_dev_etc type=\"unsignedInt\" length=\"4\">0</ext_dev_etc>",
                "  <ext_dev_etc_name type=\"string\" length=\"512\"></ext_dev_etc_name>",
                "  <eula_version type=\"unsignedInt\" length=\"4\">0</eula_version>",
                "  <drc_use type=\"unsignedInt\" length=\"4\">" + DRCUSE + "</drc_use>",
                "  <network_use type=\"unsignedInt\" length=\"4\">0</network_use>",
                "  <online_account_use type=\"unsignedInt\" length=\"4\">0</online_account_use>",
                "  <direct_boot type=\"unsignedInt\" length=\"4\">0</direct_boot>",
                "  <reserved_flag0 type=\"hexBinary\" length=\"4\">00010001</reserved_flag0>",
                "  <reserved_flag1 type=\"hexBinary\" length=\"4\">00080023</reserved_flag1>",
                "  <reserved_flag2 type=\"hexBinary\" length=\"4\">" + TitleIDHex + "</reserved_flag2>",
                "  <reserved_flag3 type=\"hexBinary\" length=\"4\">00000000</reserved_flag3>",
                "  <reserved_flag4 type=\"hexBinary\" length=\"4\">00000000</reserved_flag4>",
                "  <reserved_flag5 type=\"hexBinary\" length=\"4\">00000000</reserved_flag5>",
                "  <reserved_flag6 type=\"hexBinary\" length=\"4\">00000003</reserved_flag6>",
                "  <reserved_flag7 type=\"hexBinary\" length=\"4\">00000005</reserved_flag7>",
                "  <longname_ja type=\"string\" length=\"512\">" + longname + "</longname_ja>",
                "  <longname_en type=\"string\" length=\"512\">" + longname + "</longname_en>",
                "  <longname_fr type=\"string\" length=\"512\">" + longname + "</longname_fr>",
                "  <longname_de type=\"string\" length=\"512\">" + longname + "</longname_de>",
                "  <longname_it type=\"string\" length=\"512\">" + longname + "</longname_it>",
                "  <longname_es type=\"string\" length=\"512\">" + longname + "</longname_es>",
                "  <longname_zhs type=\"string\" length=\"512\">" + longname + "</longname_zhs>",
                "  <longname_ko type=\"string\" length=\"512\">" + longname + "</longname_ko>",
                "  <longname_nl type=\"string\" length=\"512\">" + longname + "</longname_nl>",
                "  <longname_pt type=\"string\" length=\"512\">" + longname + "</longname_pt>",
                "  <longname_ru type=\"string\" length=\"512\">" + longname + "</longname_ru>",
                "  <longname_zht type=\"string\" length=\"512\">" + longname + "</longname_zht>",
                "  <shortname_ja type=\"string\" length=\"512\">" + PackedTitleLine1.Text + "</shortname_ja>",
                "  <shortname_en type=\"string\" length=\"512\">" + PackedTitleLine1.Text + "</shortname_en>",
                "  <shortname_fr type=\"string\" length=\"512\">" + PackedTitleLine1.Text + "</shortname_fr>",
                "  <shortname_de type=\"string\" length=\"512\">" + PackedTitleLine1.Text + "</shortname_de>",
                "  <shortname_it type=\"string\" length=\"512\">" + PackedTitleLine1.Text + "</shortname_it>",
                "  <shortname_es type=\"string\" length=\"512\">" + PackedTitleLine1.Text + "</shortname_es>",
                "  <shortname_zhs type=\"string\" length=\"512\">" + PackedTitleLine1.Text + "</shortname_zhs>",
                "  <shortname_ko type=\"string\" length=\"512\">" + PackedTitleLine1.Text + "</shortname_ko>",
                "  <shortname_nl type=\"string\" length=\"512\">" + PackedTitleLine1.Text + "</shortname_nl>",
                "  <shortname_pt type=\"string\" length=\"512\">" + PackedTitleLine1.Text + "</shortname_pt>",
                "  <shortname_ru type=\"string\" length=\"512\">" + PackedTitleLine1.Text + "</shortname_ru>",
                "  <shortname_zht type=\"string\" length=\"512\">" + PackedTitleLine1.Text + "</shortname_zht>",
                "  <publisher_ja type=\"string\" length=\"256\"></publisher_ja>",
                "  <publisher_en type=\"string\" length=\"256\"></publisher_en>",
                "  <publisher_fr type=\"string\" length=\"256\"></publisher_fr>",
                "  <publisher_de type=\"string\" length=\"256\"></publisher_de>",
                "  <publisher_it type=\"string\" length=\"256\"></publisher_it>",
                "  <publisher_es type=\"string\" length=\"256\"></publisher_es>",
                "  <publisher_zhs type=\"string\" length=\"256\"></publisher_zhs>",
                "  <publisher_ko type=\"string\" length=\"256\"></publisher_ko>",
                "  <publisher_nl type=\"string\" length=\"256\"></publisher_nl>",
                "  <publisher_pt type=\"string\" length=\"256\"></publisher_pt>",
                "  <publisher_ru type=\"string\" length=\"256\"></publisher_ru>",
                "  <publisher_zht type=\"string\" length=\"256\"></publisher_zht>",
                "  <add_on_unique_id0 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id0>",
                "  <add_on_unique_id1 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id1>",
                "  <add_on_unique_id2 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id2>",
                "  <add_on_unique_id3 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id3>",
                "  <add_on_unique_id4 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id4>",
                "  <add_on_unique_id5 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id5>",
                "  <add_on_unique_id6 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id6>",
                "  <add_on_unique_id7 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id7>",
                "  <add_on_unique_id8 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id8>",
                "  <add_on_unique_id9 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id9>",
                "  <add_on_unique_id10 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id10>",
                "  <add_on_unique_id11 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id11>",
                "  <add_on_unique_id12 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id12>",
                "  <add_on_unique_id13 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id13>",
                "  <add_on_unique_id14 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id14>",
                "  <add_on_unique_id15 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id15>",
                "  <add_on_unique_id16 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id16>",
                "  <add_on_unique_id17 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id17>",
                "  <add_on_unique_id18 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id18>",
                "  <add_on_unique_id19 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id19>",
                "  <add_on_unique_id20 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id20>",
                "  <add_on_unique_id21 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id21>",
                "  <add_on_unique_id22 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id22>",
                "  <add_on_unique_id23 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id23>",
                "  <add_on_unique_id24 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id24>",
                "  <add_on_unique_id25 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id25>",
                "  <add_on_unique_id26 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id26>",
                "  <add_on_unique_id27 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id27>",
                "  <add_on_unique_id28 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id28>",
                "  <add_on_unique_id29 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id29>",
                "  <add_on_unique_id30 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id30>",
                "  <add_on_unique_id31 type=\"hexBinary\" length=\"4\">00000000</add_on_unique_id31>",
                "</menu>"
            };
            File.WriteAllLines(TempBuildPath + "meta\\meta.xml", MetaXML);

            Invoke(ActBuildProgress, 52);

            return true;
        }

        private bool ConvertImagesFormat()
        {
            //
            // Convert PNG files to TGA
            //
            Invoke(ActBuildStatus, tr.Tr("Converting all image sources to expected TGA specification..."));

            LauncherExeFile = TempToolsPath + "IMG\\png2tgacmd.exe";
            LauncherExeArgs = "-i \"" + TempIconPath + "\" -o \"" + TempBuildPath + "meta\" --width=128 --height=128 --tga-bpp=32 --tga-compression=none";
            LaunchProgram();
            LauncherExeFile = TempToolsPath + "IMG\\png2tgacmd.exe";
            LauncherExeArgs = "-i \"" + TempBannerPath + "\" -o \"" + TempBuildPath + "meta\" --width=1280 --height=720 --tga-bpp=24 --tga-compression=none";
            LaunchProgram();

            if (!FlagDrcSpecified)
            {
                File.Copy(TempBannerPath, TempDrcPath);
            }
            LauncherExeFile = TempToolsPath + "IMG\\png2tgacmd.exe";
            LauncherExeArgs = "-i \"" + TempDrcPath + "\" -o \"" + TempBuildPath + "meta\" --width=854 --height=480 --tga-bpp=24 --tga-compression=none";
            LaunchProgram();

            if (FlagLogoSpecified)
            {
                LauncherExeFile = TempToolsPath + "IMG\\png2tgacmd.exe";
                LauncherExeArgs = "-i \"" + TempLogoPath + "\" -o \"" + TempBuildPath + "meta\" --width=170 --height=42 --tga-bpp=32 --tga-compression=none";
                LaunchProgram();
            }

            if (!FlagDrcSpecified)
            {
                File.Delete(TempDrcPath);
            }

            Invoke(ActBuildProgress, 55);

            return true;
        }

        private bool ConvertBootSoundFormat()
        {
            //
            // Convert Boot Sound if provided by user
            //
            if (FlagBootSoundSpecified)
            {
                Invoke(ActBuildStatus, tr.Tr("Converting user provided sound to btsnd format..."));

                LauncherExeFile = TempToolsPath + "SOX\\sox.exe";
                LauncherExeArgs = "\"" + OpenBootSound.FileName + "\" -b 16 \"" + TempSoundPath + "\" channels 2 rate 48k trim 0 6";
                LaunchProgram();
                File.Delete(TempBuildPath + "meta\\bootSound.btsnd");
                LauncherExeFile = TempToolsPath + "JAR\\wav2btsnd.exe";
                LauncherExeArgs = "-in \"" + TempSoundPath + "\" -out \"" + TempBuildPath + "meta\\bootSound.btsnd\"" + LoopString;
                LaunchProgram();
                File.Delete(TempSoundPath);
            }

            Invoke(ActBuildProgress, 60);

            return true;
        }

        private bool BuildIso()
        {
            //
            // Build ISO based on type and user specification
            //
            Invoke(ActBuildStatus, tr.Tr("Processing game for NFS Conversion..."));

            GameIso = GameSourceDirectory.Text;

            if (SystemType == "wii")
            {
                if (FlagWBFS)
                {
                    LauncherExeFile = TempToolsPath + "EXE\\wbfs_file.exe";
                    LauncherExeArgs = "\"" + GameIso + "\" convert \"" 
                        + TempSourcePath + "wbfsconvert.iso\"";
                    LaunchProgram();
                    GameIso = TempSourcePath + "wbfsconvert.iso";
                }

                if (!DisableTrimming.Checked)
                {
                    LauncherExeFile = TempToolsPath + "WIT\\wit.exe";
                    LauncherExeArgs = "extract " + NormalizeCmdlineArg(GameIso) + " --DEST " + NormalizeCmdlineArg(TempSourcePath + "ISOEXTRACT") + " --psel data -vv1";
                    LaunchProgram();

                    if (ForceCC.Checked)
                    {
                        LauncherExeFile = TempToolsPath + "EXE\\GetExtTypePatcher.exe";
                        LauncherExeArgs = "\"" + TempSourcePath + "ISOEXTRACT\\sys\\main.dol\" -nc";
                        LaunchProgram();
                    }

                    if (WiiVMC.Checked)
                    {
                        MessageBox.Show(tr.Tr("The Wii Video Mode Changer will now be launched. I recommend using the Smart Patcher option. \n\nIf you're scared and don't know what you're doing, close the patcher window and nothing will be patched. \n\nClick OK to continue..."));
                        HideProcess = false;
                        LauncherExeFile = TempToolsPath + "EXE\\wii-vmc.exe";
                        LauncherExeArgs = "\"" + TempSourcePath + "ISOEXTRACT\\sys\\main.dol\"";
                        LaunchProgram();
                        HideProcess = true;
                        MessageBox.Show(tr.Tr("Conversion will now continue..."));
                    }

                    LauncherExeFile = TempToolsPath + "WIT\\wit.exe";
                    LauncherExeArgs = "copy " + NormalizeCmdlineArg(TempSourcePath + "ISOEXTRACT") + " --DEST " + NormalizeCmdlineArg(TempSourcePath + "game.iso") + " -ovv --links --iso";
                    LaunchProgram();

                    Directory.Delete(TempSourcePath + "ISOEXTRACT", true);
                    if (File.Exists(TempSourcePath + "wbfsconvert.iso"))
                    {
                        File.Delete(TempSourcePath + "wbfsconvert.iso");
                    }

                    GameIso = TempSourcePath + "game.iso";
                }
            }
            else if (SystemType == "dol")
            {
                FileSystem.CreateDirectory(TempSourcePath + "TEMPISOBASE");
                FileSystem.CopyDirectory(TempToolsPath + "BASE", TempSourcePath + "TEMPISOBASE");
                File.Copy(GameIso, TempSourcePath + "TEMPISOBASE\\sys\\main.dol");
                LauncherExeFile = TempToolsPath + "WIT\\wit.exe";
                LauncherExeArgs = "copy " + NormalizeCmdlineArg(TempSourcePath + "TEMPISOBASE") + " --DEST " + NormalizeCmdlineArg(TempSourcePath + "game.iso") + " -ovv --links --iso";
                LaunchProgram();
                Directory.Delete(TempSourcePath + "TEMPISOBASE", true);
                GameIso = TempSourcePath + "game.iso";
            }
            else if (SystemType == "wiiware")
            {
                FileSystem.CreateDirectory(TempSourcePath + "TEMPISOBASE");
                FileSystem.CopyDirectory(TempToolsPath + "BASE", TempSourcePath + "TEMPISOBASE");
                if (Force43NAND.Checked)
                {
                    File.Copy(TempToolsPath + "DOL\\FIX94_wiivc_chan_booter_force43.dol", TempSourcePath + "TEMPISOBASE\\sys\\main.dol");
                }
                else
                {
                    File.Copy(TempToolsPath + "DOL\\FIX94_wiivc_chan_booter.dol", TempSourcePath + "TEMPISOBASE\\sys\\main.dol");
                }

                string[] TitleTXT = { GameSourceDirectory.Text };
                File.WriteAllLines(TempSourcePath + "TEMPISOBASE\\files\\title.txt", TitleTXT);

                LauncherExeFile = TempToolsPath + "WIT\\wit.exe";
                LauncherExeArgs = "copy " + NormalizeCmdlineArg(TempSourcePath + "TEMPISOBASE") + " --DEST " + NormalizeCmdlineArg(TempSourcePath + "game.iso") + " -ovv --links --iso";
                LaunchProgram();

                Directory.Delete(TempSourcePath + "TEMPISOBASE", true);

                GameIso = TempSourcePath + "game.iso";
            }
            else if (SystemType == "gcn")
            {
                FileSystem.CreateDirectory(TempSourcePath + "TEMPISOBASE");
                FileSystem.CopyDirectory(TempToolsPath + "BASE", TempSourcePath + "TEMPISOBASE");

                if (Force43NINTENDONT.Checked)
                {
                    File.Copy(TempToolsPath + "DOL\\FIX94_nintendont_force43_autoboot.dol", TempSourcePath + "TEMPISOBASE\\sys\\main.dol");
                }
                else if (CustomMainDol.Checked)
                {
                    File.Copy(OpenMainDol.FileName, TempSourcePath + "TEMPISOBASE\\sys\\main.dol");
                }
                else if (DisableNintendontAutoboot.Checked)
                {
                    File.Copy(TempToolsPath + "DOL\\FIX94_nintendont_forwarder.dol", TempSourcePath + "TEMPISOBASE\\sys\\main.dol");
                }
                else
                {
                    File.Copy(TempToolsPath + "DOL\\FIX94_nintendont_default_autoboot.dol", TempSourcePath + "TEMPISOBASE\\sys\\main.dol");
                }

                File.Copy(GameIso, TempSourcePath + "TEMPISOBASE\\files\\game.iso");
                if (FlagGC2Specified)
                {
                    File.Copy(OpenGC2.FileName, TempSourcePath + "TEMPISOBASE\\files\\disc2.iso");
                }

                LauncherExeFile = TempToolsPath + "WIT\\wit.exe";
                LauncherExeArgs = "copy " + NormalizeCmdlineArg(TempSourcePath + "TEMPISOBASE") + " --DEST " + NormalizeCmdlineArg(TempSourcePath + "game.iso") + " -ovv --links --iso";
                LaunchProgram();

                Directory.Delete(TempSourcePath + "TEMPISOBASE", true);

                GameIso = TempSourcePath + "game.iso";
            }

            LauncherExeFile = TempToolsPath + "WIT\\wit.exe";
            LauncherExeArgs = "extract " + NormalizeCmdlineArg(GameIso) + " --psel data --files +tmd.bin --files +ticket.bin --dest " + NormalizeCmdlineArg(TempSourcePath + "TIKTEMP") + " -vv1";
            LaunchProgram();

            File.Copy(TempSourcePath + "TIKTEMP\\tmd.bin", TempBuildPath + "code\\rvlt.tmd");
            File.Copy(TempSourcePath + "TIKTEMP\\ticket.bin", TempBuildPath + "code\\rvlt.tik");
            Directory.Delete(TempSourcePath + "TIKTEMP", true);

            Invoke(ActBuildProgress, 70);

            return true;
        }

        private bool ConvertIsoToNFS()
        {
            //
            // Convert ISO to NFS format
            //
            Invoke(ActBuildStatus, tr.Tr("Converting processed game to NFS format..."));

            Directory.SetCurrentDirectory(TempBuildPath + "content");

            string lrpatchflag = "";
            if (LRPatch.Checked)
            {
                lrpatchflag = " -lrpatch";
            }
            if (SystemType == "wii")
            {
                LauncherExeFile = TempToolsPath + "EXE\\nfs2iso2nfs.exe";
                LauncherExeArgs = "-enc" + nfspatchflag + lrpatchflag + " -iso \"" + GameIso + "\"";
                LaunchProgram();
            }
            if (SystemType == "dol")
            {
                LauncherExeFile = TempToolsPath + "EXE\\nfs2iso2nfs.exe";
                LauncherExeArgs = "-enc -homebrew" + passpatch + " -iso \"" + GameIso + "\"";
                LaunchProgram();
            }
            if (SystemType == "wiiware")
            {
                LauncherExeFile = TempToolsPath + "EXE\\nfs2iso2nfs.exe";
                LauncherExeArgs = "-enc -homebrew" + nfspatchflag + lrpatchflag + " -iso \"" + GameIso + "\"";
                LaunchProgram();
            }
            if (SystemType == "gcn")
            {
                LauncherExeFile = TempToolsPath + "EXE\\nfs2iso2nfs.exe";
                LauncherExeArgs = "-enc -homebrew -passthrough -iso \"" + GameIso + "\"";
                LaunchProgram();
            }

            if (!DisableTrimming.Checked || FlagWBFS)
            {
                File.Delete(GameIso);
            }

            Invoke(ActBuildProgress, 85);

            return true;
        }

        private bool NUSPackerEncrypt()
        {
            //
            // Encrypt contents with NUSPacker
            //
            Invoke(ActBuildStatus, tr.Tr("Encrypting contents into installable WUP Package..."));

            Directory.SetCurrentDirectory(TempRootPath);
            LauncherExeFile = TempToolsPath + "JAR\\NUSPacker.exe";
            LauncherExeArgs = "-in BUILDDIR -out \"" + OutputDirectory.Text 
                + "\\WUP-N-" + TitleIDText + "_" + PackedTitleIDLine.Text 
                + "\" -encryptKeyWith " + WiiUCommonKey.Text;
            LaunchProgram();

            Invoke(ActBuildProgress, 100);

            return true;
        }

        private void BuildCleanup()
        {
            //
            // Reset working directory.
            //
            Directory.SetCurrentDirectory(Application.StartupPath);

            //
            // Delete Temp Directories
            //
            Directory.Delete(TempBuildPath, true);
            Directory.Delete(TempRootPath + "output", true);
            Directory.Delete(TempRootPath + "tmp", true);
            Directory.CreateDirectory(TempBuildPath);
        }

        private bool BuildPack()
        {
            BuildStep[] buildSteps = new BuildStep[]
            {
                PrepareTemporaryDirectory,
                CheckFreeDiskSpaceForPack,
                PrepareOutputDirectory,
                PrepareJNUSStuffs,
                PrepareBasicFilesForPack,
                GeneratePackXmls,
                ConvertImagesFormat,
                ConvertBootSoundFormat,
                BuildIso,
                ConvertIsoToNFS,
                NUSPackerEncrypt
            };

            ThrowProcessException = true;
            int succeed = 0;

            foreach (BuildStep buildStep in buildSteps)
            {
                if (LastBuildCancelled)
                {
                    break;
                }

                try
                {
                    if (!buildStep())
                    {
                        break;
                    }

                    ++succeed;
                }
                catch (Exception ex)
                {
                    Console.Write("buildStep throws an exception: " + ex.Message);
                    break;
                }
            }

            BuildCleanup();

            Invoke(ActBuildStatus, tr.Tr("Conversion complete..."));

            return (succeed == buildSteps.Length);
        }

        private struct WiiVcGenerateImage
        {
            public Bitmap bitmap;
            public Rectangle rectangle;
            public string s;
            public string savePath;
            public string dirControlName;
            public string previewControlName;
            public Color foreColor;
            public bool adjustedFontByTextRenderer;
            public bool drawStringByTextRenderer;
        };

        private void GenerateImage_Click(object sender, EventArgs e)
        {
            // Check if the required fields are fullfilled.
            if (GameNameLabel.Text == "")
            {
                MessageBox.Show(tr.Tr("Please select your game before using this option"));
                return;
            }

            // Setup font used for drawing.
            Font arialFont = new Font("Arial", 10);

            // Setup temp directory for generated images.
            string saveDir = GetTempRootPath() + "WiiVCInjector\\SOURCETEMP\\";
            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
            }

            // Create the background image for gamepad bar.
            Bitmap bitmapGamePadBar = new Bitmap(854, 480);
            using (Graphics graphics = Graphics.FromImage(bitmapGamePadBar))
            {
                graphics.DrawImage(
                    Properties.Resources.universal_Wii_WiiWare_template_bootTvTex,
                    new Rectangle(0, 0, bitmapGamePadBar.Width, bitmapGamePadBar.Height),
                    new Rectangle(
                        0, 0,
                        Properties.Resources.universal_Wii_WiiWare_template_bootTvTex.Width,
                        Properties.Resources.universal_Wii_WiiWare_template_bootTvTex.Height),
                    GraphicsUnit.Pixel);
            }

            // Create the background image for boot logo.
            Bitmap bitmapBootLogo = new Bitmap(170, 42);
            using (Graphics graphics = Graphics.FromImage(bitmapBootLogo))
            {
                graphics.FillRectangle(
                    Brushes.White, 
                    new Rectangle(0, 0, bitmapBootLogo.Width, bitmapBootLogo.Height));
            }

            // Define images.
            WiiVcGenerateImage[] images = new WiiVcGenerateImage[]
            {
                new WiiVcGenerateImage {
                    bitmap = Properties.Resources.universal_Wii_WiiWare_template_iconTex,
                    rectangle = new Rectangle(0, 23, 128, 94),
                    s = GameNameLabel.Text,
                    savePath = saveDir + "iconTex.png",
                    dirControlName = "IconSourceDirectory",
                    previewControlName = "IconPreviewBox",
                    foreColor = Color.Black,
                    adjustedFontByTextRenderer = true,
                    drawStringByTextRenderer = false,
                },
                new WiiVcGenerateImage {
                    bitmap = Properties.Resources.universal_Wii_WiiWare_template_bootTvTex,
                    rectangle = new Rectangle(224, 210, 820, 320),
                    s = GameNameLabel.Text,
                    savePath = saveDir + "bootTvTex.png",
                    dirControlName = "BannerSourceDirectory",
                    previewControlName = "BannerPreviewBox",
                    foreColor = Color.Black,
                    adjustedFontByTextRenderer = false,
                    drawStringByTextRenderer = false,
                },
                new WiiVcGenerateImage {
                    bitmap = bitmapGamePadBar,
                    rectangle = new Rectangle(148, 138, 556, 212),
                    s = GameNameLabel.Text,
                    savePath = saveDir + "bootDrcTex.png",
                    dirControlName = "DrcSourceDirectory",
                    previewControlName = "DrcPreviewBox",
                    foreColor = Color.Black,
                    adjustedFontByTextRenderer = false,
                    drawStringByTextRenderer = false,
                },
                new WiiVcGenerateImage {
                    bitmap = bitmapBootLogo,
                    rectangle = new Rectangle(0, 0, 170, 42),
                    s = "WiiWare",
                    savePath = saveDir + "bootLogoTex.png",
                    dirControlName = "LogoSourceDirectory",
                    previewControlName = "LogoPreviewBox",
                    foreColor = Color.DimGray,
                    adjustedFontByTextRenderer = true,
                    drawStringByTextRenderer = false,
                },
            };

            // Loop to generate the images.
            for (int i = 0; i < images.Length; ++i)
            {
                // Draw game name to the background image.
                Draw.ImageDrawString(
                    ref images[i].bitmap,
                    images[i].s,
                    images[i].rectangle,
                    arialFont,
                    images[i].foreColor,
                    images[i].adjustedFontByTextRenderer,
                    images[i].drawStringByTextRenderer);

                // Save the completed image to temp directory.
                images[i].bitmap.Save(images[i].savePath);

                // Show the preview image to user.
                FileStream tempstream = new FileStream(images[i].savePath, FileMode.Open);
                var tempimage = Image.FromStream(tempstream);
                PictureBox previewBox = this.Controls.Find(images[i].previewControlName, true).FirstOrDefault() as PictureBox;
                previewBox.Image = tempimage;
                tempstream.Close();

                // Set the text information of the edit control 
                // which indicates the image source path.
                Label sourceDirectory = this.Controls.Find(images[i].dirControlName, true).FirstOrDefault() as Label;
                sourceDirectory.Text = tr.Tr("Auto generated.");
                sourceDirectory.ForeColor = Color.Green;
            }

            // Set relative flags.
            FlagIconSpecified = true;
            FlagBannerSpecified = true;
            FlagDrcSpecified = true;
            FlagLogoSpecified = true;
            FlagRepo = false;
        }

        private void AutoBuildDragDrop(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                String[] files = (String[])e.Data.GetData(DataFormats.FileDrop);
                foreach (String s in files)
                {
                    Program.AppendAutoBuildList(s);
                }
            }

            AutoBuildWiiRetail();
        }

        private void GameFile_DragDrop(object sender, DragEventArgs e)
        {
            AutoBuildDragDrop(e);
        }

        private void GameFile_DragEnter(object sender, DragEventArgs e)
        {
            if (!IsBuilding && Program.AutoBuildList.Count == 0 && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void ClearBuildOuput_Click(object sender, EventArgs e)
        {
            BuildOutput.ResetText();
        }

        private void AutoScrollBuildOutput_Click(object sender, EventArgs e)
        {
            AutoScrollBuildOutput.Checked = !AutoScrollBuildOutput.Checked;
        }

        private void BrowseOutputDir_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog OutputFolderSelect = new FolderBrowserDialog();
            if (OutputFolderSelect.ShowDialog() == DialogResult.OK)
            {
                OutputDirectory.Text = OutputFolderSelect.SelectedPath;
                Registry.CurrentUser.CreateSubKey("WiiVCInjector")
                    .SetValue("OutputDirectory", OutputDirectory.Text);
            }
        }

        private bool MoveTempDir(string source, string destination)
        {
            if (source.Equals(destination, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            try
            {
                FileSystem.MoveDirectory(
                    source, destination, 
                    UIOption.AllDialogs, 
                    UICancelOption.ThrowException);
            }
            catch (Exception)
            {
                return false;
            }           

            return true;
        }

        private void BrowseTempDir_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog tempFolderSelect = new FolderBrowserDialog();
            if (tempFolderSelect.ShowDialog() == DialogResult.OK)
            {
                TemporaryDirectory.Text = tempFolderSelect.SelectedPath;
                Registry.CurrentUser.CreateSubKey("WiiVCInjector")
                    .SetValue("TemporaryDirectory", TemporaryDirectory.Text);
            }
        }

        private void OpenOutputDirButton_Click(object sender, EventArgs e)
        {
            if (Directory.Exists(OutputDirectory.Text))
            {
                System.Diagnostics.Process.Start(OutputDirectory.Text);
            }
        }

        private void OpenTempDirButton_Click(object sender, EventArgs e)
        {
            if (Directory.Exists(TemporaryDirectory.Text))
            {
                System.Diagnostics.Process.Start(TemporaryDirectory.Text);
            }
        }
    }
}
