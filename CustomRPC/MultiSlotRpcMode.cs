namespace CustomRPC
{
    /// <summary>
    /// Legacy (single activity) vs Multi-RP (fake PID per slot).
    /// </summary>
    public enum MultiSlotRpcMode
    {
        /// <summary>Legacy — one activity, shared CustomRP.exe PID.</summary>
        SingleProcess = 0,

        /// <summary>Multi-RP — unique fake PID per slot.</summary>
        CustomProcessId = 2,
    }
}
