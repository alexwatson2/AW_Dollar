using System.Drawing;
using System.Reflection;

namespace AW_Dollar.Rendering;

/// <summary>Loads PNGs embedded as resources (the USD/EUR flag badges) into standalone bitmaps.</summary>
public static class EmbeddedImageLoader
{
    public static Bitmap Load(string resourceFileName)
    {
        var asm = Assembly.GetExecutingAssembly();
        var fullName = $"AW_Dollar.Resources.{resourceFileName}";
        using var resourceStream = asm.GetManifestResourceStream(fullName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {fullName}");

        // Copy fully into memory and clone the decoded bitmap so it does not keep a lazy
        // reference back into the (about to be disposed) resource stream.
        using var memory = new MemoryStream();
        resourceStream.CopyTo(memory);
        memory.Position = 0;
        using var decoded = new Bitmap(memory);
        return new Bitmap(decoded);
    }
}
