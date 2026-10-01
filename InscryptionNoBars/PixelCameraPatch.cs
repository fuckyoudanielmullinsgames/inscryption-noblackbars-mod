using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace InscryptionNoBars
{
    /// <summary>
    /// Postfix on the game's own <c>PixelCamera.LateUpdate</c>.
    ///
    /// Running *after* the original is what makes this safe: the original keeps owning the whole
    /// pixel-perfect pipeline (reference resolution, integer pixel scale, the point-filtered
    /// downscale in <c>OnRenderImage</c>, the orthographic size that <c>PixelSnapSprite</c> uses as
    /// its pixel grid). The patch only rewrites what it must, using the values the original just
    /// computed, so nothing is duplicated and the pixel snapping keeps working.
    ///
    /// The target type and method are resolved by name so the mod does not need a compile-time
    /// dependency on the game assembly.
    /// </summary>
    internal static class PixelCameraPatch
    {
        internal const string TargetTypeName = "PixelCamera";
        internal const string TargetMethodName = "LateUpdate";

        private static readonly ConditionalWeakTable<Camera, FramingState> States =
            new ConditionalWeakTable<Camera, FramingState>();

        private static FieldInfo _gbcModeField;
        private static FieldInfo _renderWidthField;
        private static FieldInfo _renderHeightField;
        private static FieldInfo _actualWidthField;
        private static FieldInfo _actualHeightField;

        internal static bool Apply(Harmony harmony)
        {
            Type type = AccessTools.TypeByName(TargetTypeName);
            if (type == null)
            {
                NoBarsPlugin.Log?.LogWarning($"[NoBars] Type '{TargetTypeName}' not found. " +
                                             "The black bars cannot be removed and the mod will stay passive.");
                return false;
            }

            MethodInfo lateUpdate = AccessTools.Method(type, TargetMethodName, Type.EmptyTypes);
            if (lateUpdate == null)
            {
                NoBarsPlugin.Log?.LogWarning($"[NoBars] '{TargetTypeName}.{TargetMethodName}()' not found. " +
                                             "The black bars cannot be removed and the mod will stay passive.");
                return false;
            }

            _gbcModeField = AccessTools.Field(type, "gbcMode");
            _renderWidthField = AccessTools.Field(type, "renderWidth");
            _renderHeightField = AccessTools.Field(type, "renderHeight");
            _actualWidthField = AccessTools.Field(type, "actualWidth");
            _actualHeightField = AccessTools.Field(type, "actualHeight");

            harmony.Patch(lateUpdate, postfix: new HarmonyMethod(typeof(PixelCameraPatch), nameof(AfterLateUpdate)));

            NoBarsPlugin.Log?.LogInfo($"[NoBars] Patched {type.FullName}.{TargetMethodName}()");
            return true;
        }

        private static void AfterLateUpdate(Component __instance)
        {
            try
            {
                Camera camera = __instance.GetComponent<Camera>();
                if (camera == null) return;

                // Interface cameras are pinned to the letterboxed viewport by their CanvasScaler:
                // filling it rescales every glyph and every UI element. See UiCameraCache.
                if (UiCameraCache.Contains(camera)) return;

                // The original just letterboxed this camera (if it had to), so its rect is the
                // authoritative record of what the game decided to show.
                Rect authored = camera.rect;

                if (!CameraFraming.IsFullScreen(authored) && !NoBarsSettings.PatchGbcCameras && IsGbcMode(__instance))
                    return;

                float expansion = CameraFraming.ExpansionFactor(authored);
                expansion = Mathf.Min(expansion, NoBarsSettings.MaxExpansion);

                camera.rect = CameraFraming.FullRect;

                if (NoBarsSettings.Mode == FramingMode.PreserveHorizontal)
                    AlignRenderTargetScale(__instance);

                if (camera.orthographic)
                {
                    // PixelCamera assigns orthographicSize from the screen height every frame, so
                    // widening it here is a one-off multiply and cannot compound.
                    if (NoBarsSettings.Mode == FramingMode.PreserveHorizontal && expansion > 1f)
                        camera.orthographicSize *= expansion;
                    return;
                }

                ApplyFov(camera, expansion);
            }
            catch (Exception e)
            {
                NoBarsSettings.LogError(nameof(AfterLateUpdate), e);
            }
        }

        /// <summary>
        /// Keeps the game's internal render target on an integer scale of the filled viewport.
        ///
        /// The camera renders into its viewport, then <c>PixelCamera.OnRenderImage</c> squeezes that
        /// into a <c>renderWidth x renderHeight</c> temporary with point filtering and blits it back
        /// onto the viewport. Both of those dimensions are derived from <c>ReferenceHeight</c>, which
        /// <c>ReferenceResolutionSetter</c> keeps between 450 and 550 rows, so the temporary only ever
        /// covers the letterboxed area: at 1920x1200 it is 960x548 for a 1920x1096 viewport, an exact
        /// 2x both ways.
        ///
        /// Filling the viewport makes the source 1920x1200, and squeezing that into 960x548 scales
        /// vertically by 0.457 instead of 0.5. Nearest-neighbour resampling at a non-integer factor
        /// is what eats rows of glyphs and sprite edges ("the letters look cut and low quality"), and
        /// the final upscale to 1200 rows then stretches the picture by 9.5% instead of revealing more
        /// of the world.
        ///
        /// Raising the temporary to <c>Screen / num</c>, with <c>num</c> the integer scale the game
        /// itself picked, restores a clean integer upscale in both directions (rounded to the
        /// nearest row, so what is left is a fraction of a pixel: 3.002x at 2560x1600, exactly 2x
        /// at 1920x1200). The rows that the letterbox used to throw away now carry new world instead
        /// of a stretched copy of it, and the projection compensation below is unaffected because
        /// the source keeps the filled viewport's aspect. The fields are rewritten every frame, so
        /// the game just recomputes them from <c>ReferenceHeight</c> on the next <c>LateUpdate</c>
        /// and nothing can compound.
        ///
        /// Only used for <see cref="FramingMode.PreserveHorizontal"/>. In
        /// <see cref="FramingMode.PreserveVertical"/> the vertical view is kept exactly as the game
        /// authored it, which means 548 rows of world stretched across 1200 viewport rows: that
        /// upscale is 2.19x however the buffer is sized, so there is nothing to realign and the mode
        /// trades the softness for not revealing extra world.
        /// </summary>
        private static void AlignRenderTargetScale(Component instance)
        {
            if (_renderHeightField == null || _renderWidthField == null) return;
            if (_actualHeightField == null || _actualWidthField == null) return;

            try
            {
                int renderHeight = Convert.ToInt32(_renderHeightField.GetValue(instance));
                int renderWidth = Convert.ToInt32(_renderWidthField.GetValue(instance));
                if (renderHeight <= 0 || renderWidth <= 0) return;

                int scale = Mathf.Max(1, Screen.height / renderHeight);
                int height = Mathf.RoundToInt(Screen.height / (float)scale);

                // Already exact: nothing letterboxed (1920x1080), a Game Boy camera, or handheld
                // mode all land here, because their temporary already matches the viewport.
                if (height <= renderHeight) return;

                int width = Mathf.RoundToInt(Screen.width / (float)scale);

                _renderHeightField.SetValue(instance, height);
                _renderWidthField.SetValue(instance, width);
                _actualHeightField.SetValue(instance, height * scale);
                _actualWidthField.SetValue(instance, width * scale);

                if (NoBarsSettings.DebugLogging)
                {
                    NoBarsPlugin.Log?.LogInfo($"[NoBars] '{instance.name}' render target realigned to " +
                                              $"{width}x{height} for a clean {scale}x upscale (was {renderWidth}x{renderHeight}).");
                }
            }
            catch (Exception e)
            {
                NoBarsSettings.LogError(nameof(AlignRenderTargetScale), e);
            }
        }

        /// <summary>
        /// Widens the vertical FOV of a perspective camera.
        ///
        /// <c>ViewManager</c> tweens <c>Camera.fieldOfView</c> to absolute values for every camera
        /// view (60 for the default table view, 50 for the board view, ...), so a naive
        /// "multiply every frame" would compound into a runaway zoom. Instead the last value we
        /// wrote is remembered: if the camera still holds it, the game did not touch the FOV this
        /// frame and the remembered base stays valid; if it holds something else, the game has just
        /// authored a new base value and we adopt it.
        /// </summary>
        private static void ApplyFov(Camera camera, float expansion)
        {
            FramingState state = StateFor(camera);
            float current = camera.fieldOfView;
            bool untouchedByUs = state.AppliedFov > 0f && Mathf.Abs(current - state.AppliedFov) <= 0.001f;

            if (expansion <= 1f || NoBarsSettings.Mode != FramingMode.PreserveHorizontal)
            {
                // Nothing to widen (or widening is not wanted): hand the game's own FOV back if we
                // are the ones currently holding it scaled, so changing window size or mode at
                // runtime always converges to the authored view.
                if (untouchedByUs)
                    camera.fieldOfView = state.BaseFov;

                state.AppliedFov = 0f;
                return;
            }

            float baseFov = untouchedByUs ? state.BaseFov : current;
            float widened = CameraFraming.ScaleVerticalFov(baseFov, expansion);

            camera.fieldOfView = widened;

            state.BaseFov = baseFov;
            state.AppliedFov = widened;
        }

        private static FramingState StateFor(Camera camera)
        {
            if (States.TryGetValue(camera, out FramingState state)) return state;

            state = new FramingState();
            States.Add(camera, state);
            return state;
        }

        internal static bool IsGbcMode(Component instance)
        {
            if (_gbcModeField == null) return false;

            try
            {
                return _gbcModeField.GetValue(instance) is bool gbc && gbc;
            }
            catch (Exception e)
            {
                NoBarsSettings.LogError(nameof(IsGbcMode), e);
                return false;
            }
        }

        private sealed class FramingState
        {
            internal float BaseFov;
            internal float AppliedFov;
        }
    }
}