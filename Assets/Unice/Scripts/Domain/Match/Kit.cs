namespace Unice
{
    /// <summary>
    /// A child's look: one option index per slot (skin, hair style, hair color, outfit, outfit color, hat, accessory).
    /// The option names here are only for reference; what players see comes from Localization ("kit.*").
    /// </summary>
    public static class Kit
    {
        public const int SkinSlot = 0, HairSlot = 1, HairColorSlot = 2, OutfitSlot = 3, OutfitColorSlot = 4, HatSlot = 5, AccessorySlot = 6;
        public const int Slots = 7;

        public static readonly string[] Skins = { "#f3d3b8", "#dcaa82", "#b07a52", "#6f4a33" };
        public static readonly string[] Hairs = { "Curto", "Rabo de cavalo", "Maria-chiquinha", "Coque" };
        public static readonly string[] HairColors = { "#20150f", "#5b3a22", "#b5532a", "#e0b95a", "#4a6fb5" };
        public static readonly string[] Outfits = { "Lisa", "Listrada", "Bolinhas", "Vestido", "Capa" };
        public static readonly string[] OutfitColors = { "#e9795f", "#6fa8dc", "#9fcf7a", "#f2c14e", "#b98ad8", "#5fc9b8", "#ef8fb4", "#e6e0d4" };
        public static readonly string[] Hats = { "Nenhum", "Boné", "Gorro", "Laço", "Coroa", "Orelhinhas" };
        public static readonly string[] Accessories = { "Nenhum", "Mochila", "Óculos", "Ursinho" };
        /// <summary>How many options each slot has, in slot order.</summary>
        public static readonly int[] OptionCount = { 4, 4, 5, 5, 8, 6, 4 };

        /// <summary>A kit with every slot in range (missing or invalid slots become 0). Kits from other players go through here.</summary>
        public static int[] Sanitize(int[] kit)
        {
            var clean = new int[Slots];
            for (int slot = 0; slot < Slots; slot++)
            {
                int option = kit != null && slot < kit.Length ? kit[slot] : 0;
                clean[slot] = option >= 0 && option < OptionCount[slot] ? option : 0;
            }
            return clean;
        }

        public static int[] Random()
        {
            var kit = new int[Slots];
            for (int slot = 0; slot < Slots; slot++) kit[slot] = GameRandom.Index(OptionCount[slot]);
            return kit;
        }

        /// <summary>3 bits per slot fits the whole kit in one int for the network.</summary>
        public static int Pack(int[] kit)
        {
            int packed = 0;
            for (int slot = 0; slot < Slots; slot++) packed |= (kit[slot] & 7) << (slot * 3);
            return packed;
        }

        public static int[] Unpack(int packed)
        {
            var kit = new int[Slots];
            for (int slot = 0; slot < Slots; slot++) kit[slot] = (packed >> (slot * 3)) & 7;
            return Sanitize(kit);
        }

        public static string OutfitColor(int[] kit) => OutfitColors[kit[OutfitColorSlot]];
    }
}
