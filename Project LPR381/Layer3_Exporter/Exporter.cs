using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using LPR381.Models;

namespace LPR381.Layer3_Exporter
{
    // Layer 3: The Exporter.
    // Once Layer 2 finishes the hard math, this class takes those results and writes them to a cleanly formatted text file in the user's Downloads folder.
    public class Exporter
    {
        // Advanced Windows code to perfectly locate the Downloads folder, even if it is on OneDrive.
        [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out string pszPath);
        private static readonly Guid DownloadsFolderGuid = new Guid("374DE290-123F-4565-9164-39C4925E467B");

        public static string GetActiveDownloadsDirectory()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    int hr = SHGetKnownFolderPath(DownloadsFolderGuid, 0, IntPtr.Zero, out string knownFolderPath);
                    if (hr == 0 && !string.IsNullOrWhiteSpace(knownFolderPath) && Directory.Exists(knownFolderPath))
                    {
                        return knownFolderPath; // We found the folder!
                    }
                }
            }
            catch { }

            // If the advanced method fails, we fallback to the basic folder paths.
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string standardDownloads = Path.Combine(userProfile, "Downloads");
            if (Directory.Exists(standardDownloads)) return standardDownloads;

            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (Directory.Exists(desktopPath)) return desktopPath;

            return AppDomain.CurrentDomain.BaseDirectory;
        }

        // The main method that actually writes the file.
        public static string ExportResult(string originalFilePath, SimplexResult result, string iterationsLog)
        {
            // Figure out where to save the file
            string targetFolder = GetActiveDownloadsDirectory();
            if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);

            // Give the file a name based on the original model, plus a timestamp so we don't overwrite older runs.
            string modelName = string.IsNullOrWhiteSpace(originalFilePath) ? "Model" : Path.GetFileNameWithoutExtension(originalFilePath);
            string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string outputFileName = $"{modelName}_Solution_{timeStamp}.txt";
            string fullOutputPath = Path.Combine(targetFolder, outputFileName);

            // Open a stream to write out our data
            using (StreamWriter writer = new StreamWriter(fullOutputPath, false, Encoding.UTF8))
            {
                writer.WriteLine("+------------------------------------------------------------------------------+");
                writer.WriteLine("|                       LPR381 OPERATIONS RESEARCH REPORT                      |");
                writer.WriteLine("+------------------------------------------------------------------------------+");
                writer.WriteLine($" Source Model File : {(string.IsNullOrWhiteSpace(originalFilePath) ? "Manual Ingestion" : Path.GetFileName(originalFilePath))}");
                writer.WriteLine($" Execution Time    : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine($" Output Location   : {fullOutputPath}");
                writer.WriteLine("+------------------------------------------------------------------------------+");
                writer.WriteLine();

                // Print all the cool grids Layer 2 saved
                writer.WriteLine("================================================================================");
                writer.WriteLine("                        CANONICAL FORM & TABLEAU ITERATIONS                     ");
                writer.WriteLine("================================================================================");
                writer.WriteLine(iterationsLog);
                writer.WriteLine();

                // Print the final answers
                writer.WriteLine("================================================================================");
                writer.WriteLine("                               FINAL OPTIMAL STATE                              ");
                writer.WriteLine("================================================================================");
                writer.WriteLine($" Optimal Objective Value (Z) : {result.OptimalZ.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}");
                writer.WriteLine(" Variable Value Assignments  :");
                foreach (var variable in result.VariableValues)
                {
                    writer.WriteLine($"   {variable.Key,-6} = {variable.Value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),10}");
                }
                writer.WriteLine();
                writer.WriteLine("+------------------------------------------------------------------------------+");
                writer.WriteLine("|                                 END OF REPORT                                |");
                writer.WriteLine("+------------------------------------------------------------------------------+");
                writer.Flush();
            }

            // A quick safety check to ensure it actually wrote to the hard drive.
            if (!File.Exists(fullOutputPath))
            {
                throw new IOException($"System failed to confirm file write at: {fullOutputPath}");
            }

            return fullOutputPath;
        }
    }
}