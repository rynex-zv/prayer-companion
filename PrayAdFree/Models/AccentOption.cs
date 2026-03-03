using Microsoft.Maui.Graphics;

namespace Pray_Ad_Free.Models;

public sealed class AccentOption {
    public AccentOption(int index, string label, string hex) {
        Index = index;
        Label = label;
        Hex = hex;
        Color = Color.FromArgb(hex);
    }

    public int Index { get; }
    public string Label { get; }
    public string Hex { get; }
    public Color Color { get; }
}
