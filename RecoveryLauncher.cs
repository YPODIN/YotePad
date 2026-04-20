using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Yotepad
{
    public static class RecoveryLauncher
    {
        // Launches a new YotePad instance with the recovered content.
        // staggerIndex: 0 for first file, 1 for second, etc. — used to cascade windows.
        public static void Launch(RecoveryFile file, Point basePos, int staggerIndex)
        {
            try
            {
                // Step 1: claim the .ypr file by renaming it so no other instance scans it
                string claimedPath = file.RecoveryFilePath;
                if (!claimedPath.EndsWith(".restoring"))
                {
                    string renamed = file.RecoveryFilePath + ".restoring";
                    try
                    {
                        File.Move(file.RecoveryFilePath, renamed);
                        claimedPath = renamed;
                    }
                    catch { }
                }

                // Step 2: write content to a temp file for the new instance to load
                string tempPath = Path.Combine(
                    Path.GetTempPath(),
                    $"yotepad_restore_{Guid.NewGuid()}.txt");
                File.WriteAllText(tempPath, file.Content);

                // Step 3: delete the claimed .ypr/.ypr.restoring file — content is safe in temp
                try { File.Delete(claimedPath); } catch { }

                // Step 4: calculate staggered position for cascade effect
                int offset = 24 * (staggerIndex + 1);
                Point spawnPos = new Point(basePos.X + offset, basePos.Y + offset);
                string posArg = $"{spawnPos.X},{spawnPos.Y}";

                // Step 5: launch the new instance
                // Note: --pos handler in Program.cs applies +24 to the passed position,
                // so we pass the raw target minus 24 to land exactly where we want
                string adjustedPos = $"{spawnPos.X - 24},{spawnPos.Y - 24}";

               Process.Start(
                Application.ExecutablePath,
                $"--restore \"{tempPath}\" --original \"{file.OriginalFilePath}\" --pos {adjustedPos} --no-recovery"
            );
            }
            catch { }
        }

        // Hack: need a local alias since we can't use System.Diagnostics.Process directly
        // in a static class without an explicit using, and we want one file self-contained
        private static class Process
        {
            public static void Start(string fileName, string arguments)
            {
                System.Diagnostics.Process.Start(fileName, arguments);
            }
        }
    }
}