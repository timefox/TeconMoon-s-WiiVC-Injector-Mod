using ImageUtils;
using LogLevels;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Media;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using TeconMoon_s_WiiVC_Injector.Utils;
using TeconMoon_s_WiiVC_Injector.Utils.Build;

namespace TeconMoon_s_WiiVC_Injector
{
    public partial class WiiVC_Injector : Form
    {
        public WiiVC_Injector()
        {
            InitializeComponent();

            //
            // Extract tool chains to temp dir.
            //
            if (!ExtractToolChainsToTemp())
            {
                MessageBox.Show(
                    Trt.Tr("Create temporary directory failed, it may be caused by "
                    + "low space on hard drive, permission denied or invalid path name."),
                    Trt.Tr("Error"));

                Environment.Exit(0);
            }

            //
            // Apply GUI translation.
            //
            ApplyTranslation();

            //
            // Print verion information to main window's title.
            //
            this.Text += String.Format(" - [{0}]", Program.Version);

            //
            // Hide 'Debug' button for release version.
            //
#if !DEBUG
            this.DebugButton.Visible = false;
#endif

            //
            // Some extra initializing works for controls.
            //
            InitializeControlDatas();

            //
            // Load program settings.
            //
            LoadSettings();

            // 
            // Initialize actions for build thread.
            //
            InitializeBuildActions();

            //
            // Process any pending build requests.
            //
            AutoBuild();
        }

        private bool ExtractToolChainsToTemp()
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

        private void UpdateTempDirs()
        {
            TempSourcePath = TempRootPath + "SOURCETEMP\\";
            TempBuildPath = TempRootPath + "BUILDDIR\\";
            TempToolsPath = TempRootPath + "TOOLDIR\\";
            TempIconPath = TempRootPath + "SOURCETEMP\\iconTex.tga";
            TempBannerPath = TempRootPath + "SOURCETEMP\\bootTvTex.tga";
            TempDrcPath = TempRootPath + "SOURCETEMP\\bootDrcTex.tga";
            TempLogoPath = TempRootPath + "SOURCETEMP\\bootLogoTex.tga";
            TempSoundPath = TempRootPath + "SOURCETEMP\\bootSound.wav";
        }

        private void CleanupBuildSourceTemp()
        {
            //
            // Reserve some temp files if user cancel the building,
            // and build again, these files should still can be reused.
            //
            string[] reservedTempFiles =
            {
                "iconTex.tga",
                "bootTvTex.tga",
                "bootDrcTex.tga",
                "bootLogoTex.tga"
            };

            foreach (string tempFileName in reservedTempFiles)
            {
                string tempFilePath = TempSourcePath + "\\" + tempFileName;
                string tempSavePath = TempRootPath + "\\" + tempFileName;

                if (File.Exists(tempFilePath))
                {
                    File.Move(tempFilePath, tempSavePath);
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

            foreach (string tempFileName in reservedTempFiles)
            {
                string tempFilePath = TempSourcePath + "\\" + tempFileName;
                string tempSavePath = TempRootPath + "\\" + tempFileName;

                if (File.Exists(tempSavePath))
                {
                    File.Move(tempSavePath, tempFilePath);
                }
            }
        }

        private void InitializeControlDatas()
        {
            //
            // Initialize log levels.
            //
            foreach (string logLevel in LogLevel.Names)
            {
                LogLevelBox.Items.Add(logLevel);
            }

            //
            // Assign the menu of GenerateImage Button.
            //
            GenerateImage.SplitMenu = GenerateImageMenu;

            //
            // Setup the menu items for GenerateImage Button's menu.
            //
            KeyValuePair<GenerateImageBackgndSource, string>[] gimenuItems = new KeyValuePair<GenerateImageBackgndSource, string>[]
            {
                new KeyValuePair<GenerateImageBackgndSource, string>(
                    GenerateImageBackgndSource.DownloadFromGameTDB, 
                    Trt.Tr("Download background from GameTDB.com")),
                new KeyValuePair<GenerateImageBackgndSource, string>(
                    GenerateImageBackgndSource.LocalDefault, 
                    Trt.Tr("Use default background")),
            };

            foreach (KeyValuePair<GenerateImageBackgndSource, string>  gimenuItem in gimenuItems)
            {
                GenerateImageMenu.Items.Add(gimenuItem.Value).Tag = gimenuItem.Key;
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

            int logLevel;

            if (!int.TryParse(
                appKey.GetValue("LogLevel", LogLevel.Info.Level).ToString(),
                out logLevel))
            {
                logLevel = LogLevel.Info.Level;
            }

            currentLogLevel = LogLevel.getLogLevelByLevel(logLevel);
            LogLevelBox.SelectedItem = currentLogLevel.Name;

            OutputDirectory.Text = appKey.GetValue("OutputDirectory") as string;
            TemporaryDirectory.Text = GetAppTempPath(false);

            if (!Enum.TryParse(
                appKey.GetValue(
                    "GenerateImageBackgndSource",
                    GenerateImageBackgndSource.DownloadFromGameTDB) as string,
                out GenerateImageBackgndSource generateImageBackgnd))
            {
                generateImageBackgnd = GenerateImageBackgndSource.DownloadFromGameTDB;
            }

            GenerateImageBackgnd = generateImageBackgnd;

            appKey.Close();
        }

        private void ApplyTranslation()
        {
            if (Trt.IsValidate)
            {
                Trt.TranslateForm(this);
            }

            // 
            // OpenIcon
            // 
            this.OpenIcon.FileName = "iconTex";
            this.OpenIcon.Title = Trt.Tr("Your image will be converted to PNG (Lossless) to be later processed.");
            // 
            // OpenBanner
            // 
            this.OpenBanner.FileName = "bootTvTex";
            this.OpenBanner.Title = Trt.Tr("Your image will be converted to PNG (Lossless) to be later processed.");
            // 
            // OpenGame
            // 
            this.OpenGame.FileName = "game";
            this.OpenGame.Filter = Trt.Tr("Wii Dumps (*.iso,*.wbfs)|*.iso;*.wbfs");
            this.OpenGame.Title = Trt.Tr("Specify your game file");
            // 
            // OpenDrc
            // 
            this.OpenDrc.FileName = "bootDrcTex";
            this.OpenDrc.Title = Trt.Tr("Your image will be converted to PNG (Lossless) to be later processed.");
            // 
            // OpenLogo
            // 
            this.OpenLogo.FileName = "bootLogoTex";
            this.OpenLogo.Title = Trt.Tr("Your image will be converted to PNG (Lossless) to be later processed.");
            // 
            // OpenBootSound
            // 
            this.OpenBootSound.FileName = "BootSound";
            this.OpenBootSound.Filter = Trt.Tr("Supported Sound Files (*.wav)|*wav");
            this.OpenBootSound.Title = Trt.Tr("Specify your boot sound");
            // 
            // OpenMainDol
            // 
            this.OpenMainDol.FileName = "main";
            this.OpenMainDol.Filter = Trt.Tr("Nintendont Forwarder (*.dol)|*.dol");
            this.OpenMainDol.Title = Trt.Tr("Specify your replacement Nintendont Forwarder");
            // 
            // OpenGC2
            // 
            this.OpenGC2.FileName = "disc2";
            this.OpenGC2.Filter = Trt.Tr("GameCube Disk 2 (*.gcm,*.iso)|*.gcm;*.iso");
            this.OpenGC2.Title = Trt.Tr("Specify your GameCube game\'s 2nd disc");
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
                EnsureEnableControl(LogLevelBox.Control, true);
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

        private void AutoBuild()
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
            AutoBuildInvalidList.Clear();
            AutoBuildSkippedList.Clear();

            BuildCompletedEx += WiiVC_Injector_BuildCompletedEx;

            PropmtForSucceed = false;
            AutoBuildNext();
        }

        private void AutoBuildNext()
        {
            while (Program.AutoBuildList.Any())
            {
                string game = Program.AutoBuildList[0];

                // Search for second disc
                if (GCRetail.Checked)
                {
                    string[] discs = SearchGCDiscs(game);
                    game = discs[0];
                    if (discs.Length > 1)
                    {
                        OpenGC2.FileName = discs[1];
                        SelectGC2Source(discs[1]);
                    }
                }

                if (SelectGameSource(game, true))
                {
                    if (Directory.Exists(GetOutputFolder()))
                    {
                        AppendBuildOutput(new BuildOutputItem()
                        {
                            Output = String.Format(Trt.Tr("Title output folder already exists: {0}\nSkipping: {1}.\n"), GetOutputFolder(), game),
                            OutputType = BuildOutputType.Error
                        });
                        AutoBuildSkippedList.Add(game);
                        Program.AutoBuildList.RemoveAt(0);
                        continue;
                    } else
                    {
                        BuildCurrent();
                        break;
                    }
                }

                AppendBuildOutput(new BuildOutputItem(){
                    Output = String.Format(Trt.Tr("Invalid Title: {0}.\n"), game),
                    OutputType = BuildOutputType.Error
                    }
                );
                AutoBuildInvalidList.Add(game);
                Program.AutoBuildList.RemoveAt(0);
            }

            if(!Program.AutoBuildList.Any())
            {
                BuildCompletedEx -= WiiVC_Injector_BuildCompletedEx;

                if (!InClosing)
                {
                    string s = String.Format(
                        Trt.Tr("All conversions have been completed.\nSucceed: {0}.\nFailed: {1}.\nSkipped: {2}.\nInvalid: {3}."),
                        AutoBuildSucceedList.Count,
                        AutoBuildFailedList.Count,
                        AutoBuildSkippedList.Count,
                        AutoBuildInvalidList.Count);

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

        private bool EndsWithDiscSuffix(string name, int discNumber)
        {
            string[] discSuffixes = new string[] { "-", "-dvd", "-disc", "-d" };
            bool result = false;

            foreach (string discSuffix in discSuffixes)
            {
                if (name.EndsWith(discSuffix + discNumber))
                    result = true;
                break;
            }
            return result;
        }

        private string[] SearchGCDiscs(string selectedGame)
        {
            string[] result = new string[2];
            int searchFor;



            if (EndsWithDiscSuffix(Path.GetFileNameWithoutExtension(selectedGame), 1))
            {
                result[0] = selectedGame;
                searchFor = 2;
            }
            else if (EndsWithDiscSuffix(Path.GetFileNameWithoutExtension(selectedGame), 2))
            {
                result[1] = selectedGame;
                searchFor = 1;
            }
            else
            {
                result[0] = selectedGame;
                return result;
            }

            string selectedGameDirectory = Path.GetDirectoryName(selectedGame);

            if (Program.AutoBuildList.Count() != 0)
            {
                foreach (string game in Program.AutoBuildList)
                {
                    string gameDir = Path.GetDirectoryName(game);
                    if (selectedGameDirectory == gameDir && EndsWithDiscSuffix(Path.GetFileNameWithoutExtension(game), searchFor))
                    {
                        result[searchFor - 1] = game;
                        Program.AutoBuildList.Remove(game);
                        return result;
                    }

                }
            }

            DirectoryInfo currDirectory = new DirectoryInfo(selectedGameDirectory);
            foreach (FileInfo isoFile in currDirectory.GetFiles("*.iso"))
            {
                if (EndsWithDiscSuffix(Path.GetFileNameWithoutExtension(isoFile.Name), searchFor))
                {
                    result[searchFor - 1] = isoFile.FullName;
                    return result;
                }
            }

            return result;
        }

        private bool BuildCurrent()
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

            switch (item.OutputType)
            {
                case BuildOutputType.Succeed:
                    color = Color.Green;
                    font = new Font(BuildOutput.Font, FontStyle.Bold);
                    break;
                case BuildOutputType.Error:
                    color = Color.DarkRed;
                    font = new Font(BuildOutput.Font, FontStyle.Bold);
                    break;
                case BuildOutputType.Step:
                    color = Color.DarkOliveGreen;
                    font = new Font(BuildOutput.Font.FontFamily, BuildOutput.Font.Size + 1, FontStyle.Bold);
                    break;
                case BuildOutputType.Exec:
                    color = Color.Blue;
                    font = new Font(BuildOutput.Font, FontStyle.Bold);
                    break;
                case BuildOutputType.Normal:
                default:
                    break;
            }
            BuildOutput.AppendText(item.Output, color, font);

            if (AutoScrollBuildOutput.Checked)
            {
                BuildOutput.SelectionStart = BuildOutput.TextLength;
                BuildOutput.ScrollToCaret();
            }
        }

        //Specify public variables for later use (ASK ALAN)
        string SystemType = "wii";
        string TitleIDHex;
        string TitleFullIDHex;
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
        string LoopString = " -noLoop";
        string nfspatchflag = "";
        string passpatch = " -passthrough";
        ProcessStartInfo Launcher;
        string LauncherExeFile;
        string LauncherExeArgs;
        string JNUSToolDownloads = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + "\\JNUSToolDownloads\\";
        static string DefaultTempRootPath = Path.GetTempPath() + "WiiVCInjector\\";
        static string TempRootPath = GetAppTempPath() + "WiiVCInjector\\";
        string TempSourcePath = TempRootPath + "SOURCETEMP\\";
        string TempBuildPath = TempRootPath + "BUILDDIR\\";
        string TempToolsPath = TempRootPath + "TOOLDIR\\";
        string TempIconPath = TempRootPath + "SOURCETEMP\\iconTex.tga";
        string TempBannerPath = TempRootPath + "SOURCETEMP\\bootTvTex.tga";
        string TempDrcPath = TempRootPath + "SOURCETEMP\\bootDrcTex.tga";
        string TempLogoPath = TempRootPath + "SOURCETEMP\\bootLogoTex.tga";
        Size IconSize = new Size(128, 128);
        Size BannerSize = new Size(1280, 720);
        Size DrcSize = new Size(854, 480);
        Size LogoSize = new Size(170, 42);
        string TempSoundPath = TempRootPath + "SOURCETEMP\\bootSound.wav";
        SoundPlayer bootSoundPlayer = new SoundPlayer();
        LogLevel currentLogLevel;

        string gameTDBBaseURL = "https://art.gametdb.com/wii";
        string[] coverLanguages = { "US", "EN", "FR", "DE", "IT", "NL", "AU" };
        Dictionary<string, string[]> idMap = TitleIdMap.BuildIdMap();

        string GameIso;
        List<string> AutoBuildSucceedList = new List<string>();
        List<string> AutoBuildFailedList = new List<string>();
        List<string> AutoBuildInvalidList = new List<string>();
        List<string> AutoBuildSkippedList = new List<string>();
        Dictionary<String, bool> ControlEnabledStatus = new Dictionary<String, bool>();

        private enum GenerateImageBackgndSource
        {
            DownloadFromGameTDB,
            LocalDefault,
        };

        private GenerateImageBackgndSource GenerateImageBackgnd { get; set; } = GenerateImageBackgndSource.DownloadFromGameTDB;

        private static string GetAppTempPath(bool endWithDirectorySeparatorChar = true)
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

            if (endWithDirectorySeparatorChar)
            {
                if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()))
                {
                    path += Path.DirectorySeparatorChar;
                }
            }
            else
            {
                path = path.TrimEnd(new char[] { Path.DirectorySeparatorChar });
            }

            return path;
        }

        //call options
        private bool LaunchProgram()
        {
            bool exitNormally = true;

            Launcher = new ProcessStartInfo(LauncherExeFile, LauncherExeArgs);

            if (HideProcess)
            {
                Launcher.CreateNoWindow = true;
                Launcher.WindowStyle = ProcessWindowStyle.Hidden;
            }

            Launcher.UseShellExecute = false;
            Launcher.RedirectStandardOutput = true;
            Launcher.RedirectStandardError = true;

            BuildOutputBuffer buildOutputBuffer = new BuildOutputBuffer();
            buildOutputBuffer.FlushBuffer += (s, e) =>
            {
                BeginInvoke(ActBuildOutput, e);
            };

            if (currentLogLevel <= LogLevel.Debug)
            {
                BeginInvoke(ActBuildOutput, new BuildOutputItem()
                {
                    Output = Trt.Tr("Executing:") + ' ' + LauncherExeFile + Environment.NewLine
                           + Trt.Tr("Args:") + ' ' + LauncherExeArgs + Environment.NewLine,
                    OutputType = BuildOutputType.Exec
                });
            }

            try
            {
                Process process = Process.Start(Launcher);
                System.Timers.Timer OutputPumpTimer = new System.Timers.Timer();

                process.OutputDataReceived += (s, d) =>
                {
                    if (currentLogLevel <= LogLevel.Debug)
                    {
                        lock(buildOutputBuffer)
                        {
                            buildOutputBuffer.AppendOutput(d.Data, BuildOutputType.Normal);
                        }                       
                    }
                };

                process.ErrorDataReceived += (s, d) =>
                {
                    //
                    // Whatever, the error information should be printed.
                    //
                    lock(buildOutputBuffer)
                    {
                        buildOutputBuffer.AppendOutput(d.Data, BuildOutputType.Error);
                    }                    
                };

                OutputPumpTimer.Interval = 100;
                OutputPumpTimer.Elapsed += (sender, e) =>
                {
                    lock (buildOutputBuffer)
                    {
                        buildOutputBuffer.Flush();
                    }
                };
                OutputPumpTimer.Start();

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

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

                process.Close();

                lock (buildOutputBuffer)
                {
                    buildOutputBuffer.Flush();
                }
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
                    + Trt.Tr(" does not exit normally."));
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

        private void DownloadImageFromRepo()
        {
            var client = new WebClient();

            string RepoSrc = "https://raw.githubusercontent.com/cucholix/wiivc-bis/master/" + SystemType + "/image/" + CucholixRepoID + "/iconTex.png";
            string LocalDst = GetAppTempPath() + "WiiVCInjector\\SOURCETEMP\\iconTex.png";

            if (File.Exists(LocalDst))
            {
                File.Delete(LocalDst);
            }

            client.DownloadFile(RepoSrc, LocalDst);

            Image image = Draw.ResizeAndFitImage(LoadImage(LocalDst), IconSize);
            Tga.saveTGA(image, PixelFormat.Format32bppArgb, TempIconPath);

            IconPreviewBox.Image = image;
            IconSourceDirectory.Text = Trt.Tr("iconTex.png downloaded from Cucholix's Repo");
            IconSourceDirectory.ForeColor = Color.Black;
            FlagIconSpecified = true;
            File.Delete(LocalDst);

            RepoSrc = "https://raw.githubusercontent.com/cucholix/wiivc-bis/master/" + SystemType + "/image/" + CucholixRepoID + "/bootTvTex.png";
            LocalDst = GetAppTempPath() + "WiiVCInjector\\SOURCETEMP\\bootTvTex.png";

            if (File.Exists(LocalDst))
            {
                File.Delete(LocalDst);
            }

            client.DownloadFile(RepoSrc, LocalDst);

            image = Draw.ResizeAndFitImage(LoadImage(LocalDst), BannerSize);
            Tga.saveTGA(image, PixelFormat.Format24bppRgb, TempBannerPath);

            BannerPreviewBox.Image = image;
            BannerSourceDirectory.Text = Trt.Tr("bootTvTex.png downloaded from Cucholix's Repo");
            BannerSourceDirectory.ForeColor = Color.Black;
            FlagBannerSpecified = true;

            image = Draw.ResizeAndFitImage(LoadImage(LocalDst), DrcSize);
            Tga.saveTGA(image, PixelFormat.Format24bppRgb, TempDrcPath);

            DrcPreviewBox.Image = image;
            DrcSourceDirectory.Text = Trt.Tr("bootTvTex.png downloaded from Cucholix's Repo");
            DrcSourceDirectory.ForeColor = Color.Black;
            FlagDrcSpecified = true;

            GenerateLogo();
            File.Delete(LocalDst);

            FlagRepo = true;
        }
        //Called from RepoDownload_Click to check if files exist before downloading
        private bool RemoteFileExists(string url)
        {
            bool result;
            HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
            request.Method = "HEAD";
            try
            {
                using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
                {
                    result = (response.StatusCode == HttpStatusCode.OK);
                }
            }
            catch (WebException)
            {
                result = false;
            }

            return result;
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
            switch (MessageBox.Show(this, Trt.Tr("Are you sure you want to close?"),
                Trt.Tr("Closing"), MessageBoxButtons.YesNo))
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
                GameSourceButton.Text = Trt.Tr("Game...");
                OpenGame.FileName = Trt.Tr("game");
                OpenGame.Filter = Trt.Tr("Wii Dumps (*.iso,*.wbfs)|*.iso;*.wbfs");
                GameSourceDirectory.Text = Trt.Tr("Game file has not been specified");
                GameSourceDirectory.ForeColor = Color.Red;
                FlagGameSpecified = false;
                SystemType = "wii";
                GameNameLabel.Text = "";
                TitleIDLabel.Text = "";
                TitleIDInt = 0;
                TitleIDHex = "";
                TitleFullIDHex = "";
                GameType = 0;
                CucholixRepoID = "";
                PackedTitleLine1.Text = "";
                PackedTitleIDLine.Text = "";
                GC2SourceButton.Enabled = false;
                GC2SourceDirectory.Text = Trt.Tr("2nd GameCube Disc Image has not been specified");
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
                GameSourceButton.Text = Trt.Tr("Game...");
                OpenGame.FileName = "boot.dol";
                OpenGame.Filter = Trt.Tr("DOL Files (*.dol)|*.dol");
                GameSourceDirectory.Text = Trt.Tr("Game file has not been specified");
                GameSourceDirectory.ForeColor = Color.Red;
                FlagGameSpecified = false;
                SystemType = "dol";
                GameNameLabel.Text = "";
                TitleIDLabel.Text = "";
                TitleIDInt = 0;
                TitleIDHex = "";
                TitleFullIDHex = "";
                GameType = 0;
                CucholixRepoID = "";
                PackedTitleLine1.Text = "";
                PackedTitleIDLine.Text = "";
                DRCUSE = "65537";
                GC2SourceButton.Enabled = false;
                GC2SourceDirectory.Text = Trt.Tr("2nd GameCube Disc Image has not been specified");
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
                GameSourceButton.Text = Trt.Tr("TitleID...");
                OpenGame.FileName = Trt.Tr("NULL");
                GameNameLabel.Text = "";
                TitleIDLabel.Text = "";
                TitleIDInt = 0;
                TitleIDHex = "";
                TitleFullIDHex = "";
                GameType = 0;
                CucholixRepoID = "";
                PackedTitleLine1.Text = "";
                PackedTitleIDLine.Text = "";
                GC2SourceButton.Enabled = false;
                GC2SourceDirectory.Text = Trt.Tr("2nd GameCube Disc Image has not been specified");
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
                    Trt.Tr("Enter your installed Wii Channel's 4-letter Title ID. If you don't know it, open a WAD for the channel in something like ShowMiiWads to view it."),
                    Trt.Tr("Enter your WAD's Title ID"), Trt.Tr("XXXX"), 0, 0);
                if (GameSourceDirectory.Text == "")
                {
                    GameSourceDirectory.ForeColor = Color.Red;
                    GameSourceDirectory.Text = Trt.Tr("Title ID specification cancelled, reselect vWii NAND Title Launcher to specify");
                    FlagGameSpecified = false;
                    goto skipWiiNandLoopback;
                }
                if (GameSourceDirectory.Text.Length == 4)
                {
                    GameSourceDirectory.Text = GameSourceDirectory.Text.ToUpper();
                    GameSourceDirectory.ForeColor = Color.Black;
                    FlagGameSpecified = true;
                    SystemType = "wiiware";
                    GameNameLabel.Text = Trt.Tr("N/A");
                    TitleIDLabel.Text = Trt.Tr("N/A");
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
                    GameSourceDirectory.Text = Trt.Tr("Invalid Title ID");
                    FlagGameSpecified = false;
                    MessageBox.Show(
                        Trt.Tr("Only 4 characters can be used, try again. Example: The Star Fox 64 (USA) Channel's Title ID is NADE01, so you would specify NADE as the Title ID"));
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
                GameSourceButton.Text = Trt.Tr("Game...");
                OpenGame.FileName = Trt.Tr("game");
                OpenGame.Filter = Trt.Tr("GameCube Dumps (*.gcm,*.iso)|*.gcm;*.iso");
                GameSourceDirectory.Text = Trt.Tr("Game file has not been specified");
                GameSourceDirectory.ForeColor = Color.Red;
                FlagGameSpecified = false;
                SystemType = "gcn";
                GameNameLabel.Text = "";
                TitleIDLabel.Text = "";
                TitleIDInt = 0;
                TitleIDHex = "";
                TitleFullIDHex = "";
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

        private void EnableSystemSelection(bool enabled)
        {
            WiiRetail.Enabled = enabled;
            WiiHomebrew.Enabled = enabled;
            WiiNAND.Enabled = enabled;
            GCRetail.Enabled = enabled;
        }

        private void CheckBuildRequirements()
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
            byte[] TitleFullIDBytes;

            if (String.IsNullOrEmpty(gameFilePath))
            {
                GameSourceDirectory.Text = Trt.Tr("Game file has not been specified");
                GameSourceDirectory.ForeColor = Color.Red;
                FlagGameSpecified = false;
                GameNameLabel.Text = "";
                TitleIDLabel.Text = "";
                TitleIDInt = 0;
                TitleIDHex = "";
                TitleFullIDHex = "";
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
                    reader.BaseStream.Position = 0x00;
                    TitleFullIDBytes = reader.ReadBytes(6);

                    //WBFS Check
                    if (TitleIDInt == 1397113431 /*'SFBW'*/) //Performs actions if the header indicates a WBFS file
                    {
                        FlagWBFS = true;
                        reader.BaseStream.Position = 0x200;
                        TitleIDInt = reader.ReadInt32();
                        reader.BaseStream.Position = 0x200;
                        TitleFullIDBytes = reader.ReadBytes(6);
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
                            reader.BaseStream.Position = 0x2A0;
                            TitleFullIDBytes = reader.ReadBytes(6);
                            InternalGameName = Trt.Tr("N/A");
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
                    GameSourceDirectory.Text = Trt.Tr("Game file has not been specified");
                    GameSourceDirectory.ForeColor = Color.Red;
                    FlagGameSpecified = false;
                    GameNameLabel.Text = "";
                    TitleIDLabel.Text = "";
                    TitleIDInt = 0;
                    TitleIDHex = "";
                    TitleFullIDHex = "";
                    GameType = 0;
                    CucholixRepoID = "";
                    PackedTitleLine1.Text = "";
                    PackedTitleIDLine.Text = "";
                    if (!silent)
                    {
                        MessageBox.Show(Trt.Tr("This is not a Wii image. It will not be loaded."));
                    }
                    return false;
                }
                if (SystemType == "gcn" && GameType != 4440324665927270400)
                {
                    GameSourceDirectory.Text = Trt.Tr("Game file has not been specified");
                    GameSourceDirectory.ForeColor = Color.Red;
                    FlagGameSpecified = false;
                    GameNameLabel.Text = "";
                    TitleIDLabel.Text = "";
                    TitleIDInt = 0;
                    TitleIDHex = "";
                    TitleFullIDHex = "";
                    GameType = 0;
                    CucholixRepoID = "";
                    PackedTitleLine1.Text = "";
                    PackedTitleIDLine.Text = "";
                    if (!silent)
                    {
                        MessageBox.Show(Trt.Tr("This is not a GameCube image. It will not be loaded."));
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

                TitleFullIDHex = BitConverter.ToString(TitleFullIDBytes).Replace("-", string.Empty);

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
                // Search for second disc
                if (GCRetail.Checked)
                {
                    string[] discs = SearchGCDiscs(OpenGame.FileName);
                    OpenGame.FileName = discs[0];
                    if (discs.Length > 1)
                    {
                        OpenGC2.FileName = discs[1];
                        SelectGC2Source(discs[1]);
                    }
                }

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
                IconSourceDirectory.Text = Trt.Tr("Icon has not been specified");
                IconSourceDirectory.ForeColor = Color.Red;
                FlagIconSpecified = false;
                FlagRepo = false;
            }
            else
            {
                try
                {
                    Image image = Draw.ResizeAndFitImage(LoadImage(filePath), IconSize);
                    Tga.saveTGA(image, PixelFormat.Format32bppArgb, TempIconPath);
                    IconPreviewBox.Image = image;
                    IconSourceDirectory.Text = filePath;
                    IconSourceDirectory.ForeColor = Color.Black;
                    FlagIconSpecified = true;
                    FlagRepo = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
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
            }
            MessageBox.Show(Trt.Tr("Make sure your icon is 128x128 (1:1) to prevent distortion"));
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
                BannerSourceDirectory.Text = Trt.Tr("Banner has not been specified");
                BannerSourceDirectory.ForeColor = Color.Red;
                FlagBannerSpecified = false;
                FlagRepo = false;
            }
            else
            {
                try
                {
                    Image image = Draw.ResizeAndFitImage(LoadImage(filePath), BannerSize);
                    Tga.saveTGA(image, PixelFormat.Format24bppRgb, TempBannerPath);
                    BannerPreviewBox.Image = image;
                    BannerSourceDirectory.Text = filePath;
                    BannerSourceDirectory.ForeColor = Color.Black;
                    FlagBannerSpecified = true;
                    FlagRepo = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
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
            }
            MessageBox.Show(Trt.Tr("Make sure your Banner is 1280x720 (16:9) to prevent distortion"));
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
                MessageBox.Show(Trt.Tr("Please select your game before using this option"));
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
                        DownloadImageFromRepo();
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
                            Trt.Tr("Failed to connect to Cucholix's Repo."),
                            Trt.Tr("Download Error"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return;
                    }
                }
            }

            FlagRepo = false;
            if (MessageBox.Show(
                Trt.Tr("Cucholix's Repo does not have assets for your game. You will need to provide your own. Would you like to visit the GBAtemp request thread?"),
                Trt.Tr("Game not found on Repo"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk)
                == DialogResult.Yes)
            {
                System.Diagnostics.Process.Start("https://gbatemp.net/threads/483080/");
            }
        }

        private void GenerateImage_Click(object sender, EventArgs e)
        {
            // Check if the required fields are fullfilled.
            if (String.IsNullOrWhiteSpace(GameNameLabel.Text))
            {
                MessageBox.Show(Trt.Tr("Please select your game before using this option"));
                return;
            }
            
            switch (GenerateImageBackgnd)
            {
                case GenerateImageBackgndSource.LocalDefault:
                    GenerateImageLocalDefault();
                    break;
                default:
                    GenerateImageGameTDB();
                    break;
            }
        }

        //Events for the "Optional Source Files" Tab
        private void GC2SourceButton_Click(object sender, EventArgs e)
        {
            if (OpenGC2.ShowDialog() == DialogResult.OK)
            {
                SelectGC2Source(OpenGC2.FileName);
            }
        }

        private void SelectGC2Source(string filePath)
        {

            if (String.IsNullOrEmpty(filePath))
            {
                GC2SourceDirectory.Text = Trt.Tr("2nd GameCube Disc Image has not been specified");
                GC2SourceDirectory.ForeColor = Color.Red;
                FlagGC2Specified = false;
            }
            else
            {
                using (var reader = new BinaryReader(File.OpenRead(filePath)))
                {
                    reader.BaseStream.Position = 0x18;
                    long GC2GameType = reader.ReadInt64();
                    if (GC2GameType != 4440324665927270400)
                    {
                        MessageBox.Show(Trt.Tr("This is not a GameCube image. It will not be loaded."));
                        GC2SourceDirectory.Text = Trt.Tr("2nd GameCube Disc Image has not been specified");
                        GC2SourceDirectory.ForeColor = Color.Red;
                        FlagGC2Specified = false;
                    }
                    else
                    {
                        GC2SourceDirectory.Text = filePath;
                        GC2SourceDirectory.ForeColor = Color.Black;
                        FlagGC2Specified = true;
                    }
                }
            }
        }

        private void SelectDrcSource(string filePath)
        {
            if (String.IsNullOrEmpty(filePath))
            {
                DrcPreviewBox.Image = null;
                DrcSourceDirectory.Text = Trt.Tr("GamePad Banner has not been specified");
                DrcSourceDirectory.ForeColor = Color.Red;
            }
            else
            {
                try
                {
                    Image image = Draw.ResizeAndFitImage(LoadImage(filePath), DrcSize);
                    Tga.saveTGA(image, PixelFormat.Format24bppRgb, TempDrcPath);
                    DrcPreviewBox.Image = image;
                    DrcSourceDirectory.Text = filePath;
                    DrcSourceDirectory.ForeColor = Color.Black;
                    FlagDrcSpecified = true;
                    FlagRepo = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        private void DrcSourceButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show(Trt.Tr("Make sure your GamePad Banner is 854x480 (16:9) to prevent distortion"));
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
                LogoSourceDirectory.Text = Trt.Tr("GamePad Banner has not been specified");
                LogoSourceDirectory.ForeColor = Color.Red;
            }
            else
            {
                try
                {
                    Image image = Draw.ResizeAndFitImage(LoadImage(filePath), LogoSize);
                    Tga.saveTGA(image, PixelFormat.Format32bppArgb, TempLogoPath);
                    LogoPreviewBox.Image = image;
                    LogoSourceDirectory.Text = filePath;
                    LogoSourceDirectory.ForeColor = Color.Black;
                    FlagLogoSpecified = true;
                    FlagRepo = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        private void LogoSourceButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show(Trt.Tr("Make sure your Logo is 170x42 to prevent distortion"));
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
            MessageBox.Show(Trt.Tr("Your sound file will be cut off if it's longer than 6 seconds to prevent the Wii U from not loading it. When the Wii U plays the boot sound, it will fade out once it's done loading the game (usually after about 5 seconds). You can not change this."));
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

                        bootSoundPlayer.Stop();
                        BootSoundPreviewButton.Text = Trt.Tr("Play Sound");
                        bootSoundPlayer.SoundLocation = OpenBootSound.FileName;
                    }
                    else
                    {
                        MessageBox.Show(Trt.Tr("This is not a valid WAV file. It will not be loaded. \nConsider converting it with something like Audacity."));
                        BootSoundDirectory.Text = Trt.Tr("Boot Sound has not been specified");
                        BootSoundDirectory.ForeColor = Color.Red;
                        BootSoundPreviewButton.Enabled = false;
                        FlagBootSoundSpecified = false;

                        bootSoundPlayer.Stop();
                        BootSoundPreviewButton.Text = Trt.Tr("Play Sound");
                        bootSoundPlayer.SoundLocation = string.Empty;
                    }
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
            if (string.IsNullOrEmpty(bootSoundPlayer.SoundLocation))
            {
                return;
            }

            if (BootSoundPreviewButton.Text == Trt.Tr("Stop Sound"))
            {
                bootSoundPlayer.Stop();
                BootSoundPreviewButton.Text = Trt.Tr("Play Sound");
            }
            else
            {
                if (ToggleBootSoundLoop.Checked)
                {
                    bootSoundPlayer.PlayLooping();
                    BootSoundPreviewButton.Text = Trt.Tr("Stop Sound");
                }
                else
                {
                    bootSoundPlayer.Play();
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
                PackedTitleLine2.Text = Trt.Tr("(Optional) Line 2");
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
                MainDolLabel.Text = Trt.Tr("<- Specify custom main.dol file");
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
                MainDolLabel.Text = Trt.Tr("<- Specify custom main.dol file");
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
                MessageBox.Show(Trt.Tr("The Wii U Starbuck Ancast Key has been verified."));
                AncastKey.ReadOnly = true;
                AncastKey.BackColor = Color.Lime;
            }
            else
            {
                MessageBox.Show(Trt.Tr("The Wii U Starbuck Ancast Key you have provided is incorrect" + "\n" + "(MD5 Hash verification failed)"));
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
                MessageBox.Show(Trt.Tr("The Wii U Common Key has been verified."));
                MainTabs.SelectedTab = AdvancedTab;
                MainTabs.SelectedTab = BuildTab;
            }
            else
            {
                MessageBox.Show(Trt.Tr("The Wii U Common Key you have provided is incorrect" + "\n" + "(MD5 Hash verification failed)"));
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
                MessageBox.Show(Trt.Tr("The Title Key has been verified."));
                MainTabs.SelectedTab = AdvancedTab;
                MainTabs.SelectedTab = BuildTab;
            }
            else
            {
                MessageBox.Show(Trt.Tr("The Title Key you have provided is incorrect" + "\n" + "(MD5 Hash verification failed)"));
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

        private string GetOutputFolder()
        {
            string regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            Regex r = new Regex(string.Format("[{0}]", Regex.Escape(regexSearch)));
            string escapedGameName = r.Replace(GameNameLabel.Text, "");

            return OutputDirectory.Text + Path.DirectorySeparatorChar + escapedGameName + " [" + PackedTitleIDLine.Text + "]";
        }

        private List<string> BuildGameTDBDownloadRetryList(bool banner = false)
        {
            List<string> retryList = new List<string>();

            string titleID = string.Join("", System.Text.RegularExpressions.Regex
                .Split(TitleFullIDHex, "(?<=\\G..)(?!$)")
                .Select(x => (char)Convert.ToByte(x, 16)));
            string baseUrl = $"{gameTDBBaseURL}/{(banner ? "cover3D" : "cover")}";

            foreach (string language in coverLanguages)
            {
                retryList.Add($"{baseUrl}/{language}/{titleID}.png");
                if (idMap.ContainsKey(titleID))
                {
                    foreach (string titleId in idMap[titleID])
                    {
                        retryList.Add($"{baseUrl}/{language}/{titleId}.png");
                    }
                }
            }

            return retryList;
        }

        private bool DownloadImagesFromGameTDB(string logoPath, string bannerPath)
        {
            bool result = false;

            try
            {
                using (var client = new WebClient())
                {
                    // Download logo
                    foreach (string url in BuildGameTDBDownloadRetryList())
                    {
                        if (!RemoteFileExists(url))
                            continue;

                        client.DownloadFile(url, logoPath);
                        result = true;
                        break;
                    }

                    // Download banner
                    if (result)
                    {
                        result = false;
                        foreach (string url in BuildGameTDBDownloadRetryList(true))
                        {
                            if (!RemoteFileExists(url))
                                continue;

                            client.DownloadFile(url, bannerPath);
                            result = true;
                            break;
                        }
                    }
                }
            }
            catch (Exception)
            {
                result = false;
            }

            return result;
        }

        private void GenerateLogo()
        {
            using (Stream stream = Resources.getResouceStream("wiiware.png"))
            {
                Image image = Draw.ResizeAndFitImage(Image.FromStream(stream), LogoSize);
                Tga.saveTGA(image, PixelFormat.Format32bppArgb, TempLogoPath);
                LogoPreviewBox.Image = image;
                LogoSourceDirectory.Text = Trt.Tr("Auto generated.");
                LogoSourceDirectory.ForeColor = Color.Green;
                FlagLogoSpecified = true;
            }
        }

        private Image AddCaptionToImage(Image image, string caption, int captionFontSize, Size imageOrigSize, string titleType)
        {
            Bitmap result = (Bitmap)image;

            System.Drawing.Text.PrivateFontCollection privateFonts = Fonts.craeteNewFontCollection(titleType + ".ttf");

            Color captionColor;
            if (titleType == "gc")
            {
                captionColor = Color.FromArgb(100, 80, 151);
            }
            else
            {
                captionColor = Color.FromArgb(139, 139, 139);
            }

            using (SolidBrush brush = new SolidBrush(captionColor))
            using (Font font = new Font(privateFonts.Families[0], captionFontSize))
            {
                Graphics gfx = Graphics.FromImage(result);
                Draw.SetGraphicsBestQuility(ref gfx);

                StringFormat captionFormat = new StringFormat();
                captionFormat.Alignment = StringAlignment.Center;
                captionFormat.LineAlignment = StringAlignment.Center;

                int height = (image.Height - imageOrigSize.Height) / 2;

                gfx.DrawString(
                    caption,
                    font,
                    brush,
                    new RectangleF(0, image.Height - height, image.Width, height),
                    captionFormat);
            }

            return result;
        }

        private void GenerateImageGameTDB()
        {
            string logoPath = Path.Combine(TempSourcePath, "logo.png");
            string bannerPath = Path.Combine(TempSourcePath, "banner.png");

            if (!DownloadImagesFromGameTDB(logoPath, bannerPath))
            {
                MessageBox.Show(Trt.Tr("Fail to download images from GameTDB.com."));
                return;
            }

            string titleType = "wii";
            if (GCRetail.Checked)
            {
                titleType = "gc";
            }

            Size newSize;
            Image image;

            // Prepare icon
            image = Draw.ResizeAndFitImage(LoadImage(logoPath), IconSize);
            Tga.saveTGA(image, PixelFormat.Format32bppArgb, TempIconPath);
            IconPreviewBox.Image = image;
            IconSourceDirectory.Text = Trt.Tr("Auto generated.");
            IconSourceDirectory.ForeColor = Color.Green;
            FlagIconSpecified = true;

            // Prepare banner
            newSize = new Size(176, 248);
            image = Draw.ResizeAndFitImage(LoadImage(bannerPath), newSize, BannerSize);
            image = AddCaptionToImage(image, GameNameLabel.Text, 40, newSize, titleType);
            Tga.saveTGA(image, PixelFormat.Format24bppRgb, TempBannerPath);
            BannerPreviewBox.Image = image;
            BannerSourceDirectory.Text = Trt.Tr("Auto generated.");
            BannerSourceDirectory.ForeColor = Color.Green;
            FlagBannerSpecified = true;

            // GamePad banner
            newSize = new Size(132, 186);
            image = Draw.ResizeAndFitImage(LoadImage(bannerPath), newSize, DrcSize);
            image = AddCaptionToImage(image, GameNameLabel.Text, 30, newSize, titleType);
            Tga.saveTGA(image, PixelFormat.Format24bppRgb, TempDrcPath);
            DrcPreviewBox.Image = image;
            DrcSourceDirectory.Text = Trt.Tr("Auto generated.");
            DrcSourceDirectory.ForeColor = Color.Green;
            FlagDrcSpecified = true;

            // Logo
            GenerateLogo();

            FlagRepo = false;

            File.Delete(logoPath);
            File.Delete(bannerPath);
        }

        private struct WiiVcGenerateImage
        {
            public Bitmap bitmap;
            public Rectangle rectangle;
            public string captain;
            public string savePath;
            public string dirControlName;
            public string previewControlName;
            public Color foreColor;
            public bool adjustedFontByTextRenderer;
            public bool drawStringByTextRenderer;
        };

        private void GenerateImageLocalDefault()
        {
            // Setup font used for drawing.
            Font arialFont = new Font("Arial", 10);

            // Setup temp directory for generated images.
            string saveDir = GetAppTempPath() + "WiiVCInjector\\SOURCETEMP\\";
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
                    captain = GameNameLabel.Text,
                    savePath = saveDir + "iconTex.tga",
                    dirControlName = "IconSourceDirectory",
                    previewControlName = "IconPreviewBox",
                    foreColor = Color.Black,
                    adjustedFontByTextRenderer = true,
                    drawStringByTextRenderer = false,
                },
                new WiiVcGenerateImage {
                    bitmap = Properties.Resources.universal_Wii_WiiWare_template_bootTvTex,
                    rectangle = new Rectangle(224, 210, 820, 320),
                    captain = GameNameLabel.Text,
                    savePath = saveDir + "bootTvTex.tga",
                    dirControlName = "BannerSourceDirectory",
                    previewControlName = "BannerPreviewBox",
                    foreColor = Color.Black,
                    adjustedFontByTextRenderer = false,
                    drawStringByTextRenderer = false,
                },
                new WiiVcGenerateImage {
                    bitmap = bitmapGamePadBar,
                    rectangle = new Rectangle(148, 138, 556, 212),
                    captain = GameNameLabel.Text,
                    savePath = saveDir + "bootDrcTex.tga",
                    dirControlName = "DrcSourceDirectory",
                    previewControlName = "DrcPreviewBox",
                    foreColor = Color.Black,
                    adjustedFontByTextRenderer = false,
                    drawStringByTextRenderer = false,
                },
                new WiiVcGenerateImage {
                    bitmap = bitmapBootLogo,
                    rectangle = new Rectangle(0, 0, 170, 42),
                    captain = "WiiWare",
                    savePath = saveDir + "bootLogoTex.tga",
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
                    images[i].captain,
                    images[i].rectangle,
                    arialFont,
                    images[i].foreColor,
                    images[i].adjustedFontByTextRenderer,
                    images[i].drawStringByTextRenderer);

                // Save the completed image to temp directory.
                string pngPath = images[i].savePath + ".png";
                images[i].bitmap.Save(pngPath);

                // Show the preview image to user.
                FileStream tempstream = new FileStream(pngPath, FileMode.Open);
                var tempimage = Image.FromStream(tempstream);
                PictureBox previewBox = this.Controls.Find(images[i].previewControlName, true).FirstOrDefault() as PictureBox;
                previewBox.Image = tempimage;
                tempstream.Close();

                // Convert to TGA immediately.
                Tga.saveTGA(images[i].bitmap, PixelFormat.Format24bppRgb, images[i].savePath);
                File.Delete(pngPath);

                // Set the text information of the edit control
                // which indicates the image source path.
                Label sourceDirectory = this.Controls.Find(images[i].dirControlName, true).FirstOrDefault() as Label;
                sourceDirectory.Text = Trt.Tr("Auto generated.");
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
                    //detect whether its a directory or file
                    if (Directory.Exists(s))
                    {
                        foreach (string file in "*.iso|*.wbfs".Split('|').SelectMany(
                            pattern => Directory.EnumerateFiles(s, pattern, System.IO.SearchOption.AllDirectories)))
                        {
                            Program.AppendAutoBuildList(file);
                        }
                    }
                    else
                    {
                        Program.AppendAutoBuildList(s);
                    }
                }
            }

            AutoBuild();
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
            if (Misc.PathEquals(source, destination))
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
                Process.Start(OutputDirectory.Text);
            }
        }

        private void OpenTempDirButton_Click(object sender, EventArgs e)
        {
            if (Directory.Exists(TemporaryDirectory.Text))
            {
                Process.Start(TemporaryDirectory.Text);
            }
        }

        private void LogLevelBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            currentLogLevel = LogLevel.getLogLevelByName(LogLevelBox.SelectedItem.ToString());
            RegistryKey appKey = Registry.CurrentUser.CreateSubKey("WiiVCInjector");
            appKey.SetValue("LogLevel", currentLogLevel.Level.ToString());
            appKey.Close();
        }

        private void GenerateImageMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //
            // If we use WPF, it will be very smart to do this in 'Binding' way. :P
            //
            foreach (ToolStripMenuItem item in GenerateImageMenu.Items)
            {
                item.Checked = item.Tag.Equals(GenerateImageBackgnd);
            }
        }

        private void GenerateImageMenu_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            GenerateImageBackgnd = (GenerateImageBackgndSource)e.ClickedItem.Tag;
            Registry.CurrentUser.CreateSubKey("WiiVCInjector")
                .SetValue("GenerateImageBackgndSource", GenerateImageBackgnd);
        }
    }
}