using ImageUtils;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using TeconMoon_WiiVC_Injector_Jam.Utils;
using TeconMoon_WiiVC_Injector_Jam.Utils.Build;

namespace TeconMoon_WiiVC_Injector_Jam
{
    partial class WiiVC_Injector
    {
        private delegate bool BuildAction();

        private event EventHandler PreBuild;
        private event EventHandler<bool> PostBuild;

        private Thread BuilderThread;

        private Action<string> ActBuildStatus;
        private Action<int> ActBuildProgress;
        private Action<BuildOutputItem> ActBuildOutput;

        private bool IsBuilding => BuilderThread != null;

        private Stopwatch BuildStopwatch { get; } = new Stopwatch();

		private static readonly string commonWiiUId0 = Plop("\uffd0\uffd0\uffd0ￋ\uffd0\uffd0ￏ\uffd0ￏ\uffd0\uffd0\uffd0ￌ\uffd0\uffd0\uffd0"); // 0005001010004000
		private static readonly string commonWiiUId1 = Plop("\uffd0\uffd0\uffd0ￋ\uffd0\uffd0ￏ\uffd0ￏ\uffd0\uffd0\uffd0ￌ\uffd0\uffd0ￏ"); // 0005001010004001
		private static readonly string titleId = Plop("\uffd0\uffd0\uffd0ￋ\uffd0\uffd0\uffd0\uffd0ￏ\uffd0ￏﾞ\uffd0\uffc9\uffd0\uffd0"); // 00050000101b0700
		private static readonly string titleGameTitleName = Plop("ﾮﾘﾇﾌﾘﾓ￠ﾸﾛﾟﾊﾛﾒ￠ﾺﾛﾊﾛﾎ"); // Rhythm Heaven Fever
		private static readonly string titleGameTitleNameTiedToJame = $"{titleGameTitleName} [VAKE01]";

		private static string Plop(string s)
		{
			var n = s.Select(ch => (char)(ch * -1)).ToArray();
			return new string(n);
		}

		private struct BuildStep
        {
            public BuildAction buildAction;
            public string description;
            public int progressWeight;
        };

        private void InitializeBuildActions()
        {
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
        }

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
                Output = string.Format(
                    Trt.Tr("Processing [{0}] [{1}]..."),
                    GameNameLabel.Text, 
                    GameSourceDirectory.Text)
                    + Environment.NewLine
                    + Environment.NewLine,
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

        private void CancelBuild()
        {
            TheBigOneTM.Enabled = false;
            LastBuildCancelled = true;
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
                PreBuild = (s, e) =>
                {
                    PromptForSucceed = true;
                    BuildOutput.ResetText();
                    PrintBuildOverview();
                };

                BuildAnsync();
            }
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

        private bool BuildAnsync()
        {
            //
            // Disable form elements so navigation can't be 
            // attempted during build process.
            //
            FreezeFormBuild(true);
            TheBigOneTM.Text = Trt.Tr("STOP");

            //
            // Stop boot sound preview.
            //
            bootSoundPlayer.Stop();
            BootSoundPreviewButton.Text = Trt.Tr("Play Sound");

            // 
            // Reset build status ui indicators.
            //
            BuildStatus.Text = "";
            BuildStatus.ForeColor = Color.Black;

            //
            // Fire 'PreBuild' event.
            //
            PreBuild?.Invoke(this, new EventArgs());

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

            if (succeed && PromptForSucceed && !InClosing)
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

            PostBuild?.Invoke(this, succeed);

            if (!InClosing)
            {
                BuildOutputItem buildResult = new BuildOutputItem
                {
                    Output = Environment.NewLine
                };

                if (succeed)
                {
                    buildResult.Output += Trt.Tr("Build succeed.");
                    if (Program.BatchBuildList.Count > 1)
                        buildResult.Output += string.Format(Trt.Tr("Left [{0}]."), 
                            Program.BatchBuildList.Count);
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


                buildResult.Output += $"({BuildStopwatch.Elapsed.Duration()})";
                buildResult.Output += Environment.NewLine;
                AppendBuildOutput(buildResult);
            }
        }

        private string GetCurrentBuildTypeString()
        {
            RadioButton[] typeButtons = new RadioButton[]
            {
                WiiRetail,
                WiiHomebrew,
                GCRetail,
                WiiNAND,
            };

            return (from rb in typeButtons
                    where rb.Checked
                    select rb.Text).First();
        }

        private string GetBatchBuildListString()
        {
            StringBuilder stringBuilder = new StringBuilder(4096);

            for (int i = 0; i < Program.BatchBuildList.Count(); ++i)
            {
                stringBuilder.AppendLine();
                stringBuilder.AppendFormat("{0}.{1}", 
                    i + 1, Program.BatchBuildList[i]);
            }

            return stringBuilder.ToString();
        }

        private class BuildValue
        {
            public string Name { get; set; }
            public string Value { get; set; }
        };

        private void PrintBuildValues(StringBuilder stringBuilder, BuildValue[] buildValues)
        {
            foreach (BuildValue buildValue in buildValues)
            {
                stringBuilder.AppendFormat("{0}: {1}{2}",
                    buildValue.Name.TrimEnd(new char[] { '.' }),
                    buildValue.Value,
                    Environment.NewLine);
            }
        }

        private void PrintBuildInputFiles(StringBuilder stringBuilder)
        {
            BuildValue[] buildInputFiles = new BuildValue[]
            {
                new BuildValue()
                {
                    Name = GameSourceButton.Text,
                    Value = Program.BatchBuildList.Any() 
                        ? GetBatchBuildListString() 
                        : GameSourceDirectory.Text
                },
                new BuildValue()
                {
                    Name = IconSourceButton.Text,
                    Value = IconSourceDirectory.Text
                },
                new BuildValue()
                {
                    Name = BannerSourceButton.Text,
                    Value = BannerSourceDirectory.Text
                },
                new BuildValue()
                {
                    Name = GC2SourceButton.Text,
                    Value = Program.BatchBuildList.Any() 
                        ? string.Empty 
                        : GC2SourceDirectory.Text
                },
                new BuildValue()
                {
                    Name = DrcSourceButton.Text,
                    Value = DrcSourceDirectory.Text
                },
                new BuildValue()
                {
                    Name = LogoSourceButton.Text,
                    Value = LogoSourceDirectory.Text
                },
                new BuildValue()
                {
                    Name = BootSoundButton.Text,
                    Value = BootSoundDirectory.Text
                },
            };

            PrintBuildValues(stringBuilder, buildInputFiles);
        }

        private void PrintBuildOptions(StringBuilder stringBuilder, Control control)
        {
            if (control is CheckBox checkBox && checkBox.Checked)
            {
                stringBuilder.AppendFormat("'{0}', ", checkBox.Text);
            }

            if (control is RadioButton radioButton && radioButton.Checked)
            {
                stringBuilder.AppendFormat("'{0}', ", radioButton.Text);
            }

            foreach (Control subControl in control.Controls)
            {
                PrintBuildOptions(stringBuilder, subControl);
            }
        }

        private void PrintBuildOptions(StringBuilder stringBuilder)
        {
            foreach (Control control in Controls)
            {
                PrintBuildOptions(stringBuilder, control);
            }

            //
            // TrimEnd ', '.
            //
            stringBuilder.Remove(stringBuilder.Length - 2, 2);
        }

        private void PrintSpaceInformation(StringBuilder stringBuilder)
        {
            BuildValue[] spaceValues = new BuildValue[]
            {
                new BuildValue()
                {
                    Name = OutputDirLabel.Text,
                    Value = string.Format(Trt.Tr("{0}(Available free space: {1})"), 
                    OutputDirectory.Text,
                    String.IsNullOrEmpty(OutputDirectory.Text) 
                        ? "0" 
                        : Misc.GetLengthString(
                            new DriveInfo(OutputDirectory.Text)
                            .AvailableFreeSpace))
                },
                new BuildValue()
                {
                    Name = TempDirLabel.Text,
                    Value = string.Format(Trt.Tr("{0}(Available free space: {1})"),
                    TemporaryDirectory.Text,
                    String.IsNullOrEmpty(TemporaryDirectory.Text)
                        ? "0"
                        : Misc.GetLengthString(
                            new DriveInfo(OutputDirectory.Text)
                            .AvailableFreeSpace))
                },
            };

            PrintBuildValues(stringBuilder, spaceValues);
        }

        private void PrintBuildOverview()
        {
            StringBuilder overview = new StringBuilder(4096);

            //
            // Add generic information.
            //
            overview.AppendFormat(Trt.Tr("Injector version: {0}"), Program.Version);
            overview.AppendLine();
            overview.AppendFormat(Trt.Tr("Build type: {0}"), GetCurrentBuildTypeString());
            overview.AppendLine();
            overview.AppendFormat(
                Trt.Tr("Batch build: {0}"), 
                Program.BatchBuildList.Any() 
                ? Trt.Tr("Yes") : Trt.Tr("No"));
            overview.AppendLine();

            //
            // Add input files information.
            //
            PrintBuildInputFiles(overview);

            //
            // Add space report.
            //
            PrintSpaceInformation(overview);

            //
            // Add selected options information.
            //
            overview.Append(Trt.Tr("Build options:"));
            overview.AppendLine();
            PrintBuildOptions(overview);
            overview.AppendLine();

            //
            // Add timestamp.
            //
            overview.AppendFormat(
                Trt.Tr("Build time(UTC): {0}"), 
                DateTime.Now.ToUniversalTime());
            overview.AppendLine();

            //
            // Add a newline for next output.
            //
            overview.AppendLine();

            AppendBuildOutput(overview.ToString(), BuildOutputType.Step);
        }

        #region BatchBuild

        private void BatchBuild()
        {
            if (!Program.BatchBuildList.Any())
            {
                return;
            }

            if (IsBuilding)
            {
                Program.BatchBuildList.Clear();
                return;
            }

            BuildOutput.ResetText();

            BatchBuildSucceedList.Clear();
            BatchBuildFailedList.Clear();
            BatchBuildInvalidList.Clear();
            BatchBuildSkippedList.Clear();

            PreBuild = (s, e) =>
            {
                BuildOutput.ResetText();
                PrintBuildOverview();
            };

            PostBuild += WiiVC_Injector_PostBuild;

            PromptForSucceed = false;
            BatchBuildNext();
        }

        private bool BatchBuildCurrent()
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

        private void BatchBuildNext()
        {
            while (Program.BatchBuildList.Any())
            {
                string game = Program.BatchBuildList[0];

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
                        AppendBuildOutput(
                            string.Format(
                                Trt.Tr("Title output folder already exists: {0}\nSkipping: {1}.\n"),
                                GetOutputFolder(), game),
                            BuildOutputType.Error);

                        BatchBuildSkippedList.Add(game);
                        Program.BatchBuildList.RemoveAt(0);
                        continue;
                    }
                    else
                    {
                        BatchBuildCurrent();
                        PreBuild = null;
                        break;
                    }
                }

                AppendBuildOutput(
                    string.Format(Trt.Tr("Invalid Title: {0}.\n"), game),
                    BuildOutputType.Error);

                BatchBuildInvalidList.Add(game);
                Program.BatchBuildList.RemoveAt(0);
            }

            if (!Program.BatchBuildList.Any())
            {
                PostBuild -= WiiVC_Injector_PostBuild;

                if (!InClosing)
                {
                    MessageBox.Show(string.Format(
                        Trt.Tr("All conversions have been completed.\nSucceed: {0}.\nFailed: {1}.\nSkipped: {2}.\nInvalid: {3}."),
                        BatchBuildSucceedList.Count,
                        BatchBuildFailedList.Count,
                        BatchBuildSkippedList.Count,
                        BatchBuildInvalidList.Count));
                }
            }
        }

        private void WiiVC_Injector_PostBuild(object sender, bool e)
        {
            if (e)
            {
                BatchBuildSucceedList.Add(Program.BatchBuildList[0]);
            }
            else
            {
                BatchBuildFailedList.Add(Program.BatchBuildList[0]);
            }

            Program.BatchBuildList.RemoveAt(0);

            if (LastBuildCancelled)
            {
                BatchBuildSkippedList.AddRange(Program.BatchBuildList);
                Program.BatchBuildList.Clear();
            }

            BatchBuildNext();
        }

        #endregion

        #region BuildSteps

        #region BuildStep1

        // Step 1
        private bool PrepareTemporaryDirectory()
        {
            Invoke(new Action(() => { TemporaryDirectory.Text = TemporaryDirectory.Text.Trim(); }));

            string tempDir = TemporaryDirectory.Text;

            if (string.IsNullOrWhiteSpace(tempDir))
            {
                tempDir = GetAppTempPath();
            }

            if (!tempDir.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                tempDir += Path.DirectorySeparatorChar;
            }

            string newTempRootPath = tempDir + "WiiVCInjector" + Path.DirectorySeparatorChar;
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

        #endregion

        #region BuildStep2

        // Step 2
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

            DriveInfo drive = new DriveInfo(TempRootPath);

            if (drive.AvailableFreeSpace < requiredFreespace[SystemType])
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

        #endregion

        #region BuildStep3

        // Step 3
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

        #endregion

        #region BuildStep4

        // Step 4
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
                $"{commonWiiUId0}\\code\\deint.txt",
                $"{commonWiiUId0}\\code\\font.bin",
                $"{commonWiiUId1}\\code\\c2w.img",
                $"{commonWiiUId1}\\code\\boot.bin",
                $"{commonWiiUId1}\\code\\dmcu.d.hex",
                $"{titleGameTitleNameTiedToJame}\\code\\cos.xml",
                $"{titleGameTitleNameTiedToJame}\\code\\frisbiiU.rpx",
                $"{titleGameTitleNameTiedToJame}\\code\\fw.img",
                $"{titleGameTitleNameTiedToJame}\\code\\fw.tmd",
                $"{titleGameTitleNameTiedToJame}\\code\\htk.bin",
                $"{titleGameTitleNameTiedToJame}\\code\\nn_hai_user.rpl",
                $"{titleGameTitleNameTiedToJame}\\content\\assets\\shaders\\cafe\\banner.gsh",
                $"{titleGameTitleNameTiedToJame}\\content\\assets\\shaders\\cafe\\fade.gsh",
                $"{titleGameTitleNameTiedToJame}\\meta\\bootMovie.h264",
                $"{titleGameTitleNameTiedToJame}\\meta\\bootLogoTex.tga",
                $"{titleGameTitleNameTiedToJame}\\meta\\bootSound.btsnd",
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

			string mention = "(One-Time Download) Downloading base files from Nintendo...";

			Invoke(
                ActBuildStatus,
                Trt.Tr($"{mention}"));

            string[] JNUSToolConfig = { "http://ccs.cdn.wup.shop.nintendo.net/ccs/download", WiiUCommonKey.Text };
            File.WriteAllLines(TempToolsPath + "JAR\\config", JNUSToolConfig);
            Directory.SetCurrentDirectory(TempToolsPath + "JAR");

            Invoke(ActBuildProgress, 10);

			string titleKey = TitleKey.Text;

			var downloadItems = new[]{
                new {
                    buildStatus = Trt.Tr($"{mention} (deint.txt)"),
                    exeArgs = $"{commonWiiUId0} -file /code/deint.txt",
                    progress = 12,
                },
                new {
                    buildStatus = Trt.Tr($"{mention} (font.bin)"),
                    exeArgs = $"{commonWiiUId0} -file /code/font.bin",
                    progress = 15,
                },
                new {
                    buildStatus = Trt.Tr($"{mention} (c2w.img)"),
                    exeArgs = $"{commonWiiUId1} -file /code/c2w.img",
                    progress = 17,
                },
                new {
                    buildStatus = Trt.Tr($"{mention} (boot.bin)"),
                    exeArgs = $"{commonWiiUId1} -file /code/boot.bin",
                    progress = 20,
                },
                new {
                    buildStatus = Trt.Tr($"{mention} (dmcu.d.hex)"),
                    exeArgs = $"{commonWiiUId1} -file /code/dmcu.d.hex",
                    progress = 23,
                },
                new {
                    buildStatus = Trt.Tr($"{mention} (cos.xml)"),
                    exeArgs = $"{titleId} {titleKey} -file /code/cos.xml",
                    progress = 25,
                },
                new {
                    buildStatus = Trt.Tr($"{mention} (frisbiiU.rpx)"),
                    exeArgs = $"{titleId} {titleKey} -file /code/frisbiiU.rpx",
                    progress = 27,
                },
                new {
                    buildStatus = Trt.Tr($"{mention} (fw.img)"),
                    exeArgs = $"{titleId} {titleKey} -file /code/fw.img",
                    progress = 30,
                },
                new {
                    buildStatus = Trt.Tr($"{mention} (fw.tmd)"),
                    exeArgs = $"{titleId} {titleKey} -file /code/fw.tmd",
                    progress = 32,
                },
                new {
                    buildStatus = Trt.Tr($"{mention} (htk.bin)"),
                    exeArgs = $"{titleId} {titleKey} -file /code/htk.bin",
                    progress = 35,
                },
                new {
                    buildStatus = Trt.Tr($"{mention} (nn_hai_user.rpl)"),
                    exeArgs = $"{titleId} {titleKey} -file /code/nn_hai_user.rpl",
                    progress = 37,
                },
                new {
                    buildStatus = Trt.Tr($"{mention} (banner.gsh / fade.gsh)"),
                    exeArgs = $"{titleId} {titleKey} -file /content/assets/.*",
                    progress = 40,
                },
                new {
                    buildStatus = Trt.Tr($"{mention} (bootMovie.h264)"),
                    exeArgs = $"{titleId} {titleKey} -file /meta/bootMovie.h264",
                    progress = 42,
                },
                new {
                    buildStatus = Trt.Tr($"{mention} (bootLogoTex.tga)"),
                    exeArgs = $"{titleId} {titleKey} -file /meta/bootLogoTex.tga",
                    progress = 45,
                },
                new {
                    buildStatus = Trt.Tr($"{mention} (bootSound.btsnd)"),
                    exeArgs = $"{titleId} {titleKey} -file /meta/bootSound.btsnd",
                    progress = 47,
                },
            };

            LauncherExeFile = "java";

            foreach (var downloadItem in downloadItems)
            {
                Invoke(ActBuildStatus, downloadItem.buildStatus);
                LauncherExeArgs = "-jar JNUSTool.jar " + downloadItem.exeArgs;

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
                    Directory.CreateDirectory(JNUSToolDownloads + titleGameTitleNameTiedToJame);
                    Directory.CreateDirectory(JNUSToolDownloads + commonWiiUId0);
                    Directory.CreateDirectory(JNUSToolDownloads + commonWiiUId1);
                    FileSystem.CopyDirectory(titleGameTitleNameTiedToJame, JNUSToolDownloads + titleGameTitleNameTiedToJame);
                    FileSystem.CopyDirectory(commonWiiUId0, JNUSToolDownloads + commonWiiUId0);
                    FileSystem.CopyDirectory(commonWiiUId1, JNUSToolDownloads + commonWiiUId1);
                    Directory.Delete(titleGameTitleNameTiedToJame, true);
                    Directory.Delete(commonWiiUId0, true);
                    Directory.Delete(commonWiiUId1, true);
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
                    Directory.Delete(titleGameTitleNameTiedToJame, true);
                    Directory.Delete(commonWiiUId0, true);
                    Directory.Delete(commonWiiUId1, true);
                    File.Delete("config");
                }
                catch (Exception)
                {

                }
            }

            return downloadCompleted;
        }

        #endregion

        #region BuildStep5

        // Step 5
        private bool PrepareBasicFilesForPack()
        {
            //
            // Copy downloaded files to the build directory
            //
            Directory.SetCurrentDirectory(TempRootPath);
            FileSystem.CopyDirectory(JNUSToolDownloads + titleGameTitleNameTiedToJame, TempBuildPath);

            if (C2WPatchFlag.Checked)
            {
                FileSystem.CopyDirectory(JNUSToolDownloads + commonWiiUId0, TempBuildPath);
                FileSystem.CopyDirectory(JNUSToolDownloads + commonWiiUId1, TempBuildPath);
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

        #endregion

        #region BuildStep6

        // Step 6
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

        private string EscapeXml(string str)
        {
            return System.Security.SecurityElement.Escape(str);
        }

        #endregion

        #region BuildStep7

        // Step 7
        private bool PrepareImages()
        {
            File.Copy(TempIconPath, Path.Combine(TempBuildPath, "meta", Path.GetFileName(TempIconPath)));
            File.Copy(TempBannerPath, Path.Combine(TempBuildPath, "meta", Path.GetFileName(TempBannerPath)));

            if (!FlagDrcSpecified)
            {
                Image image = Draw.ResizeAndFitImage(LoadImage(TempBannerPath), DrcSize);
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

        #endregion

        #region BuildStep8

        // Step 8
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

        #endregion

        #region BuildStep9

        // Step 9
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

        #endregion

        #region BuildStep10

        // Step 10
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

        #endregion

        #region BuildStep11

        // Step 11
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

        #endregion

        #endregion
    }
}