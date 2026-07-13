using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace CustomRPC
{
    /// <summary>
    /// Persists multi-activity slots next to the app's user.config.
    /// </summary>
    public static class SlotStorage
    {
        const string FileName = "multi-RP-slots.json";

        static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        /// <summary>
        /// Same directory as .NET user.config (LocalAppData\maximmax42\CustomRP.exe_Url_…\…).
        /// </summary>
        public static string ConfigDirectory
        {
            get
            {
                try
                {
                    var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
                    string dir = Path.GetDirectoryName(config.FilePath);
                    if (!string.IsNullOrEmpty(dir))
                        return dir;
                }
                catch
                {
                }

                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "maximmax42",
                    "CustomRP");
            }
        }

        public static string ConfigPath => Path.Combine(ConfigDirectory, FileName);

        static string LegacyCustomRpFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CustomRP");

        static string LegacyCustomRpSlotsPath =>
            Path.Combine(LegacyCustomRpFolder, "presence-slots.json");

        static string LegacyStartupConfigPath =>
            Path.Combine(Application.StartupPath, "presence-slots.json");

        public static bool Exists() =>
            File.Exists(ConfigPath) ||
            File.Exists(LegacyCustomRpSlotsPath) ||
            File.Exists(LegacyStartupConfigPath);

        public static SlotConfig Load()
        {
            if (File.Exists(ConfigPath))
                return LoadFrom(ConfigPath);

            if (File.Exists(LegacyCustomRpSlotsPath))
            {
                var config = LoadFrom(LegacyCustomRpSlotsPath);
                if (config != null)
                {
                    Save(config);
                    TryDeleteLegacyCustomRpStorage();
                }
                return config;
            }

            if (File.Exists(LegacyStartupConfigPath))
            {
                var config = LoadFrom(LegacyStartupConfigPath);
                if (config != null)
                {
                    Save(config);
                    try { File.Delete(LegacyStartupConfigPath); } catch { }
                }
                return config;
            }

            return null;
        }

        static SlotConfig LoadFrom(string path)
        {
            try
            {
                var stored = Serializer.Deserialize<StoredSlotFile>(File.ReadAllText(path));
                if (stored == null)
                    return null;

                return new SlotConfig
                {
                    Version = stored.Version > 0 ? stored.Version : 1,
                    SelectedSlotId = stored.SelectedSlotId ?? "",
                    LoadedPresetPath = stored.LoadedPresetPath ?? "",
                    Slots = (stored.Slots ?? new List<StoredPresenceSlot>())
                        .Select(s => s.ToPresenceSlot())
                        .ToList(),
                };
            }
            catch
            {
                return null;
            }
        }

        public static bool Save(SlotConfig config)
        {
            try
            {
                if (!Directory.Exists(ConfigDirectory))
                    Directory.CreateDirectory(ConfigDirectory);

                var stored = new StoredSlotFile
                {
                    Version = config?.Version > 0 ? config.Version : 1,
                    SelectedSlotId = config?.SelectedSlotId ?? "",
                    LoadedPresetPath = config?.LoadedPresetPath ?? "",
                    Slots = (config?.Slots ?? new List<PresenceSlot>())
                        .Select(StoredPresenceSlot.FromPresenceSlot)
                        .ToList(),
                };

                File.WriteAllText(ConfigPath, Serializer.Serialize(stored));
                return true;
            }
            catch (Exception ex)
            {
                QuietMessageBox.Show($"{Strings.errorSavingSettings} {ex.Message}", Strings.error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        public static SlotConfig FromSlots(IEnumerable<PresenceSlot> slots, string selectedSlotId, string loadedPresetPath = null)
        {
            return new SlotConfig
            {
                Version = 1,
                SelectedSlotId = selectedSlotId ?? "",
                LoadedPresetPath = loadedPresetPath ?? "",
                Slots = slots.Select(CloneForExport).ToList(),
            };
        }

        public static PresenceSlot CloneForExport(PresenceSlot slot) =>
            StoredPresenceSlot.FromPresenceSlot(slot).ToPresenceSlot();

        public static StoredPresenceSlot ToStored(PresenceSlot slot) =>
            StoredPresenceSlot.FromPresenceSlot(slot);

        static void TryDeleteLegacyCustomRpStorage()
        {
            try
            {
                if (File.Exists(LegacyCustomRpSlotsPath))
                    File.Delete(LegacyCustomRpSlotsPath);

                if (Directory.Exists(LegacyCustomRpFolder) &&
                    Directory.GetFileSystemEntries(LegacyCustomRpFolder).Length == 0)
                    Directory.Delete(LegacyCustomRpFolder);
            }
            catch
            {
            }
        }
    }

    public class SlotConfig
    {
        public int Version { get; set; } = 1;
        public string SelectedSlotId { get; set; } = "";
        public string LoadedPresetPath { get; set; } = "";
        public List<PresenceSlot> Slots { get; set; } = new List<PresenceSlot>();
    }

    /// <summary>
    /// Disk/preset shape for a slot — persistent fields only (no runtime clients/state).
    /// </summary>
    public class StoredPresenceSlot
    {
        public string SlotId { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public string Label { get; set; } = "";
        public string ApplicationId { get; set; } = "";
        public int Pipe { get; set; } = -1;
        public int Type { get; set; }
        public int Display { get; set; }
        public string Name { get; set; } = "";
        public string Details { get; set; } = "";
        public string DetailsUrl { get; set; } = "";
        public string State { get; set; } = "";
        public string StateUrl { get; set; } = "";
        public int PartySize { get; set; }
        public int PartyMax { get; set; }
        public int Timestamps { get; set; }
        public DateTime CustomTimestamp { get; set; } = new DateTime(1969, 1, 1);
        public bool CustomTimestampEndEnabled { get; set; }
        public DateTime CustomTimestampEnd { get; set; } = new DateTime(1969, 1, 1);
        public string LargeImageKey { get; set; } = "";
        public string LargeImageText { get; set; } = "";
        public string LargeImageUrl { get; set; } = "";
        public string SmallImageKey { get; set; } = "";
        public string SmallImageText { get; set; } = "";
        public string SmallImageUrl { get; set; } = "";
        public string Button1Text { get; set; } = "";
        public string Button1Url { get; set; } = "";
        public string Button2Text { get; set; } = "";
        public string Button2Url { get; set; } = "";

        public static StoredPresenceSlot FromPresenceSlot(PresenceSlot slot)
        {
            if (slot == null)
                return new StoredPresenceSlot();

            return new StoredPresenceSlot
            {
                SlotId = slot.SlotId ?? "",
                Enabled = slot.Enabled,
                Label = slot.Label ?? "",
                ApplicationId = slot.ApplicationId ?? "",
                Pipe = slot.Pipe,
                Type = slot.Type,
                Display = slot.Display,
                Name = slot.Name ?? "",
                Details = slot.Details ?? "",
                DetailsUrl = slot.DetailsUrl ?? "",
                State = slot.State ?? "",
                StateUrl = slot.StateUrl ?? "",
                PartySize = slot.PartySize,
                PartyMax = slot.PartyMax,
                Timestamps = slot.Timestamps,
                CustomTimestamp = slot.CustomTimestamp,
                CustomTimestampEndEnabled = slot.CustomTimestampEndEnabled,
                CustomTimestampEnd = slot.CustomTimestampEnd,
                LargeImageKey = slot.LargeImageKey ?? "",
                LargeImageText = slot.LargeImageText ?? "",
                LargeImageUrl = slot.LargeImageUrl ?? "",
                SmallImageKey = slot.SmallImageKey ?? "",
                SmallImageText = slot.SmallImageText ?? "",
                SmallImageUrl = slot.SmallImageUrl ?? "",
                Button1Text = slot.Button1Text ?? "",
                Button1Url = slot.Button1Url ?? "",
                Button2Text = slot.Button2Text ?? "",
                Button2Url = slot.Button2Url ?? "",
            };
        }

        public PresenceSlot ToPresenceSlot()
        {
            var slot = new PresenceSlot
            {
                SlotId = string.IsNullOrWhiteSpace(SlotId) ? Guid.NewGuid().ToString("N") : SlotId,
                Enabled = Enabled,
                Label = Label ?? "",
                ApplicationId = ApplicationId ?? "",
                Pipe = Pipe,
                Type = Type,
                Display = Display,
                Name = Name ?? "",
                Details = Details ?? "",
                DetailsUrl = DetailsUrl ?? "",
                State = State ?? "",
                StateUrl = StateUrl ?? "",
                PartySize = PartySize,
                PartyMax = PartyMax,
                Timestamps = Timestamps,
                CustomTimestamp = CustomTimestamp,
                CustomTimestampEndEnabled = CustomTimestampEndEnabled,
                CustomTimestampEnd = CustomTimestampEnd,
                LargeImageKey = LargeImageKey ?? "",
                LargeImageText = LargeImageText ?? "",
                LargeImageUrl = LargeImageUrl ?? "",
                SmallImageKey = SmallImageKey ?? "",
                SmallImageText = SmallImageText ?? "",
                SmallImageUrl = SmallImageUrl ?? "",
                Button1Text = Button1Text ?? "",
                Button1Url = Button1Url ?? "",
                Button2Text = Button2Text ?? "",
                Button2Url = Button2Url ?? "",
            };
            slot.ResetRuntimeState();
            return slot;
        }
    }

    class StoredSlotFile
    {
        public int Version { get; set; } = 1;
        public string SelectedSlotId { get; set; } = "";
        public string LoadedPresetPath { get; set; } = "";
        public List<StoredPresenceSlot> Slots { get; set; } = new List<StoredPresenceSlot>();
    }
}
