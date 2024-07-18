
using NReco.VideoConverter;
using System.ComponentModel;
using System.Diagnostics;
using static ContentWarningClips.CACHE;

namespace ContentWarningClips
{
    // Cache class
    class CACHE
    {
        // Main running variable for the program
        public static bool RUNNING = true;

        // Output folder path
        public static string OUTPUT_FOLDER = string.Empty;

        // Current parent Temp folder
        public static string TEMP_PARENT = string.Empty;

        // Old parent Temp folders
        public static List<string> KNOWN_PARENTS = [];

        // List of known child folders
        public static List<string> KNOWN_CHILDREN = [];

        // Filename to use for copied file
        public static string FILENAME = string.Empty;
    }

    // Main class for the program
    class App
    {
        // Entry point of the program
        private static void Main()
        {
            // Notify of program start
            Console.WriteLine("Program is Starting");

            /* Load the output folder */
            // Set path
            OUTPUT_FOLDER = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "ContentWarningClips");
            // Check if folder exists, and create if it doesn't
            if (!Path.Exists(OUTPUT_FOLDER)) Directory.CreateDirectory(OUTPUT_FOLDER);

            /* Create the background listener for file updates */
            // Define worker
            BackgroundWorker file_worker = new();
            // Set work task
            file_worker.DoWork += ListenForFileUpdate!;
            // Start worker
            file_worker.RunWorkerAsync();

            /* Create the background listener for commands */
            // Define worker
            BackgroundWorker cmd_worker = new();
            // Set work task
            cmd_worker.DoWork += ListenForCommand!;
            // Start worker
            cmd_worker.RunWorkerAsync();

            // Notify of progra ready
            Console.WriteLine("Program is Ready");

            // Keep thread running so program doesn't crash
            while (RUNNING) { };

            // Notify of program shutdown
            Console.WriteLine("Program is Shutting Down");
        }

        // Listener for command entry
        private static void ListenForCommand(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            // Keep listening for commands
            while (true)
            {
                // Send Command Prompt Symbol
                Console.Write("\n> ");

                // Get raw input
                string? line = Console.ReadLine();
                // Check for null
                if (line == null) continue;
                // Trim spaces and make lowercase
                string fullcommand = line.Trim().ToLower();

                /* Load command details */
                // Load command name
                string command = fullcommand.Contains(' ') ? fullcommand[..fullcommand.IndexOf(' ')] : fullcommand;

                /* Check which command was entered */
                // Stop server
                if (new List<string>(["stop", "shutdown", "exit", "end", "close"]).Contains(command))
                {
                    // Notify user
                    Console.WriteLine("Issuing Program Shutdown Command");
                    // Set running var
                    RUNNING = false;
                    // Break out of loop
                    return;
                }
                // Open Output Folder
                else if (new List<string>(["open", "output"]).Contains(command)) Process.Start(new ProcessStartInfo { Arguments = OUTPUT_FOLDER, FileName = "explorer.exe" });
                // Command not found
                else Console.WriteLine($"Command Not Found: {command}");
            }
        }

        // Listener for file copying
        private static void ListenForFileUpdate(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            // Keep listening for file updates
            while (true)
            {
                // Only check every second
                Thread.Sleep(1000);
                // Check for new parent folder
                string? new_parent;
                try { new_parent = Directory.GetDirectories(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local", "Temp", "rec")).FirstOrDefault(x => !KNOWN_PARENTS.Contains(x!), null); }
                catch (DirectoryNotFoundException) { continue; }
                // If new parent found
                if (new_parent != null)
                {
                    // Set in cache
                    TEMP_PARENT = new_parent;
                    // Clear known children
                    KNOWN_CHILDREN = [];
                    // Add new parent to known parent list
                    KNOWN_PARENTS.Add(new_parent);
                    // Set new filename to use
                    FILENAME = DateTime.Now.ToString("dd-MM-yyyy_HH-mm") + ".webm";
                }
                // Check to see that a parent folder has been selected. If not, wait for next check
                if (TEMP_PARENT == string.Empty) continue;
                // Check for new child folders
                foreach (string path in Directory.GetDirectories(TEMP_PARENT))
                {
                    // If new folder found AND webm file is within
                    if (!KNOWN_CHILDREN.Contains(path) && Directory.GetFiles(path).Any(x => x.EndsWith(".webm")))
                    {
                        // Add to known folders
                        KNOWN_CHILDREN.Add(path);
                        /* Export video */
                        // Get file path of exported video
                        string? export_path = Directory.GetFiles(OUTPUT_FOLDER).FirstOrDefault(x => x!.Contains(FILENAME), null);
                        // If video doesn't exist, copy original file
                        if (export_path == null)
                        {
                            // Make sure the file isn't empty before copying
                            if (new FileInfo(Path.Combine(path, "output.webm")).Length > 0) File.Copy(Path.Combine(path, "output.webm"), Path.Combine(OUTPUT_FOLDER, FILENAME));
                            // Not finished, redo
                            else { KNOWN_CHILDREN.Remove(path); }
                        }
                        // Otherwise combine videos
                        else
                        {
                            // Temp file variable
                            string temp_path = export_path.Replace("webm", "tmp");
                            // Move old video to TEMP file
                            File.Move(export_path, temp_path);
                            // Define FFMPEG converter
                            FFMpegConverter ffmpeg = new();
                            // Create array of filenames
                            string[] _filenames = [temp_path, Path.Combine(path, "output.webm")];
                            // Append video
                            try { ffmpeg.ConcatMedia(_filenames, export_path, Format.webm, new()); }
                            catch (FFMpegException)
                            {
                                /* Failed to convert, try again */
                                // Remove from known children
                                KNOWN_CHILDREN.Remove(path);
                                // Restore old recording
                                File.Move(temp_path, export_path);
                                // Try again
                                break;
                            }
                            // Delete TEMP file
                            File.Delete(temp_path);
                        }
                        // Break out of filecheck loop
                        break;
                    }
                }
            }
        }
    }
}
