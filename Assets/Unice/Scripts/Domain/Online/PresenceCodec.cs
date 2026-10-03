using System;
using System.IO;

namespace Unice
{
    /// <summary>
    /// <see cref="Presence"/> on the wire: every player sends theirs ~15 times a second.
    /// The first byte is a format version; bump <see cref="Version"/> whenever the layout changes.
    /// </summary>
    public static class PresenceCodec
    {
        const byte Version = 2;
        /// <summary>Voice ids are short ids from the voice service; anything else is dropped.</summary>
        const int MaxVoiceIdLength = 64;

        public static byte[] Write(in Presence presence)
        {
            using var stream = new MemoryStream(64);
            using var writer = new BinaryWriter(stream);
            writer.Write(Version);
            writer.Write(presence.name ?? ""); writer.Write(presence.kit); writer.Write(presence.joinTime); writer.Write(presence.ready); writer.Write(presence.teleportSeq);
            writer.Write(presence.hasPosition);
            if (presence.hasPosition) { writer.Write(presence.x); writer.Write(presence.y); writer.Write(presence.faceX); writer.Write(presence.faceY); writer.Write(presence.sneaking); }
            writer.Write(presence.actionSeq); writer.Write((byte)presence.action); writer.Write((short)presence.actionArg);
            writer.Write(presence.talking);
            writer.Write(presence.voiceId ?? "");
            writer.Flush();
            return stream.ToArray();
        }

        /// <summary>Reads another player's presence. This data comes from other devices: anything malformed returns false.</summary>
        public static bool TryRead(byte[] data, out Presence presence)
        {
            presence = default;
            try
            {
                using var stream = new MemoryStream(data);
                using var reader = new BinaryReader(stream);
                if (reader.ReadByte() != Version) return false;
                presence.name = TextFormat.CleanName(reader.ReadString()); presence.kit = reader.ReadInt32(); presence.joinTime = reader.ReadInt64();
                presence.ready = reader.ReadBoolean(); presence.teleportSeq = reader.ReadInt32();
                presence.hasPosition = reader.ReadBoolean();
                if (presence.hasPosition)
                {
                    presence.x = reader.ReadSingle(); presence.y = reader.ReadSingle();
                    presence.faceX = reader.ReadSingle(); presence.faceY = reader.ReadSingle(); presence.sneaking = reader.ReadBoolean();
                }
                if (float.IsNaN(presence.x) || float.IsNaN(presence.y) || float.IsNaN(presence.faceX) || float.IsNaN(presence.faceY)) presence.hasPosition = false;
                presence.actionSeq = reader.ReadInt32();
                byte action = reader.ReadByte();
                presence.action = action <= (byte)PlayerAction.Laugh ? (PlayerAction)action : PlayerAction.None;
                presence.actionArg = reader.ReadInt16();
                presence.talking = reader.ReadBoolean();
                presence.voiceId = CleanVoiceId(reader.ReadString());
                return true;
            }
            catch (Exception) { return false; }
        }

        /// <summary>Keeps a voice id only if it looks like one (letters, digits, '-' and '_', not too long); otherwise null.</summary>
        public static string CleanVoiceId(string voiceId)
        {
            if (string.IsNullOrEmpty(voiceId) || voiceId.Length > MaxVoiceIdLength) return null;
            foreach (char c in voiceId)
                if (!(char.IsLetterOrDigit(c) && c < 128) && c != '-' && c != '_') return null;
            return voiceId;
        }
    }
}
