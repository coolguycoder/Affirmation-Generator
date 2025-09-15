using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace AffirmationImageGeneratorNice
{
    public class Settings
    {
        public string? OutputFolder { get; set; }
        public string? FontPath { get; set; }
        public decimal FontSize { get; set; }
        public int TextColor { get; set; }
        public bool RandomBase { get; set; }
        public bool ProcessAllImages { get; set; }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            Application.Run(new WizardForm());
        }
    }

    // Simple GitHub Releases updater. Performs a version check against the latest release
    // and prompts the user to download and apply the update. If the user accepts, downloads
    // the .zip asset, extracts to a temporary folder, and spawns a PowerShell script to replace
    // the current application folder and launch the new executable.
    public class Updater
    {
        private readonly string owner;
        private readonly string repo;
        private const string GitHubApiLatest = "https://api.github.com/repos/{0}/{1}/releases/latest";

        public enum UpdateAction
        {
            None,
            UpdatedAndRelaunched,
            Skipped
        }

        public Updater(string owner, string repo)
        {
            this.owner = owner;
            this.repo = repo;
        }

        public async System.Threading.Tasks.Task<UpdateAction> CheckAndPromptForUpdateAsync()
        {
            try
            {
                using var http = new System.Net.Http.HttpClient();
                http.DefaultRequestHeaders.UserAgent.ParseAdd("AffirmationImageGenerator-Updater/1.0");
                var url = string.Format(GitHubApiLatest, owner, repo);
                var resp = await http.GetAsync(url).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return UpdateAction.None;

                var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                var latestTag = root.GetProperty("tag_name").GetString() ?? "";
                var name = root.GetProperty("name").GetString() ?? latestTag;
                var body = root.TryGetProperty("body", out var b) ? b.GetString() : null;


                // Always compare release tag to app version string
                var currentVersion = GetCurrentVersionString();
                bool isNewer = !string.IsNullOrWhiteSpace(latestTag) && !IsSameVersion(currentVersion, latestTag);
                // Optionally, also check published date if you want to support timestamp-based updates
                // (uncomment below if you want both checks)
                // DateTimeOffset? releasePublished = null;
                // if (root.TryGetProperty("published_at", out var pub) && pub.ValueKind == System.Text.Json.JsonValueKind.String)
                // {
                //     var pubStr = pub.GetString();
                //     if (!string.IsNullOrWhiteSpace(pubStr) && DateTimeOffset.TryParse(pubStr, out var dto))
                //         releasePublished = dto.ToUniversalTime();
                // }
                // DateTime localExeWriteUtc = DateTime.MinValue;
                // try
                // {
                //     var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                //     if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
                //         localExeWriteUtc = File.GetLastWriteTimeUtc(exePath);
                // }
                // catch { }
                // if (releasePublished.HasValue && releasePublished.Value.UtcDateTime > localExeWriteUtc)
                //     isNewer = true;

                if (!isNewer) return UpdateAction.None;

                // compute a friendly installed version string for the prompt
                var installedVersion = GetCurrentVersionString();
                var message = $"A new release is available: {name} ({latestTag}).\nInstalled: {installedVersion}\n\nRelease notes:\n{(body ?? "(no notes)")}";

                var res = MessageBox.Show(message + "\n\nDo you want to download and install the update now?", "Update available", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (res != DialogResult.Yes) return UpdateAction.Skipped;

                // find first zip asset
                string? zipUrl = null;
                if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var a in assets.EnumerateArray())
                    {
                        if (a.TryGetProperty("browser_download_url", out var d) && a.TryGetProperty("name", out var n))
                        {
                            var nameAsset = n.GetString() ?? "";
                            if (nameAsset.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                            {
                                zipUrl = d.GetString();
                                break;
                            }
                        }
                    }
                }

                if (zipUrl == null) MessageBox.Show("No .zip asset found for the latest release.");
                else
                {
                    try
                    {
                        var currentDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
                        var zipPath = Path.Combine(currentDir, "update_release.zip");
                        var stream = await http.GetStreamAsync(zipUrl).ConfigureAwait(false);
                        var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
                        await stream.CopyToAsync(fs).ConfigureAwait(false);
                        fs.Close();
                        stream.Dispose();

                        // Extract zip directly into current app folder, overwrite files
                        System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, currentDir, true);
                        try { File.Delete(zipPath); } catch { }

                        // Relaunch the app
                        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = exePath,
                            WorkingDirectory = currentDir,
                            UseShellExecute = true
                        };
                        System.Diagnostics.Process.Start(psi);

                        Application.Exit();
                        return UpdateAction.UpdatedAndRelaunched;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Update failed: " + ex.Message);
                        return UpdateAction.None;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Update check failed: " + ex.Message);
            }
            return UpdateAction.None;
        }

    public static string GetCurrentVersionString()
        {
            try
            {
                var asm = System.Reflection.Assembly.GetEntryAssembly();
                var ver = asm?.GetName().Version?.ToString();
                if (!string.IsNullOrWhiteSpace(ver)) return ver!;
            }
            catch { }
            // fallback: exe last write time
            try
            {
                var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (exe != null && File.Exists(exe))
                {
                    var t = File.GetLastWriteTimeUtc(exe);
                    return t.ToString("yyyyMMddHHmmss");
                }
            }
            catch { }
            return "0";
        }

        private static bool IsSameVersion(string current, string latestTag)
        {
            if (string.IsNullOrWhiteSpace(current)) return false;
            // normalize: strip leading 'v' from tag
            var tag = latestTag.TrimStart('v', 'V');
            return current.Contains(tag) || tag.Contains(current);
        }

        private static string? FindExeInDirectory(string path)
        {
            try
            {
                // first try top-level exe matching repo name
                var files = Directory.GetFiles(path, "*.exe", SearchOption.AllDirectories);
                if (files.Length == 0) return null;
                foreach (var f in files)
                {
                    if (Path.GetFileNameWithoutExtension(f).IndexOf("Affirmation", StringComparison.OrdinalIgnoreCase) >= 0)
                        return f;
                }
                return files[0];
            }
            catch { return null; }
        }
    }

    public class Options
    {
        public string FontFile = "";
        public float FontSize = 56f;
        public Color Color = Color.White;
        public int X = 30;
        public int Y = 0;
        public int Width = 0;
        public int Height = 0;
        public bool RandomBase = true;
        public bool ProcessAllImages = false;
        public string Prefix = "affirmation_";
    }

    public class WizardForm : Form
    {
    // Fields for WizardForm
    private int stepIndex = 0;
    private Panel[] steps = new Panel[4];
    private Panel headerPanel = new Panel();
    private Label titleLabel = new Label();
    private FlowLayoutPanel stepPanel = new FlowLayoutPanel();
    private MenuStrip mainMenu;
    private ToolStripMenuItem versionMenuItem;
    private ToolStripMenuItem checkUpdateMenuItem;
    private TextBox basePathBox = new TextBox();
    private Button btnChooseBase = new Button();
    private ListBox baseImagesList = new ListBox();
    private Label baseCountLabel = new Label();
    private Label lOut = new Label();
    private Label lFont = new Label();
    private Label lSize = new Label();
    private Label lColor = new Label();
    private TextBox outputFolderBox = new TextBox();
    private Button btnChooseOutput = new Button();
    private TextBox fontPathBox = new TextBox();
    private Button btnChooseFont = new Button();
    private NumericUpDown fontSizeUp = new NumericUpDown();
    private Button btnChooseColor = new Button();
    private Panel colorPreview = new Panel();
    private CheckBox chkRandomBase = new CheckBox();
    private ListBox lstAffirmations = new ListBox();
    private TextBox txtNewAffirmation = new TextBox();
    private Button btnAddAff = new Button();
    private Button btnRemoveAff = new Button();
    private Button btnLoadList = new Button();
    private Button btnSaveList = new Button();
    private PictureBox previewBox = new PictureBox();
    private Button btnPreview = new Button();
    private Button btnGenerate = new Button();
    // ...existing navigation, setup, font, color, settings fields already present...
    private Button btnBack = new Button();
    private Button btnNext = new Button();

    // setup mode controls
    private Button btnHiddenSetup = new Button();   // tiny hidden button to enter setup
    private Button btnSaveSetup = new Button();     // visible only while in setup mode

    // fonts and styling
    private PrivateFontCollection pfc = new PrivateFontCollection();
    private Font? loadedFont = null;
    private Color chosenColor = Color.White;

    // settings
    private readonly string settingsPath = Path.Combine(AppContext.BaseDirectory, "affirmation_settings.json");
    private bool setupMode = false;
    private bool processAllImages = false;

    public WizardForm()
    {
        // Initialize menu controls
        mainMenu = new MenuStrip();
        versionMenuItem = new ToolStripMenuItem($"Version: {Updater.GetCurrentVersionString()}");
        checkUpdateMenuItem = new ToolStripMenuItem("Check for Updates");
        checkUpdateMenuItem.Click += async (s, e) => await CheckForUpdatesAsync();
        var aboutMenuItem = new ToolStripMenuItem("About");
        aboutMenuItem.Click += (s, e) => ShowAboutDialog();
        mainMenu.Items.Add(versionMenuItem);
        mainMenu.Items.Add(checkUpdateMenuItem);
        mainMenu.Items.Add(aboutMenuItem);
    Controls.Add(mainMenu);
    MainMenuStrip = mainMenu;

        this.Text = "Affirmation Image Maker";
        this.Width = 1100;
        this.Height = 760;
        this.KeyPreview = true;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Color.FromArgb(245, 247, 250);
        this.Font = new Font("Segoe UI", 10);
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;

    BuildHeader();
    BuildSteps();
    BuildNavigation();
    WireKeys();

    // Always bring header and navigation to front
    headerPanel.BringToFront();
    btnBack.BringToFront();
    btnNext.BringToFront();

    // load saved settings if present (auto-apply to controls, but do NOT change starting page)
    LoadSettingsIfExists();

    // populate base images list immediately (so preview works right away if a base/folder is already set)
    PopulateBaseList();

    // ensure we start on setup page so user sees Step 1 on load
    stepIndex = 0;
    UpdateStep();
    this.FormClosing += WizardForm_FormClosing;
    }

    // Event handler for form closing
    private void WizardForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (setupMode)
        {
            SaveSettings();
        }
    }
        // ...existing code...
        private void ShowAboutDialog()
        {
            var version = Updater.GetCurrentVersionString();
            var credits = "Affirmation Generator\nBy coolguycoder\nGitHub: github.com/coolguycoder/Affirmation-Generator";

            var aboutForm = new Form() 
            {
                Text = "About Affirmation Generator",
                Width = 420,
                Height = 260,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var lblTitle = new Label() 
            {
                Text = "Affirmation Generator",
                Font = new Font("Segoe UI Semibold", 16f),
                AutoSize = true,
                Location = new Point(24, 18)
            };
            aboutForm.Controls.Add(lblTitle);

            var lblVersion = new Label() 
            {
                Text = $"Version: {version}",
                Font = new Font("Segoe UI", 11f),
                AutoSize = true,
                Location = new Point(24, 54)
            };
            aboutForm.Controls.Add(lblVersion);

            var lblCredits = new Label() 
            {
                Text = "By coolguycoder\nGitHub: github.com/coolguycoder/Affirmation-Generator",
                Font = new Font("Segoe UI", 10f),
                AutoSize = true,
                Location = new Point(24, 84)
            };
            aboutForm.Controls.Add(lblCredits);

            var btnUpdate = new Button() 
            {
                Text = "Check for Updates",
                Width = 140,
                Height = 36,
                Location = new Point(24, 130)
            };
            btnUpdate.Click += async (s, e) =>
            {
                btnUpdate.Enabled = false;
                btnUpdate.Text = "Checking...";
                var updater = new Updater("coolguycoder", "Affirmation-Generator");
                var updateResult = await updater.CheckAndPromptForUpdateAsync();
                if (updateResult == Updater.UpdateAction.None)
                {
                    MessageBox.Show("No updates available.", "Update Check", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                btnUpdate.Enabled = true;
                btnUpdate.Text = "Check for Updates";
            };
            aboutForm.Controls.Add(btnUpdate);

            var btnOK = new Button() 
            {
                Text = "OK",
                Width = 80,
                Height = 32,
                Location = new Point(aboutForm.ClientSize.Width - 104, aboutForm.ClientSize.Height - 56),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                DialogResult = DialogResult.OK
            };
            aboutForm.Controls.Add(btnOK);
            aboutForm.AcceptButton = btnOK;

            aboutForm.ShowDialog(this);
        }

            // ...existing code...

            // Duplicate constructor code removed; all initialization is inside the WizardForm constructor above.
        // Fix for missing CheckForUpdatesAsync method
        private async System.Threading.Tasks.Task CheckForUpdatesAsync()
        {
            var updater = new Updater("coolguycoder", "Affirmation-Generator");
            var updateResult = await updater.CheckAndPromptForUpdateAsync();
            if (updateResult == Updater.UpdateAction.None)
            {
                MessageBox.Show("No updates available.", "Update Check", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BuildHeader()
        {
            headerPanel.SetBounds(0, 0, ClientSize.Width, 120);
            headerPanel.BackColor = Color.White;
            headerPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(headerPanel);

            titleLabel.Text = "Affirmation Image Maker";
            titleLabel.Font = new Font("Segoe UI Semibold", 20f);
            titleLabel.ForeColor = Color.FromArgb(35, 47, 63);
            titleLabel.SetBounds(20, 12, 600, 36);
            headerPanel.Controls.Add(titleLabel);

            var subtitle = new Label
            {
                Text = "Make nice image cards from simple lines of text",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(100, 110, 120),
            };
            subtitle.SetBounds(22, 52, 600, 26);
            headerPanel.Controls.Add(subtitle);

            stepPanel.FlowDirection = FlowDirection.LeftToRight;
            stepPanel.SetBounds(650, 18, 420, 80);
            stepPanel.Padding = new Padding(8);
            stepPanel.AutoSize = false;
        headerPanel.Controls.Add(stepPanel);
        for (int i = 0; i < 4; i++)
        {
            var card = CreateStepCard(i + 1, i == 0 ? "Setup" : i == 1 ? "Affirmations" : i == 2 ? "Preview" : "About");
            stepPanel.Controls.Add(card);
        }

        // tiny hidden setup button (top-right of header). small, flat, visually unobtrusive.
        btnHiddenSetup.SetBounds(headerPanel.Width - 28, 12, 16, 16);
        btnHiddenSetup.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnHiddenSetup.FlatStyle = FlatStyle.Flat;
        btnHiddenSetup.FlatAppearance.BorderSize = 0;
        btnHiddenSetup.BackColor = Color.Transparent;
        btnHiddenSetup.TabStop = false;
        btnHiddenSetup.Click += (s, e) => EnterSetupMode();
        headerPanel.Controls.Add(btnHiddenSetup);

        // save-setup button (only visible while in setup mode)
        btnSaveSetup.Text = "Save setup";
        btnSaveSetup.SetBounds(headerPanel.Width - 140, 64, 120, 32);
        btnSaveSetup.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnSaveSetup.Click += (s, e) => SaveSetupAndExit();
        btnSaveSetup.Visible = false;
        headerPanel.Controls.Add(btnSaveSetup);
    }

        private Control CreateStepCard(int num, string text)
        {
            var panel = new Panel { Width = 128, Height = 64, BackColor = Color.FromArgb(250, 250, 250) };
            panel.Margin = new Padding(6);
            panel.Padding = new Padding(6);
            panel.BorderStyle = BorderStyle.None;
            panel.Tag = num - 1;

            panel.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(230, 230, 230));
                e.Graphics.DrawRectangle(p, 0, 0, panel.Width - 1, panel.Height - 1);
            };

            var lblNum = new Label
            {
                Text = num.ToString(),
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = Color.FromArgb(60, 70, 90),
                TextAlign = ContentAlignment.MiddleCenter,
                Width = 28,
                Height = 28,
            };
            lblNum.SetBounds(8, 8, 28, 28);
            panel.Controls.Add(lblNum);

            var lblText = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(90, 100, 110),
            };
            lblText.SetBounds(44, 10, 76, 36);
            panel.Controls.Add(lblText);

            return panel;
        }

        private void BuildSteps()
        {
            steps = new Panel[4];
            int top = headerPanel.Bottom + 12;
            int left = 20;
            int stepW = ClientSize.Width - 40;
            int stepH = ClientSize.Height - top - 90;

            var p1 = new Panel { Left = left, Top = top, Width = stepW, Height = stepH, BackColor = Color.Transparent };
            BuildStep1(p1);
            Controls.Add(p1);
            steps[0] = p1;

            var p2 = new Panel { Left = left, Top = top, Width = stepW, Height = stepH, BackColor = Color.Transparent };
            BuildStep2(p2);
            Controls.Add(p2);
            steps[1] = p2;

            var p3 = new Panel { Left = left, Top = top, Width = stepW, Height = stepH, BackColor = Color.Transparent };
            BuildStep3(p3);
            Controls.Add(p3);
            steps[2] = p3;

            var p4 = new Panel { Left = left, Top = top, Width = stepW, Height = stepH, BackColor = Color.Transparent };
            BuildStep4(p4);
            Controls.Add(p4);
            steps[3] = p4;
        }

        private void BuildStep1(Panel pane)
        {
            var card = MakeCard(20, 20, 640, 480);
            pane.Controls.Add(card);

            int left = 18;
            int top = 18;
            int labelW = 140;
            int ctrlW = 420;
            int spacing = 36;

            // Base image controls
            var lBase = new Label { Text = "Base image or folder:", Left = left, Top = top, Width = labelW };
            basePathBox.SetBounds(left + labelW, top - 2, ctrlW - 110, 28);
            btnChooseBase.Text = "Browse";
            btnChooseBase.SetBounds(basePathBox.Right + 8, top - 2, 90, 28);
            btnChooseBase.Click += BtnChooseBase_Click;

            // Output folder controls
            var outTop = btnChooseBase.Bottom + spacing;
            lOut.Text = "Output folder:";
            lOut.SetBounds(left, outTop, labelW, 28);
            outputFolderBox.SetBounds(left + labelW, outTop, ctrlW - 110, 28);
            btnChooseOutput.Text = "Browse";
            btnChooseOutput.SetBounds(outputFolderBox.Right + 8, outTop, 90, 28);
            btnChooseOutput.Click += BtnChooseOutput_Click;

            // Font controls
            var fontTop = outputFolderBox.Bottom + spacing;
            lFont.Text = "Font file:";
            lFont.SetBounds(left, fontTop, labelW, 28);
            fontPathBox.SetBounds(left + labelW, fontTop, ctrlW - 110, 28);
            btnChooseFont.Text = "Browse";
            btnChooseFont.SetBounds(fontPathBox.Right + 8, fontTop, 90, 28);
            btnChooseFont.Click += BtnChooseFont_Click;

            lSize.Text = "Font size:";
            lSize.SetBounds(left, fontPathBox.Bottom + 8, labelW, 28);
            fontSizeUp.SetBounds(left + labelW, fontPathBox.Bottom + 8, 80, 28);
            fontSizeUp.Minimum = 10;
            fontSizeUp.Maximum = 120;
            fontSizeUp.Value = 56;

            // Color controls
            var colorTop = fontSizeUp.Bottom + spacing;
            lColor.Text = "Text color:";
            lColor.SetBounds(left, colorTop, labelW, 28);
            btnChooseColor.Text = "Choose";
            btnChooseColor.SetBounds(left + labelW, colorTop, 90, 28);
            btnChooseColor.Click += BtnChooseColor_Click;
            colorPreview.SetBounds(btnChooseColor.Right + 8, colorTop, 40, 28);
            colorPreview.BackColor = chosenColor;

            // Base images list
            baseImagesList.SetBounds(left, btnChooseColor.Bottom + spacing, 320, 120);
            baseCountLabel.SetBounds(left, baseImagesList.Bottom + 8, 200, 24);
            baseCountLabel.Text = "No images selected";

            card.Controls.AddRange(new Control[] {
                lBase, basePathBox, btnChooseBase,
                lOut, outputFolderBox, btnChooseOutput,
                lFont, fontPathBox, btnChooseFont, lSize, fontSizeUp,
                lColor, btnChooseColor, colorPreview,
                baseImagesList, baseCountLabel
            });

            // auto-populate when basePathBox changes by leaving focus
            basePathBox.Leave += (s, e) => PopulateBaseList();
        }

        private void BuildStep2(Panel pane)
        {
            var cardLeft = MakeCard(20, 20, 520, 520);
            var cardRight = MakeCard(cardLeft.Right + 18, 20, 480, 520);
            pane.Controls.Add(cardLeft);
            pane.Controls.Add(cardRight);

            lstAffirmations.SetBounds(12, 12, cardLeft.Width - 24, 380);
            lstAffirmations.Font = new Font("Segoe UI", 11);
            lstAffirmations.ItemHeight = 22;

            txtNewAffirmation.SetBounds(12, lstAffirmations.Bottom + 12, 360, 32);
            txtNewAffirmation.Font = new Font("Segoe UI", 11);

            btnAddAff.Text = "Add";
            btnAddAff.SetBounds(txtNewAffirmation.Right + 8, txtNewAffirmation.Top, cardLeft.Width - txtNewAffirmation.Right - 20, 34);
            btnAddAff.Click += BtnAddAff_Click;

            btnRemoveAff.Text = "Remove Selected";
            btnRemoveAff.SetBounds(btnAddAff.Left, btnAddAff.Bottom + 8, btnAddAff.Width, 34);
            btnRemoveAff.Click += BtnRemoveAff_Click;

            btnLoadList.Text = "Load .txt";
            btnLoadList.SetBounds(12, btnAddAff.Bottom + 8, 120, 34);
            btnLoadList.Click += BtnLoadList_Click;

            btnSaveList.Text = "Save .txt";
            btnSaveList.SetBounds(btnLoadList.Right + 8, btnLoadList.Top, 120, 34);
            btnSaveList.Click += BtnSaveList_Click;

            cardLeft.Controls.AddRange(new Control[] {
                lstAffirmations, txtNewAffirmation, btnAddAff, btnRemoveAff, btnLoadList, btnSaveList
            });

            lstAffirmations.DoubleClick += (s, e) =>
            {
                if (lstAffirmations.SelectedIndex >= 0)
                {
                    var cur = lstAffirmations.Items[lstAffirmations.SelectedIndex].ToString();
                    var res = Prompt.ShowDialog("Edit affirmation", "Edit", cur ?? "");
                    if (res != null) lstAffirmations.Items[lstAffirmations.SelectedIndex] = res;
                }
            };
    }

        private void BuildStep3(Panel pane)
        {
            var card = MakeCard(20, 20, 940, 520);
            pane.Controls.Add(card);

            previewBox.SetBounds(card.Left + 14, card.Top + 14, 560, 420);
            previewBox.SizeMode = PictureBoxSizeMode.Zoom;
            previewBox.BorderStyle = BorderStyle.FixedSingle;

            btnPreview.Text = "Preview Selected";
            btnPreview.SetBounds(previewBox.Right + 18, previewBox.Top + 20, 240, 44);
            btnPreview.Click += BtnPreview_Click;

            btnGenerate.Text = "Generate";
            btnGenerate.SetBounds(previewBox.Right + 18, previewBox.Top + 86, 240, 72);
            btnGenerate.Font = new Font("Segoe UI Semibold", 12f);
            btnGenerate.BackColor = Color.FromArgb(46, 139, 87);
            btnGenerate.ForeColor = Color.White;
            btnGenerate.FlatStyle = FlatStyle.Flat;
            btnGenerate.Click += BtnGenerate_Click;

            card.Controls.AddRange(new Control[] { previewBox, btnPreview, btnGenerate });
    }

        private void BuildStep4(Panel pane)
        {
            var card = MakeCard(20, 20, 640, 480);
            pane.Controls.Add(card);

            var versionLabel = new Label
            {
                Text = $"Version: {System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version}",
                Font = new Font("Segoe UI Semibold", 14f),
                ForeColor = Color.FromArgb(35, 47, 63),
                Location = new Point(20, 20),
                AutoSize = true
            };
            card.Controls.Add(versionLabel);

            var updateButton = new Button
            {
                Text = "Check for Updates",
                Location = new Point(20, versionLabel.Bottom + 20),
                AutoSize = true
            };
            updateButton.Click += async (s, e) =>
            {
                var updater = new Updater("coolguycoder", "Affirmation-Generator");
                var updateResult = await updater.CheckAndPromptForUpdateAsync();
                if (updateResult == Updater.UpdateAction.None)
                {
                    MessageBox.Show("No updates available.", "Update Check", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            card.Controls.Add(updateButton);
    }

        private Panel MakeCard(int x, int y, int w, int h)
        {
            var p = new Panel
            {
                Left = x,
                Top = y,
                Width = w,
                Height = h,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None
            };
            p.Paint += (s, e) =>
            {
                using var br = new SolidBrush(Color.FromArgb(245, 247, 250));
                e.Graphics.FillRectangle(br, new Rectangle(0, 0, p.Width, p.Height));
                using var pen = new Pen(Color.FromArgb(230, 230, 230));
                e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
            };
            return p;
    }

        private void BuildNavigation()
        {
            btnBack.Text = "Back";
            btnBack.SetBounds(ClientSize.Width - 340, ClientSize.Height - 62, 120, 40);
            btnBack.Click += (s, e) => { if (stepIndex > 0) stepIndex--; UpdateStep(); };

            btnNext.Text = "Next";
            btnNext.SetBounds(ClientSize.Width - 200, ClientSize.Height - 62, 120, 40);
            btnNext.Click += (s, e) => { if (stepIndex < steps.Length - 1) stepIndex++; UpdateStep(); };

            Controls.AddRange(new Control[] { btnBack, btnNext });
    }

        private void UpdateStep()
        {
            for (int i = 0; i < steps.Length; i++)
            {
                steps[i].Visible = (i == stepIndex);
                if (steps[i].Visible) steps[i].BringToFront();
            }
            // Always bring mainMenu, headerPanel, and navigation buttons to front
            if (mainMenu != null) mainMenu.BringToFront();
            if (headerPanel != null) headerPanel.BringToFront();
            btnBack.BringToFront();
            btnNext.BringToFront();

            foreach (Control c in stepPanel.Controls)
            {
                if (c is Panel p && p.Tag is int idx)
                {
                    bool active = idx == stepIndex;
                    p.BackColor = active ? Color.FromArgb(46, 139, 87) : Color.FromArgb(250, 250, 250);
                    foreach (Control ch in p.Controls)
                    {
                        if (ch is Label lb) lb.ForeColor = active ? Color.White : Color.FromArgb(90, 100, 110);
                    }
                }
            }

            btnBack.Enabled = stepIndex > 0;
            btnNext.Enabled = stepIndex < steps.Length - 1;
            btnGenerate.Visible = (stepIndex == steps.Length - 2);
            btnNext.Visible = (stepIndex < steps.Length - 1);

            var headerHint = stepIndex == 0 ? "Step 1 — pick backgrounds & output" :
                             stepIndex == 1 ? "Step 2 — enter your affirmations" :
                             stepIndex == 2 ? "Step 3 — preview and generate" :
                             "About";
            titleLabel.Text = "Affirmation Image Maker — " + headerHint;

            // show/hide save-setup button
            btnSaveSetup.Visible = setupMode;
    }

        // keys
        // Handles Ctrl+A in mainMenu
        private void MainMenu_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A)
            {
                ShowAboutDialog();
                e.Handled = true;
            }
        }
        private void WireKeys()
        {
            this.KeyDown += WizardForm_KeyDown;
    }

        private void WizardForm_KeyDown(object? sender, KeyEventArgs e)
        {
            // press F to enter setup mode
            if (e.KeyCode == Keys.F)
            {
                EnterSetupMode();
                e.Handled = true;
            }

            // Ctrl+A to open About window
            if (e.Control && e.KeyCode == Keys.A)
            {
                ShowAboutDialog();
                e.Handled = true;
            }

            // Ctrl+D to open settings
            if (e.Control && e.KeyCode == Keys.D)
            {
                ShowSettingsDialog();
                e.Handled = true;
            }

            // Ctrl+S to open snake game
            if (e.Control && e.KeyCode == Keys.S)
            {
                var snakeForm = new SnakeForm();
                snakeForm.ShowDialog(this);
                e.Handled = true;
            }

            // quick preview: press P when on step 3
            if (e.KeyCode == Keys.P && stepIndex == 2)
            {
                BtnPreview_Click(this, EventArgs.Empty);
            }
        }

        private void ShowSettingsDialog()
        {
            using (var settingsForm = new SettingsForm(chkRandomBase.Checked, processAllImages))
            {
                if (settingsForm.ShowDialog(this) == DialogResult.OK)
                {
                    chkRandomBase.Checked = settingsForm.chkRandomBase.Checked;
                    processAllImages = settingsForm.chkProcessAllImages.Checked;
                    SaveSettings();
                }
            }
        }

        

        private void EnterSetupMode()
        {
            setupMode = true;
            // reveal the setup panel (step 0) and navigate to it
            stepIndex = 0;
            UpdateStep();
            MessageBox.Show("Setup mode: change Output / Font / Colour then click 'Save setup' (top-right). Base image is NOT saved.");
    }

        private void SaveSettings()
        {
            var s = new Settings
            {
                OutputFolder = string.IsNullOrWhiteSpace(outputFolderBox.Text) ? null : outputFolderBox.Text,
                FontPath = string.IsNullOrWhiteSpace(fontPathBox.Text) ? null : fontPathBox.Text,
                FontSize = fontSizeUp.Value,
                TextColor = chosenColor.ToArgb(),
                RandomBase = chkRandomBase.Checked,
                ProcessAllImages = processAllImages
            };
            try
            {
                var opts = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(settingsPath, JsonSerializer.Serialize(s, opts));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to save settings: " + ex.Message);
            }
        }

        private void SaveSetupAndExit()
        {
            SaveSettings();
            setupMode = false;
            UpdateStep();
            MessageBox.Show("Setup saved.");
        }

        private void LoadSettingsIfExists()
        {
            try
            {
                if (!File.Exists(settingsPath)) return;
                var json = File.ReadAllText(settingsPath);
                var s = JsonSerializer.Deserialize<Settings>(json);
                if (s == null) return;

                if (!string.IsNullOrWhiteSpace(s.OutputFolder)) outputFolderBox.Text = s.OutputFolder!;
                if (!string.IsNullOrWhiteSpace(s.FontPath)) fontPathBox.Text = s.FontPath!;
                fontSizeUp.Value = s.FontSize >= fontSizeUp.Minimum && s.FontSize <= fontSizeUp.Maximum ? s.FontSize : fontSizeUp.Value;
                chosenColor = Color.FromArgb(s.TextColor);
                colorPreview.BackColor = chosenColor;
                chkRandomBase.Checked = s.RandomBase;
                processAllImages = s.ProcessAllImages;

                // NOTE: do NOT change stepIndex here — leave UI on setup so user sees it on load
            }
            catch
            {
                // ignore load errors
            }
        }

        // event handlers & helpers

        private void BtnChooseBase_Click(object? sender, EventArgs e)
        {
            using var of = new OpenFileDialog();
            of.Title = "Select base image (or cancel to pick folder)";
            of.Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp|All files|*.*";
            if (of.ShowDialog() == DialogResult.OK)
            {
                basePathBox.Text = of.FileName;
                PopulateBaseList();
            }
            else
            {
                using var fd = new FolderBrowserDialog();
                if (fd.ShowDialog() == DialogResult.OK)
                {
                    basePathBox.Text = fd.SelectedPath;
                    PopulateBaseList();
                }
            }
        }

        private void BaseImagesList_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // update preview if requested
            if (stepIndex == 2 && lstAffirmations.Items.Count > 0)
                BtnPreview_Click(this, EventArgs.Empty);
        }

        private void PopulateBaseList()
        {
            baseImagesList.Items.Clear();
            var path = basePathBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(path)) 
            {
                baseCountLabel.Text = "No images selected";
                return;
            }
            var images = ResolveBaseImages(path);
            foreach (var im in images) baseImagesList.Items.Add(im);
            baseCountLabel.Text = $"{images.Count} image(s)";

            // auto-select first so preview uses it when available
            if (baseImagesList.Items.Count > 0 && baseImagesList.SelectedIndex < 0)
                baseImagesList.SelectedIndex = 0;
        }

        private void BtnChooseOutput_Click(object? sender, EventArgs e)
        {
            using var fd = new FolderBrowserDialog();
            if (fd.ShowDialog() == DialogResult.OK) outputFolderBox.Text = fd.SelectedPath;
        }

        private void BtnChooseFont_Click(object? sender, EventArgs e)
        {
            using var of = new OpenFileDialog();
            of.Filter = "Fonts|*.ttf;*.otf|All|*.*";
            if (of.ShowDialog() == DialogResult.OK)
            {
                fontPathBox.Text = of.FileName;
                try
                {
                    pfc = new PrivateFontCollection();
                    pfc.AddFontFile(of.FileName);
                    var ff = pfc.Families.First();
                    loadedFont?.Dispose();
                    loadedFont = new Font(ff, (float)fontSizeUp.Value, FontStyle.Regular);
                }
                catch (Exception ex) { MessageBox.Show("Failed to load font: " + ex.Message); }
            }
        }

        private void BtnChooseColor_Click(object? sender, EventArgs e)
        {
            using var cd = new ColorDialog();
            cd.Color = chosenColor;
            if (cd.ShowDialog() == DialogResult.OK)
            {
                chosenColor = cd.Color;
                colorPreview.BackColor = chosenColor;
            }
        }

        private void BtnAddAff_Click(object? sender, EventArgs e)
        {
            var t = txtNewAffirmation.Text.Trim();
            if (string.IsNullOrEmpty(t)) return;
            lstAffirmations.Items.Add(t);
            txtNewAffirmation.Clear();
        }

        private void BtnRemoveAff_Click(object? sender, EventArgs e)
        {
            if (lstAffirmations.SelectedIndex >= 0) lstAffirmations.Items.RemoveAt(lstAffirmations.SelectedIndex);
        }

        private void BtnLoadList_Click(object? sender, EventArgs e)
        {
            using var of = new OpenFileDialog();
            of.Filter = "Text files|*.txt|All|*.*";
            if (of.ShowDialog() == DialogResult.OK)
            {
                var lines = File.ReadAllLines(of.FileName).Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
                lstAffirmations.Items.Clear();
                lstAffirmations.Items.AddRange(lines);
            }
        }

        private void BtnSaveList_Click(object? sender, EventArgs e)
        {
            using var sf = new SaveFileDialog();
            sf.Filter = "Text files|*.txt|All|*.*";
            if (sf.ShowDialog() == DialogResult.OK)
            {
                var items = lstAffirmations.Items.Cast<object>().Select(o => o.ToString() ?? "");
                File.WriteAllLines(sf.FileName, items);
            }
        }

        // improved preview that shows placeholders and auto-selects first base if none selected
        private void BtnPreview_Click(object? sender, EventArgs e)
        {
            if (lstAffirmations.Items.Count == 0)
            {
                MessageBox.Show("No affirmations in the list.");
                return;
            }

            var idx = Math.Max(0, lstAffirmations.SelectedIndex);
            var text = lstAffirmations.Items[idx].ToString() ?? "";

            var baseList = ResolveBaseImages(basePathBox.Text);
            if (baseList.Count == 0)
            {
                // show a placeholder telling the user no base is selected
                var placeholder = CreatePlaceholderPreview("No base image selected\nChoose a file or folder in Setup", previewBox.Width, previewBox.Height);
                previewBox.Image?.Dispose();
                previewBox.Image = placeholder;
                previewBox.Refresh();
                return;
            }

            string baseFile;
            if (baseImagesList.SelectedIndex >= 0)
                baseFile = baseImagesList.SelectedItem?.ToString() ?? baseList[0];
            else
                baseFile = baseList[0];

            // if baseImagesList hasn't selected anything but there are items, auto-select the first so user knows what's in the list
            if (baseImagesList.Items.Count > 0 && baseImagesList.SelectedIndex < 0)
                baseImagesList.SelectedIndex = 0;

            try
            {
                if (!File.Exists(baseFile))
                {
                    var placeholder = CreatePlaceholderPreview("Selected base file missing", previewBox.Width, previewBox.Height);
                    previewBox.Image?.Dispose();
                    previewBox.Image = placeholder;
                    previewBox.Refresh();
                    return;
                }

                using var src = (Bitmap)Image.FromFile(baseFile);
                var bmp = RenderAffirmationToBitmap(src, text, GetOptions(), previewBox.Width, previewBox.Height);
                previewBox.Image?.Dispose();
                previewBox.Image = bmp;
                previewBox.Refresh();
            }
            catch (Exception ex)
            {
                // show a helpful placeholder instead of nothing
                var placeholder = CreatePlaceholderPreview("Preview error: " + ex.Message, previewBox.Width, previewBox.Height);
                previewBox.Image?.Dispose();
                previewBox.Image = placeholder;
                previewBox.Refresh();
            }
        }

        private void BtnGenerate_Click(object? sender, EventArgs e)
        {
            if (lstAffirmations.Items.Count == 0) { MessageBox.Show("No affirmations to generate."); return; }
            var outDir = outputFolderBox.Text;
            if (string.IsNullOrWhiteSpace(outDir)) { MessageBox.Show("Choose output folder."); return; }
            Directory.CreateDirectory(outDir);
            var baseList = ResolveBaseImages(basePathBox.Text);
            if (baseList.Count == 0) { MessageBox.Show("No base images found."); return; }

            var opts = GetOptions();
            var rnd = new Random();
            var totalImages = processAllImages ? lstAffirmations.Items.Count * baseList.Count : lstAffirmations.Items.Count;
            using var prog = new ProgressForm(totalImages);
            prog.Show(this);

            if (processAllImages)
            {
                int imageCounter = 0;
                for (int i = 0; i < lstAffirmations.Items.Count; i++)
                {
                    for (int j = 0; j < baseList.Count; j++)
                    {
                        Application.DoEvents();
                        var text = lstAffirmations.Items[i].ToString() ?? "";
                        string baseFile = baseList[j];

                        try
                        {
                            using var src = (Bitmap)Image.FromFile(baseFile);
                            using var bmp = RenderAffirmationToBitmap(src, text, opts, 0, 0);
                            var filename = $"{opts.Prefix}{imageCounter + 1:000}.png";
                            var outPath = Path.Combine(outDir, filename);
                            bmp.Save(outPath, ImageFormat.Png);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Failed on item {i + 1}: {ex.Message}");
                        }
                        prog.Increment();
                        imageCounter++;
                    }
                }
            }
            else
            {
                for (int i = 0; i < lstAffirmations.Items.Count; i++)
                {
                    Application.DoEvents();
                    var text = lstAffirmations.Items[i].ToString() ?? "";

                    // if user selected specific base in list, use that for all; otherwise random or first
                    string baseFile;
                    if (baseImagesList.SelectedIndex >= 0)
                        baseFile = baseImagesList.SelectedItem?.ToString() ?? baseList[0];
                    else if (baseList.Count == 1 || !opts.RandomBase)
                        baseFile = baseList[0];
                    else
                        baseFile = baseList[rnd.Next(baseList.Count)];

                    try
                    {
                        using var src = (Bitmap)Image.FromFile(baseFile);
                        using var bmp = RenderAffirmationToBitmap(src, text, opts, 0, 0);
                        var filename = $"{opts.Prefix}{i + 1:000}.png";
                        var outPath = Path.Combine(outDir, filename);
                        bmp.Save(outPath, ImageFormat.Png);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed on item {i + 1}: {ex.Message}");
                    }
                    prog.Increment();
                }
            }
            prog.Close();
            MessageBox.Show("Done. Check the output folder.");
        }

        private List<string> ResolveBaseImages(string path)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(path)) return list;
            if (File.Exists(path)) { list.Add(path); return list; }
            if (Directory.Exists(path))
            {
                var exts = new[] { ".png", ".jpg", ".jpeg", ".bmp" };
                foreach (var f in Directory.EnumerateFiles(path))
                    if (exts.Contains(Path.GetExtension(f).ToLowerInvariant())) list.Add(f);
            }
            return list;
        }

        private Options GetOptions()
        {
            return new Options
            {
                FontFile = fontPathBox.Text,
                FontSize = (float)fontSizeUp.Value,
                Color = chosenColor,
                X = 30,
                Y = 0,
                Width = 0,
                Height = 0,
                RandomBase = chkRandomBase.Checked,
                ProcessAllImages = processAllImages,
                Prefix = "affirmation_"
            };
        }

        private System.Drawing.Bitmap RenderAffirmationToBitmap(Bitmap srcImage, string text, Options opts, int previewW, int previewH)
        {
            int outW = opts.Width > 0 ? opts.Width : srcImage.Width;
            int outH = opts.Height > 0 ? opts.Height : srcImage.Height;

            if (previewW > 0 && previewH > 0)
            {
                var tmp = RenderAffirmationToBitmap(srcImage, text, opts, 0, 0);
                var scaled = new Bitmap(previewW, previewH);
                using var g = Graphics.FromImage(scaled);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.Clear(Color.Black);
                g.DrawImage(tmp, 0, 0, previewW, previewH);
                tmp.Dispose();
                return scaled;
            }

            var dst = new Bitmap(outW, outH);
            using (var g = Graphics.FromImage(dst))
            {
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

                g.Clear(Color.Black);
                g.DrawImage(srcImage, 0, 0, outW, outH);

                Font drawFont;
                if (!string.IsNullOrWhiteSpace(opts.FontFile) && File.Exists(opts.FontFile))
                {
                    try
                    {
                        var localPfc = new PrivateFontCollection();
                        localPfc.AddFontFile(opts.FontFile);
                        var ff = localPfc.Families.First();
                        drawFont = new Font(ff, opts.FontSize, FontStyle.Bold, GraphicsUnit.Point);
                    }
                    catch
                    {
                        drawFont = new Font(FontFamily.GenericSansSerif, opts.FontSize, FontStyle.Bold, GraphicsUnit.Point);
                    }
                }
                else
                {
                    drawFont = new Font("Segoe UI Semibold", opts.FontSize, FontStyle.Bold, GraphicsUnit.Point);
                }

                var rect = new RectangleF(opts.X, opts.Y, outW - opts.X * 2, outH - opts.Y * 2);
                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

                using var textBrush = new SolidBrush(opts.Color);
                using var outlineBrush = new SolidBrush(Color.FromArgb(220, 0, 0, 0));

                for (int ox = -2; ox <= 2; ox++)
                    for (int oy = -2; oy <= 2; oy++)
                    {
                        if (Math.Abs(ox) + Math.Abs(oy) == 0) continue;
                        g.DrawString(text, drawFont, outlineBrush, new RectangleF(rect.X + ox, rect.Y + oy, rect.Width, rect.Height), sf);
                    }
                g.DrawString(text, drawFont, textBrush, rect, sf);

                drawFont.Dispose();
            }
            return dst;
        }

        // placeholder preview generator
        private Bitmap CreatePlaceholderPreview(string message, int w, int h)
        {
            int width = Math.Max(240, w);
            int height = Math.Max(160, h);
            var bmp = new Bitmap(width, height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(250, 250, 252));
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                using var b = new SolidBrush(Color.FromArgb(170, 170, 170));
                using var f = new Font("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Point);
                var rc = new Rectangle(0, 0, width, height);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(message, f, b, rc, sf);
            }
            return bmp;
        }
    }
}