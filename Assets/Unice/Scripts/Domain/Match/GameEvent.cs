namespace Unice
{
    public enum EvType : byte { None, Start, Enter, Giggle, Pass, Open, Catch, End, Final, Host, Left, Lobby }

    /// <summary>Something that happened in the match (args a..e/s depend on <see cref="type"/>). The host appends them; every device plays them once.</summary>
    public struct GameEvent
    {
        public int seq; public EvType type; public int a, b, c, d, e; public string s;
    }
}
