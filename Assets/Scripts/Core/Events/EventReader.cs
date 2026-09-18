namespace LastGround.Core.Events
{
    /// <summary>Per-consumer cursor into an <see cref="EventChannel{T}"/>.</summary>
    public struct EventReader<T> where T : struct
    {
        public ulong Cursor;

        /// <summary>Events overwritten before this reader got to them.</summary>
        public ulong Dropped;

        public EventReader(ulong cursor)
        {
            Cursor = cursor;
            Dropped = 0;
        }
    }
}
