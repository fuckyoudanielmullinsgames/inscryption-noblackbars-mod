# Inscryption NoBars

Removes the vertical black bars Inscryption draws whenever the window is taller than the aspect
ratio the game is willing to render, so the game uses the whole screen instead of boxing a 1.75:1
picture in the middle of the window.

Works on every window shape: maximized windows that are not a clean multiple of the game's pixel
scale, 16:10, 4:3, and any custom resolution.

## How it works

The bars come from one script, `PixelCamera.LateUpdate()`. Every frame it picks a vertical pixel
scale, snaps it to a whole number of screen rows, and then shrinks the camera's normalized
viewport to whatever rows were left over, centring it:

```csharp
rect.height = (float)actualHeight / (float)Screen.height;   // <- the bars
rect.y = (1f - rect.height) / 2f;
cam.rect = rect;
```

`ReferenceResolutionSetter` is what makes that happen on most machines: it caps the camera's
reference height so the aspect ratio never drops below 1.75, which on a taller window leaves rows
that cannot be covered by an integer pixel scale.

The mod postfixes `PixelCamera.LateUpdate()` instead of disabling it, so the game keeps owning
everything that matters (reference resolution, integer pixel scale, the point-filtered downscale
in `OnRenderImage`, and the orthographic size that `PixelSnapSprite` uses as its pixel grid). The
patch only rewrites the viewport rect to full screen and compensates the projection:

| Camera | What the patch does |
| --- | --- |
| Orthographic | `orthographicSize *= width/height` (safe: the game assigns it absolutely every frame) |
| Perspective | `fieldOfView` is scaled by `width/height` in tangent space, and handed back untouched whenever the game itself writes a new FOV (`ViewManager` tweens it for every camera view) |
| Interface (`ScreenSpaceCamera` canvas) | Nothing. See [Why the interface is left alone](#why-the-interface-is-left-alone) |

Filling the viewport changes the camera aspect from `(width * screenW) / (height * screenH)` to
`screenW / screenH`. Since a camera's horizontal extent is `2 * depth * tan(fov / 2) * aspect`,
keeping it identical means scaling the vertical extent by `rect.width / rect.height` - so the extra
rows reveal more of the world instead of cropping the sides. Nothing is stretched, nothing is cut
off, and the card/combat compositions stay where they were designed to be. At resolutions that
already fit (1920x1080, 2560x1440, ...) the patch is a no-op.

### The render target has to follow

The projection is only half of it, because `OnRenderImage` does not blit the camera image 1:1: it
squeezes the rendered viewport into a `renderWidth x renderHeight` temporary with point filtering
and blits that onto the viewport, and that is where the pixel-art downscale happens. Both
dimensions come from `ReferenceHeight`, which `ReferenceResolutionSetter` keeps between 450 and 550
rows, so the temporary only ever covers the letterboxed area: 960x548 for a 1920x1096 viewport, an
exact 2x in both directions.

Filling the viewport while leaving that temporary alone would squeeze 1920x1200 into 960x548, a
0.457 vertical scale instead of 0.5. Point filtering resolves a non-integer factor by dropping
rows, so glyphs and sprite edges get chewed up ("the letters look cut and low quality"), and the
final upscale to 1200 rows then stretches the picture by 9.5% instead of showing more world.

The patch therefore also raises the temporary to `Screen / num`, where `num` is the integer scale
the game itself picked (2 at 1920x1200), restoring a clean integer upscale in both directions
(rounded to the nearest row: exactly 2x at 1920x1200, 3.0019x at 2560x1600). The pixels per world
unit on screen stay exactly what the game intends, so nothing is magnified: the freed rows carry
new world. The fields are rewritten every frame, so the game recomputes them from `ReferenceHeight`
on the next `LateUpdate` and nothing compounds.

## Why the interface is left alone

The UI is not drawn by the world camera: the menus, dialogue and card interface are
`ScreenSpaceCamera` canvases rendered by `PerspectiveUICamera` and `OrthographicUICamera`. Their
`CanvasScaler` runs in `ScaleWithScreenSize` mode with `MatchWidthOrHeight = MatchHeight` against a
1920x1080 reference, and Unity's `CanvasScaler` reads its screen size from `Camera.pixelRect` for
those canvases. The letterbox is therefore what pins the interface to a fixed pixel size.

Filling a UI camera's viewport changes its pixel height, so the canvas scale factor becomes
`screenHeight / 1080` instead of `letterboxedHeight / 1080`: the whole interface is resampled
(about 9% at 1920x1200) and the text ends up softer and larger than in the unmodded game.

So cameras that render a `ScreenSpaceCamera` canvas are detected at runtime (a one-second scan for
canvases whose `worldCamera` is the camera in question) and are skipped entirely, in every framing
mode. Their letterboxed area does not turn into bars: those cameras do not clear colour outside
their viewport, so the interface keeps the game's exact layout while the world camera, which does
fill the screen, stays visible in the freed rows and columns.

## Configuration

`BepInEx/config/inscryption.nobars.cfg`, created on first launch and re-read live (no restart).

| Key | Default | Meaning |
| --- | --- | --- |
| `Framing:Mode` | `PreserveHorizontal` | `PreserveHorizontal` widens the vertical view and keeps the 16:9 horizontal framing, with the render target realigned so nothing is resampled. `PreserveVertical` keeps the vertical view and widens horizontally (classic ultrawide; can leave empty space at the sides of the 2D/GBC scenes, and the picture is softer because the render target stays at the letterboxed resolution). |
| `Framing:PatchGbcCameras` | `true` | Also fill the screen on the 2D Game Boy cameras. Turn off if a GBC scene looks misaligned. |
| `Framing:MaxExpansion` | `1.6` | Cap on how much the vertical view may be widened. `1.0` fills the screen without widening (framing is then cropped horizontally). |
| `Compatibility:ForceFullRectOnUnpatchedCameras` | `true` | Sweeps twice a second and on scene loads for any camera that still letterboxes without a `PixelCamera`. Cameras rendering into a texture are never touched. |
| `Debug:Enabled` | `false` | Verbose logging. |

## Notes

* Rendering at full screen means the post-processing buffers (`pixelWidth`/`pixelHeight` based)
  grow with the window instead of with the letterboxed area, so VRAM use rises a little on 4K.
* The world is now rendered with more rows than the game would use (600 instead of 548 at
  1920x1200), because the render target has to cover the filled viewport. That is ~9% more fill
  rate at 16:10, and more at 4:3 (1280x1024 renders at full resolution instead of 550 rows). In
  exchange the pixels stay square and the text and sprites stay as sharp as the game intends.
* With `Framing:Mode = PreserveVertical` and 2D scenes authored to fill the frame, the extra space
  at the sides can be empty. Use `PreserveHorizontal` if that happens.
* On ultrawide windows the game letterboxes on the sides instead (it never renders wider than
  1.75:1). Those are filled by simply revealing more of the scene sideways, which is the classic
  ultrawide behaviour; the projection is deliberately left alone there, because narrowing it would
  stretch the picture.
* Works alongside other mods: if something disables `PixelCamera`, the watchdog still removes the
  bars (framing compensation is then skipped, since nothing is running the game's pixel math).

## Installation

1. Install BepInEx 5.4.1902 (BepInExPack_Inscryption) if you have not already.
2. Extract `InscryptionNoBars.zip` into a folder named `InscryptionNoBars` inside `BepInEx/plugins/`,
   giving `BepInEx/plugins/InscryptionNoBars/manifest.json`. BepInEx reads the manifest of that
   folder and loads the assembly sitting next to it.

## Build

```
dotnet build InscryptionNoBars.sln -c Release
```

The build produces a single file, the installable pack:

```
InscryptionNoBars/bin/Release/InscryptionNoBars.zip
├── manifest.json
├── icon.png
└── InscryptionNoBars.dll
```

Nothing else is left in `bin/Release`; the assembly is compiled into `obj/` and packed from there.

Targets Inscryption 1.9.0 (built against `Inscryption.GameLibs` 1.9.0-r.0). The patch resolves
the game type by name, so it degrades to a warning instead of failing if a future update changes
the script.