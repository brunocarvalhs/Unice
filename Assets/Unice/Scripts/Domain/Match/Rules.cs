namespace Unice
{
    /// <summary>The tuning numbers of the game (times in seconds, distances and speeds in house pixels). Same as the web version.</summary>
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
