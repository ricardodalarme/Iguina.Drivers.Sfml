# Iguina.Drivers.Sfml

[![NuGet](https://img.shields.io/nuget/v/Iguina.Drivers.Sfml.svg)](https://www.nuget.org/packages/Iguina.Drivers.Sfml/)
[![Build](https://img.shields.io/github/actions/workflow/status/ricardodalarme/Iguina.Drivers.Sfml/build.yml?branch=main)](https://github.com/ricardodalarme/Iguina.Drivers.Sfml/actions)

SFML rendering and input drivers for the [Iguina](https://github.com/RonenNess/Iguina) UI system.

## Quick start

```csharp
using SFML.Graphics;
using SFML.Window;
using Iguina.Drivers.Sfml;

var window = new RenderWindow(new VideoMode(800, 600), "My Game");
var font = new Font("path/to/font.ttf");
var ui = SfmlIguinaSetup.Create(window, "path/to/IguinaTheme", "path/to/IguinaTheme/system_style.json", font);

var clock = new Clock();
while (window.IsOpen)
{
    window.DispatchEvents();
    var dt = clock.Restart().AsSeconds();
    
    window.Clear();
    ui.Update(dt);
    ui.Draw();
    window.Display();
}
```

## Manual setup

For more control, use the drivers directly:

```csharp
var renderer = new SfmlRenderer(window, themePath, defaultFont);
var input = new SfmlInputProvider(window);
var ui = new UISystem(Path.Combine(themePath, "system_style.json"), renderer, input);
```

## Offscreen rendering

The renderer also works with `RenderTexture`:

```csharp
var target = new RenderTexture(new Vector2u(800, 600));
var renderer = new SfmlRenderer(target, themePath, defaultFont);
```

## Features

- Lazy texture and font loading with caching
- Software scissor region clipping for scrollable panels
- Extensible effect pipeline via <c>RegisterEffect</c>/<c>RemoveEffect</c>
- Keyboard repeat delay/rate handling for text input commands
- Mouse wheel and text input event handling
- Works with `RenderWindow` and `RenderTexture`

## Requirements

- .NET 9.0 or .NET 10.0
- Iguina >= 1.1.4
- SFML.Graphics, SFML.Window, SFML.System >= 3.0.0
