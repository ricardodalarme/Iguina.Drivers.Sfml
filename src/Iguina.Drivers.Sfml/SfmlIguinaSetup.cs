using SFML.Graphics;

namespace Iguina.Drivers.Sfml;

/// <summary>Convenience factory for creating an Iguina <see cref="UISystem"/> with SFML drivers.</summary>
public static class SfmlIguinaSetup
{
    /// <summary>Creates a fully-configured <see cref="UISystem"/> with SFML rendering and input.</summary>
    /// <param name="window">The SFML render window (used for both rendering and input).</param>
    /// <param name="themePath">Directory containing the Iguina theme files (styles, textures). Used to resolve relative texture and font paths from stylesheets.</param>
    /// <param name="systemStylesheetPath">Full path to the system-level stylesheet JSON file (e.g., <c>"path/to/theme/system_style.json"</c>).</param>
    /// <param name="defaultFont">Fallback font used when a stylesheet does not specify a font ID.</param>
    /// <returns>A new <see cref="UISystem"/> ready to use.</returns>
    /// <remarks>
    /// Example usage:
    /// <code>
    /// var window = new RenderWindow(new VideoMode(800, 600), "My Game");
    /// var font = new Font("path/to/font.ttf");
    /// var ui = SfmlIguinaSetup.Create(window, "path/to/IguinaTheme", "path/to/IguinaTheme/system_style.json", font);
    /// </code>
    /// </remarks>
    public static UISystem Create(RenderWindow window, string themePath, string systemStylesheetPath, Font defaultFont)
    {
        var renderer = new SfmlRenderer(window, themePath, defaultFont);
        var input = new SfmlInputProvider(window);
        try
        {
            return new UISystem(systemStylesheetPath, renderer, input);
        }
        catch
        {
            input.Dispose();
            renderer.Dispose();
            throw;
        }
    }
}
