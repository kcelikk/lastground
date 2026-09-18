using System;
using LastGround.Core.Net.Wire;

namespace LastGround.Networking.Discovery
{
    /// <summary>Discovery wire format (TDD_02 §18.1). Magic "LGRD" guards against unrelated traffic on the port.</summary>
    public static class DiscoveryPacket
    {
        public const uint Magic = 0x4452474C; // "LGRD" little-endian
        public const byte KindRequest = 1;
        public const byte KindResponse = 2;

        public struct Request
        {
            public uint ProtocolVersion;
            public uint Nonce;
            public double SentAt;
        }

        public struct Response
        {
            public uint ProtocolVersion;
            public uint ContentHash;
            public string SessionName;
            public byte Players;
            public byte MaxPlayers;
            public ushort GamePort;
            public bool InRun;
            public uint Nonce;
            public double EchoSentAt;
        }

        public static void Write(NetWriter w, in Request r)
        {
            w.Reset();
            w.WriteUInt(Magic);
            w.WriteByte(KindRequest);
            w.WriteUInt(r.ProtocolVersion);
            w.WriteUInt(r.Nonce);
            w.WriteDouble(r.SentAt);
        }

        public static void Write(NetWriter w, in Response r)
        {
            w.Reset();
            w.WriteUInt(Magic);
            w.WriteByte(KindResponse);
            w.WriteUInt(r.ProtocolVersion);
            w.WriteUInt(r.ContentHash);
            w.WriteString(r.SessionName);
            w.WriteByte(r.Players);
            w.WriteByte(r.MaxPlayers);
            w.WriteUShort(r.GamePort);
            w.WriteBool(r.InRun);
            w.WriteUInt(r.Nonce);
            w.WriteDouble(r.EchoSentAt);
        }

        /// <summary>Returns the packet kind, or 0 for foreign/malformed data.</summary>
        public static byte ReadKind(ArraySegment<byte> data, out NetReader reader)
        {
            reader = new NetReader(data);
            if (reader.ReadUInt() != Magic) return 0;
            byte kind = reader.ReadByte();
            return reader.Failed ? (byte)0 : kind;
        }

        public static bool TryRead(ref NetReader r, out Request request)
        {
            request = new Request { ProtocolVersion = r.ReadUInt(), Nonce = r.ReadUInt(), SentAt = r.ReadDouble() };
            return !r.Failed;
        }

        public static bool TryRead(ref NetReader r, out Response response)
        {
            response = new Response
            {
                ProtocolVersion = r.ReadUInt(),
                ContentHash = r.ReadUInt(),
                SessionName = r.ReadString(),
                Players = r.ReadByte(),
                MaxPlayers = r.ReadByte(),
                GamePort = r.ReadUShort(),
                InRun = r.ReadBool(),
                Nonce = r.ReadUInt(),
                EchoSentAt = r.ReadDouble(),
            };
            return !r.Failed;
        }
    }
}
