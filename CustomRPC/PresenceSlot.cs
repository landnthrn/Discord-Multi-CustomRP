using DiscordRPC;
using System;
using System.Xml.Serialization;

namespace CustomRPC
{
    /// <summary>
    /// Per-slot connection state tracked independently from other slots.
    /// </summary>
    public enum SlotConnectionState
    {
        Disconnected,
        Connecting,
        UpdatingPresence,
        Connected,
        Error,
    }

    /// <summary>
    /// One Rich Presence activity slot with its own Discord application ID and RP client.
    /// </summary>
    [Serializable]
    public class PresenceSlot
    {
        public string SlotId { get; set; } = Guid.NewGuid().ToString("N");
        public bool Enabled { get; set; } = true;
        public string Label { get; set; } = "Activity 1";
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

        [XmlIgnore]
        [NonSerialized]
        public DiscordRpcClient Client;

        [XmlIgnore]
        public int AssignedProcessId { get; set; }

        [XmlIgnore]
        public SlotConnectionState ConnectionState { get; set; } = SlotConnectionState.Disconnected;

        [XmlIgnore]
        public string LastError { get; set; } = "";

        [XmlIgnore]
        public DateTime TimestampConnected { get; set; } = DateTime.UtcNow;

        [XmlIgnore]
        public DateTime TimestampStarted { get; set; } = DateTime.UtcNow;

        [XmlIgnore]
        public DateTime LastPresenceUpdate { get; set; } = DateTime.MinValue;

        [XmlIgnore]
        public string BoundApplicationId { get; set; } = "";

        [XmlIgnore]
        public int BoundPipe { get; set; } = int.MinValue;

        public ActivityType ActivityType => (ActivityType)Type;
        public StatusDisplayType StatusDisplay => (StatusDisplayType)Display;
        public TimestampType TimestampType => (TimestampType)Timestamps;

        public bool IsConnected
        {
            get
            {
                if (ConnectionState != SlotConnectionState.Connected &&
                    ConnectionState != SlotConnectionState.UpdatingPresence)
                    return false;

                return Client != null && !Client.IsDisposed;
            }
        }

        public static PresenceSlot CreateDefault(string label, string defaultApplicationId, int pipe)
        {
            return new PresenceSlot
            {
                Label = label,
                ApplicationId = defaultApplicationId,
                Pipe = pipe,
            };
        }

        public void ClearToNewPresetDefaults(int activityIndex)
        {
            ApplicationId = "";
            Name = "";
            Label = $"Activity {activityIndex}";
            Type = (int)ActivityType.Playing;
            Display = (int)StatusDisplayType.Name;
            Details = "";
            DetailsUrl = "";
            State = "";
            StateUrl = "";
            PartySize = 0;
            PartyMax = 0;
            Timestamps = (int)TimestampType.SinceLastConnection;
            CustomTimestamp = new DateTime(1969, 1, 1);
            CustomTimestampEndEnabled = false;
            CustomTimestampEnd = new DateTime(1969, 1, 1);
            LargeImageKey = "";
            LargeImageText = "";
            LargeImageUrl = "";
            SmallImageKey = "";
            SmallImageText = "";
            SmallImageUrl = "";
            Button1Text = "";
            Button1Url = "";
            Button2Text = "";
            Button2Url = "";
            Enabled = true;
        }

        public static PresenceSlot FromPreset(Preset preset, int pipe, string label = null)
        {
            return new PresenceSlot
            {
                Label = label ?? (string.IsNullOrWhiteSpace(preset.Name) ? "Imported preset" : preset.Name),
                ApplicationId = preset.ID ?? "",
                Pipe = pipe,
                Type = preset.Type,
                Display = preset.Display,
                Name = preset.Name ?? "",
                Details = preset.Details ?? "",
                DetailsUrl = preset.DetailsURL ?? "",
                State = preset.State ?? "",
                StateUrl = preset.StateURL ?? "",
                PartySize = preset.PartySize,
                PartyMax = preset.PartyMax,
                Timestamps = preset.Timestamps,
                CustomTimestamp = preset.CustomTimestamp,
                CustomTimestampEndEnabled = preset.CustomTimestampEndEnabled,
                CustomTimestampEnd = preset.CustomTimestampEnd,
                LargeImageKey = preset.LargeKey ?? "",
                LargeImageText = preset.LargeText ?? "",
                LargeImageUrl = preset.LargeURL ?? "",
                SmallImageKey = preset.SmallKey ?? "",
                SmallImageText = preset.SmallText ?? "",
                SmallImageUrl = preset.SmallURL ?? "",
                Button1Text = preset.Button1Text ?? "",
                Button1Url = preset.Button1URL ?? "",
                Button2Text = preset.Button2Text ?? "",
                Button2Url = preset.Button2URL ?? "",
                Enabled = !preset.EnabledSpecified || preset.Enabled,
            };
        }

        public Preset ToPreset()
        {
            return new Preset
            {
                ID = ApplicationId,
                Type = Type,
                Display = Display,
                Name = Name,
                Details = Details,
                DetailsURL = DetailsUrl,
                State = State,
                StateURL = StateUrl,
                PartySize = PartySize,
                PartyMax = PartyMax,
                Timestamps = Timestamps,
                CustomTimestamp = CustomTimestamp,
                CustomTimestampEndEnabled = CustomTimestampEndEnabled,
                CustomTimestampEnd = CustomTimestampEnd,
                LargeKey = LargeImageKey,
                LargeText = LargeImageText,
                LargeURL = LargeImageUrl,
                SmallKey = SmallImageKey,
                SmallText = SmallImageText,
                SmallURL = SmallImageUrl,
                Button1Text = Button1Text,
                Button1URL = Button1Url,
                Button2Text = Button2Text,
                Button2URL = Button2Url,
                Enabled = Enabled,
                EnabledSpecified = true,
            };
        }

        internal static PresenceSlot FromLegacySettings(Properties.Settings settings)
        {
            return new PresenceSlot
            {
                Label = string.IsNullOrWhiteSpace(settings.name) ? "Activity 1" : settings.name,
                ApplicationId = settings.id ?? "",
                Pipe = (int)settings.pipe,
                Type = settings.type,
                Display = settings.display,
                Name = settings.name ?? "",
                Details = settings.details ?? "",
                DetailsUrl = settings.detailsURL ?? "",
                State = settings.state ?? "",
                StateUrl = settings.stateURL ?? "",
                PartySize = (int)settings.partySize,
                PartyMax = (int)settings.partyMax,
                Timestamps = settings.timestamps,
                CustomTimestamp = settings.customTimestamp,
                CustomTimestampEndEnabled = settings.customTimestampEndEnabled,
                CustomTimestampEnd = settings.customTimestampEnd,
                LargeImageKey = settings.largeKey ?? "",
                LargeImageText = settings.largeText ?? "",
                LargeImageUrl = settings.largeURL ?? "",
                SmallImageKey = settings.smallKey ?? "",
                SmallImageText = settings.smallText ?? "",
                SmallImageUrl = settings.smallURL ?? "",
                Button1Text = settings.button1Text ?? "",
                Button1Url = settings.button1URL ?? "",
                Button2Text = settings.button2Text ?? "",
                Button2Url = settings.button2URL ?? "",
            };
        }

        public void ResetRuntimeState()
        {
            Client = null;
            AssignedProcessId = 0;
            ConnectionState = SlotConnectionState.Disconnected;
            LastError = "";
            BoundApplicationId = "";
            BoundPipe = int.MinValue;
            TimestampConnected = DateTime.UtcNow;
            TimestampStarted = DateTime.UtcNow;
            LastPresenceUpdate = DateTime.MinValue;
        }

        public PresenceSlot Clone(string newLabel = null)
        {
            var copy = (PresenceSlot)MemberwiseClone();
            copy.SlotId = Guid.NewGuid().ToString("N");
            copy.Label = newLabel ?? (Label + " copy");
            copy.ApplicationId = "";
            copy.ResetRuntimeState();
            return copy;
        }

        /// <summary>
        /// Copies editor/presence fields from another slot, keeping SlotId and live connection state.
        /// </summary>
        public void ApplyPresenceFieldsFrom(PresenceSlot source)
        {
            if (source == null)
                return;

            Enabled = source.Enabled;
            Label = source.Label;
            ApplicationId = source.ApplicationId ?? "";
            Pipe = source.Pipe;
            Type = source.Type;
            Display = source.Display;
            Name = source.Name ?? "";
            Details = source.Details ?? "";
            DetailsUrl = source.DetailsUrl ?? "";
            State = source.State ?? "";
            StateUrl = source.StateUrl ?? "";
            PartySize = source.PartySize;
            PartyMax = source.PartyMax;
            Timestamps = source.Timestamps;
            CustomTimestamp = source.CustomTimestamp;
            CustomTimestampEndEnabled = source.CustomTimestampEndEnabled;
            CustomTimestampEnd = source.CustomTimestampEnd;
            LargeImageKey = source.LargeImageKey ?? "";
            LargeImageText = source.LargeImageText ?? "";
            LargeImageUrl = source.LargeImageUrl ?? "";
            SmallImageKey = source.SmallImageKey ?? "";
            SmallImageText = source.SmallImageText ?? "";
            SmallImageUrl = source.SmallImageUrl ?? "";
            Button1Text = source.Button1Text ?? "";
            Button1Url = source.Button1Url ?? "";
            Button2Text = source.Button2Text ?? "";
            Button2Url = source.Button2Url ?? "";
        }
    }

    /// <summary>
    /// Multi-slot preset file format (.cmrp JSON).
    /// </summary>
    [Serializable]
    public class MultiSlotPreset
    {
        public int Version { get; set; } = 1;
        public string SelectedSlotId { get; set; } = "";
        public bool? MatchDiscordListOrder { get; set; }
        public decimal? MatchListOrderDelaySeconds { get; set; }
        public StoredPresenceSlot[] Slots { get; set; } = new StoredPresenceSlot[0];
    }
}
