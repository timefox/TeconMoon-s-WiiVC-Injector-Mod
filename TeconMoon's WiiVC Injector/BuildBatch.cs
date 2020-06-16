using System.IO;
using System.Linq;
using System.Windows.Forms;
using TeconMoon_s_WiiVC_Injector.Utils.Build;

namespace TeconMoon_s_WiiVC_Injector
{
    partial class WiiVC_Injector
    {
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
    }
}