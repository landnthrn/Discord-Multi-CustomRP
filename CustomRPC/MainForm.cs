using CommonMark;
using DiscordRPC;
using DiscordRPC.Helper;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Octokit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Serialization;
using Application = System.Windows.Forms.Application;
using Button = System.Windows.Forms.Button;
using Timer = System.Timers.Timer;

namespace CustomRPC
{
    /*
     * TODO: profiles not as external files
     */

    /// <summary>
    /// A struct for handling preset importing/exporting.
    /// </summary>
    [Serializable]
    public struct Preset
    {
        public string ID;
        public int Type;
        public int Display;
        public string Name;
        public string Details;
        public string DetailsURL;
        public string State;
        public string StateURL;
        public int PartySize;
        public int PartyMax;
        public int Timestamps;
        public DateTime CustomTimestamp;
        public bool CustomTimestampEndEnabled;
        public DateTime CustomTimestampEnd;
        public string LargeKey;
        public string LargeText;
        public string LargeURL;
        public string SmallKey;
        public string SmallText;
        public string SmallURL;
        public string Button1Text;
        public string Button1URL;
        public string Button2Text;
        public string Button2URL;
        public bool Enabled;
        [XmlIgnore]
        public bool EnabledSpecified;
    }

    /// <summary>
    /// A struct for getting available image assets for current application.
    /// </summary>
    public struct ImageAssets
    {
        public string ID;
        public string Type;
        public string Name;
    }

    /// <summary>
    /// A struct describing activity types to use in a type selection combobox.
    /// </summary>
    public struct PresenceType
    {
        public string Name { get; private set; }
        public ActivityType Type { get; private set; }

        public PresenceType(string name, ActivityType type)
        {
            Name = name;
            Type = type;
        }
    }

    /// <summary>
    /// A struct describing display types to use in a display selection combobox.
    /// </summary>
    public struct DisplayType
    {
        public string Name { get; private set; }
        public StatusDisplayType Type { get; private set; }

        public DisplayType(StatusDisplayType type)
        {
            Name = type.ToString();
            Type = type;
        }
    }

    /// <summary>
    /// An enum for the timestamp setting.
    /// </summary>
    public enum TimestampType
    {
        SinceLastConnection = 0,
        SinceStartup = 1,
        LocalTime = 2,
        Custom = 3, // These are hardcoded for backwards compatibility
        SincePresenceUpdate
    }

    public partial class MainForm : Form
    {
        const string MultiSlotPresetExtension = ".cmrp";
        const string LoadPresetFilter =
            "Multi-RP's Preset (*.cmrp)|*.cmrp|Uni-RP Preset (*.crp)|*.crp|Both (*.cmrp) (*.crp)|*.cmrp;*.crp";
        const string SavePresetFilter =
            "Multi-RP's Preset (*.cmrp)|*.cmrp|Uni-RP Preset (*.crp)|*.crp";

        static bool IsMultiSlotPresetFile(string path) =>
            !string.IsNullOrEmpty(path) &&
            path.EndsWith(MultiSlotPresetExtension, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Last preset file opened via Load / double-click / drag-drop (not alternating).
        /// Save Changes writes only to this path.
        /// </summary>
        string loadedPresetPath;

        /// <summary>
        /// Chart fingerprint captured when <see cref="loadedPresetPath"/> was set or saved.
        /// Used for the menu "(Altered)" marker.
        /// </summary>
        string loadedPresetBaselineFingerprint;

        ToolStripLabel toolStripLabelActivePresetPrefix;
        ToolStripLabel toolStripLabelActivePresetName;

        /// <summary>
        /// Prevents some event handlers from executing while the app is loading.
        /// </summary>
        bool loading = true;
        /// <summary>
        /// Avoids recursion in <see cref="OnlyNumbers"/>.
        /// </summary>
        /// <remarks>
        /// ...This is stupid.
        /// </remarks>
        bool toAvoidRecursion = false;

        /// <summary>
        /// This bool makes sure the "The app will run in the tray" tooltip shows only once per session
        /// </summary>
        bool wasTooltipShown = false;

        /// <summary>
        /// A timer that updates presence at midnight for local-time slots.
        /// </summary>
        Timer localTimeTimer = new Timer();

        /// <summary>
        /// Settings of the application. Self-explanatory.
        /// </summary>
        Properties.Settings settings = Properties.Settings.Default;

        /// <summary>
        /// GitHub client used for fetching all the releases of the app.
        /// </summary>
        GitHubClient githubClient = new GitHubClient(new ProductHeaderValue("CustomRP"));
        /// <summary>
        /// Latest release of the app available for downloading.
        /// </summary>
        Release latestRelease;

        /// <summary>
        /// A DateTime object showing since what moment you can make an API request in <see cref="FetchAssets(object, EventArgs)"/>.
        /// </summary>
        DateTime nextAssetCheck = DateTime.Now;
        /// <summary>
        /// A string showing what application ID was checked last in <see cref="FetchAssets(object, EventArgs)"/>.
        /// </summary>
        string lastIDChecked = "";

        /// <summary>
        /// A part of the URL path for docs.customrp.xyz links used in the app.
        /// Has the form of empty string if the app is in English or the docs aren't translated to the current UI language, "v/[locale]/" or "v/[locale]-[country]/" otherwise.
        /// </summary>
        string localeUrl = "";

        /// <summary>
        /// Path to the autorun link file.
        /// </summary>
        readonly string linkPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup) + @"\CustomRP" + (Program.IsSecondInstance ? " 2" : "") + ".lnk";

        /// <summary>
        /// Resource manager. Yes I know, very descriptive.
        /// </summary>
        readonly System.ComponentModel.ComponentResourceManager res = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));

        /// <summary>
        /// List of locales docs.customrp.xyz is translated to.
        /// </summary>
        readonly List<string> translatedWikiLocales = new List<string> { "de", "es", "fi", "fr", "hi", "ko", "nl", "pl", "ro", "ru", "th", "uk", "vi" };

        /// <summary>
        /// List of available activity types.
        /// </summary>
        readonly List<PresenceType> presenceTypes = new List<PresenceType>
            {
                new PresenceType(Strings.presenceTypePlaying, ActivityType.Playing),
                new PresenceType(Strings.presenceTypeListening, ActivityType.Listening),
                new PresenceType(Strings.presenceTypeWatching, ActivityType.Watching),
                new PresenceType(Strings.presenceTypeCompeting, ActivityType.Competing),
            };

        /// <summary>
        /// List of available display types.
        /// </summary>
        readonly List<DisplayType> displayTypes = new List<DisplayType>
            {
                new DisplayType(StatusDisplayType.Name),
                new DisplayType(StatusDisplayType.Details),
                new DisplayType(StatusDisplayType.State),
            };

        /// <summary>
        /// Unicode character "No-Break Space" (" ").
        /// </summary>
        readonly string U00A0 = "\u00A0";

        /// <summary>
        /// Default ID to connect with if the user doesn't provide any.
        /// </summary>
        readonly string defaultID = "896771305108553788";

        /// <summary>
        /// The constructor of the form.
        /// </summary>
        /// <param name="preset">File location of a preset to load on startup or <see langword="null"/>.</param>
        public MainForm(string preset)
        {
            InitializeComponent();

            // Populating language related menu items
            Utils.LanguagesSetup(translatorsToolStripMenuItem, OpenPersonsPage, languageToolStripMenuItem, ChangeLanguage);

            // Populating supporters menu items
            Utils.SupportersSetup(supportersToolStripMenuItem, OpenPersonsPage);

            // Setting up dark/light mode
            ThemeSetup();

            // Associate .cmrp with the same preset icon as .crp (portable + already-installed)
            Utils.EnsureCmrpFileAssociation();

            // Setting up startup link for current user (enabled by default)
#if !DEBUG
            StartupSetup();
#endif

            // Setting up a midnight presence update timer
            localTimeTimer.AutoReset = false;
            localTimeTimer.Elapsed += LocalTimeTimer_Elapsed;

            // Defer preset import until the slot system exists (CLI / drag-drop on startup).
            string startupPresetPath = preset as string;

            // Setting up checkboxes because apparently property binding doesn't work
            runOnStartupToolStripMenuItem.Checked = settings.runOnStartup;
            startMinimizedToolStripMenuItem.Checked = settings.startMinimized;
            autoconnectToolStripMenuItem.Checked = settings.autoconnect;
            // Hide Check for Updates so this fork does not pull CustomRP releases.
            settings.checkUpdates = false;
            Utils.SaveSettings();
            checkUpdatesToolStripMenuItem.Visible = false;
            checkUpdatesToolStripMenuItem.Enabled = false;
            if (checkForUpdatesToolStripMenuItem != null)
            {
                checkForUpdatesToolStripMenuItem.Visible = false;
                checkForUpdatesToolStripMenuItem.Enabled = false;
            }
            if (downloadUpdateToolStripMenuItem != null)
            {
                downloadUpdateToolStripMenuItem.Visible = false;
                downloadUpdateToolStripMenuItem.Enabled = false;
            }
            // Fork: no telemetry to upstream / App Center for end users.
            settings.analytics = false;
            Utils.SaveSettings();
            if (allowAnalyticsToolStripMenuItem != null)
            {
                allowAnalyticsToolStripMenuItem.Visible = false;
                allowAnalyticsToolStripMenuItem.Enabled = false;
            }
            // checkUpdates + Allow Analytics are both hidden — drop the unused separator above them.
            if (toolStripSeparatorSettings1 != null)
                toolStripSeparatorSettings1.Visible = false;
            darkModeToolStripMenuItem.Checked = settings.darkMode;

            // Helper function that recursively gets all DropDownItems
            IEnumerable<ToolStripItem> GetAllDropDownItems(ToolStripDropDownItem item)
            {
                foreach (ToolStripItem subItem in item.DropDownItems)
                {
                    yield return subItem;

                    if (subItem is ToolStripDropDownItem subContainer)
                    {
                        foreach (ToolStripItem nestedItem in GetAllDropDownItems(subContainer))
                        {
                            yield return nestedItem;
                        }
                    }
                }
            }

            // Checks the chosen language setting
            foreach (var toolStripItemObj in GetAllDropDownItems(languageToolStripMenuItem))
            {
                if (toolStripItemObj is ToolStripSeparator)
                    continue;

                var langItem = (ToolStripMenuItem)toolStripItemObj;

                if ((string)langItem.Tag == settings.language)
                {
                    langItem.Checked = true;
                    break;
                }
            }

            // Setting up activity type combobox
            comboBoxType.DisplayMember = "Name";
            comboBoxType.ValueMember = "Type";
            comboBoxType.DataSource = presenceTypes;
            comboBoxType.SelectedValue = (ActivityType)settings.type;

            // Setting up display combobox
            comboBoxDisplay.DisplayMember = "Name";
            comboBoxDisplay.ValueMember = "Type";
            comboBoxDisplay.DataSource = displayTypes;
            comboBoxDisplay.SelectedValue = (StatusDisplayType)settings.display;

            // Set up tags for the radio buttons
            radioButtonLastConnection.Tag = TimestampType.SinceLastConnection;
            radioButtonStartTime.Tag = TimestampType.SinceStartup;
            radioButtonPresence.Tag = TimestampType.SincePresenceUpdate;
            radioButtonLocalTime.Tag = TimestampType.LocalTime;
            radioButtonCustom.Tag = TimestampType.Custom;

            // Checks the needed timestamp radiobuttons because settings binding can't do that
            switch ((TimestampType)settings.timestamps)
            {
                case TimestampType.SinceLastConnection: radioButtonLastConnection.Checked = true; break;
                case TimestampType.SincePresenceUpdate: radioButtonPresence.Checked = true; break;
                case TimestampType.SinceStartup: radioButtonStartTime.Checked = true; break;
                case TimestampType.LocalTime: radioButtonLocalTime.Checked = true; break;
                case TimestampType.Custom: radioButtonCustom.Checked = true; break;
            }

            // Enable or disable the date and time pickers depending on whether a custom timestamp setting is chosen
            tableLayoutPanelCustomTimestamps.Enabled = radioButtonCustom.Checked;

            // Enable or disable End timestamp picker
            dateTimePickerTimestampEnd.Enabled = checkBoxTimestampEnd.Checked;

            // Change the date and time pickers' format according to system's culture
            dateTimePickerTimestampStart.CustomFormat = dateTimePickerTimestampEnd.CustomFormat = 
                CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern + " "
                + CultureInfo.CurrentCulture.DateTimeFormat.LongTimePattern;

            // If the app was launched for the first time (including since update), set the default time to current one
            if (settings.customTimestamp.CompareTo(new DateTime(1969, 1, 1, 0, 0, 0)) == 0)
                settings.customTimestamp = DateTime.Now;
            if (settings.customTimestampEnd.CompareTo(new DateTime(1969, 1, 1, 0, 0, 0)) == 0)
                settings.customTimestampEnd = DateTime.Now;

            // Change the earliest date user can choose according to user's timezone
            dateTimePickerTimestampStart.MinDate = dateTimePickerTimestampEnd.MinDate =
                new DateTime(1970, 1, 1, 0, 0, 1, DateTimeKind.Utc).ToLocalTime();

            // Localize the Disconnect button in the tray menu, unless it is already localized
            if (trayMenuDisconnect.Text == res.GetString("trayMenuDisconnect.Text", CultureInfo.GetCultureInfo("en")))
                trayMenuDisconnect.Text = res.GetString("buttonDisconnect.Text");

            SyncTrayMenuForRpcMode();

            // Localize the statusbar text in case the autoconnect is disabled
            toolStripStatusLabelStatus.Text = Strings.statusDisconnected;

            DarkToolTipHelper.Configure(toolTipInfo);

            // Add version info to main window title
            Text += " " + VersionHelper.GetVersionString(Application.ProductVersion);
#if DEBUG
            Text += " DEV";
#endif

            // Slightly changing the name of the tray tooptip and main window title for a second instance of the app
            if (Program.IsSecondInstance)
            {
                trayIcon.Text += " (2)";
                Text += " (2)";
            }

            // Set up a localeUrl variable if docs are translated to the current UI language.
            if (translatedWikiLocales.FindLast(localePredicate) is string locale && locale != "")
                localeUrl = "v/" + locale + '/';

            bool localePredicate(string loc)
            {
                var currentLocale = CultureInfo.CurrentUICulture.Name;
                return loc == currentLocale || loc == currentLocale.Split('-')[0];
            }

            // Set up shortcuts for certain menu elements
            newPresetToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.N;
            loadPresetToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.O;
            savePresetToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.S;
            saveChangesToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.S;
            uploadAssetsToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.U;
            openTheManualToolStripMenuItem.ShortcutKeys = Keys.F1;

            loading = false;

            InitializeSlotSystem();
            SetupActivePresetMenuLabels();
            UpdateSaveChangesMenuItem();
            UpdateActivePresetMenuLabels();

            if (!string.IsNullOrEmpty(startupPresetPath))
                LoadPreset(startupPresetPath, addAsNewSlot: true, confirmMultiSlotReplace: true);

            // Starts minimized to tray by default, unless you just changed language
            if (settings.changedLanguage || !settings.startMinimized)
                Show();

            // That means user has upgraded from older version without that flag
            if (settings.id != "" && settings.firstStart)
            {
                settings.firstStart = false;
                Utils.SaveSettings();
            }

            if (settings.firstStart)
            {
                // Asking if the user wants the manual
                var messageBox = QuietMessageBox.Show(this, Strings.firstTimeRunText, Strings.firstTimeRun, MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);

                if (messageBox == DialogResult.Yes)
                    // Opens the setup manual
                    Utils.OpenInBrowser("https://docs.customrp.xyz/" + localeUrl + "setting-up");

                settings.firstStart = false;
                Utils.SaveSettings();
            }

            if ((settings.changedLanguage && settings.wasConnected) || (settings.autoconnect && !settings.changedLanguage))
                AutoconnectOnStartup();

            CheckIfCrashed();

            // Update checks disabled for this fork (menu items hidden).
            // if (settings.checkUpdates)
            //     CheckForUpdates();

            settings.changedLanguage = false;
        }

        /// <summary>
        /// Handles communication between instances.
        /// </summary>
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == Program.WM_SHOWFIRSTINSTANCE)
            {
                MaximizeFromTray();
            }
            else if (message.Msg == Program.WM_IMPORTPRESET)
            {
                LoadPreset(File.ReadAllText(Program.IPCPath), addAsNewSlot: true, confirmMultiSlotReplace: true);
            }
            else if (message.Msg == 0x0016) // WM_ENDSESSION
            {
                SaveSlotsToStorage();
                Utils.SaveSettings();
                Application.Exit();
            }

            base.WndProc(ref message);
        }

        /// <summary>
        /// Sets up color scheme of the application.
        /// </summary>
        private void ThemeSetup()
        {
            if (WinApi.UseImmersiveDarkMode(Handle))
            {
                // Hacky way to forcefully redraw a window's title bar
                Opacity = 0.99;
                Opacity = 1;
            }

            CurrentColors.Update();

            BackColor = CurrentColors.BgColor;
            ForeColor = CurrentColors.TextColor;

            var allControls = Controls.Cast<Control>().Concat(flowLayoutPanelParty.Controls.Cast<Control>());

            foreach (Control ctrl in allControls)
            {
                if (ctrl is TextBox || ctrl is ComboBox || ctrl is NumericUpDown)
                {
                    ctrl.BackColor = CurrentColors.BgTextFields;
                    ctrl.ForeColor = CurrentColors.TextColor;
                }

                if (ctrl is Panel panel && panel.Name.StartsWith("panelSeparator"))
                    panel.BackColor = CurrentColors.TextColor;
            }

            if (slotService != null)
            {
                switch (ConnectionManager.State)
                {
                    case ConnectionState.Disconnected:
                    case ConnectionState.Connecting:
                    case ConnectionState.UpdatingPresence:
                        if (GetSelectedSlot()?.IsConnected != true)
                            textBoxID.BackColor = CurrentColors.BgTextFields;
                        break;
                    case ConnectionState.Connected:
                        if (GetSelectedSlot()?.IsConnected == true)
                            textBoxID.BackColor = CurrentColors.BgTextFieldsSuccess;
                        break;
                    case ConnectionState.Error:
                        if (GetSelectedSlot()?.ConnectionState == SlotConnectionState.Error)
                            textBoxID.BackColor = CurrentColors.BgTextFieldsError;
                        break;
                }
            }
            else
            {
                textBoxID.BackColor = CurrentColors.BgTextFields;
            }

            if (settings.darkMode)
            {
                ToolStripManager.Renderer = new DarkModeRenderer();
            }
            else
            {
                ToolStripManager.Renderer = new LightModeRenderer();
            }

            ApplyActivitiesPanelTheme();
            ApplyAlternatingPresetsTheme();
            UpdateCyclingEditLock();
            UpdateActivePresetMenuLabels();
        }

        void ClearActionButtonFocus(object sender, EventArgs e)
        {
            if (ActiveControl is Button && buttonFocusSink != null)
                buttonFocusSink.Focus();
        }

        /// <summary>
        /// Will be called at midnight to update local-time presence slots.
        /// </summary>
        private void LocalTimeTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Invoke(new MethodInvoker(() => UpdateAllEnabledSlots()));
        }

        /// <summary>
        /// Checks if the app has crashed in the previous session.
        /// </summary>
        private async void CheckIfCrashed()
        {
            if (!await Crashes.HasCrashedInLastSessionAsync())
                return;

            var report = await Crashes.GetLastSessionCrashReportAsync();

            new ErrorReportViewer(report.StackTrace).ShowDialog();
        }

        /// <summary>
        /// Checks for updates.
        /// </summary>
        /// <param name="manual"><see langword="True"/> if the user requested the check, <see langword="false"/> otherwise.</param>
        private async void CheckForUpdates(bool manual = false)
        {
            IReadOnlyList<Release> releases;

            // Fetching all releases
            try
            {
                releases = await githubClient.Repository.Release.GetAll("maximmax42", "Discord-CustomRP");
                latestRelease = releases[0];
            }
            catch
            {
                // If there's no internet or Github is down, do nothing, unless it's a user requested update check
                if (manual)
                    QuietMessageBox.Show(this, Strings.errorNoInternet, Strings.error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string latestStr = latestRelease.TagName;

            if (latestStr == settings.ignoreVersion && !manual)
                return; // The user ignored this version; this gets ignored if the user requested the update check manually, maybe they changed their mind?

            Version current = VersionHelper.GetVersion(Application.ProductVersion);
            Version latest = VersionHelper.GetVersion(latestStr);

            if (current.CompareTo(latest) < 0) // If update is available...
            {
                var changelogBuilder = new System.Text.StringBuilder(); // ...build the changelog...

                foreach (var release in releases)
                {
                    Version releaseVer = VersionHelper.GetVersion(release.TagName);

                    if (releaseVer.Equals(current))
                        break;

                    var releaseBodyArr = release.Body.Split("\r\n".ToCharArray());
                    var releaseBody = "";

                    // Removing 1st ("Changes:") and 2 last lines (description to exe and zip files) from the changelog
                    for (int i = 1; i < releaseBodyArr.Length - 3; i++)
                    {
                        releaseBody += releaseBodyArr[i] + "\r\n";
                    }

                    changelogBuilder
                        .Append("<h3>" + release.Name + "</h3>")
                        .Append(CommonMarkConverter.Convert(releaseBody.Trim()));
                }

                string changelog = changelogBuilder.ToString();

                downloadUpdateToolStripMenuItem.Visible = true; // ...activate the "Download update" button...
                MaximizeFromTray(); // ...make sure the app window is shown if it was minimized...

                var messageBox = new UpdatePrompt(current, latest, changelog).ShowDialog(); // ...and show a dialog box telling there's an update

                if (messageBox == DialogResult.Yes)
                {
                    DownloadAndInstallUpdate();
                    downloadUpdateToolStripMenuItem.Enabled = false;
                }
                else if (messageBox == DialogResult.Ignore)
                {
                    settings.ignoreVersion = latestStr;
                    Analytics.TrackEvent("Ignored an update", new Dictionary<string, string> {
                        { "Version", latestStr }
                    });
                }

                checkUpdatesToolStripMenuItem.Checked = settings.checkUpdates;

                if (!settings.checkUpdates || messageBox == DialogResult.Ignore)
                    downloadUpdateToolStripMenuItem.Visible = false; // If user doesn't want update notifications, let's not bother them
            }
            else if (manual) // If there's no update available and it was a user initiated update check, notify them about it
                QuietMessageBox.Show(this, Strings.noUpdatesFound, Strings.information, MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
        }

        /// <summary>
        /// Downloading and installing latest update from GitHub.
        /// </summary>
        public async void DownloadAndInstallUpdate()
        {
            if (latestRelease == null)
                return; // Probably shouldn't happen, but just in case

            // Check whether the application is installed or used as a portable app
            int fileType = Application.StartupPath.EndsWith("Roaming\\CustomRP") ? 0 : 1; // 0 is exe, 1 is zip

            var wc = new WebClient();
            var exec = Path.GetTempPath() + latestRelease.Assets[fileType].Name;

            wc.DownloadProgressChanged += DownloadProgress;

            while (true)
            {
                try
                {
                    if (!File.Exists(exec))
                        await wc.DownloadFileTaskAsync(latestRelease.Assets[fileType].BrowserDownloadUrl, exec);

                    if (fileType == 1) // Open up app's folder for ease of manual update
                        Process.Start(Application.StartupPath);

                    Process.Start(exec);

                    Application.Exit();
                    break;
                }
                catch
                {
                    try
                    {
                        if (File.Exists(exec))
                            File.Delete(exec);
                    }
                    catch
                    {
                    }

                    var result = QuietMessageBox.Show(this, Strings.errorUpdateFailed, Strings.error, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);

                    if (result == DialogResult.Yes)
                        continue;
                    else if (result == DialogResult.No)
                        Utils.OpenInBrowser(latestRelease.Assets[fileType].BrowserDownloadUrl);

                    downloadUpdateToolStripMenuItem.Enabled = true;
                    downloadUpdateToolStripMenuItem.Text = res.GetString("downloadUpdateToolStripMenuItem.Text");

                    break;
                }
            }
        }

        /// <summary>
        /// Provides visual feedback for downloading.
        /// </summary>
        private void DownloadProgress(object sender, DownloadProgressChangedEventArgs e)
        {
            downloadUpdateToolStripMenuItem.Text = e.ProgressPercentage.ToString() + "%";
        }

        /// <summary>
        /// Sets up new presence from the selected slot.
        /// </summary>
        private bool SetPresence()
        {
            var slot = GetSelectedSlot();
            if (slot == null || !slot.IsConnected)
                return false;

            SaveSlotsToStorage();
            return SetPresenceForSlot(slot);
        }

        /// <summary>
        /// Sets up the startup link for the app.
        /// </summary>
        private void StartupSetup()
        {
            try
            {
                if (settings.runOnStartup && !File.Exists(linkPath)) // If run on startup is enabled and the link isn't in the Startup folder
                {
                    IWshRuntimeLibrary.WshShell wsh = new IWshRuntimeLibrary.WshShell();
                    IWshRuntimeLibrary.IWshShortcut shortcut = wsh.CreateShortcut(linkPath) as IWshRuntimeLibrary.IWshShortcut;
                    shortcut.Description = "Discord Custom Rich Presence Manager";
                    shortcut.TargetPath = Environment.CurrentDirectory + @"\CustomRP.exe";
                    shortcut.WorkingDirectory = Environment.CurrentDirectory + @"\";
                    if (Program.IsSecondInstance)
                        shortcut.Arguments = "--second-instance";
                    shortcut.Save();
                }
                else if (!settings.runOnStartup && File.Exists(linkPath)) // If run on startup is disabled and the link is in the Startup folder
                    File.Delete(linkPath);
            }
            catch (Exception e)
            {
                // I *think* this would only happen if an antivirus would intervene saving/deleting a file in a user folder,
                // therefore I'm just allowing the user to quickly try changing the option again
                runOnStartupToolStripMenuItem.Checked = !settings.runOnStartup;
                QuietMessageBox.Show($"{Strings.errorStartupShortcut} {e.Message}", Strings.error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Called when you drag a file into app's window.
        /// </summary>
        private void DragDropEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.All;
            else
                e.Effect = DragDropEffects.None;
        }

        /// <summary>
        /// Called upon dropping a file.
        /// </summary>
        private void DragDropHandler(object sender, DragEventArgs e)
        {
            if (!(e.Data.GetData(DataFormats.FileDrop) is string[] files) || files.Length == 0)
                return;

            var crpFiles = files.Where(f => f.EndsWith(".crp", StringComparison.OrdinalIgnoreCase)).ToArray();
            var multiSlotPresetFiles = files.Where(f => IsMultiSlotPresetFile(f)).ToArray();

            if (multiSlotPresetFiles.Length > 0)
            {
                LoadMultiSlotPresetFile(multiSlotPresetFiles[0], confirmReplace: true);
                return;
            }

            if (crpFiles.Length > 1)
            {
                foreach (var file in crpFiles)
                    LoadPreset(file, addAsNewSlot: true);
                return;
            }

            if (crpFiles.Length == 1)
                LoadPreset(crpFiles[0], addAsNewSlot: true);
        }

        /// <summary>
        /// Called when you close the main window with the X button.
        /// </summary>
        private void MinimizeToTray(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) // Checks if it was closed by user and not by system in a shutdown, for example
            {
                SaveSlotsToStorage();

                // Prevent closing and hide the window to tray instead
                e.Cancel = true;
                Hide();

                if (!(settings.startMinimized || wasTooltipShown))
                {
                    // Show a tooltip if it wasn't shown already and if the app doesn't start minimized 
                    trayIcon.ShowBalloonTip(500);
                    wasTooltipShown = true;
                }
            }
        }

        /// <summary>
        /// Called when you click the tray icon, balloon tip, or the "Open" option in the tray menu.
        /// </summary>
        private void MaximizeFromTray(object sender, EventArgs e)
        {
            if (e is MouseEventArgs ev && ev.Button != MouseButtons.Left)
                return;

            MaximizeFromTray();
        }

        /// <summary>
        /// Maximizes the window.
        /// </summary>
        private void MaximizeFromTray()
        {
            switch (ConnectionManager.State) // Because invoking doesn't work while the form is hidden
            {
                case ConnectionState.Disconnected:
                    textBoxID.BackColor = CurrentColors.BgTextFields;
                    toolStripStatusLabelStatus.Text = Strings.statusDisconnected;
                    break;
                case ConnectionState.Connecting:
                    textBoxID.BackColor = CurrentColors.BgTextFields;
                    toolStripStatusLabelStatus.Text = Strings.statusConnecting;
                    break;
                case ConnectionState.UpdatingPresence:
                    textBoxID.BackColor = CurrentColors.BgTextFields;
                    toolStripStatusLabelStatus.Text = Strings.statusUpdatingPresence;
                    break;
                case ConnectionState.Connected:
                    textBoxID.BackColor = CurrentColors.BgTextFieldsSuccess;
                    toolStripStatusLabelStatus.Text = Strings.statusConnected;
                    break;
                case ConnectionState.Error:
                    textBoxID.BackColor = CurrentColors.BgTextFieldsError;
                    toolStripStatusLabelStatus.Text = Strings.statusError;
                    break;
            }

            Show();
            Activate();
        }

        /// <summary>
        /// Called when you press New Preset button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NewPreset(object sender, EventArgs e)
        {
            if (IsCyclingEditLocked())
                return;

            if (QuietMessageBox.Show(this, Strings.newPresetConfirmation, Application.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1, new QuietMessageBox.ChoiceButtonLabels { Yes = "&Yes", No = "&Cancel" }) != DialogResult.Yes)
                return;

            ClearLoadedPresetPath();
            ApplyNewPreset();
        }

        /// <summary>
        /// Base function for loading a preset.
        /// </summary>
        /// <param name="file">A file stream of the preset file.</param>
        private void LoadPreset(Stream file, bool addAsNewSlot = false)
        {
            try
            {
                var xs = new XmlSerializer(typeof(Preset));
                var preset = (Preset)xs.Deserialize(file);

                if (addAsNewSlot)
                {
                    ImportPresetAsSlot(preset, addAsNewSlot: true);
                    Analytics.TrackEvent("Loaded a preset");
                    return;
                }

                var slot = GetSelectedSlot();
                bool wasConnected = slot?.IsConnected == true;
                bool isNewID = slot != null && slot.ApplicationId != preset.ID;

                ImportPresetAsSlot(preset, addAsNewSlot: false);

                Analytics.TrackEvent("Loaded a preset");

                if (!wasConnected)
                    return;

                var imported = GetSelectedSlot();
                if (imported == null)
                    return;

                if (isNewID || !imported.IsConnected)
                    slotService.ReconnectSlot(imported);
                else
                    SetPresenceForSlot(imported);
            }
            catch
            {
                QuietMessageBox.Show(Strings.errorInvalidPresetFile, Strings.error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                file.Close();
            }
        }

        /// <summary>
        /// Called when you press Load Preset button.
        /// </summary>
        private void LoadPreset(object sender, EventArgs e)
        {
            if (IsCyclingEditLocked())
                return;

            var presetFile = new OpenFileDialog()
            {
                Filter = LoadPresetFilter,
                FilterIndex = 3,
                DefaultExt = "cmrp",
                Multiselect = true,
            };

            if (presetFile.ShowDialog() != DialogResult.OK)
                return;

            try
            {
                if (presetFile.FileNames.Length > 1)
                {
                    foreach (var file in presetFile.FileNames)
                    {
                        if (IsMultiSlotPresetFile(file))
                            LoadMultiSlotPresetFile(file, confirmReplace: true);
                        else
                            LoadPreset(file, addAsNewSlot: true);
                    }
                    return;
                }

                var filePath = presetFile.FileName;
                if (IsMultiSlotPresetFile(filePath))
                {
                    LoadMultiSlotPresetFile(filePath, confirmReplace: true, rememberAsLoaded: true);
                }
                else
                {
                    var choice = QuietMessageBox.Show(
                        this,
                        ActivitiesUiText.LoadCrpChooseMode,
                        Application.ProductName,
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question,
                        MessageBoxDefaultButton.Button1,
                        new QuietMessageBox.ChoiceButtonLabels
                        {
                            Yes = ActivitiesUiText.LoadCrpReplaceButton,
                            No = ActivitiesUiText.LoadCrpAddButton,
                            Cancel = ActivitiesUiText.LoadCrpCancelButton,
                        });

                    if (choice == DialogResult.Cancel)
                        return;

                    if (choice == DialogResult.Yes)
                    {
                        LoadCrpPresetReplacingChart(filePath);
                    }
                    else
                    {
                        // Add into chart — keep current Active Preset indicator / loaded path.
                        LoadPreset(File.OpenRead(filePath), addAsNewSlot: true);
                    }
                }
            }
            catch
            {
                QuietMessageBox.Show(Strings.errorInvalidPresetFile, Strings.error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Loads preset from a file.
        /// </summary>
        /// <param name="filePath">The path to the preset file.</param>
        private void LoadPreset(string filePath, bool addAsNewSlot = false, bool confirmMultiSlotReplace = false)
        {
            try
            {
                if (IsMultiSlotPresetFile(filePath))
                {
                    LoadMultiSlotPresetFile(filePath, confirmReplace: confirmMultiSlotReplace, rememberAsLoaded: true);
                }
                else
                {
                    LoadPreset(File.OpenRead(filePath), addAsNewSlot);
                    // Adding a .crp into the chart must not steal the Active Preset name.
                    if (!addAsNewSlot)
                        SetLoadedPresetPath(filePath);
                }
            }
            catch
            {
                QuietMessageBox.Show(Strings.errorInvalidPresetFile, Strings.error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void LoadMultiSlotPresetFile(string filePath, bool confirmReplace = false, bool rememberAsLoaded = true)
        {
            try
            {
                if (confirmReplace)
                {
                    var confirm = QuietMessageBox.Show(
                        this,
                        ActivitiesUiText.LoadCmrpReplaceConfirm,
                        Application.ProductName,
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2,
                        new QuietMessageBox.ChoiceButtonLabels
                        {
                            Yes = "&Yes",
                            No = "&Cancel",
                        });

                    if (confirm != DialogResult.Yes)
                        return;
                }

                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var preset = serializer.Deserialize<MultiSlotPreset>(File.ReadAllText(filePath));
                if (preset?.Slots == null || preset.Slots.Length == 0)
                    throw new InvalidDataException();

                LoadMultiSlotPreset(preset);
                if (rememberAsLoaded)
                    SetLoadedPresetPath(filePath);
                Analytics.TrackEvent("Loaded a multi-slot preset");
            }
            catch
            {
                QuietMessageBox.Show(Strings.errorInvalidPresetFile, Strings.error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void LoadCrpPresetReplacingChart(string filePath)
        {
            try
            {
                using (var file = File.OpenRead(filePath))
                {
                    var xs = new XmlSerializer(typeof(Preset));
                    var preset = (Preset)xs.Deserialize(file);
                    ReplaceChartWithCrpPreset(preset);
                    SetLoadedPresetPath(filePath);
                    Analytics.TrackEvent("Loaded a preset");
                }
            }
            catch
            {
                QuietMessageBox.Show(Strings.errorInvalidPresetFile, Strings.error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Called when you press Save Preset button.
        /// </summary>
        private void SavePreset(object sender, EventArgs e)
        {
            if (IsCyclingEditLocked())
                return;

            var xs = new XmlSerializer(typeof(Preset));
            var presetFile = new SaveFileDialog()
            {
                Filter = SavePresetFilter,
                FilterIndex = 1,
                DefaultExt = "cmrp",
            };

            if (presetFile.ShowDialog() != DialogResult.OK || presetFile.FileNames.Length == 0)
                return;

            while (true)
            {
                try
                {
                    SaveEditorToSelectedSlot();

                    string savePath = presetFile.FileName;
                    var selectedSlots = GetSelectedSlotsInChartOrder();

                    if (IsMultiSlotPresetFile(savePath))
                    {
                        // Multi-select: save only selected slots. Single/none: save entire chart.
                        if (selectedSlots.Count > 1)
                        {
                            SaveMultiSlotPreset(savePath, selectedSlots);
                            ClearLoadedPresetPath();
                        }
                        else
                        {
                            SaveMultiSlotPreset(savePath);
                            SetLoadedPresetPath(savePath);
                        }
                    }
                    else
                    {
                        if (selectedSlots.Count > 1)
                        {
                            SaveSelectedSlotsAsSeparateCrpFiles(savePath, selectedSlots, xs);
                            ClearLoadedPresetPath();
                        }
                        else
                        {
                            var slot = selectedSlots.Count == 1
                                ? selectedSlots[0]
                                : (GetSelectedSlot() ?? (slotService.Slots.Count > 0 ? slotService.Slots[0] : null));
                            if (slot == null)
                                return;

                            using (var file = File.Create(savePath))
                                xs.Serialize(file, slot.ToPreset());
                            SetLoadedPresetPath(savePath);
                        }
                    }

                    Analytics.TrackEvent("Saved a preset");

                    return;
                }
                catch (Exception ex)
                {
                    if (QuietMessageBox.Show(ex.Message, Strings.error, MessageBoxButtons.RetryCancel, MessageBoxIcon.Error) == DialogResult.Cancel)
                        return;
                }
            }
        }

        void SetLoadedPresetPath(string path)
        {
            loadedPresetPath = string.IsNullOrWhiteSpace(path) ? null : path;
            CaptureLoadedPresetBaseline();
            UpdateSaveChangesMenuItem();
            UpdateActivePresetMenuLabels();
            SaveSlotsToStorage();
        }

        void ClearLoadedPresetPath()
        {
            loadedPresetPath = null;
            loadedPresetBaselineFingerprint = null;
            UpdateSaveChangesMenuItem();
            UpdateActivePresetMenuLabels();
            SaveSlotsToStorage();
        }

        void CaptureLoadedPresetBaseline()
        {
            loadedPresetBaselineFingerprint = string.IsNullOrWhiteSpace(loadedPresetPath)
                ? null
                : ComputeChartFingerprint();
        }

        string ComputeChartFingerprint()
        {
            if (slotService?.Slots == null)
                return "";

            var sb = new System.Text.StringBuilder();
            foreach (var slot in slotService.Slots)
            {
                if (slot == null)
                    continue;

                sb.Append(slot.Enabled ? '1' : '0').Append('\u001f');
                sb.Append(slot.Label ?? "").Append('\u001f');
                sb.Append((slot.ApplicationId ?? "").Trim()).Append('\u001f');
                sb.Append(slot.Type).Append('\u001f');
                sb.Append(slot.Display).Append('\u001f');
                sb.Append(slot.Name ?? "").Append('\u001f');
                sb.Append(slot.Details ?? "").Append('\u001f');
                sb.Append(slot.DetailsUrl ?? "").Append('\u001f');
                sb.Append(slot.State ?? "").Append('\u001f');
                sb.Append(slot.StateUrl ?? "").Append('\u001f');
                sb.Append(slot.PartySize).Append('\u001f');
                sb.Append(slot.PartyMax).Append('\u001f');
                sb.Append(slot.Timestamps).Append('\u001f');
                sb.Append(slot.CustomTimestamp.Ticks).Append('\u001f');
                sb.Append(slot.CustomTimestampEndEnabled ? '1' : '0').Append('\u001f');
                sb.Append(slot.CustomTimestampEnd.Ticks).Append('\u001f');
                sb.Append(slot.LargeImageKey ?? "").Append('\u001f');
                sb.Append(slot.LargeImageText ?? "").Append('\u001f');
                sb.Append(slot.LargeImageUrl ?? "").Append('\u001f');
                sb.Append(slot.SmallImageKey ?? "").Append('\u001f');
                sb.Append(slot.SmallImageText ?? "").Append('\u001f');
                sb.Append(slot.SmallImageUrl ?? "").Append('\u001f');
                sb.Append(slot.Button1Text ?? "").Append('\u001f');
                sb.Append(slot.Button1Url ?? "").Append('\u001f');
                sb.Append(slot.Button2Text ?? "").Append('\u001f');
                sb.Append(slot.Button2Url ?? "").Append('\u001e');
            }

            return sb.ToString();
        }

        bool IsLoadedPresetAltered()
        {
            if (string.IsNullOrWhiteSpace(loadedPresetPath) || loadedPresetBaselineFingerprint == null)
                return false;
            if (settings != null && settings.alternatingPresetsEnabled)
                return false;

            return !string.Equals(
                loadedPresetBaselineFingerprint,
                ComputeChartFingerprint(),
                StringComparison.Ordinal);
        }

        void SetupActivePresetMenuLabels()
        {
            if (menuStrip == null || toolStripLabelActivePresetPrefix != null)
                return;

            // Right-aligned items lay out right-to-left in collection order:
            // add the name first so it sits at the far right, prefix to its left.
            // Negative left margin on the name cancels ToolStrip's default item gap
            // so it reads as a single space: "Active Preset: (name)".
            toolStripLabelActivePresetName = new ToolStripLabel
            {
                Alignment = ToolStripItemAlignment.Right,
                AutoSize = true,
                Margin = new Padding(-6, 1, 8, 2),
                Padding = Padding.Empty,
            };
            toolStripLabelActivePresetPrefix = new ToolStripLabel
            {
                Alignment = ToolStripItemAlignment.Right,
                AutoSize = true,
                Margin = new Padding(8, 1, 0, 2),
                Padding = Padding.Empty,
            };

            menuStrip.Items.Add(toolStripLabelActivePresetName);
            menuStrip.Items.Add(toolStripLabelActivePresetPrefix);
            Resize += (s, e) => UpdateActivePresetMenuLabels();
        }

        void UpdateActivePresetMenuLabels()
        {
            if (toolStripLabelActivePresetPrefix == null || toolStripLabelActivePresetName == null)
                return;

            string nameInParens;

            if (settings != null && settings.alternatingPresetsEnabled)
            {
                nameInParens = BuildActiveCyclePresetMenuName();
            }
            else if (!string.IsNullOrWhiteSpace(loadedPresetPath))
            {
                nameInParens = "(" + Path.GetFileName(loadedPresetPath) + ")";
                if (IsLoadedPresetAltered())
                    nameInParens += " (Altered)";
            }
            else
            {
                nameInParens = "(no preset loaded)";
            }

            nameInParens = TruncateActivePresetName("", nameInParens);

            toolStripLabelActivePresetPrefix.Text = "";
            toolStripLabelActivePresetPrefix.Visible = false;
            toolStripLabelActivePresetName.Text = nameInParens;
            toolStripLabelActivePresetName.ForeColor = CurrentColors.TextInactive;
            toolStripLabelActivePresetPrefix.ToolTipText = null;

            string tooltipPath = null;
            if (settings != null && settings.alternatingPresetsEnabled)
            {
                if (!string.IsNullOrWhiteSpace(pendingCycleTargetPath))
                    tooltipPath = (cycleTransitionFromPath ?? activeCyclePresetPath ?? loadedPresetPath) +
                        " --> " + pendingCycleTargetPath;
                else if (!string.IsNullOrWhiteSpace(activeCyclePresetPath))
                    tooltipPath = activeCyclePresetPath;
                else
                    tooltipPath = loadedPresetPath;
            }
            else
            {
                tooltipPath = loadedPresetPath;
            }

            toolStripLabelActivePresetName.ToolTipText =
                nameInParens.Contains("…") || nameInParens.Contains("...")
                    ? tooltipPath
                    : null;
        }

        string TruncateActivePresetName(string prefix, string nameInParens)
        {
            if (menuStrip == null || toolStripLabelActivePresetPrefix == null)
                return nameInParens;

            int leftMenusWidth = 0;
            foreach (ToolStripItem item in menuStrip.Items)
            {
                if (item == toolStripLabelActivePresetPrefix || item == toolStripLabelActivePresetName)
                    continue;
                if (item.Available)
                    leftMenusWidth += item.Width;
            }

            int available = menuStrip.Width - leftMenusWidth - 24;
            if (available < 80)
                available = 80;

            Font font = menuStrip.Font;
            int prefixWidth = TextRenderer.MeasureText(prefix, font).Width;
            int maxNameWidth = Math.Max(40, available - prefixWidth);

            if (TextRenderer.MeasureText(nameInParens, font).Width <= maxNameWidth)
                return nameInParens;

            const string arrow = " --> ";
            int arrowAt = nameInParens.IndexOf(arrow, StringComparison.Ordinal);
            if (arrowAt > 0)
            {
                string left = nameInParens.Substring(0, arrowAt);
                string right = nameInParens.Substring(arrowAt + arrow.Length);
                string leftInner = StripCycleParen(left);
                string rightInner = StripCycleParen(right);

                while (leftInner.Length > 1 || rightInner.Length > 1)
                {
                    if (leftInner.Length >= rightInner.Length && leftInner.Length > 1)
                        leftInner = leftInner.Substring(0, leftInner.Length - 1);
                    else if (rightInner.Length > 1)
                        rightInner = rightInner.Substring(0, rightInner.Length - 1);
                    else
                        break;

                    string candidate = "(" + leftInner.TrimEnd() + "…)" + arrow + "(" + rightInner.TrimEnd() + "…)";
                    if (TextRenderer.MeasureText(candidate, font).Width <= maxNameWidth)
                        return candidate;
                }

                return "(…)" + arrow + "(…)";
            }

            string alteredSuffix = "";
            const string alteredMarker = " (Altered)";
            if (nameInParens.EndsWith(alteredMarker, StringComparison.Ordinal))
            {
                alteredSuffix = alteredMarker;
                nameInParens = nameInParens.Substring(0, nameInParens.Length - alteredMarker.Length);
            }

            string inner = StripCycleParen(nameInParens);
            string ellipsis = "…";
            while (inner.Length > 1)
            {
                inner = inner.Substring(0, inner.Length - 1);
                string candidate = "(" + inner.TrimEnd() + ellipsis + ")" + alteredSuffix;
                if (TextRenderer.MeasureText(candidate, font).Width <= maxNameWidth)
                    return candidate;
            }

            return "(…)" + alteredSuffix;
        }

        static string StripCycleParen(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            if (value.StartsWith("(") && value.EndsWith(")") && value.Length > 2)
                return value.Substring(1, value.Length - 2);
            return value;
        }

        void UpdateSaveChangesMenuItem()
        {
            UpdatePresetFileMenuItems();
        }

        void UpdatePresetFileMenuItems()
        {
            bool locked = IsCyclingEditLocked();

            if (newPresetToolStripMenuItem != null)
                newPresetToolStripMenuItem.Enabled = !locked;
            if (loadPresetToolStripMenuItem != null)
                loadPresetToolStripMenuItem.Enabled = !locked;
            if (savePresetToolStripMenuItem != null)
                savePresetToolStripMenuItem.Enabled = !locked;

            if (saveChangesToolStripMenuItem == null)
                return;

            saveChangesToolStripMenuItem.Enabled =
                !locked &&
                !string.IsNullOrWhiteSpace(loadedPresetPath) &&
                File.Exists(loadedPresetPath);
        }

        /// <summary>
        /// Overwrites the currently loaded preset file in place (the only write path for an already-loaded preset).
        /// </summary>
        private void SaveChanges(object sender, EventArgs e)
        {
            if (IsCyclingEditLocked())
                return;

            if (string.IsNullOrWhiteSpace(loadedPresetPath) || !File.Exists(loadedPresetPath))
            {
                QuietMessageBox.Show(
                    this,
                    ActivitiesUiText.SaveChangesNoLoadedPreset,
                    Application.ProductName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                UpdateSaveChangesMenuItem();
                return;
            }

            string presetName = Path.GetFileName(loadedPresetPath);
            if (QuietMessageBox.Show(
                    this,
                    string.Format(ActivitiesUiText.SaveChangesConfirm, presetName),
                    Application.ProductName,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2,
                    new QuietMessageBox.ChoiceButtonLabels
                    {
                        Yes = "&Yes",
                        No = "&Cancel",
                    }) != DialogResult.Yes)
                return;

            while (true)
            {
                try
                {
                    SaveEditorToSelectedSlot();

                    if (IsMultiSlotPresetFile(loadedPresetPath))
                    {
                        SaveMultiSlotPreset(loadedPresetPath);
                    }
                    else
                    {
                        var slot = GetSelectedSlot() ?? (slotService.Slots.Count > 0 ? slotService.Slots[0] : null);
                        if (slot == null)
                            return;

                        var xs = new XmlSerializer(typeof(Preset));
                        using (var file = File.Create(loadedPresetPath))
                            xs.Serialize(file, slot.ToPreset());
                    }

                    Analytics.TrackEvent("Saved changes to loaded preset");
                    CaptureLoadedPresetBaseline();
                    UpdateActivePresetMenuLabels();
                    return;
                }
                catch (Exception ex)
                {
                    if (QuietMessageBox.Show(ex.Message, Strings.error, MessageBoxButtons.RetryCancel, MessageBoxIcon.Error) == DialogResult.Cancel)
                        return;
                }
            }
        }

        void SaveSelectedSlotsAsSeparateCrpFiles(string savePath, List<PresenceSlot> slots, XmlSerializer xs)
        {
            string directory = Path.GetDirectoryName(savePath) ?? "";
            string baseName = Path.GetFileNameWithoutExtension(savePath);
            string extension = Path.GetExtension(savePath);
            if (string.IsNullOrEmpty(extension))
                extension = ".crp";

            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var slot in slots)
            {
                string idPart = string.IsNullOrWhiteSpace(slot.ApplicationId)
                    ? "no-id"
                    : SanitizeFileNamePart(slot.ApplicationId.Trim());

                string fullPath = Path.Combine(directory, $"{baseName}_{idPart}{extension}");
                int suffix = 2;
                while (!usedNames.Add(fullPath))
                {
                    fullPath = Path.Combine(directory, $"{baseName}_{idPart}_{suffix}{extension}");
                    suffix++;
                }

                using (var file = File.Create(fullPath))
                    xs.Serialize(file, slot.ToPreset());
            }
        }

        static string SanitizeFileNamePart(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "no-id";

            var invalid = Path.GetInvalidFileNameChars();
            var chars = value.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray();
            string cleaned = new string(chars).Trim(' ', '.');
            return string.IsNullOrEmpty(cleaned) ? "no-id" : cleaned;
        }

        /// <summary>
        /// Called when you press Upload Assets button.
        /// </summary>
        private void OpenDiscordSite(object sender, EventArgs e)
        {
            SaveEditorToSelectedSlot();
            var slot = GetSelectedSlot();
            string id = slot?.ApplicationId ?? "";

            if (id == "")
            {
                QuietMessageBox.Show(Strings.errorNoID, Strings.error, MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                return;
            }

            Utils.OpenInBrowser("https://discord.com/developers/applications/" + id + "/rich-presence/assets");
        }

        /// <summary>
        /// Called when you click File -> Quit or right-click on the tray icon and choose Quit.
        /// </summary>
        private void Quit(object sender, EventArgs e)
        {
            StopAlternatingPresetsTimer();
            SaveSlotsToStorage();
            slotService?.Dispose();

            if (Utils.SaveSettings())
                Application.Exit();
        }

        /// <summary>
        /// Called when you press anything in Settings submenu of menu strip.
        /// </summary>
        /// <exception cref="NotImplementedException">In case I forgot something.</exception>
        private void SaveMenuSettings(object sender, EventArgs e)
        {
            if (loading)
                return;

            var setting = (ToolStripMenuItem)sender;
            var properName = setting.Name.Replace("ToolStripMenuItem", "");

            switch (properName)
            {
                case "runOnStartup":
                    // Apparently property binding doesn't work either for checkboxes or for bool variables
                    settings.runOnStartup = setting.Checked;
                    StartupSetup();
                    break;
                case "startMinimized":
                    settings.startMinimized = setting.Checked;
                    break;
                case "autoconnect":
                    settings.autoconnect = setting.Checked;
                    break;
                case "checkUpdates":
                    settings.checkUpdates = setting.Checked;
                    if (setting.Checked)
                        CheckForUpdates();
                    break;
                case "allowAnalytics":
                    // Menu item is hidden; keep analytics forced off for this fork.
                    settings.analytics = false;
                    Analytics.SetEnabledAsync(false);
                    if (allowAnalyticsToolStripMenuItem != null)
                        allowAnalyticsToolStripMenuItem.Checked = false;
                    break;
                case "darkMode":
                    settings.darkMode = setting.Checked;
                    ThemeSetup();
                    break;
                default:
                    throw new NotImplementedException(properName);
            }

            Utils.SaveSettings();
        }

        /// <summary>
        /// Called when you change the language.
        /// </summary>
        private void ChangeLanguage(object sender, EventArgs e)
        {
            var lang = (ToolStripMenuItem)sender;

            settings.language = (string)lang.Tag;
            settings.changedLanguage = true;
            settings.wasConnected = slotService?.Slots.Any(s => s.IsConnected) == true;
            Utils.SaveSettings();
            Program.AppMutex.Close();
            Application.Restart();
        }

        /// <summary>
        /// Called when you press on menu items that open websites.
        /// </summary>
        private void OpenSite(object sender, EventArgs e)
        {
            var item = (ToolStripMenuItem)sender;
            var url = (string)item.Tag;

            Utils.OpenInBrowser(url.Replace("docs.customrp.xyz/", "docs.customrp.xyz/" + localeUrl));
        }

        /// <summary>
        /// Called when you press on a translator's or supporter's nickname.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenPersonsPage(object sender, EventArgs e)
        {
            var personItem = (ToolStripMenuItem)sender;

            if (personItem.Tag == null)
                return;

            var (personType, personUrl) = (ValueTuple<string, string>)personItem.Tag;

            if (string.IsNullOrWhiteSpace(personUrl))
                return;

            string personName = personItem.Text;

            if (personType == "supporter")
                personName = personName.Replace(" - ", "|").Split('|')[0]; // Doing this replacement thing just in case someone will have "-" in their nickname

            Analytics.TrackEvent("Clicked on a " + personType, new Dictionary<string, string> {
                { "Name", personName },
                { "URL", personUrl }
            });

            Utils.OpenInBrowser(personUrl);
        }

        /// <summary>
        /// Called when you press Check for updates... button.
        /// </summary>
        private void CheckForUpdates(object sender, EventArgs e) => CheckForUpdates(true);

        /// <summary>
        /// Called when you press About... button.
        /// </summary>
        private void ShowAbout(object sender, EventArgs e)
        {
            Analytics.TrackEvent("Opened about window");
            new About().ShowDialog(this);
        }

        /// <summary>
        /// Called when you press Download Update button.
        /// </summary>
        private void DownloadUpdate(object sender, EventArgs e)
        {
            downloadUpdateToolStripMenuItem.Enabled = false;
            DownloadAndInstallUpdate();
        }

        /// <summary>
        /// Called when you input into the ID textbox.
        /// </summary>
        /// <remarks>
        /// This is overcomplicated isn't it, but hey, at least it works with pasting as well!
        /// At least it used to. Huh.
        /// </remarks>
        private void OnlyNumbers(object sender, EventArgs e)
        {
            if (toAvoidRecursion || textBoxID.ReadOnly)
                return;

            toAvoidRecursion = true;

            int sel = textBoxID.SelectionStart;
            int changed = 0;
            string newline = "";

            foreach (var symbol in textBoxID.Text)
            {
                if (char.IsDigit(symbol))
                    newline += symbol;
                else
                    changed++;
            }

            if (changed > 0)
            {
                textBoxID.Text = newline;
                textBoxID.SelectionStart = Math.Max(0, sel - changed);
                textBoxID.SelectionLength = 0;
            }

            toAvoidRecursion = false;
        }

        /// <summary>
        /// Called when a presence type is changed in comboBoxType.
        /// </summary>
        private void PresenceTypeChanged(object sender, EventArgs e)
        {
            if (comboBoxType.Items.Count == 0 || comboBoxType.SelectedItem == null)
                return;

            ActivityType type = (ActivityType)comboBoxType.SelectedValue;

            if (!loading)
            {
                if (!IsCyclingEditLocked())
                    SaveEditorToSelectedSlot();
                SaveSlotsToStorage();
            }

            bool canHaveParty = true, canHaveTimestamps = true;

            if (type != ActivityType.Playing)
                canHaveParty = false;

            flowLayoutPanelParty.Enabled = canHaveParty && !IsCyclingEditLocked();
            panelTimestamps.Enabled = canHaveTimestamps && !IsCyclingEditLocked();
        }

        /// <summary>
        /// Called when a display type is changed in comboBoxDisplay.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DisplayTypeChanged(object sender, EventArgs e)
        {
            if (comboBoxDisplay.Items.Count == 0 || comboBoxDisplay.SelectedItem == null)
                return;

            StatusDisplayType display = (StatusDisplayType)comboBoxDisplay.SelectedValue;

            if (!loading)
            {
                if (!IsCyclingEditLocked())
                    SaveEditorToSelectedSlot();
                SaveSlotsToStorage();
            }
        }

        /// <summary>
        /// Called on Leave event for all text fields except ID.
        /// </summary>
        private void TrimTextBoxes(object sender, EventArgs e)
        {
            Control box = (Control)sender;
            if (box.Text.StartsWith(U00A0))
                return;
            box.Text = box.Text.Trim();
        }

        /// <summary>
        /// Called on Validating event for all text fields except ID.
        /// </summary>
        private void LengthValidationFocus(object sender, System.ComponentModel.CancelEventArgs e)
        {
            dynamic box = sender;

            bool useBytes = box.Name.EndsWith("Button1Text") || box.Name.EndsWith("Button2Text");

            if (box.Text.Length == 1 || useBytes && !StringTools.WithinLength(box.Text, box.MaxLength))
            {
                e.Cancel = true;
                System.Media.SystemSounds.Beep.Play();
            }
        }

        /// <summary>
        /// Called on TextChanged event for all text fields except ID.
        /// </summary>
        private void LengthValidation(object sender, EventArgs e)
        {
            dynamic box = sender;

            bool useBytes = box.Name.EndsWith("Button1Text") || box.Name.EndsWith("Button2Text");

            box.BackColor = (box.Text.Length == 1 || useBytes && !StringTools.WithinLength(box.Text, box.MaxLength)) ? CurrentColors.BgTextFieldsError : CurrentColors.BgTextFields;
        }

        /// <summary>
        /// Called on Validating event to validate party size values.
        /// </summary>
        private void PartySizeValidation(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (sender == numericUpDownPartyMax && numericUpDownPartyMax.Value == 0)
                numericUpDownPartySize.Value = 0;
            else if (numericUpDownPartySize.Value > numericUpDownPartyMax.Value)
            {
                numericUpDownPartyMax.Value = numericUpDownPartySize.Value;
                // If user sets max value less than current value, play error sound, but not if user sets current value more than max
                if (sender == numericUpDownPartyMax) System.Media.SystemSounds.Beep.Play();
            }
        }

        /// <summary>
        /// Called when a timestamp radiobutton changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TimestampsChanged(object sender, EventArgs e)
        {
            if (loading)
                return;

            RadioButton btn = (RadioButton)sender;

            if (!btn.Checked)
                return;

            // settings.timestamps = btn.TabIndex; // I mean... it's a great container for int values
            // It was, until I needed to add a new type in the middle of the list
            settings.timestamps = (int)btn.Tag;
            SaveEditorToSelectedSlot();
            SaveSlotsToStorage();

            tableLayoutPanelCustomTimestamps.Enabled = (TimestampType)btn.Tag == TimestampType.Custom;
            dateTimePickerTimestampEnd.Enabled = checkBoxTimestampEnd.Checked;
        }

        /// <summary>
        /// Called when End timestamp checkbox changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TimestampEndChanged(object sender, EventArgs e)
        {
            dateTimePickerTimestampEnd.Enabled = checkBoxTimestampEnd.Checked;
        }

        /// <summary>
        /// Called on DropDown event for <see cref="comboBoxLargeKey"/> and <see cref="comboBoxSmallKey"/>.
        /// </summary>
        private void FetchAssets(object sender, EventArgs e)
        {
            SaveEditorToSelectedSlot();
            var slot = GetSelectedSlot();
            string appId = slot?.ApplicationId ?? "";

            if (appId == "" || (lastIDChecked == appId && nextAssetCheck.CompareTo(DateTime.Now) > 0))
                return;

            lastIDChecked = appId;

            comboBoxLargeKey.Items.Clear();
            comboBoxSmallKey.Items.Clear();

            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 0, 15);

                try
                {
                    ServicePointManager.Expect100Continue = true;
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                    var res = client.GetAsync($"https://discord.com/api/oauth2/applications/{appId}/assets").Result;

                    if (res.IsSuccessStatusCode)
                    {
                        var resList = res.Content.ReadAsAsync<List<ImageAssets>>().Result;

                        resList.ForEach(asset =>
                        {
                            comboBoxLargeKey.Items.Add(asset.Name);
                            comboBoxSmallKey.Items.Add(asset.Name);
                        });

                        nextAssetCheck = DateTime.Now.Add(new TimeSpan(0, 1, 0));
                    }
                }
                catch
                {
                    QuietMessageBox.Show(Strings.errorNoInternet, Strings.error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// Called on Paint event for some controls that can be disabled.
        /// </summary>
        private void DisabedTextPaint(object sender, PaintEventArgs e)
        {
            Control ctrl = (Control)sender;
            if (ctrl.Enabled)
                return;

            if (ctrl is Button button)
            {
                // Same disabled text treatment for Slot / All action buttons in both themes.
                Color fill = settings.darkMode
                    ? (button.BackColor.IsEmpty ? CurrentColors.BgButton : button.BackColor)
                    : SystemColors.Control;
                using (var background = new SolidBrush(fill))
                    e.Graphics.FillRectangle(background, button.ClientRectangle);

                TextRenderer.DrawText(
                    e.Graphics,
                    button.Text,
                    button.Font,
                    button.ClientRectangle,
                    CurrentColors.TextInactive,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                return;
            }

            var newClipRect = new Rectangle(e.ClipRectangle.Location, e.ClipRectangle.Size);
            if (sender is CheckBox)
                newClipRect.Location = new Point(7, -1);
            if (sender is RadioButton)
                newClipRect.Location = new Point(7, 0);

            TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
            TextRenderer.DrawText(e.Graphics, ctrl.Text, ctrl.Font, newClipRect, CurrentColors.TextInactive, flags);
        }

        /// <summary>
        /// Called when you press the Connect button or right-click on the tray icon and choose Reconnect.
        /// </summary>
        private void Connect(object sender, EventArgs e)
        {
#if DEBUG
            if (ModifierKeys == (Keys.Control | Keys.Alt) && sender is Button)
                Crashes.GenerateTestCrash();
#endif
            if (ModifierKeys == (Keys.Control | Keys.Shift) && sender is Button)
            {
                Analytics.TrackEvent("Opened pipe selector window");

                new PipeSelector().ShowDialog(this);
                return;
            }

            Connect();
        }

        /// <summary>
        /// Connects to Discord and makes UI changes.
        /// </summary>
        private void Connect()
        {
            if (IsCyclingEditLocked())
                return;

            SaveEditorToSelectedSlot();
            SaveSlotsToStorage();

            var slot = GetSelectedSlot();
            if (slot == null)
                return;

            if (!TryValidateSlotForConnect(slot, out string error))
            {
                ShowSlotConstraintMessage(error);
                return;
            }

            if (!IsMultiSlotRpcMode())
            {
                foreach (var other in slotService.Slots)
                {
                    if (other.SlotId == slot.SlotId)
                        continue;

                    if (other.IsConnected ||
                        other.ConnectionState == SlotConnectionState.Connecting ||
                        other.ConnectionState == SlotConnectionState.UpdatingPresence)
                        slotService.DisconnectSlot(other);
                }
            }

            if (ConnectionManager.State == ConnectionState.Disconnected)
                Analytics.TrackEvent("Connected");

            slotService.ConnectSlot(slot);
            UpdateGlobalConnectionUi();
        }

        /// <summary>
        /// Called when you press the Disconnect button.
        /// </summary>
        private void Disconnect(object sender, EventArgs e) => Disconnect();

        /// <summary>
        /// Disconnects from Discord and makes UI changes.
        /// </summary>
        private void Disconnect()
        {
            if (IsCyclingEditLocked())
                return;

            var slot = GetSelectedSlot();
            if (slot != null)
                DisconnectSlot(slot);

            localTimeTimer.Stop();
            UpdateGlobalConnectionUi();
            Analytics.TrackEvent("Disconnected");
        }

        /// <summary>
        /// Disconnects from Discord and instantly connects back.
        /// </summary>
        private void Reconnect()
        {
            var slot = GetSelectedSlot();
            if (slot == null)
                return;

            SaveEditorToSelectedSlot();
            SaveSlotsToStorage();
            slotService.ReconnectSlot(slot);
            UpdateGlobalConnectionUi();
        }

        /// <summary>
        /// Called when you press the Update Presence button.
        /// </summary>
        private void Update(object sender, EventArgs e)
        {
            if (IsCyclingEditLocked())
                return;

            SaveSlotsToStorage();
            SetPresence();
        }
    }
}