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
            string[] tempImages =
            {
                "iconTex.tga",
                "bootTvTex.tga",
                "bootDrcTex.tga",
                "bootLogoTex.tga"
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
        Thread BuilderThread;
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

        private enum GenerateImageBackgndSource
        {
            DownloadFromGameTDB,
            LocalDefault,
        };

        private GenerateImageBackgndSource GenerateImageBackgnd { get; set; } = GenerateImageBackgndSource.DownloadFromGameTDB;

        private delegate bool BuildAction();

        private event EventHandler<bool> BuildCompletedEx;

        private static string GetAppTempPath(bool endWithPathSeparator = true)
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

            if (endWithPathSeparator)
            {
                if (!path.EndsWith(Path.PathSeparator.ToString()))
                {
                    path += Path.PathSeparator;
                }
            }
            else
            {
                path = path.TrimEnd(new char[] { Path.PathSeparator });
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

            Image image = ResizeAndFitImage(LoadImage(LocalDst), IconSize);
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

            image = ResizeAndFitImage(LoadImage(LocalDst), BannerSize);
            Tga.saveTGA(image, PixelFormat.Format24bppRgb, TempBannerPath);

            BannerPreviewBox.Image = image;
            BannerSourceDirectory.Text = Trt.Tr("bootTvTex.png downloaded from Cucholix's Repo");
            BannerSourceDirectory.ForeColor = Color.Black;
            FlagBannerSpecified = true;

            image = ResizeAndFitImage(LoadImage(LocalDst), DrcSize);
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
                    Image image = ResizeAndFitImage(LoadImage(filePath), IconSize);
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
                    Image image = ResizeAndFitImage(LoadImage(filePath), BannerSize);
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
                    Image image = ResizeAndFitImage(LoadImage(filePath), DrcSize);
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
                    Image image = ResizeAndFitImage(LoadImage(filePath), LogoSize);
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
                    }
                    else
                    {
                        MessageBox.Show(Trt.Tr("This is not a valid WAV file. It will not be loaded. \nConsider converting it with something like Audacity."));
                        BootSoundDirectory.Text = Trt.Tr("Boot Sound has not been specified");
                        BootSoundDirectory.ForeColor = Color.Red;
                        BootSoundPreviewButton.Enabled = false;
                        FlagBootSoundSpecified = false;
                    }
                }
            }
            else
            {
                if (BootSoundPreviewButton.Text != Trt.Tr("Stop Sound"))
                {
                    BootSoundDirectory.Text = Trt.Tr("Boot Sound has not been specified");
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
            if (BootSoundPreviewButton.Text == Trt.Tr("Stop Sound"))
            {
                simpleSound.Stop();
                BootSoundPreviewButton.Text = Trt.Tr("Play Sound");
            }
            else
            {
                if (ToggleBootSoundLoop.Checked)
                {
                    simpleSound.PlayLooping();
                    BootSoundPreviewButton.Text = Trt.Tr("Stop Sound");
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

            BeginInvoke(new Action<bool>((Succeed) =>
            {
                this.BuildCompleted(Succeed);
            }), buildSucceed);
        }

        private void ToggleBuild()
        {
            if (IsBuilding)
            {
                DialogResult dialogResult = MessageBox.Show(
                    Trt.Tr("Are you sure to cancel the current build progress?"),
                    Trt.Tr("Stop building"),
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
            TheBigOneTM.Text = Trt.Tr("STOP");

            // 
            // Reset build status ui indicators.
            //
            BuildStatus.Text = "";
            BuildStatus.ForeColor = Color.Black;

            //
            // Reset build output.
            //
            if (currentLogLevel <= LogLevel.Debug)
            {
                BuildOutput.ResetText();
            }

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
                MessageBox.Show(Trt.Tr("Conversion Complete! Your packed game can be found here: ")
                    + GetOutputFolder()
                    + Trt.Tr(".\n\nInstall your title using WUP Installer GX2 with signature patches enabled (CBHC, Haxchi, etc). Make sure you have signature patches enabled when launching your title.\n\n Click OK to continue..."),
                    PackedTitleLine1.Text + Trt.Tr(" Conversion Complete..."));
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
            TheBigOneTM.Text = Trt.Tr("BUILD");
            BuildStatus.Text = "";
            BuildProgress.Value = 0;

            BuildCompletedEx?.Invoke(this, succeed);

            if (!InClosing)
            {
                BuildOutputItem buildResult = new BuildOutputItem
                {
                    Output = "\n"
                };

                if (succeed)
                {
                    buildResult.Output += Trt.Tr("Build succeed.");
                    if (Program.AutoBuildList.Count > 1)
                        buildResult.Output += Trt.Tr(String.Format("Left [{0}].", Program.AutoBuildList.Count));
                    buildResult.OutputType = BuildOutputType.Succeed;
                }
                else
                {
                    if (LastBuildCancelled)
                    {
                        buildResult.Output += Trt.Tr("Build cancelled.");
                        buildResult.OutputType = BuildOutputType.Error;
                    }
                    else
                    {
                        buildResult.Output += Trt.Tr("Build failed.");
                        buildResult.OutputType = BuildOutputType.Error;
                    }
                }


                buildResult.Output += String.Format("({0})", BuildStopwatch.Elapsed.Duration().ToString());
                buildResult.Output += Environment.NewLine;
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
            Invoke(new Action(() => { TemporaryDirectory.Text = TemporaryDirectory.Text.Trim(); }));

            string tempDir = TemporaryDirectory.Text;

            if (String.IsNullOrWhiteSpace(tempDir))
            {
                tempDir = GetAppTempPath();
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
                Trt.Tr("Create temporary directory failed, it may be caused by "
                + "low space on hard drive, permission denied or invalid path name."),
                Trt.Tr("Error"));

            //
            // Restore the default temp dir location on failed.
            //
            Invoke(new Action(() => { TemporaryDirectory.Text = Path.GetTempPath(); }));
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
                    Trt.Tr("Your hard drive may be low on space. The conversion process involves temporary files that can amount to more than double the size of your game. If you continue without clearing some hard drive space, the conversion may fail. Do you want to continue anyways?"),
                    Trt.Tr("Check your hard drive space"), MessageBoxButtons.YesNo);
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
            Invoke(new Action(() => { OutputDirectory.Text = OutputDirectory.Text.Trim(); }));

            if (String.IsNullOrEmpty(OutputDirectory.Text))
            {
                FolderBrowserDialog OutputFolderSelect = new FolderBrowserDialog();
                if (OutputFolderSelect.ShowDialog() == DialogResult.Cancel)
                {
                    MessageBox.Show(Trt.Tr("Output folder selection has been cancelled, conversion will not continue."));
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
                        Trt.Tr("Can't create the specified output directory, "
                        + "conversion will not continue.\n"
                        + "Additional error information: ")
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
                    Trt.Tr("Your internet connection could not be verified, do you wish to try and download the necessary base files from Nintendo anyways? (This is a one-time download)"),
                    Trt.Tr("Internet Connection Verification Failed"), MessageBoxButtons.YesNo);
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
                Trt.Tr("(One-Time Download) Downloading base files from Nintendo..."));

            string[] JNUSToolConfig = { "http://ccs.cdn.wup.shop.nintendo.net/ccs/download", WiiUCommonKey.Text };
            File.WriteAllLines(TempToolsPath + "JAR\\config", JNUSToolConfig);
            Directory.SetCurrentDirectory(TempToolsPath + "JAR");

            Invoke(ActBuildProgress, 10);

            JNUSStuffsDownloadItem[] downloadItems = new JNUSStuffsDownloadItem[]{
                new JNUSStuffsDownloadItem {
                    buildStatus = Trt.Tr("(One-Time Download) Downloading base files from Nintendo... (deint.txt)"),
                    exeArgs = "0005001010004000 -file /code/deint.txt",
                    progress = 12,
                },
                new JNUSStuffsDownloadItem {
                    buildStatus = Trt.Tr("(One-Time Download) Downloading base files from Nintendo... (font.bin)"),
                    exeArgs = "0005001010004000 -file /code/font.bin",
                    progress = 15,
                },
                new JNUSStuffsDownloadItem {
                    buildStatus = Trt.Tr("(One-Time Download) Downloading base files from Nintendo... (c2w.img)"),
                    exeArgs = "0005001010004001 -file /code/c2w.img",
                    progress = 17,
                },
                new JNUSStuffsDownloadItem {
                    buildStatus = Trt.Tr("(One-Time Download) Downloading base files from Nintendo... (boot.bin)"),
                    exeArgs = "0005001010004001 -file /code/boot.bin",
                    progress = 20,
                },
                new JNUSStuffsDownloadItem {
                    buildStatus = Trt.Tr("(One-Time Download) Downloading base files from Nintendo... (dmcu.d.hex)"),
                    exeArgs = "0005001010004001 -file /code/dmcu.d.hex",
                    progress = 23,
                },
                new JNUSStuffsDownloadItem {
                    buildStatus = Trt.Tr("(One-Time Download) Downloading base files from Nintendo... (cos.xml)"),
                    exeArgs = "00050000101b0700 " + TitleKey.Text + " -file /code/cos.xml",
                    progress = 25,
                },
                new JNUSStuffsDownloadItem {
                    buildStatus = Trt.Tr("(One-Time Download) Downloading base files from Nintendo... (frisbiiU.rpx)"),
                    exeArgs = "00050000101b0700 " + TitleKey.Text + " -file /code/frisbiiU.rpx",
                    progress = 27,
                },
                new JNUSStuffsDownloadItem {
                    buildStatus = Trt.Tr("(One-Time Download) Downloading base files from Nintendo... (fw.img)"),
                    exeArgs = "00050000101b0700 " + TitleKey.Text + " -file /code/fw.img",
                    progress = 30,
                },
                new JNUSStuffsDownloadItem {
                    buildStatus = Trt.Tr("(One-Time Download) Downloading base files from Nintendo... (fw.tmd)"),
                    exeArgs = "00050000101b0700 " + TitleKey.Text + " -file /code/fw.tmd",
                    progress = 32,
                },
                new JNUSStuffsDownloadItem {
                    buildStatus = Trt.Tr("(One-Time Download) Downloading base files from Nintendo... (htk.bin)"),
                    exeArgs = "00050000101b0700 " + TitleKey.Text + " -file /code/htk.bin",
                    progress = 35,
                },
                new JNUSStuffsDownloadItem {
                    buildStatus = Trt.Tr("(One-Time Download) Downloading base files from Nintendo... (nn_hai_user.rpl)"),
                    exeArgs = "00050000101b0700 " + TitleKey.Text + " -file /code/nn_hai_user.rpl",
                    progress = 37,
                },
                new JNUSStuffsDownloadItem {
                    buildStatus = Trt.Tr("(One-Time Download) Downloading base files from Nintendo... (banner.gsh / fade.gsh)"),
                    exeArgs = "00050000101b0700 " + TitleKey.Text + " -file /content/assets/.*",
                    progress = 40,
                },
                new JNUSStuffsDownloadItem {
                    buildStatus = Trt.Tr("(One-Time Download) Downloading base files from Nintendo... (bootMovie.h264)"),
                    exeArgs = "00050000101b0700 " + TitleKey.Text + " -file /meta/bootMovie.h264",
                    progress = 42,
                },
                new JNUSStuffsDownloadItem {
                    buildStatus = Trt.Tr("(One-Time Download) Downloading base files from Nintendo... (bootLogoTex.tga)"),
                    exeArgs = "00050000101b0700 " + TitleKey.Text + " -file /meta/bootLogoTex.tga",
                    progress = 45,
                },
                new JNUSStuffsDownloadItem {
                    buildStatus = Trt.Tr("(One-Time Download) Downloading base files from Nintendo... (bootSound.btsnd)"),
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
                    MessageBox.Show(Trt.Tr("Failed to download base files using JNUSTool, conversion will not continue"));
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

        private string EscapeXml(string str)
        {
            return System.Security.SecurityElement.Escape(str);
        }

        private bool GeneratePackXmls()
        {
            //
            // Generate app.xml & meta.xml
            //
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

            string longname = EscapeXml(PackedTitleLine1.Text);
            string shortname = longname;

            if (EnablePackedLine2.Checked && !String.IsNullOrWhiteSpace(PackedTitleLine2.Text))
            {
                longname += "&#x000A;" + EscapeXml(PackedTitleLine2.Text);
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
                "  <shortname_ja type=\"string\" length=\"512\">" + shortname + "</shortname_ja>",
                "  <shortname_en type=\"string\" length=\"512\">" + shortname + "</shortname_en>",
                "  <shortname_fr type=\"string\" length=\"512\">" + shortname + "</shortname_fr>",
                "  <shortname_de type=\"string\" length=\"512\">" + shortname + "</shortname_de>",
                "  <shortname_it type=\"string\" length=\"512\">" + shortname + "</shortname_it>",
                "  <shortname_es type=\"string\" length=\"512\">" + shortname + "</shortname_es>",
                "  <shortname_zhs type=\"string\" length=\"512\">" + shortname + "</shortname_zhs>",
                "  <shortname_ko type=\"string\" length=\"512\">" + shortname + "</shortname_ko>",
                "  <shortname_nl type=\"string\" length=\"512\">" + shortname + "</shortname_nl>",
                "  <shortname_pt type=\"string\" length=\"512\">" + shortname + "</shortname_pt>",
                "  <shortname_ru type=\"string\" length=\"512\">" + shortname + "</shortname_ru>",
                "  <shortname_zht type=\"string\" length=\"512\">" + shortname + "</shortname_zht>",
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

        private Image LoadImage(string imagePath)
        {
            Image result = null;

            using (FileStream stream = new FileStream(imagePath, FileMode.Open))
            {
                if (Path.GetExtension(imagePath) == ".tga")
                {
                    result = Tga.loadTga(stream);
                }
                else
                {
                    result = Image.FromStream(stream);
                }
            }

            return result;
        }

        private bool PrepareImages()
        {
            File.Copy(TempIconPath, Path.Combine(TempBuildPath, "meta", Path.GetFileName(TempIconPath)));
            File.Copy(TempBannerPath, Path.Combine(TempBuildPath, "meta", Path.GetFileName(TempBannerPath)));

            if (!FlagDrcSpecified)
            {
                Image image = ResizeAndFitImage(LoadImage(TempBannerPath), DrcSize);
                Tga.saveTGA(image, PixelFormat.Format24bppRgb, Path.Combine(TempBuildPath, "meta", Path.GetFileName(TempDrcPath)));
                DrcPreviewBox.Image = image;
                DrcSourceDirectory.Text = Trt.Tr("Auto generated.");
                DrcSourceDirectory.ForeColor = Color.Green;
                FlagDrcSpecified = true;

            }
            else
            {
                File.Copy(TempDrcPath, Path.Combine(TempBuildPath, "meta", Path.GetFileName(TempDrcPath)));
            }

            if (!FlagLogoSpecified)
            {
                GenerateLogo();
            }
            File.Copy(TempLogoPath, Path.Combine(TempBuildPath, "meta", Path.GetFileName(TempLogoPath)), true);

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
                        MessageBox.Show(
                            Trt.Tr("The Wii Video Mode Changer will now be launched. "
                            + "I recommend using the Smart Patcher option. \n\n"
                            + "If you're scared and don't know what you're doing, "
                            + "close the patcher window and nothing will be patched. \n\n"
                            + "Click OK to continue..."));
                        HideProcess = false;
                        LauncherExeFile = TempToolsPath + "EXE\\wii-vmc.exe";
                        LauncherExeArgs = "\"" + TempSourcePath + "ISOEXTRACT\\sys\\main.dol\"";
                        LaunchProgram();
                        HideProcess = true;
                        MessageBox.Show(Trt.Tr("Conversion will now continue..."));
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

        private string GetOutputFolder()
        {
            string regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            Regex r = new Regex(string.Format("[{0}]", Regex.Escape(regexSearch)));
            string escapedGameName = r.Replace(GameNameLabel.Text, "");

            return OutputDirectory.Text + Path.DirectorySeparatorChar + escapedGameName + " [" + PackedTitleIDLine.Text + "]";
        }

        private bool NUSPackerEncrypt()
        {
            //
            // Encrypt contents with NUSPacker
            //
            Directory.SetCurrentDirectory(TempRootPath);
            LauncherExeFile = TempToolsPath + "JAR\\NUSPacker.exe";
            LauncherExeArgs = "-in BUILDDIR -out \"" + GetOutputFolder() + "\" -encryptKeyWith " + WiiUCommonKey.Text;
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

        private struct BuildStep
        {
            public BuildAction buildAction;
            public string description;
            public int progressWeight;
        };

        private bool BuildPack()
        {
            BuildStep[] buildSteps = new BuildStep[]
            {
                new BuildStep
                {
                    buildAction = PrepareTemporaryDirectory,
                    description = Trt.Tr("Checking temporary directory"),
                    progressWeight = 1,
                },
                new BuildStep
                {
                    buildAction = CheckFreeDiskSpaceForPack,
                    description = Trt.Tr("Checking free disk space"),
                    progressWeight = 1,
                },
                new BuildStep
                {
                    buildAction = PrepareOutputDirectory,
                    description = Trt.Tr("Checking output directory"),
                    progressWeight = 1,
                },
                new BuildStep
                {
                    buildAction = PrepareJNUSStuffs,
                    description = Trt.Tr("Checking JNUS stuffs"),
                    progressWeight = 3,
                },
                new BuildStep
                {
                    buildAction = PrepareBasicFilesForPack,
                    description = Trt.Tr("Copying base files to temporary build directory"),
                    progressWeight = 1,
                },
                new BuildStep
                {
                    buildAction = GeneratePackXmls,
                    description = Trt.Tr("Generating app.xml and meta.xml"),
                    progressWeight = 1,
                },
                new BuildStep
                {
                    buildAction = PrepareImages,
                    description = Trt.Tr("Converting all image sources to expected TGA specification"),
                    progressWeight = 1,
                },
                new BuildStep
                {
                    buildAction = ConvertBootSoundFormat,
                    description = Trt.Tr("Converting user provided sound to btsnd format"),
                    progressWeight = 1,
                },
                new BuildStep
                {
                    buildAction = BuildIso,
                    description = Trt.Tr("Processing game for NFS Conversion"),
                    progressWeight = 30,
                },
                new BuildStep
                {
                    buildAction = ConvertIsoToNFS,
                    description = Trt.Tr("Converting processed game to NFS format"),
                    progressWeight = 15,
                },
                new BuildStep
                {
                    buildAction = NUSPackerEncrypt,
                    description = Trt.Tr("Encrypting contents into installable WUP Package"),
                    progressWeight = 30,
                },
            };

            ThrowProcessException = true;
            int succeed = 0;

            BeginInvoke(ActBuildOutput, new BuildOutputItem()
            {
                Output = string.Format(Trt.Tr("Processing [{0}] [{1}]..."), GameNameLabel.Text, GameSourceDirectory.Text) + Environment.NewLine,
                OutputType = BuildOutputType.Step,
            });

            Stopwatch stepStopwatch = new Stopwatch();

            for (int i = 0; i < buildSteps.Length; ++i)
            {
                BuildStep buildStep = buildSteps[i];

                if (LastBuildCancelled)
                {
                    break;
                }

                try
                {
                    string buildStatus = $"({i + 1}/{buildSteps.Length}){buildStep.description}...";

                    BeginInvoke(ActBuildStatus, buildStatus);

                    BeginInvoke(ActBuildOutput, new BuildOutputItem()
                    {
                        Output = buildStatus + Environment.NewLine,
                        OutputType = BuildOutputType.Step,
                    });

                    stepStopwatch.Restart();

                    if (!buildStep.buildAction())
                    {
                        BeginInvoke(ActBuildOutput, new BuildOutputItem()
                        {
                            Output = buildStep.description + Trt.Tr("failed.") + Environment.NewLine + Environment.NewLine,
                            OutputType = BuildOutputType.Error,
                        });
                        break;
                    }

                    stepStopwatch.Stop();

                    BeginInvoke(ActBuildOutput, new BuildOutputItem()
                    {
                        Output = buildStep.description + "..." + Trt.Tr("done.")
                               + $"({stepStopwatch.Elapsed.Duration()})"
                               + Environment.NewLine + Environment.NewLine,
                        OutputType = BuildOutputType.Step,
                    });

                    ++succeed;
                }
                catch (Exception ex)
                {
                    Console.Write("buildStep throws an exception: " + ex.Message);
                    BeginInvoke(ActBuildOutput, new BuildOutputItem()
                    {
                        Output = buildStep.description + Trt.Tr(" terminated unexpectedly: ") 
                               + ex.Message + Environment.NewLine + Environment.NewLine,
                        OutputType = BuildOutputType.Error,
                    });

                    break;
                }
            }

            BuildCleanup();

            Invoke(ActBuildStatus, Trt.Tr("Conversion complete..."));

            return (succeed == buildSteps.Length);
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

        private void SetGraphicsBestQuility(ref Graphics gfx)
        {
            gfx.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            gfx.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            gfx.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            gfx.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            gfx.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        }

        private Image ResizeAndFitImage(Image image, Size newSize)
        {
            return ResizeAndFitImage(image, newSize, newSize);
        }

        public Image ResizeAndFitImage(Image image, Size newSize, Size fillSize)
        {
            Bitmap result = new Bitmap(fillSize.Width, fillSize.Height);
            result.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            Graphics gfx = Graphics.FromImage(result);
            SetGraphicsBestQuility(ref gfx);
            gfx.FillRectangle(Brushes.White, 0, 0, fillSize.Width, fillSize.Height);

            using (var wrapMode = new ImageAttributes())
            {
                wrapMode.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);

                int y = (fillSize.Height / 2) - newSize.Height / 2;
                int x = (fillSize.Width / 2) - newSize.Width / 2;

                Rectangle destRect = new Rectangle(x, y, newSize.Width, newSize.Height);
                gfx.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
            }

            gfx.Dispose();
            image.Dispose();
            return result;

        }

        private void GenerateLogo()
        {
            using (Stream stream = Resources.getResouceStream("wiiware.png"))
            {
                Image image = ResizeAndFitImage(Image.FromStream(stream), LogoSize);
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
                SetGraphicsBestQuility(ref gfx);

                StringFormat captionFormat = new StringFormat();
                captionFormat.Alignment = StringAlignment.Center;
                captionFormat.LineAlignment = StringAlignment.Center;

                int height = (image.Height - imageOrigSize.Height) / 2;

                gfx.DrawString(caption,
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
            image = ResizeAndFitImage(LoadImage(logoPath), IconSize);
            Tga.saveTGA(image, PixelFormat.Format32bppArgb, TempIconPath);
            IconPreviewBox.Image = image;
            IconSourceDirectory.Text = Trt.Tr("Auto generated.");
            IconSourceDirectory.ForeColor = Color.Green;
            FlagIconSpecified = true;

            // Prepare banner
            newSize = new Size(176, 248);
            image = ResizeAndFitImage(LoadImage(bannerPath), newSize, BannerSize);
            image = AddCaptionToImage(image, GameNameLabel.Text, 40, newSize, titleType);
            Tga.saveTGA(image, PixelFormat.Format24bppRgb, TempBannerPath);
            BannerPreviewBox.Image = image;
            BannerSourceDirectory.Text = Trt.Tr("Auto generated.");
            BannerSourceDirectory.ForeColor = Color.Green;
            FlagBannerSpecified = true;

            // GamePad banner
            newSize = new Size(132, 186);
            image = ResizeAndFitImage(LoadImage(bannerPath), newSize, DrcSize);
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
            public string s;
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