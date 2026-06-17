using SFML.Graphics;
using SFML.System;

using Color = Iguina.Defs.Color;
using Point = Iguina.Defs.Point;
using Rectangle = Iguina.Defs.Rectangle;
using SfColor = SFML.Graphics.Color;

namespace Iguina.Drivers.Sfml;

/// <summary>
/// SFML implementation of <see cref="IRenderer"/> for the Iguina UI system.
/// </summary>
/// <remarks>
/// Supports any <see cref="IRenderTarget"/> (e.g., <see cref="RenderWindow"/>, <see cref="RenderTexture"/>).
/// Provides lazy texture/font loading with caching and software-based scissor region clipping.
/// Implements <see cref="IDisposable"/> to release native SFML resources.
/// </remarks>
public sealed class SfmlRenderer : IRenderer, IDisposable
{
    private readonly IRenderTarget _target;
    private readonly string _themePath;
    private readonly Font _defaultFont;
    private readonly Dictionary<string, Texture> _textures = [];
    private readonly Dictionary<string, Font> _fonts = [];
    private readonly Dictionary<string, Func<Color, Color>> _effects = [];

    private Sprite? _sprite;
    private Text? _text;
    private readonly RectangleShape _rectShape = new();
    private Rectangle? _scissorRegion;
    private bool _disposed;

    /// <summary>
    /// Gets the collection of registered effects keyed by identifier string.
    /// </summary>
    /// <remarks>
    /// Each function receives the original tint <see cref="Color"/> and returns the modified color.
    /// Use <see cref="RegisterEffect"/> and <see cref="RemoveEffect"/> to manage effects.
    /// </remarks>
    public IReadOnlyDictionary<string, Func<Color, Color>> Effects => _effects.AsReadOnly();

    /// <summary>
    /// Registers an effect that transforms draw colors before rendering.
    /// </summary>
    /// <param name="identifier">The effect identifier matching the stylesheet's <c>EffectIdentifier</c>.</param>
    /// <param name="effect">A function that receives the original color and returns the modified color.</param>
    public void RegisterEffect(string identifier, Func<Color, Color> effect) => _effects[identifier] = effect;

    /// <summary>
    /// Removes a previously registered effect.
    /// </summary>
    /// <param name="identifier">The effect identifier to remove.</param>
    /// <returns><c>true</c> if the effect was found and removed; otherwise, <c>false</c>.</returns>
    public bool RemoveEffect(string identifier) => _effects.Remove(identifier);

    /// <summary>Creates a new SFML renderer targeting a <see cref="RenderWindow"/>.</summary>
    /// <param name="window">The render window to draw onto.</param>
    /// <param name="themePath">Base directory path for resolving texture and font IDs from stylesheets.</param>
    /// <param name="defaultFont">Fallback font used when a stylesheet does not specify a font ID.</param>
    public SfmlRenderer(RenderWindow window, string themePath, Font defaultFont)
        : this((IRenderTarget)window, themePath, defaultFont)
    {
    }

    /// <summary>Creates a new SFML renderer targeting a <see cref="RenderTexture"/> (offscreen).</summary>
    /// <param name="target">The render texture to draw onto.</param>
    /// <param name="themePath">Base directory path for resolving texture and font IDs from stylesheets.</param>
    /// <param name="defaultFont">Fallback font used when a stylesheet does not specify a font ID.</param>
    public SfmlRenderer(RenderTexture target, string themePath, Font defaultFont)
        : this((IRenderTarget)target, themePath, defaultFont)
    {
    }

    /// <summary>Creates a new SFML renderer targeting any <see cref="IRenderTarget"/>.</summary>
    /// <param name="target">The render target to draw onto.</param>
    /// <param name="themePath">Base directory path for resolving texture and font IDs from stylesheets.</param>
    /// <param name="defaultFont">Fallback font used when a stylesheet does not specify a font ID.</param>
    public SfmlRenderer(IRenderTarget target, string themePath, Font defaultFont)
    {
        _target = target;
        _themePath = themePath;
        _defaultFont = defaultFont;
    }

    /// <inheritdoc/>
    public Rectangle GetScreenBounds()
    {
        Vector2u size = _target.Size;
        return new Rectangle(0, 0, (int)size.X, (int)size.Y);
    }

    /// <inheritdoc/>
    public void DrawTexture(string? effectIdentifier, string textureId, Rectangle destRect, Rectangle sourceRect, Color color)
    {
        Texture? texture = GetTexture(textureId);
        if (texture == null) return;

        color = ApplyEffect(effectIdentifier, color);

        destRect = ApplyScissor(destRect, sourceRect, out sourceRect);
        if (destRect.Width <= 0 || destRect.Height <= 0) return;

        _sprite ??= new Sprite(texture);
        _sprite.Texture = texture;
        _sprite.TextureRect = new IntRect(
            new Vector2i(sourceRect.X, sourceRect.Y),
            new Vector2i(sourceRect.Width, sourceRect.Height));
        _sprite.Position = new Vector2f(destRect.X, destRect.Y);
        _sprite.Scale = new Vector2f(
            (float)destRect.Width / sourceRect.Width,
            (float)destRect.Height / sourceRect.Height);
        _sprite.Color = ToSfColor(color);

        _target.Draw(_sprite);
    }

    /// <inheritdoc/>
    public Point MeasureText(string text, string? fontId, int fontSize, float spacing)
    {
        Font font = GetFont(fontId);
        EnsureText(font);
        _text!.DisplayedString = text;
        _text.CharacterSize = (uint)fontSize;
        _text.LetterSpacing = spacing;

        FloatRect bounds = _text.GetLocalBounds();
        return new Point((int)bounds.Width, (int)bounds.Height);
    }

    /// <inheritdoc/>
    public int GetTextLineHeight(string? fontId, int fontSize)
    {
        Font font = GetFont(fontId);
        EnsureText(font);
        _text!.DisplayedString = "A";
        _text.CharacterSize = (uint)fontSize;

        FloatRect bounds = _text.GetLocalBounds();
        return (int)bounds.Height;
    }

    /// <inheritdoc/>
    public void DrawText(string? effectIdentifier, string text, string? fontId, int fontSize,
        Point position, Color fillColor, Color outlineColor, int outlineWidth, float spacing)
    {
        Font font = GetFont(fontId);
        EnsureText(font);
        _text!.DisplayedString = text;
        _text.CharacterSize = (uint)fontSize;
        _text.LetterSpacing = spacing;
        _text.FillColor = ToSfColor(ApplyEffect(effectIdentifier, fillColor));
        _text.OutlineColor = ToSfColor(ApplyEffect(effectIdentifier, outlineColor));
        _text.OutlineThickness = outlineWidth;
        _text.Position = new Vector2f(position.X, position.Y);

        _target.Draw(_text);
    }

    /// <inheritdoc/>
    public void DrawRectangle(Rectangle rectangle, Color color)
    {
        _rectShape.Size = new Vector2f(rectangle.Width, rectangle.Height);
        _rectShape.Position = new Vector2f(rectangle.X, rectangle.Y);
        _rectShape.FillColor = ToSfColor(color);
        _rectShape.OutlineThickness = 0;

        _target.Draw(_rectShape);
    }

    /// <inheritdoc/>
    public void SetScissorRegion(Rectangle region) => _scissorRegion = region;

    /// <inheritdoc/>
    public Rectangle? GetScissorRegion() => _scissorRegion;

    /// <inheritdoc/>
    public void ClearScissorRegion() => _scissorRegion = null;

    /// <inheritdoc/>
    public Color GetPixelFromTexture(string textureId, Point sourcePosition)
    {
        Texture? texture = GetTexture(textureId);
        if (texture == null) return new Color();

        using Image image = texture.CopyToImage();
        SfColor pixel = image.GetPixel(new Vector2u((uint)sourcePosition.X, (uint)sourcePosition.Y));
        return new Color { R = pixel.R, G = pixel.G, B = pixel.B, A = pixel.A };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Color picker texture scanning is not supported by this driver.
    /// <c>ColorPicker</c> and <c>ColorSlider</c> entities will not function correctly.
    /// </remarks>
    public Point? FindPixelOffsetInTexture(string textureId, Rectangle sourceRect, Color color, bool returnNearestColor) => null;

    /// <summary>
    /// Disposes all cached textures and clears the texture cache.
    /// </summary>
    /// <remarks>
    /// The internal <see cref="Sprite"/> is also disposed and reset to avoid dangling references.
    /// </remarks>
    public void ClearTextureCache()
    {
        foreach (Texture t in _textures.Values) t.Dispose();
        _textures.Clear();
        _sprite?.Dispose();
        _sprite = null;
    }

    /// <summary>
    /// Disposes all cached fonts and clears the font cache.
    /// </summary>
    /// <remarks>
    /// The internal <see cref="Text"/> is also disposed and reset to avoid dangling references.
    /// </remarks>
    public void ClearFontCache()
    {
        foreach (Font f in _fonts.Values) f.Dispose();
        _fonts.Clear();
        _text?.Dispose();
        _text = null;
    }

    /// <summary>
    /// Releases all native SFML resources held by this renderer.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _sprite?.Dispose();
        _text?.Dispose();
        _rectShape.Dispose();

        foreach (Texture t in _textures.Values) t.Dispose();
        _textures.Clear();

        foreach (Font f in _fonts.Values) f.Dispose();
        _fonts.Clear();
    }

    private Color ApplyEffect(string? effectIdentifier, Color color)
    {
        if (effectIdentifier != null && _effects.TryGetValue(effectIdentifier, out Func<Color, Color>? effect))
            return effect(color);
        return color;
    }

    private Rectangle ApplyScissor(Rectangle destRect, Rectangle sourceRect, out Rectangle clippedSource)
    {
        if (destRect.Width <= 0 || destRect.Height <= 0)
        {
            clippedSource = default;
            return default;
        }

        if (_scissorRegion == null)
        {
            clippedSource = sourceRect;
            return destRect;
        }

        Rectangle s = _scissorRegion.Value;
        int left = Math.Max(destRect.X, s.X);
        int top = Math.Max(destRect.Y, s.Y);
        int right = Math.Min(destRect.X + destRect.Width, s.X + s.Width);
        int bottom = Math.Min(destRect.Y + destRect.Height, s.Y + s.Height);

        if (right <= left || bottom <= top)
        {
            clippedSource = default;
            return default;
        }

        float scaleX = (float)sourceRect.Width / destRect.Width;
        float scaleY = (float)sourceRect.Height / destRect.Height;

        clippedSource = new Rectangle(
            sourceRect.X + (int)((left - destRect.X) * scaleX),
            sourceRect.Y + (int)((top - destRect.Y) * scaleY),
            (int)((right - left) * scaleX),
            (int)((bottom - top) * scaleY));

        return new Rectangle(left, top, right - left, bottom - top);
    }

    private Texture? GetTexture(string textureId)
    {
        if (string.IsNullOrEmpty(textureId)) return null;

        if (_textures.TryGetValue(textureId, out Texture? cached))
            return cached;

        string path = Path.Combine(_themePath, textureId.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path)) return null;

        var texture = new Texture(path);
        _textures[textureId] = texture;
        return texture;
    }

    private Font GetFont(string? fontId)
    {
        if (fontId != null && _fonts.TryGetValue(fontId, out Font? cached))
            return cached;

        if (fontId == null)
            return _defaultFont;

        Font font = LoadFont(fontId) ?? _defaultFont;
        _fonts[fontId] = font;
        return font;
    }

    private Font? LoadFont(string fontId)
    {
        string path = Path.Combine(_themePath, fontId.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? new Font(path) : null;
    }

    private void EnsureText(Font font)
    {
        if (_text == null)
            _text = new Text(font, string.Empty);
        else
            _text.Font = font;
    }

    private static SfColor ToSfColor(Color c) =>
        new(c.R, c.G, c.B, c.A);
}
