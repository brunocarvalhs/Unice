namespace Unice
{
    public enum Phase : byte { Home, Lobby, Hide, Hunt, Result, Final }
    public enum Act : byte { None, Hide, Exit, Pass, Open, Catch, Laugh }

    public sealed class DollState { public Ent holder; public float giggleT = Rules.GiggleEvery; public bool hasLast; public float lastX, lastY, lastT; public Furn lastSpot; }
}
