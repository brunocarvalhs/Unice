namespace Unice
{
    /// <summary>A child's look: skin, hair style, hair color, outfit, outfit color, hat, accessory (indices).</summary>
    public static class Kit
    {
        public static readonly string[] Skins = { "#f3d3b8", "#dcaa82", "#b07a52", "#6f4a33" };
        public static readonly string[] Hairs = { "Curto", "Rabo de cavalo", "Maria-chiquinha", "Coque" };
        public static readonly string[] HairColors = { "#20150f", "#5b3a22", "#b5532a", "#e0b95a", "#4a6fb5" };
        public static readonly string[] Outfits = { "Lisa", "Listrada", "Bolinhas", "Vestido", "Capa" };
        public static readonly string[] OutfitColors = { "#e9795f", "#6fa8dc", "#9fcf7a", "#f2c14e", "#b98ad8", "#5fc9b8", "#ef8fb4", "#e6e0d4" };
        public static readonly string[] Hats = { "Nenhum", "Boné", "Gorro", "Laço", "Coroa", "Orelhinhas" };
        public static readonly string[] Accessories = { "Nenhum", "Mochila", "Óculos", "Ursinho" };
        public static readonly int[] Max = { 4, 4, 5, 5, 8, 6, 4 };
        public const int Slots = 7;

        public static int[] Clean(int[] k)
        {
            var r = new int[Slots];
            for (int i = 0; i < Slots; i++) { int v = k != null && i < k.Length ? k[i] : 0; r[i] = v >= 0 && v < Max[i] ? v : 0; }
            return r;
        }
        public static int[] Random()
        {
            var r = new int[Slots];
            for (int i = 0; i < Slots; i++) r[i] = U.RandInt(Max[i]);
            return r;
        }
        /// <summary>3 bits per slot fits every option in one int for the network.</summary>
        public static int Pack(int[] k) { int v = 0; for (int i = 0; i < Slots; i++) v |= (k[i] & 7) << (i * 3); return v; }
        public static int[] Unpack(int v) { var r = new int[Slots]; for (int i = 0; i < Slots; i++) r[i] = (v >> (i * 3)) & 7; return Clean(r); }
        public static string OutfitColor(int[] k) => OutfitColors[k[4]];
    }
}
