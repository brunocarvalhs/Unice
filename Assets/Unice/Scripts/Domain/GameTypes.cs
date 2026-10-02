using System.Collections.Generic;

namespace Unice
{
    public enum Phase : byte { Home, Lobby, Hide, Hunt, Result, Final }
    public enum EvType : byte { None, Start, Enter, Giggle, Pass, Open, Catch, End, Final, Host, Left, Lobby }
    public enum Act : byte { None, Hide, Exit, Pass, Open, Catch, Laugh }

    public struct GameEvent
    {
        public int seq; public EvType type; public int a, b, c, d, e; public string s;
    }

    /// <summary>What a player tells everyone else about themselves (the web version's "presence").</summary>
    public struct Presence
    {
        public string name; public int kit; public long joinT; public bool rdy; public int tp;
        public bool hasPos; public float x, y, fx, fy; public bool sneak, talking;
        public int aSeq; public Act aType; public int aArg;
    }

    public sealed class PeerInfo
    {
        public string id; public bool isMe; public Presence pres;
        public byte[] lastSnap; public int lastSnapEp; public float lastSnapAt = -999;
        public float presAt = -999;   // when their presence last arrived (stale = app paused or gone)
    }

    public sealed class DollState { public Ent holder; public float giggleT = Rules.GiggleEvery; public bool hasLast; public float lastX, lastY, lastT; public Furn lastSpot; }

    public static class Rules
    {
        public const float RoundTime = 120, HideTime = 30, ResultTime = 9, GiggleEvery = 10, PassCd = 5, LaughCd = 5, PassR = 60, CatchR = 30, Stun = 2.5f;
        public const int MaxPlayers = 8;
        public const float SpdRun = 150, SpdSneak = 78, SpdUnice = 152, SpdTired = 112, SpdPatrol = 138, SpdUnicePlayer = 165;
        public const float ViewHider = 300, ViewHidden = 110, ViewUnice = 300, ViewSpect = 520;
        public const float AiSense = 330, AiHear = 200, AiCheck = 1, NoiseR = 270, HeartR = 280;
        public static readonly string[] BotNames = { "Bia", "Tito", "Maru", "Zeca", "Nina", "Otto", "Lia", "Caio" };
    }
}
