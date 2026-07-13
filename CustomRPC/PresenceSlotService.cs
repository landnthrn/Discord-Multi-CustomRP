using DiscordRPC;
using DiscordRPC.Message;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CustomRPC
{
    /// <summary>
    /// Manages multiple independent Discord RP clients, one per presence slot.
    /// </summary>
    public class PresenceSlotService
    {
        public const int BulkUpdateCooldownMs = 5000;
        public const int SlotInitDelayMs = 750;
        const int RestartDelayMs = 10 * 1000;
        const int SlotConnectWaitTimeoutMs = 120 * 1000;

        readonly HashSet<string> matchOrderDelayingSlotIds = new HashSet<string>();
        CancellationTokenSource sequenceCts;

        readonly string defaultApplicationId;
        readonly int defaultPipe;
        readonly Action<PresenceSlot> onSlotStateChanged;
        readonly Func<PresenceSlot, PresenceBuilder.BuildResult> buildPresence;
        readonly Action<Action> runOnUiThread;
        readonly Action onProfileSwitchRecovery;
        readonly string logDirectory;
        MultiSlotRpcMode rpcMode;

        public MultiSlotRpcMode RpcMode
        {
            get => rpcMode;
            set
            {
                if (rpcMode == value)
                    return;

                DisconnectAllSlots();
                rpcMode = value;
            }
        }

        public List<PresenceSlot> Slots { get; } = new List<PresenceSlot>();

        public PresenceSlotService(
            string defaultApplicationId,
            int defaultPipe,
            string logDirectory,
            MultiSlotRpcMode rpcMode,
            Action<PresenceSlot> onSlotStateChanged,
            Func<PresenceSlot, PresenceBuilder.BuildResult> buildPresence,
            Action<Action> runOnUiThread,
            Action onProfileSwitchRecovery)
        {
            this.defaultApplicationId = defaultApplicationId;
            this.defaultPipe = defaultPipe;
            this.logDirectory = logDirectory;
            this.rpcMode = rpcMode;
            this.onSlotStateChanged = onSlotStateChanged;
            this.buildPresence = buildPresence;
            this.runOnUiThread = runOnUiThread ?? (action => action());
            this.onProfileSwitchRecovery = onProfileSwitchRecovery;
        }

        readonly Dictionary<string, System.Timers.Timer> restartTimers = new Dictionary<string, System.Timers.Timer>();
        readonly Dictionary<string, int> restartAttemptsLeft = new Dictionary<string, int>();
        string lastKnownDiscordUsername;
        bool profileRecoveryActive;
        bool profileSwitchRecoveryQueued;
        DateTime lastBulkUpdateUtc = DateTime.MinValue;
        bool bulkUpdatePending;

        public void SetProfileRecoveryActive(bool active) => profileRecoveryActive = active;

        public PresenceSlot GetSlot(string slotId) =>
            Slots.FirstOrDefault(s => s.SlotId == slotId);

        public bool InitSlot(PresenceSlot slot)
        {
            if (slot == null)
                return false;

            return InitSlotInProcess(slot, overrideProcessId: rpcMode == MultiSlotRpcMode.CustomProcessId);
        }

        bool InitSlotInProcess(PresenceSlot slot, bool overrideProcessId)
        {
            string applicationId = (slot.ApplicationId ?? "").Trim();
            if (string.IsNullOrEmpty(applicationId))
                return false;

            int pipe = slot.Pipe >= 0 ? slot.Pipe : defaultPipe;
            bool needsNewClient = slot.Client == null || slot.Client.IsDisposed ||
                slot.BoundApplicationId != applicationId || slot.BoundPipe != pipe;

            if (!needsNewClient && (slot.IsConnected ||
                slot.ConnectionState == SlotConnectionState.Connecting ||
                slot.ConnectionState == SlotConnectionState.UpdatingPresence))
                return true;

            if (!needsNewClient)
                DisposeClient(slot);

            slot.Client = new DiscordRpcClient(applicationId, pipe);
            slot.BoundApplicationId = applicationId;
            slot.BoundPipe = pipe;

            slot.Client.OnReady += (sender, args) => SlotOnReady(slot, sender, args);
            slot.Client.OnPresenceUpdate += (sender, args) => SlotOnPresenceUpdate(slot, sender, args);
            slot.Client.OnError += (sender, args) => SlotOnError(slot, sender, args);
            slot.Client.OnConnectionFailed += (sender, args) => SlotOnConnectionFailed(slot, sender, args);
            slot.Client.OnClose += (sender, args) => SlotOnClose(slot, sender, args);
            slot.Client.Logger = new TimestampFileLogger(logDirectory);

            if (overrideProcessId)
            {
                int slotIndex = Math.Max(0, Slots.IndexOf(slot));
                slot.AssignedProcessId = DiscordRpcProcessIdHelper.AllocateFakeProcessId(slot.SlotId, slotIndex);

                if (!DiscordRpcProcessIdHelper.TrySetProcessId(slot.Client, slot.AssignedProcessId))
                {
                    slot.ConnectionState = SlotConnectionState.Error;
                    slot.LastError = "Could not override Process ID for this slot.";
                    onSlotStateChanged?.Invoke(slot);
                    return false;
                }
            }

            slot.ConnectionState = SlotConnectionState.Connecting;
            slot.LastError = "";
            onSlotStateChanged?.Invoke(slot);

            bool initialized = slot.Client.Initialize();
            if (!initialized)
            {
                slot.ConnectionState = SlotConnectionState.Error;
                slot.LastError = "Initialize failed";
                onSlotStateChanged?.Invoke(slot);
            }

            return initialized;
        }

        public bool SetPresenceForSlot(PresenceSlot slot, bool showErrors = true, bool ignoreThrottle = false)
        {
            if (slot == null)
                return false;

            if (!ignoreThrottle && (DateTime.UtcNow - slot.LastPresenceUpdate).TotalMilliseconds < 1000)
                return false;

            var build = buildPresence(slot);
            if (!build.Success)
            {
                slot.ConnectionState = SlotConnectionState.Error;
                slot.LastError = build.ErrorMessage;
                onSlotStateChanged?.Invoke(slot);

                if (showErrors)
                    ShowBuildError(build);

                return false;
            }

            if (slot.Client == null || slot.Client.IsDisposed)
                return false;

            try
            {
                EnterUpdatingPresence(slot);
                slot.Client.SetPresence(build.Presence);
                slot.LastPresenceUpdate = DateTime.UtcNow;
                return true;
            }
            catch (Exception e)
            {
                slot.ConnectionState = SlotConnectionState.Error;
                slot.LastError = e.Message;
                onSlotStateChanged?.Invoke(slot);

                if (showErrors)
                    QuietMessageBox.Show(e.Message, Strings.error, MessageBoxButtons.OK, MessageBoxIcon.Error);

                return false;
            }
        }

        public void DisconnectSlot(PresenceSlot slot)
        {
            if (slot == null)
                return;

            StopRestartTimer(slot.SlotId);
            DisposeClient(slot);
            slot.LastPresenceUpdate = DateTime.MinValue;
            slot.ConnectionState = SlotConnectionState.Disconnected;
            slot.LastError = "";
            onSlotStateChanged?.Invoke(slot);
        }

        public void DisconnectAllSlots()
        {
            CancelPendingSequences();
            foreach (var slot in Slots.ToList())
                DisconnectSlot(slot);
        }

        void CancelPendingSequences()
        {
            try
            {
                sequenceCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            ClearMatchOrderDelaying();
        }

        /// <summary>
        /// Stops an in-progress Match Order delay sequence and immediately connects/updates
        /// any slots that were still waiting in the delaying state.
        /// </summary>
        public void CancelMatchOrderDelayAndProceed()
        {
            var pending = Slots
                .Where(s => matchOrderDelayingSlotIds.Contains(s.SlotId))
                .ToList();

            CancelPendingSequences();

            foreach (var slot in pending)
            {
                if (slot == null || !slot.Enabled)
                    continue;

                if (slot.IsConnected)
                {
                    SetPresenceForSlot(slot, showErrors: false);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(slot.ApplicationId))
                    continue;

                if (slot.ConnectionState == SlotConnectionState.Connecting ||
                    slot.ConnectionState == SlotConnectionState.UpdatingPresence)
                    continue;

                ConnectSlot(slot);
            }
        }

        CancellationToken BeginSequence()
        {
            CancelPendingSequences();
            sequenceCts = new CancellationTokenSource();
            return sequenceCts.Token;
        }

        static async Task DelayAsync(int milliseconds, CancellationToken cancellationToken)
        {
            if (milliseconds <= 0)
                return;

            await Task.Delay(milliseconds, cancellationToken);
        }

        public void ConnectSlot(PresenceSlot slot)
        {
            if (slot == null)
                return;

            if (rpcMode == MultiSlotRpcMode.SingleProcess)
            {
                foreach (var other in Slots.Where(s =>
                    s != slot &&
                    (s.IsConnected ||
                     s.ConnectionState == SlotConnectionState.Connecting ||
                     s.ConnectionState == SlotConnectionState.UpdatingPresence)))
                {
                    DisconnectSlot(other);
                }
            }

            if (string.IsNullOrWhiteSpace(slot.ApplicationId))
            {
                slot.ConnectionState = SlotConnectionState.Error;
                slot.LastError = "Application ID is required.";
                onSlotStateChanged?.Invoke(slot);
                return;
            }

            if (slot.IsConnected)
            {
                SetPresenceForSlot(slot, showErrors: true);
                return;
            }

            if (slot.ConnectionState == SlotConnectionState.Connecting ||
                slot.ConnectionState == SlotConnectionState.UpdatingPresence)
                return;

            slot.TimestampConnected = DateTime.UtcNow;
            slot.TimestampStarted = DateTime.UtcNow;
            InitSlot(slot);
        }

        public bool IsMatchOrderDelaying(PresenceSlot slot) =>
            slot != null && matchOrderDelayingSlotIds.Contains(slot.SlotId);

        public async Task ConnectAllEnabledSlotsAsync(bool matchListOrder = false, Func<int> getListeningWatchingDelayMs = null)
        {
            var cancellationToken = BeginSequence();
            var enabled = Slots.Where(s => s.Enabled && !string.IsNullOrWhiteSpace(s.ApplicationId) &&
                !s.IsConnected &&
                s.ConnectionState != SlotConnectionState.Connecting &&
                s.ConnectionState != SlotConnectionState.UpdatingPresence).ToList();

            try
            {
                if (!matchListOrder)
                {
                    for (int i = 0; i < enabled.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        ConnectSlot(enabled[i]);
                        if (i < enabled.Count - 1)
                            await DelayAsync(SlotInitDelayMs, cancellationToken);
                    }

                    return;
                }

                var playingOrCompeting = enabled.Where(s => !IsListeningOrWatching(s)).ToList();
                var listeningOrWatching = enabled.Where(IsListeningOrWatching).ToList();
                listeningOrWatching.Reverse();

                await RunMatchOrderSequenceAsync(
                    playingOrCompeting,
                    listeningOrWatching,
                    getListeningWatchingDelayMs ?? (() => 12000),
                    async slot =>
                    {
                        ConnectSlot(slot);
                        await WaitForSlotConnectedAsync(slot, cancellationToken);
                    },
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }

        void SetMatchOrderDelayingSlots(IEnumerable<PresenceSlot> slots)
        {
            var next = new HashSet<string>(slots.Select(s => s.SlotId));
            var changed = Slots.Where(s => matchOrderDelayingSlotIds.Contains(s.SlotId) || next.Contains(s.SlotId)).ToList();
            matchOrderDelayingSlotIds.Clear();
            foreach (var slot in slots)
                matchOrderDelayingSlotIds.Add(slot.SlotId);

            NotifySlotsChanged(changed);
        }

        void ClearMatchOrderDelaying()
        {
            if (matchOrderDelayingSlotIds.Count == 0)
                return;

            var changed = Slots.Where(s => matchOrderDelayingSlotIds.Contains(s.SlotId)).ToList();
            matchOrderDelayingSlotIds.Clear();
            NotifySlotsChanged(changed);
        }

        void NotifySlotsChanged(IEnumerable<PresenceSlot> slots)
        {
            foreach (var slot in slots)
                onSlotStateChanged?.Invoke(slot);
        }

        static bool IsListeningOrWatching(PresenceSlot slot) =>
            slot.ActivityType == ActivityType.Listening || slot.ActivityType == ActivityType.Watching;

        static async Task WaitForSlotConnectedAsync(PresenceSlot slot, CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(SlotConnectWaitTimeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (slot.ConnectionState == SlotConnectionState.Connected)
                    return;

                if (slot.ConnectionState == SlotConnectionState.Error ||
                    slot.ConnectionState == SlotConnectionState.Disconnected)
                    return;

                await DelayAsync(50, cancellationToken);
            }
        }

        public void UpdateAllEnabledSlots(bool force = false)
        {
            if (!force && !TryBeginBulkUpdate())
                return;

            foreach (var slot in Slots.Where(s => s.Enabled && s.IsConnected))
                SetPresenceForSlot(slot, showErrors: false);
        }

        public async Task UpdateAllEnabledSlotsAsync(
            bool force = false,
            bool matchListOrder = false,
            Func<int> getListeningWatchingDelayMs = null)
        {
            if (!force && !TryBeginBulkUpdate())
                return;

            var cancellationToken = BeginSequence();
            var enabled = Slots.Where(s => s.Enabled && s.IsConnected).ToList();

            try
            {
                if (!matchListOrder)
                {
                    foreach (var slot in enabled)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        SetPresenceForSlot(slot, showErrors: false);
                        await DelayAsync(250, cancellationToken);
                    }

                    return;
                }

                var playingOrCompeting = enabled.Where(s => !IsListeningOrWatching(s)).ToList();
                var listeningOrWatching = enabled.Where(IsListeningOrWatching).ToList();
                listeningOrWatching.Reverse();

                await RunMatchOrderSequenceAsync(
                    playingOrCompeting,
                    listeningOrWatching,
                    getListeningWatchingDelayMs ?? (() => 12000),
                    async slot =>
                    {
                        SetPresenceForSlot(slot, showErrors: false);
                        await WaitForSlotConnectedAsync(slot, cancellationToken);
                    },
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }

        async Task RunMatchOrderSequenceAsync(
            List<PresenceSlot> playingOrCompeting,
            List<PresenceSlot> listeningOrWatching,
            Func<int> getListeningWatchingDelayMs,
            Func<PresenceSlot, Task> processSlotAsync,
            CancellationToken cancellationToken)
        {
            try
            {
                if (listeningOrWatching.Count > 0)
                    SetMatchOrderDelayingSlots(listeningOrWatching);

                for (int i = 0; i < playingOrCompeting.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await processSlotAsync(playingOrCompeting[i]);
                    if (i < playingOrCompeting.Count - 1)
                        await DelayAsync(SlotInitDelayMs, cancellationToken);
                }

                for (int i = 0; i < listeningOrWatching.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var waiting = listeningOrWatching.Skip(i + 1).ToList();
                    if (waiting.Count > 0)
                        SetMatchOrderDelayingSlots(waiting);
                    else
                        ClearMatchOrderDelaying();

                    await processSlotAsync(listeningOrWatching[i]);

                    if (i < listeningOrWatching.Count - 1)
                    {
                        SetMatchOrderDelayingSlots(listeningOrWatching.Skip(i + 1));
                        int delayMs = Math.Max(0, getListeningWatchingDelayMs());
                        await DelayAsync(delayMs, cancellationToken);
                    }
                }
            }
            finally
            {
                ClearMatchOrderDelaying();
            }
        }

        public void ReconnectSlot(PresenceSlot slot)
        {
            if (slot == null)
                return;

            StopRestartTimer(slot.SlotId);
            slot.ConnectionState = SlotConnectionState.Disconnected;
            DisposeClient(slot);
            ConnectSlot(slot);
        }

        bool TryBeginBulkUpdate()
        {
            var now = DateTime.UtcNow;
            if ((now - lastBulkUpdateUtc).TotalMilliseconds < BulkUpdateCooldownMs)
            {
                if (!bulkUpdatePending)
                {
                    bulkUpdatePending = true;
                    Task.Delay(BulkUpdateCooldownMs).ContinueWith(_ =>
                    {
                        bulkUpdatePending = false;
                        UpdateAllEnabledSlots(force: true);
                    });
                }
                return false;
            }

            lastBulkUpdateUtc = now;
            return true;
        }

        void SlotOnReady(PresenceSlot slot, object sender, ReadyMessage args)
        {
            if (profileRecoveryActive || !IsCurrentClient(slot, sender))
                return;

            if (TryHandleDiscordUsernameChange(args.User?.Username))
                return;

            StopRestartTimer(slot.SlotId);
            EnterUpdatingPresence(slot);
            SetPresenceForSlot(slot, showErrors: true, ignoreThrottle: true);
        }

        void SlotOnPresenceUpdate(PresenceSlot slot, object sender, PresenceMessage args)
        {
            if (!IsCurrentClient(slot, sender))
                return;

            slot.ConnectionState = SlotConnectionState.Connected;
            slot.LastError = "";
            StopRestartTimer(slot.SlotId);
            onSlotStateChanged?.Invoke(slot);
        }

        void SlotOnClose(PresenceSlot slot, object sender, CloseMessage args)
        {
            if (profileRecoveryActive || !IsCurrentClient(slot, sender))
                return;

            // Intentional disconnect already set Disconnected — ignore teardown Close noise.
            if (slot.ConnectionState == SlotConnectionState.Disconnected)
                return;

            slot.ConnectionState = SlotConnectionState.Disconnected;
            slot.LastError = "";
            onSlotStateChanged?.Invoke(slot);

            if (rpcMode == MultiSlotRpcMode.SingleProcess)
                StartRestartTimer(slot);
        }

        void SlotOnError(PresenceSlot slot, object sender, ErrorMessage args)
        {
            if (!IsCurrentClient(slot, sender))
                return;

            if (slot.ConnectionState == SlotConnectionState.Disconnected)
                return;

            slot.LastError = args.Message;

            // Discord RPC often emits a brief OnError during handshake before OnReady.
            // Stay on Connecting so the status does not flash Error then Updating.
            if (slot.ConnectionState == SlotConnectionState.Connecting)
            {
                StartRestartTimer(slot);
                return;
            }

            slot.ConnectionState = SlotConnectionState.Error;
            onSlotStateChanged?.Invoke(slot);
            StartRestartTimer(slot);
        }

        void SlotOnConnectionFailed(PresenceSlot slot, object sender, ConnectionFailedMessage args)
        {
            if (!IsCurrentClient(slot, sender))
                return;

            if (slot.ConnectionState == SlotConnectionState.Disconnected)
                return;

            slot.LastError = "Connection failed";

            if (slot.ConnectionState == SlotConnectionState.Connecting)
            {
                StartRestartTimer(slot);
                return;
            }

            slot.ConnectionState = SlotConnectionState.Error;
            onSlotStateChanged?.Invoke(slot);
            StartRestartTimer(slot);
        }

        static bool IsCurrentClient(PresenceSlot slot, object sender) =>
            slot != null &&
            slot.Client != null &&
            !slot.Client.IsDisposed &&
            ReferenceEquals(slot.Client, sender);

        void EnterUpdatingPresence(PresenceSlot slot)
        {
            slot.ConnectionState = SlotConnectionState.UpdatingPresence;
            onSlotStateChanged?.Invoke(slot);
        }

        void StartRestartTimer(PresenceSlot slot)
        {
            if (!restartTimers.TryGetValue(slot.SlotId, out var timer))
            {
                timer = new System.Timers.Timer(RestartDelayMs) { AutoReset = false };
                timer.Elapsed += (sender, e) =>
                {
                    if (profileRecoveryActive || !slot.Enabled)
                        return;

                    if (!restartAttemptsLeft.ContainsKey(slot.SlotId))
                        restartAttemptsLeft[slot.SlotId] = 30;

                    if (restartAttemptsLeft[slot.SlotId]-- <= 0)
                    {
                        DisconnectSlot(slot);
                        return;
                    }

                    ReconnectSlot(slot);
                };
                restartTimers[slot.SlotId] = timer;
            }

            timer.Stop();
            timer.Start();
        }

        void StopRestartTimer(string slotId)
        {
            if (restartTimers.TryGetValue(slotId, out var timer))
                timer.Stop();
            restartAttemptsLeft.Remove(slotId);
        }

        bool TryHandleDiscordUsernameChange(string username)
        {
            username = username ?? "";

            if (!string.IsNullOrEmpty(lastKnownDiscordUsername) &&
                !string.IsNullOrEmpty(username) &&
                !string.Equals(username, lastKnownDiscordUsername, StringComparison.Ordinal))
            {
                lastKnownDiscordUsername = username;
                if (rpcMode != MultiSlotRpcMode.SingleProcess)
                    RequestProfileSwitchRecovery();
                return rpcMode != MultiSlotRpcMode.SingleProcess;
            }

            if (!string.IsNullOrEmpty(username))
                lastKnownDiscordUsername = username;

            return false;
        }

        void RequestProfileSwitchRecovery()
        {
            if (profileSwitchRecoveryQueued || profileRecoveryActive)
                return;

            profileSwitchRecoveryQueued = true;
            runOnUiThread(() =>
            {
                try
                {
                    onProfileSwitchRecovery?.Invoke();
                }
                finally
                {
                    profileSwitchRecoveryQueued = false;
                }
            });
        }

        void DisposeClient(PresenceSlot slot)
        {
            if (slot.Client == null)
                return;

            if (!slot.Client.IsDisposed)
                slot.Client.Dispose();

            slot.Client = null;
            slot.BoundApplicationId = "";
            slot.BoundPipe = int.MinValue;
        }

        static void ShowBuildError(PresenceBuilder.BuildResult build)
        {
            if (!string.IsNullOrEmpty(build.ImageErrorField))
            {
                var res = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
                QuietMessageBox.Show(
                    Strings.errorInvalidImageURL + " (" + res.GetString("label" + build.ImageErrorField + ".Text") + ")",
                    Strings.error,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            if (!string.IsNullOrEmpty(build.ErrorMessage))
                QuietMessageBox.Show(build.ErrorMessage, Strings.error, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public void Dispose()
        {
            DisconnectAllSlots();
            foreach (var timer in restartTimers.Values)
                timer.Dispose();
            restartTimers.Clear();
        }
    }
}
