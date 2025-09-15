// Add LAUNCHER to your project's "Conditional compilation symbols" to build the launcher EXE.
// Remove it (or leave it undefined) to build the Installer.Win.dll.
#define LAUNCHER 

#if LAUNCHER

using System;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace InstallerRunner
{
    class Program
    {
        private const string DllUrl = "https://github.com/coolguycoder/Affirmation-Generator/releases/download/Latest/Installer.Win.dll";

        [STAThread]
        static void Main(string[] args)
        {
            // Good practice for any WinForms app.
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                // Block and wait for the async download to complete.
                // This is acceptable here because no UI message loop is running yet.
                RunAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                // Catch exceptions from the async download process.
                MessageBox.Show($"A critical error occurred during initialization: {ex.Message}", "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        static async Task RunAsync()
        {
            byte[]? assemblyBytes = await DownloadAssembly(DllUrl);
            if (assemblyBytes != null)
            {
                // The assembly must be loaded and run on a Single-Threaded Apartment (STA) thread for WinForms.
                RunFromAssembly(assemblyBytes);
            }
            else
            {
                // This MessageBox will be shown if the download fails.
                MessageBox.Show("Failed to download the required installer component. Please check your internet connection and try again.", "Download Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

    private static async Task<byte[]?> DownloadAssembly(string url)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    return await client.GetByteArrayAsync(url);
                }
            }
            catch (Exception ex)
            {
                // The exception will be caught in the calling method, which will show a MessageBox.
                // We return null to indicate failure.
                Console.WriteLine($"Download failed: {ex.Message}"); // Log to console for debugging, though user won't see it.
                return null;
            }
        }

        private static void RunFromAssembly(byte[] assemblyBytes)
        {
            // We need to create a new thread to ensure it's an STA thread.
            var thread = new Thread(() =>
            {
                try
                {
                    var assembly = Assembly.Load(assemblyBytes);

                    var programType = assembly.GetType("SimpleInstaller.Program");
                    if (programType == null)
                    {
                        MessageBox.Show("Could not find the 'SimpleInstaller.Program' type in the downloaded component. The file may be corrupt.", "Execution Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var mainMethod = programType.GetMethod("Main", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (mainMethod == null)
                    {
                        MessageBox.Show("Could not find the 'Main' entry point in the downloaded component.", "Execution Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // This invokes the Main method which calls Application.Run, starting the message loop.
                    mainMethod.Invoke(null, null);
                }
                catch (Exception ex)
                {
                    var error = ex;
                    if (ex is TargetInvocationException && ex.InnerException != null)
                    {
                        error = ex.InnerException;
                    }
                    MessageBox.Show($"Failed to run the installer: {error.Message}", "Execution Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            });

            // Set the apartment state and start the thread.
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            // Wait for the new thread to finish (i.e., for the installer form to close).
            thread.Join();
        }
    }
}

#else

// Program.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SimpleInstaller
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new InstallerForm());
        }
    }

    public class InstallerForm : Form
    {
        // UI
        Label lblTitle;
        Label lblProduct;
        TextBox txtPath;
        Button btnBrowse;
        Button btnInstall;
        Button btnForceInstall;
        ProgressBar progressBar;
        Label lblStatus;   // single-line status (short)
        Label lblAction;   // what it's working on currently
        Label lblETA;      // ETA or speed
        CheckBox chkRunAfter;
        CheckBox chkCreateShortcut;
        GroupBox groupBox;

        // Download URL (raw GitHub file you provided)
        private string GetDownloadUrl()
        {
            var arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture;
            if (arch == System.Runtime.InteropServices.Architecture.Arm64)
            {
                return "https://github.com/coolguycoder/Affirmation-Generator/releases/latest/download/app-arm64.zip";
            }
            return "https://github.com/coolguycoder/Affirmation-Generator/releases/latest/download/app-x64.zip";
        }

        // Quick scan config
        const int SystemScanTimeoutMs = 8000; // time budget to find existing installation (ms)
        readonly string[] FilenameTokensToFind = new[] { "Affirmation", "AffirmationImageGenerator" };

        public InstallerForm()
        {
            InitializeVisuals();
        }

        private void InitializeVisuals()
        {
            Text = "Installer";
            Width = 640;
            Height = 320;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new System.Drawing.Font("Segoe UI", 9F);

            lblTitle = new Label
            {
                Text = "Affirmation Generator Installer",
                Left = 12,
                Top = 10,
                Width = ClientSize.Width - 24,
                Height = 28,
                Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            Controls.Add(lblTitle);

            lblProduct = new Label
            {
                Text = "Installs the application and creates a desktop shortcut.",
                Left = 14,
                Top = 40,
                Width = ClientSize.Width - 28,
                Height = 20
            };
            Controls.Add(lblProduct);

            groupBox = new GroupBox
            {
                Text = "Installation options",
                Left = 12,
                Top = 66,
                Width = ClientSize.Width - 24,
                Height = 120
            };
            Controls.Add(groupBox);

            var lbl = new Label { Text = "Install folder:", Left = 10, Top = 24, Width = 100 };
            groupBox.Controls.Add(lbl);

            txtPath = new TextBox { Left = 110, Top = 20, Width = 300 };
            groupBox.Controls.Add(txtPath);

            btnBrowse = new Button { Text = "Browse...", Left = 420, Top = 18, Width = 80 };
            btnBrowse.Click += BtnBrowse_Click;
            groupBox.Controls.Add(btnBrowse);

            chkRunAfter = new CheckBox { Text = "Run program after install", Left = 110, Top = 54, Checked = true, Width = 220 };
            groupBox.Controls.Add(chkRunAfter);

            chkCreateShortcut = new CheckBox { Text = "Create desktop shortcut", Left = 340, Top = 54, Checked = true, Width = 220 };
            groupBox.Controls.Add(chkCreateShortcut);

            progressBar = new ProgressBar { Left = 20, Top = 200, Width = ClientSize.Width - 40, Height = 22 };
            Controls.Add(progressBar);

            lblAction = new Label { Left = 20, Top = 230, Width = ClientSize.Width - 40, Height = 18, Text = "Action: Idle" };
            Controls.Add(lblAction);

            lblETA = new Label { Left = 20, Top = 250, Width = ClientSize.Width - 40, Height = 18, Text = "ETA: --" };
            Controls.Add(lblETA);

            lblStatus = new Label { Left = 20, Top = 270, Width = ClientSize.Width - 40, Height = 18, Text = "Status: Ready" };
            Controls.Add(lblStatus);

            btnInstall = new Button { Text = "Install", Left = 510, Top = 18, Width = 110, Height = 32 };
            btnInstall.Click += async (s, e) => await BtnInstall_Click(forceInstall: false);
            groupBox.Controls.Add(btnInstall);

            btnForceInstall = new Button { Text = "Force Install", Left = 510, Top = 54, Width = 110, Height = 32 };
            btnForceInstall.Click += async (s, e) => await BtnInstall_Click(forceInstall: true);
            groupBox.Controls.Add(btnForceInstall);

            // default install path (LocalAppData avoids elevation issues)
            txtPath.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AffirmationGenerator");
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog();
            dlg.SelectedPath = txtPath.Text;
            if (dlg.ShowDialog() == DialogResult.OK)
                txtPath.Text = dlg.SelectedPath;
        }

        private async Task BtnInstall_Click(bool forceInstall = false)
        {
            if (forceInstall)
            {
                var result = MessageBox.Show(
                    "Forcefully installing may overwrite existing files and cause issues. Are you sure you want to continue?",
                    "Warning",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.No)
                {
                    return;
                }
            }

            // disable UI
            btnInstall.Enabled = false;
            btnForceInstall.Enabled = false;
            btnBrowse.Enabled = false;
            progressBar.Value = 0;
            UpdateActionSafe("Preparing...");
            UpdateStatusSafe("Starting");

            try
            {
                if (!forceInstall)
                {
                    // 1) QUICK SYSTEM-WIDE FIND (best-effort, time-limited)
                    UpdateActionSafe("Searching for existing installation...");
                    UpdateStatusSafe("Looking for installed application on system (quick scan)...");
                    var cts = new CancellationTokenSource(SystemScanTimeoutMs);
                    var foundExisting = await Task.Run(() => FindExistingAppAnywhere(cts.Token), cts.Token);

                    if (!string.IsNullOrEmpty(foundExisting))
                    {
                        UpdateActionSafe("Found existing installation");
                        UpdateStatusSafe($"Launching existing: {Path.GetFileName(foundExisting)}");
                        LaunchExe(foundExisting, Path.GetDirectoryName(foundExisting));
                        CloseInstallerWindow();
                        return;
                    }
                }

                // 2) Continue with install (choose folder)
                var installPath = txtPath.Text.Trim();
                if (string.IsNullOrEmpty(installPath))
                {
                    MessageBox.Show("Please choose an install folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                Directory.CreateDirectory(installPath);

                if (!forceInstall)
                {
                    // If any exe already exists inside chosen folder, just launch and close
                    var existingExeInside = Directory.GetFiles(installPath, "AffirmationImageGenerator.exe", SearchOption.AllDirectories).FirstOrDefault();
                    if (!string.IsNullOrEmpty(existingExeInside))
                    {
                        UpdateActionSafe("Detected application in chosen folder");
                        UpdateStatusSafe($"Launching: {Path.GetFileName(existingExeInside)}");
                        LaunchExe(existingExeInside, installPath);
                        CloseInstallerWindow();
                        return;
                    }
                }

                // 3) Download + extract
                string tempFolder = Path.Combine(Path.GetTempPath(), "SimpleInstaller");
                Directory.CreateDirectory(tempFolder);

                string zipPath = Path.Combine(tempFolder, Path.GetFileName(new Uri(GetDownloadUrl()).LocalPath));

                UpdateActionSafe("Downloading package...");
                UpdateStatusSafe("Starting download");
                await DownloadValidatedZipAsync(GetDownloadUrl(), zipPath, new Progress<ProgressInfo>(info =>
                {
                    if (info != null)
                    {
                        progressBar.Value = Math.Clamp(info.Percent, 0, 100);
                        UpdateActionSafe(info.ActionText);
                        UpdateETASafe(info.ETAText);
                        UpdateStatusSafe(info.StatusText);
                    }
                }));

                UpdateActionSafe("Extracting package...");
                UpdateStatusSafe("Preparing extraction");
                await Task.Run(() => ExtractZipWithProgress(zipPath, installPath, new Progress<ProgressInfo>(info =>
                {
                    if (info != null)
                    {
                        progressBar.Value = Math.Clamp(info.Percent, 0, 100);
                        UpdateActionSafe(info.ActionText);
                        UpdateETASafe(info.ETAText);
                        UpdateStatusSafe(info.StatusText);
                    }
                })));

                UpdateActionSafe("Cleaning up...");
                UpdateStatusSafe("Removing temporary files");
                try { File.Delete(zipPath); } catch { /* ignore */ }

                // 4) Find exe and act
                var exe = Directory.GetFiles(installPath, "AffirmationImageGenerator.exe", SearchOption.AllDirectories).FirstOrDefault();
                if (exe == null)
                {
                    UpdateActionSafe("Installed (no exe found)");
                    UpdateStatusSafe("Installed, but no .exe was found to run.");
                    UpdateETASafe("--");
                    MessageBox.Show("Installed, but no executable (.exe) was found in the package.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    // close installer anyway
                    CloseInstallerWindow();
                    return;
                }

                UpdateActionSafe($"Installed. Found: {Path.GetFileName(exe)}");
                UpdateStatusSafe("Installation complete");
                UpdateETASafe("--");
                progressBar.Value = 100;

                if (chkCreateShortcut.Checked)
                {
                    TryCreateShortcut(exe);
                }

                if (chkRunAfter.Checked)
                {
                    UpdateActionSafe($"Launching {Path.GetFileName(exe)}...");
                    LaunchExe(exe, installPath);
                }

                // close the installer after launching (or completion)
                CloseInstallerWindow();
            }
            catch (OperationCanceledException)
            {
                UpdateStatusSafe("System scan timed out. Proceeding with install.");
                UpdateETASafe("--");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatusSafe("Error: " + ex.Message);
                UpdateActionSafe("Error");
                UpdateETASafe("--");
            }
            finally
            {
                // enable UI again only if the window is still open
                if (!this.IsDisposed && this.Visible)
                {
                    btnInstall.Enabled = true;
                    btnForceInstall.Enabled = true;
                    btnBrowse.Enabled = true;
                }
            }
        }

        private void CloseInstallerWindow()
        {
            // Give a tiny moment for UI to update, then close
            Task.Run(async () =>
            {
                await Task.Delay(300);
                if (this.IsHandleCreated && !this.IsDisposed)
                {
                    try
                    {
                        this.BeginInvoke(new Action(() => { this.Close(); }));
                    }
                    catch { }
                }
            });
        }

        private string FindExistingAppAnywhere(CancellationToken ct)
        {
            // Best-effort search: check a set of common folders and then BFS limited scan of logical drives.
            // Will return first matching exe path or null.
            var candidates = new List<string>();

            // check common special folders quickly
            var checkFolders = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            }.Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();

            // quick scan: look for exe names containing tokens in those folders non-recursively then shallow recursion
            foreach (var folder in checkFolders)
            {
                if (ct.IsCancellationRequested) return null;
                try
                {
                    // check top-level files
                    foreach (var f in Directory.EnumerateFiles(folder, "*.exe", SearchOption.TopDirectoryOnly))
                    {
                        if (ct.IsCancellationRequested) return null;
                        if (FileNameMatchesTokens(Path.GetFileName(f)))
                            return f;
                        if (FileMetadataMatchesTokens(f))
                            return f;
                    }

                    // shallow subdirs
                    foreach (var sub in Directory.EnumerateDirectories(folder))
                    {
                        if (ct.IsCancellationRequested) return null;
                        try
                        {
                            foreach (var f in Directory.EnumerateFiles(sub, "*.exe", SearchOption.TopDirectoryOnly))
                            {
                                if (ct.IsCancellationRequested) return null;
                                if (FileNameMatchesTokens(Path.GetFileName(f)))
                                    return f;
                                if (FileMetadataMatchesTokens(f))
                                    return f;
                            }
                        }
                        catch { /* skip inaccessible */ }
                    }
                }
                catch { /* skip inaccessible */ }
            }

            // BFS across logical drives but bounded (# directories visited) and time-limited via CancellationToken
            int maxDirsToVisit = 4000;
            var visited = 0;
            var q = new Queue<string>();
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;
                q.Enqueue(drive.RootDirectory.FullName);
            }

            while (q.Count > 0 && visited < maxDirsToVisit)
            {
                if (ct.IsCancellationRequested) return null;
                var dir = q.Dequeue();
                visited++;

                IEnumerable<string> files = null;
                try { files = Directory.EnumerateFiles(dir, "*.exe", SearchOption.TopDirectoryOnly); }
                catch { continue; }

                foreach (var f in files)
                {
                    if (ct.IsCancellationRequested) return null;
                    var name = Path.GetFileName(f);
                    if (FileNameMatchesTokens(name)) return f;
                    if (FileMetadataMatchesTokens(f)) return f;
                }

                // enqueue subdirectories (limited)
                try
                {
                    foreach (var sub in Directory.EnumerateDirectories(dir, "*", SearchOption.TopDirectoryOnly))
                    {
                        if (ct.IsCancellationRequested) return null;
                        // cheap filter: skip hidden/reparse points and system folders using attributes
                        try
                        {
                            var attr = File.GetAttributes(sub);
                            if ((attr & FileAttributes.ReparsePoint) != 0) continue;
                            if ((attr & FileAttributes.Hidden) != 0) continue;
                        }
                        catch { /* ignore */ }

                        q.Enqueue(sub);
                    }
                }
                catch { /* ignore inaccessible directories */ }
            }

            return null;
        }

        private bool FileNameMatchesTokens(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;
            var low = fileName.ToLowerInvariant();
            foreach (var t in FilenameTokensToFind)
            {
                if (low.Contains(t.ToLowerInvariant()) && low.EndsWith(".exe")) return true;
            }
            // Also treat common names
            if (low == "main.exe" || low == "app.exe" || low == "setup.exe") return true;
            return false;
        }

        private bool FileMetadataMatchesTokens(string filePath)
        {
            try
            {
                var v = FileVersionInfo.GetVersionInfo(filePath);
                var fields = new[] { v.CompanyName, v.ProductName, v.FileDescription };
                foreach (var f in fields)
                {
                    if (string.IsNullOrEmpty(f)) continue;
                    var low = f.ToLowerInvariant();
                    foreach (var t in FilenameTokensToFind)
                    {
                        if (low.Contains(t.ToLowerInvariant())) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private void LaunchExe(string exePath, string workingDirectory)
        {
            try
            {
                var psi = new ProcessStartInfo(exePath)
                {
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to launch executable: " + ex.Message, "Launch error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        #region Extraction with progress/ETA

        private void ExtractZipWithProgress(string zipPath, string destFolder, IProgress<ProgressInfo> progress)
        {
            UpdateActionSafe("Opening ZIP...");
            Directory.CreateDirectory(destFolder);

            using var archive = ZipFile.OpenRead(zipPath);
            long totalBytes = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).Sum(e => e.Length);
            if (totalBytes == 0) totalBytes = 1;

            long extractedBytes = 0;
            var sw = Stopwatch.StartNew();
            const int bufferSize = 81920;

            foreach (var entry in archive.Entries)
            {
                var entryName = entry.FullName;
                if (string.IsNullOrEmpty(entry.Name))
                {
                    var dirPath = Path.Combine(destFolder, entryName);
                    Directory.CreateDirectory(dirPath);
                    continue;
                }

                string destPath = Path.Combine(destFolder, entryName);
                Directory.CreateDirectory(Path.GetDirectoryName(destPath));

                UpdateProgress(progress, extractedBytes, totalBytes, $"Extracting: {entryName}", sw.Elapsed);

                using var src = entry.Open();
                using var dst = File.Open(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
                var buffer = new byte[bufferSize];
                int read;
                while ((read = src.Read(buffer, 0, buffer.Length)) > 0)
                {
                    dst.Write(buffer, 0, read);
                    extractedBytes += read;
                    UpdateProgress(progress, extractedBytes, totalBytes, $"Extracting: {entryName}", sw.Elapsed);
                }
            }

            sw.Stop();
            UpdateProgress(progress, totalBytes, totalBytes, "Extraction complete", sw.Elapsed);
        }

        #endregion

        #region Download with validation (slimmed) + Drive fallback retained

        private async Task DownloadValidatedZipAsync(string url, string destFilePath, IProgress<ProgressInfo> progress = null)
        {
            // 1) Try simple direct download
            await DownloadFromUrlSimpleAsync(url, destFilePath, progress);

            // 2) Quick magic-bytes check + try opening
            if (IsLikelyValidZip(destFilePath))
            {
                try
                {
                    using var z = ZipFile.OpenRead(destFilePath);
                    return; // valid ZIP, done
                }
                catch
                {
                    // fall through to fallback
                }
            }

            // 3) Try Google Drive fallback if URL contains an ID (kept for robustness)
            UpdateStatusSafe("Downloaded file is not a valid ZIP; attempting Google Drive fallback (if applicable)...");
            string fileId = GetDriveFileIdFromUrl(url);
            if (!string.IsNullOrEmpty(fileId))
            {
                await DownloadFileFromGoogleDriveAsync(fileId, destFilePath, progress);
                if (IsLikelyValidZip(destFilePath))
                {
                    try { using var z = ZipFile.OpenRead(destFilePath); return; }
                    catch { /* still invalid */ }
                }
            }

            // 4) Last resort: give clear error
            throw new InvalidOperationException("Downloaded file is not a valid ZIP archive. The server may have returned an HTML page or the download was corrupted. Try opening the download URL in a browser.");
        }

        private async Task DownloadFromUrlSimpleAsync(string url, string destFilePath, IProgress<ProgressInfo> progress = null)
        {
            using var client = new HttpClient();
            using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();

            var total = resp.Content.Headers.ContentLength ?? -1L;
            using var input = await resp.Content.ReadAsStreamAsync();
            using var output = new FileStream(destFilePath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            long read = 0;
            int r;
            var sw = Stopwatch.StartNew();
            var lastReport = DateTime.UtcNow;
            while ((r = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await output.WriteAsync(buffer, 0, r);
                read += r;
                if ((DateTime.UtcNow - lastReport).TotalMilliseconds >= 100 || read == total)
                {
                    lastReport = DateTime.UtcNow;
                    string statusText = $"{read / 1024:N0} KB downloaded";
                    int percent = total > 0 ? (int)((read * 100L) / total) : 0;

                    string etaText = "--";
                    if (total > 0 && sw.Elapsed.TotalSeconds > 0)
                    {
                        double speed = read / sw.Elapsed.TotalSeconds; // bytes/sec
                        long remaining = total - read;
                        double etaSec = speed > 0 ? (remaining / speed) : double.PositiveInfinity;
                        etaText = SafeFormatEta(etaSec);
                        statusText += $" ({FormatBytes((long)speed)}/s)";
                    }

                    progress?.Report(new ProgressInfo
                    {
                        Percent = percent,
                        ActionText = $"Downloading ({percent}%)",
                        ETAText = etaText,
                        StatusText = statusText
                    });
                }
            }

            sw.Stop();
            progress?.Report(new ProgressInfo
            {
                Percent = 100,
                ActionText = "Downloading (100%)",
                ETAText = "Done",
                StatusText = "Download complete."
            });
        }

        private async Task DownloadFileFromGoogleDriveAsync(string fileId, string destFilePath, IProgress<ProgressInfo> progress = null)
        {
            var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = true, CookieContainer = new System.Net.CookieContainer() };
            using var client = new HttpClient(handler);

            string baseUrl = $"https://drive.google.com/uc?export=download&id={fileId}";
            using (var initialResponse = await client.GetAsync(baseUrl))
            {
                if (IsDirectDownload(initialResponse))
                {
                    await SaveResponseContentToFileAsync(initialResponse, destFilePath, progress, "Downloading");
                    return;
                }

                var html = await initialResponse.Content.ReadAsStringAsync();
                var token = ExtractConfirmToken(html);

                if (string.IsNullOrEmpty(token))
                {
                    if (initialResponse.StatusCode == System.Net.HttpStatusCode.Found || initialResponse.StatusCode == System.Net.HttpStatusCode.Redirect)
                    {
                        var redirect = initialResponse.Headers.Location?.ToString();
                        if (!string.IsNullOrEmpty(redirect))
                        {
                            using var resp2 = await client.GetAsync(redirect);
                            if (IsDirectDownload(resp2))
                            {
                                await SaveResponseContentToFileAsync(resp2, destFilePath, progress, "Downloading");
                                return;
                            }
                            html = await resp2.Content.ReadAsStringAsync();
                            token = ExtractConfirmToken(html);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(token))
                {
                    string urlWithConfirm = $"https://drive.google.com/uc?export=download&confirm={token}&id={fileId}";
                    using var finalResp = await client.GetAsync(urlWithConfirm, HttpCompletionOption.ResponseHeadersRead);
                    if (IsDirectDownload(finalResp))
                    {
                        await SaveResponseContentToFileAsync(finalResp, destFilePath, progress, "Downloading");
                        return;
                    }
                    await SaveResponseContentToFileAsync(finalResp, destFilePath, progress, "Downloading");
                    return;
                }

                using var fallback = await client.GetAsync(baseUrl, HttpCompletionOption.ResponseHeadersRead);
                await SaveResponseContentToFileAsync(fallback, destFilePath, progress, "Downloading");
            }
        }

        private static bool IsDirectDownload(System.Net.Http.HttpResponseMessage resp)
        {
            if (resp == null) return false;
            if (resp.Content?.Headers?.ContentDisposition != null) return true;
            var ct = resp.Content?.Headers?.ContentType?.MediaType;
            if (!string.IsNullOrEmpty(ct) && (ct == "application/zip" || ct == "application/octet-stream")) return true;
            return false;
        }

        private static string ExtractConfirmToken(string html)
        {
            if (string.IsNullOrEmpty(html)) return null;
            var m = Regex.Match(html, @"confirm=([0-9A-Za-z_-]+)&amp;id=");
            if (m.Success) return m.Groups[1].Value;
            m = Regex.Match(html, @"confirm=([0-9A-Za-z_-]+)&");
            if (m.Success) return m.Groups[1].Value;
            m = Regex.Match(html, @"\bconfirm=([0-9A-Za-z_-]+)\b");
            if (m.Success) return m.Groups[1].Value;
            m = Regex.Match(html, @"download_warning[^\w]*([0-9A-Za-z_-]+)");
            if (m.Success) return m.Groups[1].Value;
            return null;
        }

        private async Task SaveResponseContentToFileAsync(System.Net.Http.HttpResponseMessage response, string filePath, IProgress<ProgressInfo> progress = null, string actionLabel = "Downloading")
        {
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? -1L;
            using var input = await response.Content.ReadAsStreamAsync();
            using var output = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            long read = 0;
            int r;
            var sw = Stopwatch.StartNew();
            var lastReport = DateTime.UtcNow;
            while ((r = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await output.WriteAsync(buffer, 0, r);
                read += r;
                if ((DateTime.UtcNow - lastReport).TotalMilliseconds >= 100 || read == total)
                {
                    lastReport = DateTime.UtcNow;
                    string statusText = $"{read / 1024:N0} KB downloaded";
                    int percent = total > 0 ? (int)((read * 100L) / total) : 0;

                    string etaText = "--";
                    if (total > 0 && sw.Elapsed.TotalSeconds > 0)
                    {
                        double speed = read / sw.Elapsed.TotalSeconds; // bytes/sec
                        long remaining = total - read;
                        double etaSec = speed > 0 ? (remaining / speed) : double.PositiveInfinity;
                        etaText = SafeFormatEta(etaSec);
                        statusText += $" ({FormatBytes((long)speed)}/s)";
                    }

                    progress?.Report(new ProgressInfo
                    {
                        Percent = percent,
                        ActionText = $"{actionLabel} ({percent}%)",
                        ETAText = etaText,
                        StatusText = statusText
                    });
                }
            }

            sw.Stop();
            progress?.Report(new ProgressInfo
            {
                Percent = 100,
                ActionText = $"{actionLabel} (100%)",
                ETAText = "Done",
                StatusText = "Download complete."
            });
        }

        // Extract file id from a Drive url or query (?id= or /d/<id>/)
        private string GetDriveFileIdFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            var m = Regex.Match(url, @"[?&]id=([A-Za-z0-9_\-]+)");
            if (m.Success) return m.Groups[1].Value;
            m = Regex.Match(url, @"/d/([A-Za-z0-9_\-]+)");
            return m.Success ? m.Groups[1].Value : null;
        }

        private bool IsLikelyValidZip(string path)
        {
            try
            {
                using var fs = File.OpenRead(path);
                if (fs.Length < 4) return false;
                Span<byte> b = stackalloc byte[4];
                fs.Read(b);
                // PK\x03\x04 or PK\x05\x06 or PK\x07\x08
                return b[0] == 0x50 && b[1] == 0x4B;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Helpers & UI updates

        private void UpdateProgress(IProgress<ProgressInfo> progress, long done, long total, string action, TimeSpan elapsed)
        {
            int percent = total > 0 ? (int)((done * 100L) / total) : 0;
            string eta = "--";
            if (elapsed.TotalSeconds > 0 && total > 0)
            {
                double speed = done / elapsed.TotalSeconds;
                long remaining = Math.Max(0, total - done);
                double etaSeconds = speed > 0 ? remaining / speed : double.PositiveInfinity;
                eta = SafeFormatEta(etaSeconds);
            }
            progress?.Report(new ProgressInfo
            {
                Percent = percent,
                ActionText = action,
                ETAText = eta,
                StatusText = $"{done / 1024:N0} KB / {total / 1024:N0} KB"
            });
        }

        private void UpdateActionSafe(string text)
        {
            if (lblAction.InvokeRequired) lblAction.Invoke(new Action(() => lblAction.Text = $"Action: {text}"));
            else lblAction.Text = $"Action: {text}";
        }

        private void UpdateETASafe(string text)
        {
            if (lblETA.InvokeRequired) lblETA.Invoke(new Action(() => lblETA.Text = $"ETA: {text}"));
            else lblETA.Text = $"ETA: {text}";
        }

        private void UpdateStatusSafe(string text)
        {
            if (lblStatus.InvokeRequired) lblStatus.Invoke(new Action(() => lblStatus.Text = $"Status: {text}"));
            else lblStatus.Text = $"Status: {text}";
        }

        private static string FormatTimeSpan(TimeSpan ts)
        {
            if (double.IsInfinity(ts.TotalSeconds) || ts.TotalSeconds < 0) return "--";
            if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            if (ts.TotalMinutes >= 1) return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
            return $"{ts.Seconds}s";
        }

        // Safely format an ETA in seconds to a friendly string, guarding against overflow/NaN/Infinity
        private static string SafeFormatEta(double etaSec)
        {
            if (double.IsNaN(etaSec) || double.IsInfinity(etaSec) || etaSec < 0)
                return "--";

            // Protect against values larger than TimeSpan.MaxValue.TotalSeconds
            if (etaSec > TimeSpan.MaxValue.TotalSeconds)
                return "--";

            return FormatTimeSpan(TimeSpan.FromSeconds(etaSec));
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes > 1_000_000_000) return $"{bytes / 1_000_000_000.0:F2} GB";
            if (bytes > 1_000_000) return $"{bytes / 1_000_000.0:F2} MB";
            if (bytes > 1000) return $"{bytes / 1000.0:F2} KB";
            return $"{bytes} B";
        }

        #endregion

        #region Shortcut creation

        private void TryCreateShortcut(string targetExePath)
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string exeName = Path.GetFileNameWithoutExtension(targetExePath);
                string lnkPath = Path.Combine(desktop, exeName + ".lnk");
                CreateShortcut(lnkPath, targetExePath, Path.GetDirectoryName(targetExePath), $"Shortcut to {exeName}");
                UpdateStatusSafe($"Shortcut created: {Path.GetFileName(lnkPath)}");
            }
            catch (Exception ex)
            {
                UpdateStatusSafe("Shortcut creation failed: " + ex.Message);
            }
        }

        // Uses WScript.Shell COM object via dynamic to create .lnk file (works on Windows)
        private void CreateShortcut(string shortcutPath, string targetPath, string workingDirectory, string description)
        {
            // If a previous shortcut exists, overwrite it
            try { if (File.Exists(shortcutPath)) File.Delete(shortcutPath); } catch { }

            Type t = Type.GetTypeFromProgID("WScript.Shell");
            if (t == null) throw new InvalidOperationException("WScript.Shell not available on this system.");
            dynamic shell = Activator.CreateInstance(t);
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = workingDirectory;
            shortcut.WindowStyle = 1;
            shortcut.Description = description;
            // Optionally: shortcut.IconLocation = targetPath + ",0";
            shortcut.Save();
        }

        #endregion

        private class ProgressInfo
        {
            public int Percent { get; set; }
            public string ActionText { get; set; }
            public string ETAText { get; set; }
            public string StatusText { get; set; }
        }
    }
}

#endif
