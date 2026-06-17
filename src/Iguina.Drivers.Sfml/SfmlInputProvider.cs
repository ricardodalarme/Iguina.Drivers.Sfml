using System.Diagnostics;

using SFML.System;
using SFML.Window;

using Point = Iguina.Defs.Point;

namespace Iguina.Drivers.Sfml;

/// <summary>
/// SFML implementation of <see cref="IInputProvider"/> for the Iguina UI system.
/// </summary>
/// <remarks>
/// Queries SFML mouse and keyboard state directly.
/// Handles keyboard repeat delay/rate for text command keys (Backspace, Delete, arrows).
/// Subscribes to the window's <c>TextEntered</c> and <c>MouseWheelScrolled</c> events.
/// Implements <see cref="IDisposable"/> to unsubscribe from window events.
/// </remarks>
public sealed class SfmlInputProvider : IInputProvider, IDisposable
{
    /// <summary>Default repeat delay in seconds before held keys begin repeating.</summary>
    public const float DefaultRepeatDelay = 0.5f;

    /// <summary>Default repeat rate in seconds between subsequent repeats.</summary>
    public const float DefaultRepeatRate = 0.05f;

    private static readonly (Keyboard.Key Key, TextInputCommands Command)[] CommandKeys =
    [
        (Keyboard.Key.Backspace, TextInputCommands.Backspace),
        (Keyboard.Key.Delete, TextInputCommands.Delete),
        (Keyboard.Key.Enter, TextInputCommands.BreakLine),
        (Keyboard.Key.Left, TextInputCommands.MoveCaretLeft),
        (Keyboard.Key.Right, TextInputCommands.MoveCaretRight),
        (Keyboard.Key.Up, TextInputCommands.MoveCaretUp),
        (Keyboard.Key.Down, TextInputCommands.MoveCaretDown),
        (Keyboard.Key.Home, TextInputCommands.MoveCaretStartOfLine),
        (Keyboard.Key.End, TextInputCommands.MoveCaretEndOfLine),
    ];

    private readonly Window _window;
    private readonly List<int> _textInput = [];
    private readonly Dictionary<Keyboard.Key, KeyRepeatState> _heldKeys = [];
    private readonly float _repeatDelay;
    private readonly float _repeatRate;
    private int _mouseWheelDelta;
    private bool _disposed;

    private readonly struct KeyRepeatState
    {
        public readonly long PressedAt;
        public readonly long LastRepeatAt;

        public KeyRepeatState(long pressedAt, long lastRepeatAt)
        {
            PressedAt = pressedAt;
            LastRepeatAt = lastRepeatAt;
        }

        public KeyRepeatState WithLastRepeat(long lastRepeatAt) =>
            new(PressedAt, lastRepeatAt);
    }

    /// <summary>Creates a new SFML input provider with default repeat settings.</summary>
    /// <param name="window">The SFML window used for mouse coordinate transformation.</param>
    public SfmlInputProvider(Window window)
        : this(window, DefaultRepeatDelay, DefaultRepeatRate)
    {
    }

    /// <summary>Creates a new SFML input provider with custom repeat settings.</summary>
    /// <param name="window">The SFML window used for mouse coordinate transformation.</param>
    /// <param name="repeatDelaySec">Initial delay in seconds before a held key begins repeating.</param>
    /// <param name="repeatRateSec">Interval in seconds between subsequent repeats.</param>
    public SfmlInputProvider(Window window, float repeatDelaySec, float repeatRateSec)
    {
        ArgumentNullException.ThrowIfNull(window);

        _window = window;
        _repeatDelay = repeatDelaySec;
        _repeatRate = repeatRateSec;
        window.TextEntered += OnTextEntered;
        window.MouseWheelScrolled += OnMouseWheelScrolled;
    }

    /// <inheritdoc/>
    public Point GetMousePosition()
    {
        Vector2i pos = Mouse.GetPosition(_window);
        return new Point(pos.X, pos.Y);
    }

    /// <inheritdoc/>
    public bool IsMouseButtonDown(MouseButton btn)
    {
        Mouse.Button sfmlBtn = btn switch
        {
            MouseButton.Left => Mouse.Button.Left,
            MouseButton.Right => Mouse.Button.Right,
            MouseButton.Wheel => Mouse.Button.Middle,
            _ => Mouse.Button.Left
        };
        return Mouse.IsButtonPressed(sfmlBtn);
    }

    /// <inheritdoc/>
    public int GetMouseWheelChange()
    {
        int delta = _mouseWheelDelta;
        _mouseWheelDelta = 0;
        return delta;
    }

    /// <inheritdoc/>
    public int[] GetTextInput()
    {
        int[] result = _textInput.ToArray();
        _textInput.Clear();
        return result;
    }

    /// <inheritdoc/>
    public TextInputCommands[] GetTextInputCommands()
    {
        var commands = new List<TextInputCommands>(CommandKeys.Length);
        long now = GetTimestamp();

        foreach ((Keyboard.Key key, TextInputCommands cmd) in CommandKeys)
        {
            if (!Keyboard.IsKeyPressed(key))
            {
                _heldKeys.Remove(key);
                continue;
            }

            if (!_heldKeys.TryGetValue(key, out KeyRepeatState state))
            {
                _heldKeys[key] = new KeyRepeatState(now, now);
                commands.Add(cmd);
                continue;
            }

            double elapsedSincePress = TicksToSeconds(now - state.PressedAt);
            if (elapsedSincePress < _repeatDelay)
                continue;

            double elapsedSinceRepeat = TicksToSeconds(now - state.LastRepeatAt);
            if (elapsedSinceRepeat >= _repeatRate)
            {
                _heldKeys[key] = state.WithLastRepeat(now);
                commands.Add(cmd);
            }
        }

        return commands.ToArray();
    }

    /// <inheritdoc/>
    public KeyboardInteractions? GetKeyboardInteraction()
    {
        if (Keyboard.IsKeyPressed(Keyboard.Key.Tab) ||
            Keyboard.IsKeyPressed(Keyboard.Key.Enter) ||
            Keyboard.IsKeyPressed(Keyboard.Key.Space))
            return KeyboardInteractions.Select;
        if (Keyboard.IsKeyPressed(Keyboard.Key.Up))
            return KeyboardInteractions.MoveUp;
        if (Keyboard.IsKeyPressed(Keyboard.Key.Down))
            return KeyboardInteractions.MoveDown;
        if (Keyboard.IsKeyPressed(Keyboard.Key.Left))
            return KeyboardInteractions.MoveLeft;
        if (Keyboard.IsKeyPressed(Keyboard.Key.Right))
            return KeyboardInteractions.MoveRight;

        return null;
    }

    /// <summary>
    /// Releases all event subscriptions and associated resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _window.TextEntered -= OnTextEntered;
        _window.MouseWheelScrolled -= OnMouseWheelScrolled;
    }

    private void OnTextEntered(object? sender, TextEventArgs e)
    {
        if (e.Unicode.Length > 0 && e.Unicode[0] >= 32)
            _textInput.Add(e.Unicode[0]);
    }

    private void OnMouseWheelScrolled(object? sender, MouseWheelScrollEventArgs e) => _mouseWheelDelta += (int)e.Delta;

    private static long GetTimestamp() => Stopwatch.GetTimestamp();

    private static double TicksToSeconds(long ticks) =>
        (double)ticks / Stopwatch.Frequency;
}
