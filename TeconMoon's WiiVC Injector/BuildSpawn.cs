using LogLevels;
using System;
using System.Diagnostics;
using TeconMoon_s_WiiVC_Injector.Utils.Build;

namespace TeconMoon_s_WiiVC_Injector
{
    partial class WiiVC_Injector
    {
        public string SpawnFile { get; set; }
        public string SpawnArgs { get; set; }

        private bool BuildSpawn()
        {
            bool exitNormally = true;

            ProcessStartInfo spawnInfo = new ProcessStartInfo(SpawnFile, SpawnArgs);

            if (HideProcess)
            {
                spawnInfo.CreateNoWindow = true;
                spawnInfo.WindowStyle = ProcessWindowStyle.Hidden;
            }

            spawnInfo.UseShellExecute = false;
            spawnInfo.RedirectStandardOutput = true;
            spawnInfo.RedirectStandardError = true;

            BuildOutputBuffer buildOutputBuffer = new BuildOutputBuffer();
            buildOutputBuffer.FlushBuffer += (s, e) =>
            {
                BeginInvoke(ActBuildOutput, e);
            };

            if (currentLogLevel <= LogLevel.Debug)
            {
                BeginInvoke(ActBuildOutput, new BuildOutputItem()
                {
                    Output = Trt.Tr("Executing:") + ' ' + SpawnFile + Environment.NewLine
                           + Trt.Tr("Args:") + ' ' + SpawnArgs + Environment.NewLine,
                    OutputType = BuildOutputType.Exec
                });
            }

            try
            {
                Process spawner = Process.Start(spawnInfo);
                System.Timers.Timer OutputPumpTimer = new System.Timers.Timer();

                spawner.OutputDataReceived += (s, d) =>
                {
                    if (currentLogLevel <= LogLevel.Debug)
                    {
                        lock (buildOutputBuffer)
                        {
                            buildOutputBuffer.AppendOutput(d.Data, BuildOutputType.Normal);
                        }
                    }
                };

                spawner.ErrorDataReceived += (s, d) =>
                {
                    //
                    // Whatever, the error information should be printed.
                    //
                    lock (buildOutputBuffer)
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

                spawner.BeginOutputReadLine();
                spawner.BeginErrorReadLine();

                while (!spawner.WaitForExit(500))
                {
                    if (LastBuildCancelled)
                    {
                        spawner.CloseMainWindow();
                        if (!spawner.WaitForExit(100))
                        {
                            spawner.Kill();
                        }

                        exitNormally = false;
                    }
                }

                OutputPumpTimer.Stop();

                spawner.Close();

                lock (buildOutputBuffer)
                {
                    buildOutputBuffer.Flush();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("BuildSpawn exception: " + ex.Message);

                if (ThrowProcessException)
                {
                    throw ex;
                }

                exitNormally = false;
            }

            if (!exitNormally && ThrowProcessException)
            {
                throw new Exception(NormalizeCmdlineArg(SpawnFile)
                    + Trt.Tr(" does not exit normally."));
            }

            return exitNormally;
        }
    }
}
