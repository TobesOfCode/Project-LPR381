// File: Layer3_Exporter/Exporter.cs
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using LPR381.Models;

namespace LPR381.Layer3_Exporter
{
    // -------------------------------------------------------------------------
    // CLASS: Exporter (Layer 3)
    // Purpose: Once Layer 2 finishes all the heavy mathematical lifting, this class 
    // takes those final results and writes them to a cleanly formatted text file 
    // in the user's computer. It acts as our report generator.
    // -------------------------------------------------------------------------
    public class Exporter
    {
        // -------------------------------------------------------------------------
        // WINDOWS API SECRETS
        // We use a special Windows trick here to perfectly locate the Downloads folder. 
        // Standard C# sometimes gets confused if the user has OneDrive syncing turned on, 
        // so we use 'shell32.dll' to ask the Windows Operating System directly!
        // -------------------------------------------------------------------------
        [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out string pszPath);

        // This specific ID code translates to the "Downloads" folder in Windows
        private static readonly Guid DownloadsFolderGuid = new Guid("374DE290-123F-4565-9164-39C4925E467B");

        // -------------------------------------------------------------------------
        // METHOD: GetActiveDownloadsDirectory
        // Purpose: Safely finds the best place to save our output file.
        // -------------------------------------------------------------------------
        public static string GetActiveDownloadsDirectory()
        {
            try
            {
                // First, we try our advanced Windows OS trick
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    int hr = SHGetKnownFolderPath(DownloadsFolderGuid, 0, IntPtr.Zero, out string knownFolderPath);
                    if (hr == 0 && !string.IsNullOrWhiteSpace(knownFolderPath) && Directory.Exists(knownFolderPath))
                    {
                        return knownFolderPath; // Success! We found the true Downloads folder.
                    }
                }
            }
            catch { }

            // If the advanced method fails (or if they are on a Mac/Linux), we use a series of fallbacks.
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string standardDownloads = Path.Combine(userProfile, "Downloads");
            if (Directory.Exists(standardDownloads)) return standardDownloads;

            // Fallback 2: The Desktop
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (Directory.Exists(desktopPath)) return desktopPath;

            // Absolute Last Resort: Just save it in the exact same folder the program is running from.
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        // -------------------------------------------------------------------------
        // METHOD: ExportResult
        // Purpose: Takes all the math logs and the final optimal answers, builds a 
        // nice looking ASCII report, and saves it to the hard drive.
        // -------------------------------------------------------------------------
        public static string ExportResult(string originalFilePath, SimplexResult result, string iterationsLog)
        {
            // Figure out exactly where we are going to save this file.
            string targetFolder = GetActiveDownloadsDirectory();
            if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);

            // Step 1: Create a smart file name. We include the date and time so we 
            // don't accidentally overwrite older reports if we run the program twice.
            string modelName = string.IsNullOrWhiteSpace(originalFilePath) ? "Model" : Path.GetFileNameWithoutExtension(originalFilePath);
            string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string outputFileName = $"{modelName}_Solution_{timeStamp}.txt";
            string fullOutputPath = Path.Combine(targetFolder, outputFileName);

            // Step 2: Open a stream to actually write the text to the hard drive.
            using (StreamWriter writer = new StreamWriter(fullOutputPath, false, Encoding.UTF8))
            {
                // Print a fancy header
                writer.WriteLine("+------------------------------------------------------------------------------+");
                writer.WriteLine("|                       LPR381 OPERATIONS RESEARCH REPORT                      |");
                writer.WriteLine("+------------------------------------------------------------------------------+");
                writer.WriteLine($" Source Model File : {(string.IsNullOrWhiteSpace(originalFilePath) ? "Manual Ingestion" : Path.GetFileName(originalFilePath))}");
                writer.WriteLine($" Execution Time    : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine($" Output Location   : {fullOutputPath}");
                writer.WriteLine("+------------------------------------------------------------------------------+");
                writer.WriteLine();

                // Print all the iteration tableaus that Layer 2 saved for us
                writer.WriteLine("================================================================================");
                writer.WriteLine("                        CANONICAL FORM & TABLEAU ITERATIONS                     ");
                writer.WriteLine("================================================================================");
                writer.WriteLine(iterationsLog);
                writer.WriteLine();

                // Print the absolute final answers from our 'SimplexResult' care package
                writer.WriteLine("================================================================================");
                writer.WriteLine("                               FINAL OPTIMAL STATE                              ");
                writer.WriteLine("================================================================================");

                // Force formatting with InvariantCulture so decimals print cleanly with dots
                writer.WriteLine($" Optimal Objective Value (Z) : {result.OptimalZ.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}");
                writer.WriteLine(" Variable Value Assignments  :");

                // Loop through our variables dictionary and print them perfectly aligned
                foreach (var variable in result.VariableValues)
                {
                    writer.WriteLine($"   {variable.Key,-6} = {variable.Value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),10}");
                }

                writer.WriteLine();
                writer.WriteLine("+------------------------------------------------------------------------------+");
                writer.WriteLine("|                                 END OF REPORT                                |");
                writer.WriteLine("+------------------------------------------------------------------------------+");

                // Make absolutely sure everything is pushed out of memory and onto the disk
                writer.Flush();
            }

            // Step 3: A final safety check to verify Windows actually created the file
            if (!File.Exists(fullOutputPath))
            {
                throw new IOException($"System failed to confirm file write at: {fullOutputPath}");
            }

            // Return the full file path so the Main Menu can ask if the user wants to open it right now!
            return fullOutputPath;
        }
    }
}