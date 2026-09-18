using LastGround.Core.Events;

namespace LastGround.Core.Net
{
    /// <summary>
    /// Allocation-free stream of events of one type. Presentation reads the same stream whether events come
    /// from the local sim (host) or the network (client).
    /// </summary>
    public interface IGameEventStream<T> where T : struct
    {
        /// <summary>Creates a reader positioned at the current end of the stream.</summary>
        EventReader<T> CreateReader();

        /// <summary>Reads the next event after the reader's cursor. Returns false when caught up.</summary>
        bool TryRead(ref EventReader<T> reader, out T value);
    }
}
