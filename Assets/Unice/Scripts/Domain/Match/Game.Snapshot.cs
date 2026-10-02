using System;
using System.IO;

namespace Unice
{
    /// <summary>
    /// The host's view of the match, packed small enough to send ~15 times a second, and how guests apply it.
    /// The byte layout is the protocol between devices: change <see cref="SnapshotVersion"/> whenever it changes,
    /// so a device running an older build ignores snapshots it would misread.
    /// </summary>
    public sealed partial class Game
    {
        const byte SnapshotVersion = 5;

        /// <summary>Per-character yes/no states, packed into one byte.</summary>
        [Flags]
        enum CharacterFlags : byte { None = 0, Bot = 1, OutOfRound = 2, Moving = 4, Loud = 8, Unice = 16, Tired = 32, Sneaking = 64 }

        /// <summary>A guest that sees a character this far from where it is drawn snaps it there instead of sliding.</summary>
        const float SnapDistance = 200;

        public byte[] WriteSnapshot()
        {
            using var stream = new MemoryStream(1024);
            using var writer = new BinaryWriter(stream);
            writer.Write(SnapshotVersion);

            // match state
            writer.Write(epoch); writer.Write(hostClock); writer.Write((byte)phase);
            writer.Write((byte)round); writer.Write((byte)totalRounds); writer.Write((byte)botCount);
            writer.Write(timeLeft); writer.Write(roundClock);
            writer.Write(hideTime); writer.Write(huntTime); writer.Write(giggleInterval); writer.Write(mapId);
            writer.Write((sbyte)IndexOf(unice)); writer.Write((sbyte)IndexOf(nextUnice));
            writer.Write(roundResult != null);
            if (roundResult != null)
            {
                writer.Write((sbyte)(roundResult.uniceWon ? 1 : 0)); writer.Write((sbyte)roundResult.dollHolder);
                writer.Write((sbyte)roundResult.holderSpotIndex); writer.Write((sbyte)roundResult.nextUnice);
            }

            // the doll
            writer.Write((sbyte)IndexOf(doll.holder)); writer.Write(doll.giggleTimer); writer.Write(doll.hasLastGiggle);
            if (doll.hasLastGiggle) { writer.Write(doll.lastGiggleX); writer.Write(doll.lastGiggleY); writer.Write(roundClock - doll.lastGiggleTime); }

            // characters
            writer.Write((byte)characters.Count);
            foreach (var character in characters)
            {
                writer.Write(character.playerId); writer.Write(character.name); writer.Write(Kit.Pack(character.kit));
                writer.Write((byte)FlagsOf(character));
                writer.Write((short)MathF.Round(character.x)); writer.Write((short)MathF.Round(character.y));
                writer.Write((short)MathF.Round(MathF.Atan2(character.faceY, character.faceX) * 1000));   // facing angle, in milliradians
                writer.Write((sbyte)(character.hidingSpot != null ? character.hidingSpot.spotIndex : -1));
                writer.Write(MathF.Max(0, character.passCooldown)); writer.Write(MathF.Max(0, character.laughCooldown));
                writer.Write((short)character.score); writer.Write((byte)character.uniceTurns); writer.Write(MathF.Max(0, character.stunTime));
                writer.Write(character.teleportSeq);
            }

            // the last events
            writer.Write(eventSeq); writer.Write((byte)events.Count);
            foreach (var gameEvent in events)
            {
                writer.Write(gameEvent.sequence); writer.Write((byte)gameEvent.type);
                writer.Write(gameEvent.actor); writer.Write(gameEvent.target); writer.Write(gameEvent.value); writer.Write(gameEvent.x); writer.Write(gameEvent.y);
                writer.Write(gameEvent.text != null); if (gameEvent.text != null) writer.Write(gameEvent.text);
            }
            writer.Flush();
            return stream.ToArray();
        }

        static int IndexOf(Character character) => character != null ? character.index : -1;

        static CharacterFlags FlagsOf(Character character)
        {
            var flags = CharacterFlags.None;
            if (character.isBot) flags |= CharacterFlags.Bot;
            if (character.outOfRound) flags |= CharacterFlags.OutOfRound;
            if (character.moving) flags |= CharacterFlags.Moving;
            if (character.loud) flags |= CharacterFlags.Loud;
            if (character.IsUnice) flags |= CharacterFlags.Unice;
            if (character.tiredTime > 0) flags |= CharacterFlags.Tired;
            if (character.sneaking) flags |= CharacterFlags.Sneaking;
            return flags;
        }

        /// <summary>
        /// Guest: mirror the host's snapshot. Returns this device's character (or null when only watching).
        /// The local character keeps its own position, unless the host moved it (teleportSeq changed), hid it or took it out.
        /// </summary>
        public Character ApplySnapshot(byte[] data, string myPlayerId, Character myCharacter)
        {
            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);
            if (reader.ReadByte() != SnapshotVersion) return myCharacter;

            // match state
            epoch = reader.ReadInt32(); hostClock = reader.ReadSingle();
            phase = (Phase)Math.Min((int)reader.ReadByte(), (int)Phase.Final);
            if (phase == Phase.Home) phase = Phase.Lobby;   // a guest in a room is never "home"
            round = reader.ReadByte(); totalRounds = reader.ReadByte(); botCount = reader.ReadByte();
            timeLeft = reader.ReadSingle(); roundClock = reader.ReadSingle();
            hideTime = reader.ReadSingle(); huntTime = reader.ReadSingle(); giggleInterval = reader.ReadSingle();
            mapId = reader.ReadString(); if (Map.Id != mapId) Map.Load(Maps.ById(mapId));
            int uniceIndex = reader.ReadSByte(), nextUniceIndex = reader.ReadSByte();
            if (reader.ReadBoolean())
                roundResult = new RoundResult
                {
                    uniceWon = reader.ReadSByte() != 0, dollHolder = reader.ReadSByte(),
                    holderSpotIndex = reader.ReadSByte(), nextUnice = reader.ReadSByte(),
                };
            else roundResult = null;

            // the doll
            int holderIndex = reader.ReadSByte(); float giggleTimer = reader.ReadSingle(); bool hasLastGiggle = reader.ReadBoolean();
            float giggleX = 0, giggleY = 0, giggleAge = 0;
            if (hasLastGiggle) { giggleX = reader.ReadSingle(); giggleY = reader.ReadSingle(); giggleAge = reader.ReadSingle(); }

            // characters
            int count = Math.Min((int)reader.ReadByte(), Rules.MaxPlayers);
            while (characters.Count > count) characters.RemoveAt(characters.Count - 1);
            foreach (var spot in Map.Spots) spot.occupant = null;
            Character newMyCharacter = null;
            for (int i = 0; i < count; i++)
            {
                string playerId = reader.ReadString(), name = TextFormat.CleanName(reader.ReadString());
                int packedKit = reader.ReadInt32(); var flags = (CharacterFlags)reader.ReadByte();
                float x = reader.ReadInt16(), y = reader.ReadInt16(), facingAngle = reader.ReadInt16() / 1000f;
                int spotIndex = reader.ReadSByte();
                float passCooldown = reader.ReadSingle(), laughCooldown = reader.ReadSingle();
                int score = reader.ReadInt16(), uniceTurns = reader.ReadByte();
                float stunTime = reader.ReadSingle(); int teleportSeq = reader.ReadInt32();

                // reuse the character in this slot if it is the same player, so positions keep sliding smoothly
                Character character = i < characters.Count ? characters[i] : null; bool isNew = false;
                if (character == null || character.playerId != playerId)
                {
                    character = new Character(i, playerId, name, Kit.Unpack(packedKit), false);
                    if (i < characters.Count) characters[i] = character; else characters.Add(character);
                    isNew = true;
                }
                character.name = name; character.kit = Kit.Unpack(packedKit);
                character.isBot = flags.HasFlag(CharacterFlags.Bot);
                character.outOfRound = flags.HasFlag(CharacterFlags.OutOfRound);
                character.role = flags.HasFlag(CharacterFlags.Unice) ? Role.Unice : Role.Hider;
                character.tiredTime = flags.HasFlag(CharacterFlags.Tired) ? 1 : 0;
                character.hidingSpot = spotIndex >= 0 && spotIndex < Map.Spots.Length ? Map.Spots[spotIndex] : null;
                if (character.hidingSpot != null) character.hidingSpot.occupant = character;
                character.passCooldown = passCooldown; character.laughCooldown = laughCooldown;
                character.score = score; character.uniceTurns = uniceTurns; character.stunTime = stunTime;

                bool teleported = isNew || teleportSeq != character.teleportSeq;
                if (playerId == myPlayerId)
                {
                    // this device moves its own character; only take the host's position when the host forces it
                    newMyCharacter = character;
                    if (teleported || character.hidingSpot != null || character.outOfRound || !IsLive) { character.x = character.targetX = x; character.y = character.targetY = y; }
                    if (teleported) { character.faceX = MathF.Cos(facingAngle); character.faceY = MathF.Sin(facingAngle); }
                }
                else
                {
                    // others slide towards (targetX, targetY) every frame; see App.RenderFrame
                    character.moving = flags.HasFlag(CharacterFlags.Moving); character.loud = flags.HasFlag(CharacterFlags.Loud);
                    character.sneaking = flags.HasFlag(CharacterFlags.Sneaking);
                    character.faceX = MathF.Cos(facingAngle); character.faceY = MathF.Sin(facingAngle);
                    character.targetX = x; character.targetY = y;
                    if (teleported || Geometry.Distance(x, y, character.x, character.y) > SnapDistance) { character.x = x; character.y = y; }
                }
                character.teleportSeq = teleportSeq;
            }
            unice = CharacterAt(uniceIndex); nextUnice = CharacterAt(nextUniceIndex);
            doll.holder = CharacterAt(holderIndex); doll.giggleTimer = giggleTimer; doll.hasLastGiggle = hasLastGiggle;
            doll.lastGiggleX = giggleX; doll.lastGiggleY = giggleY; doll.lastGiggleTime = roundClock - giggleAge; doll.lastGiggleSpot = null;

            // the last events
            eventSeq = reader.ReadInt32(); int eventCount = reader.ReadByte();
            events.Clear();
            for (int i = 0; i < eventCount; i++)
            {
                var gameEvent = new GameEvent
                {
                    sequence = reader.ReadInt32(), type = (GameEventType)reader.ReadByte(),
                    actor = reader.ReadInt32(), target = reader.ReadInt32(), value = reader.ReadInt32(), x = reader.ReadInt32(), y = reader.ReadInt32(),
                };
                if (reader.ReadBoolean()) gameEvent.text = TextFormat.CleanName(reader.ReadString());
                events.Add(gameEvent);
            }
            return newMyCharacter;
        }

        /// <summary>The epoch of a snapshot without decoding the rest (-1 if it isn't one this build can read).</summary>
        public static int ReadEpoch(byte[] data)
        {
            if (data == null || data.Length < 5 || data[0] != SnapshotVersion) return -1;
            return BitConverter.ToInt32(data, 1);
        }
    }
}
