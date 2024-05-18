﻿namespace LabExtended.Core.Hooking
{
    public class HookSyncOptionsValue
    {
        public bool DoNotWait { get; }
        public bool DoNotKill { get; }

        public float? Timeout { get; }

        public HookSyncOptionsValue() { }

        public HookSyncOptionsValue(bool doNotWait, bool doNotKill, float? timeout)
        {
            DoNotWait = doNotWait;
            DoNotKill = doNotKill;
            Timeout = timeout;
        }

        public override string ToString()
            => $"DoNotKill={DoNotKill} DoNotWait={DoNotWait} TimeOut={(Timeout.HasValue ? Timeout.Value.ToString() : "null")}";
    }
}