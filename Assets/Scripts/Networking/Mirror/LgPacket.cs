using System;
using Mirror;

namespace LastGround.Networking.MirrorLink
{
    /// <summary>
    /// The only Mirror message type: an opaque payload serialized by our own protocol (TDD_02 §15.11 rule 1).
    /// Mirror never sees gameplay structs, so switching frameworks does not touch the protocol.
    /// </summary>
    public struct LgPacket : NetworkMessage
    {
        public ArraySegment<byte> Payload;
    }
}
