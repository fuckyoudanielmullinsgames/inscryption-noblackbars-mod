# Changelog

## 0.1.0

- Initial release.
- Postfix on `PixelCamera.LateUpdate` that fills the camera viewport instead of letterboxing it,
  keeping the pixel-perfect pipeline (reference resolution, integer pixel scale, `OnRenderImage`
  downscale, `PixelSnapSprite` grid) owned by the game.
- Orthographic cameras widen `orthographicSize`; perspective cameras widen `fieldOfView` in
  tangent space, with per-camera bookkeeping so `ViewManager`'s absolute FOV tweens keep working
  and runtime mode/window changes converge back to the authored view.
- `FullRectWatchdog` safety net for letterboxed cameras without a `PixelCamera`; cameras
  rendering into a texture are never touched, and `Compatibility:ForceFullRectOnUnpatchedCameras`
  is re-read on every sweep so it can be disabled live.
- The internal render target is realigned with the filled viewport (`Screen / num`, `num` being
  the integer scale the game itself picked). `PixelCamera.OnRenderImage` squeezes the rendered
  viewport into a `renderWidth x renderHeight` temporary sized from `ReferenceHeight`, which
  `ReferenceResolutionSetter` caps at 450-550 rows, so it only covers the letterboxed area.
  Squeezing a full 1920x1200 viewport into 960x548 scales vertically by 0.457 instead of 0.5:
  point filtering drops rows (glyphs and sprite edges looked cut and low quality) and the final
  upscale stretched the picture by 9.5% instead of showing more world. Pixels per world unit on
  screen now stay exactly what the game intends.
- Cameras that render a `ScreenSpaceCamera` canvas (`PerspectiveUICamera`,
  `OrthographicUICamera`) are detected at runtime and skipped: their `CanvasScaler` derives the
  interface scale from `Camera.pixelRect`, so filling their viewport resampled every glyph and made
  the text look blurry and out of scale compared to the unmodded game. They keep the game's own
  layout, and the world camera still fills the freed rows and columns.
- Configurable via `BepInEx/config/inscryption.nobars.cfg` (`Framing:Mode`,
  `Framing:PatchGbcCameras`, `Framing:MaxExpansion`,
  `Compatibility:ForceFullRectOnUnpatchedCameras`, `Debug:Enabled`), re-read live.