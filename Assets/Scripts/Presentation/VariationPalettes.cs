using System;
using UnityEngine;

namespace Roloc.Presentation
{
    [CreateAssetMenu(fileName = "VariationPalettes", menuName = "ROLOC/Variation Palettes")]
    public sealed class VariationPalettes : ScriptableObject
    {
        [Serializable] public sealed class Set
        {
            public string Name;
            public Color[] Colors;
            public Set(string name, params string[] hex)
            {
                Name = name; Colors = new Color[hex.Length];
                for (int i = 0; i < hex.Length; i++) ColorUtility.TryParseHtmlString("#" + hex[i], out Colors[i]);
            }
        }
        // Stable indices: mixed, mixed, paired, paired, monochrome, monochrome.
        public Set[] Palettes = Defaults();
        public static Set[] Defaults() => new[] {
            new Set("Electric", "EF7136", "325DEB", "00A58B", "B334A2"),
            new Set("Summer", "DEB323", "007F91", "DC4B66", "7847CE"),
            new Set("Pink pair", "F6AACD", "A91B59", "246BD6", "CFB127"),
            new Set("Blue pair", "9CCDF4", "174A98", "E57437", "A23696"),
            new Set("Four pinks", "F8BEDB", "EC7AAC", "C53577", "71173E"),
            new Set("Four blues", "B0D8F5", "6CAAE4", "2C70B9", "173965") };

        public Color Get(int palette, int identity)
        {
            if (!IsValid(Palettes)) return Defaults()[palette].Colors[identity];
            return Palettes[palette].Colors[identity];
        }
        public static bool IsValid(Set[] sets)
        {
            if (sets == null || sets.Length != 6) return false;
            foreach (var set in sets)
            {
                if (set?.Colors == null || set.Colors.Length != 4) return false;
                for (int i = 0; i < 4; i++)
                {
                    var c = set.Colors[i];
                    if (c.a != 1 || !float.IsFinite(c.r) || !float.IsFinite(c.g) || !float.IsFinite(c.b)) return false;
                    for (int j = 0; j < i; j++)
                    {
                        Color32 a = c, b = set.Colors[j];
                        if (a.r == b.r && a.g == b.g && a.b == b.b) return false;
                    }
                }
            }
            return true;
        }
    }
}
