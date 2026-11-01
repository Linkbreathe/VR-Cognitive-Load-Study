using UnityEngine;

namespace EvidenceVR
{
    public static class LabPalette
    {
        public static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }
        public static readonly Color Wall = Hex("8A8985"), Floor = Hex("555A5B"), Trim = Hex("6A6D6C");
        public static readonly Color Panel = Hex("30393D"), Inset = Hex("394347"), Edge = Hex("657271");
        public static readonly Color Text = Hex("F0F1ED"), Muted = Hex("B9C4C4"), Accent = Hex("A9C7BF");
        public static readonly Color Ceiling = Hex("A3A49F"), KeyLight = Hex("FFF8EF"), AmbientSky = Hex("ADB9C0"), AmbientGround = Hex("68665E");
        public static readonly Color[] Stimuli = { new Color(.10f,.36f,.95f), new Color(1f,.42f,.08f), new Color(.12f,.72f,.32f), new Color(.77f,.15f,.67f) };
    }
}
