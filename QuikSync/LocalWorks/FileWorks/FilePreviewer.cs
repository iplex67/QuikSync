using System;
using System.Diagnostics;
using System.IO;

namespace QuikSync.LocalWorks.FileWorks
{
    /// <summary>
    /// Makes temporary copies of files that are in a sync conflict and opens them, so the user can see
    /// which version to keep. Everything lives in %TEMP%\QuikSync\(session) and is removed by CleanUp.
    /// </summary>
    public class FilePreviewer
    {
        private readonly string localRoot;
        private readonly Action<string, string> downloadHostFile;
        private readonly string sessionDir;
        private int counter = 0;

        /// <summary>
        /// Opens a file with its associated application. Replaceable so the logic can be tested without launching programs.
        /// </summary>
        public Action<string> OpenFile = path => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

        /// <summary>
        /// Constructor that initializes FilePreviewer.
        /// </summary>
        /// <param name="localRoot">Root directory that is being syncronized on this device.</param>
        /// <param name="downloadHostFile">Action that downloads the Host file (first param, relative path) to the given full path (second param).</param>
        public FilePreviewer(string localRoot, Action<string, string> downloadHostFile)
            : this(localRoot, downloadHostFile, Path.Combine(Path.GetTempPath(), "QuikSync"))
        {
        }

        /// <param name="tempRoot">Directory that holds the temp files of every session.</param>
        public FilePreviewer(string localRoot, Action<string, string> downloadHostFile, string tempRoot)
        {
            this.localRoot = localRoot;
            this.downloadHostFile = downloadHostFile;
            sessionDir = Path.Combine(tempRoot, Guid.NewGuid().ToString("N"));
        }

        /// <summary>
        /// Builds a full path for a relative path under baseDir. The relative path can come from the other
        /// device, so anything that would end up outside of baseDir is refused.
        /// </summary>
        /// <param name="baseDir">Directory the result has to stay in.</param>
        /// <param name="rel">Relative path of the file.</param>
        /// <returns>Full path inside of baseDir.</returns>
        public static string BuildPathInside(string baseDir, string rel)
        {
            if (string.IsNullOrWhiteSpace(rel))
                throw new ArgumentException("Relative path is empty.");

            if (rel.IndexOfAny(Path.GetInvalidPathChars()) != -1 || rel.IndexOf(':') != -1 || Path.IsPathRooted(rel))
                throw new ArgumentException($"Relative path '{rel}' is not allowed.");

            string baseFull = Path.GetFullPath(baseDir).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(Path.Combine(baseFull, rel));

            if (!full.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase) || full.Length == baseFull.Length)
                throw new ArgumentException($"Relative path '{rel}' leads outside of the allowed directory.");

            return full;
        }

        /// <summary>
        /// Downloads the Host version of the file to a temp directory and opens it.
        /// </summary>
        /// <param name="rel">Relative path of the file.</param>
        /// <returns>Path of the temp file.</returns>
        public string PreviewHostFile(string rel)
        {
            string tempPath = BuildPathInside(NextPreviewDir("host"), rel);
            downloadHostFile(rel, tempPath);

            if (!File.Exists(tempPath))
                throw new FileNotFoundException("The file was not received from Host.", tempPath);

            OpenFile(tempPath);
            return tempPath;
        }

        /// <summary>
        /// Copies the Joined (local) version of the file to a temp directory and opens it. The copy is opened
        /// so the real file is never held open or changed by the viewer.
        /// </summary>
        /// <param name="rel">Relative path of the file.</param>
        /// <returns>Path of the temp file.</returns>
        public string PreviewLocalFile(string rel)
        {
            string source = BuildPathInside(localRoot, rel);
            string tempPath = BuildPathInside(NextPreviewDir("joined"), rel);

            Directory.CreateDirectory(Path.GetDirectoryName(tempPath));
            File.Copy(source, tempPath);

            OpenFile(tempPath);
            return tempPath;
        }

        /// <summary>
        /// Deletes every temp file made by this FilePreviewer. Files that are still open in a viewer are left
        /// in place instead of failing.
        /// </summary>
        /// <returns>true if everything is deleted, false if something was left because it's in use.</returns>
        public bool CleanUp()
        {
            bool clean = DeleteDirectory(sessionDir);

            // Remove the shared QuikSync temp directory too, if nobody else is using it.
            try
            {
                Directory.Delete(Path.GetDirectoryName(sessionDir));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            return clean;
        }

        // Each preview gets its own directory, so a file that's still open from an earlier preview never collides.
        private string NextPreviewDir(string side)
        {
            return Path.Combine(sessionDir, (++counter) + "-" + side);
        }

        private static bool DeleteDirectory(string dir)
        {
            if (!Directory.Exists(dir))
                return true;

            bool clean = true;

            foreach (string file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
                catch (IOException) { clean = false; }
                catch (UnauthorizedAccessException) { clean = false; }
            }

            // Delete empty directories bottom-up; ones that still have a locked file in them stay.
            string[] dirs = Directory.GetDirectories(dir, "*", SearchOption.AllDirectories);
            Array.Sort(dirs, (a, b) => b.Length.CompareTo(a.Length));
            foreach (string d in dirs)
            {
                try { Directory.Delete(d); }
                catch (IOException) { clean = false; }
                catch (UnauthorizedAccessException) { clean = false; }
            }

            try { Directory.Delete(dir); }
            catch (IOException) { clean = false; }
            catch (UnauthorizedAccessException) { clean = false; }

            return clean;
        }
    }
}
