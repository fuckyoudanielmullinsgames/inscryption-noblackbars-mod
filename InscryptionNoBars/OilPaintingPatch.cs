using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace InscryptionNoBars
{
    /// <summary>
    /// Gives the oil painting the pixel density of the window instead of the fixed 780x600 it ships
    /// with.
    ///
    /// The painting in the frame is not drawn by the world camera. <c>OilPaintingPuzzle</c> keeps a
    /// serialized <c>paintingRenderCamera</c> switched on while the player faces the frame
    /// (<c>ManagedUpdate</c> -> <c>DoRenderPainting</c>), and that camera is an orthographic lens
    /// with <c>orthographicSize = 4.75</c> and <c>SetCameraAspect(1.3)</c> pointing at the
    /// <c>OilPaintingRender</c> render texture, which <c>FramePainting.mat</c> puts on the frame
    /// mesh. What it sees is a real 3D diorama (mesh frames, lit geometry), which is why it renders
    /// at all and why nothing here is pixel art.
    ///
    /// That texture is authored at 780x600 and nothing in the game ever resizes it, so its density is
    /// frozen: 600 rows over the 9.5 world units the orthographic camera covers is 63 texels per
    /// world unit, whatever the window is. Zoom into the frame - which is how the puzzle is played -
    /// and the whole window is a 600-row texture stretched to <c>Screen.height</c> rows, bilinearly
    /// filtered, which is exactly the "it looks like a lower resolution than the rest of the game"
    /// look. It is the one part of the picture that ignores the game's pixel grid, and it is the one
    /// thing still soft once the bars are gone and the world renders at a clean <c>Screen / num</c>.
    ///
    /// The camera is deliberately left exactly where the game put it. The diorama is geometry, not
    /// sprites, so keeping the authored aspect (780/600 = 1.3, the same number
    /// <c>SetCameraAspect</c> hands the camera) and <c>orthographicSize</c> while adding texels means
    /// the same framing with a sharper picture: nothing moves, nothing is cropped, nothing is
    /// stretched. <c>orthographicSize</c> in particular must not be touched, because for an
    /// orthographic camera it defines the vertical world extent, and the RT width is derived from it
    /// through the aspect.
    ///
    /// The resize runs from a postfix on the game's own <c>ManagedUpdate()</c> so it follows window
    /// resizes and live config changes, and it reallocates only when the size actually changes, so a
    /// steady-state frame costs two integer comparisons.
    /// </summary>
    internal static class OilPaintingPatch
    {
        internal const string TargetTypeName = "OilPaintingPuzzle";
        internal const string TargetMethodName = "ManagedUpdate";
        private const string CameraFieldName = "paintingRenderCamera";

        /// <summary>
        /// The size each render texture was authored at. Read once per texture, on the first frame
        /// it is seen, because after the first resize the texture no longer reports the shipped
        /// numbers and the authored aspect is the only thing that must survive every later resize.
        /// </summary>
        private static readonly ConditionalWeakTable<RenderTexture, AuthoredSize> AuthoredSizes =
            new ConditionalWeakTable<RenderTexture, AuthoredSize>();

        private static FieldInfo _cameraField;

        internal static bool Apply(Harmony harmony)
        {
            Type type = AccessTools.TypeByName(TargetTypeName);
            if (type == null)
            {
                NoBarsPlugin.Log?.LogWarning($"[NoBars] Type '{TargetTypeName}' not found. " +
                                             "The oil painting will keep the game's 780x600 render texture.");
                return false;
            }

            MethodInfo update = AccessTools.Method(type, TargetMethodName, Type.EmptyTypes);
            if (update == null)
            {
                NoBarsPlugin.Log?.LogWarning($"[NoBars] '{TargetTypeName}.{TargetMethodName}()' not found. " +
                                             "The oil painting will keep the game's 780x600 render texture.");
                return false;
            }

            _cameraField = AccessTools.Field(type, CameraFieldName);
            if (_cameraField == null)
            {
                NoBarsPlugin.Log?.LogWarning($"[NoBars] Field '{TargetTypeName}.{CameraFieldName}' not found. " +
                                             "The oil painting will keep the game's 780x600 render texture.");
                return false;
            }

            harmony.Patch(update, postfix: new HarmonyMethod(typeof(OilPaintingPatch), nameof(AfterManagedUpdate)));

            NoBarsPlugin.Log?.LogInfo($"[NoBars] Patched {type.FullName}.{TargetMethodName}()");
            return true;
        }

        private static void AfterManagedUpdate(Component __instance)
        {
            try
            {
                // Re-read every frame, like every other setting, so turning this off or lowering the
                // cap from the config file takes effect without restarting the game.
                if (!NoBarsSettings.PaintingFollowsScreen) return;

                int maxHeight = NoBarsSettings.PaintingMaxHeight;
                if (_cameraField == null || maxHeight <= 0) return;

                Camera camera = _cameraField.GetValue(__instance) as Camera;
                if (camera == null) return;

                RenderTexture target = camera.targetTexture;
                if (target == null) return;

                AuthoredSize authored = AuthoredFor(target);
                if (authored == null) return;

                // A cap below the shipped height would mean "make it worse than the game does", so it
                // is ignored rather than clamped: the picture never gets fewer texels than it ships
                // with.
                if (maxHeight < authored.Height) return;

                // The window height is the final resolution of the picture, so matching it is what
                // makes the frame 1:1 at its largest, which is the only point where the fixed 780x600
                // was actually visible.
                int height = Mathf.Clamp(Screen.height, authored.Height, maxHeight);
                int width = Mathf.RoundToInt(height * authored.Aspect);
                if (width < authored.Width) width = authored.Width;

                if (target.width == width && target.height == height) return;

                Resize(target, authored, width, height);
            }
            catch (Exception e)
            {
                NoBarsSettings.LogError(nameof(AfterManagedUpdate), e);
            }
        }

        /// <summary>
        /// Reallocates the render texture at a new size.
        ///
        /// Release, set the dimensions, create: that is the order Unity expects, because the size of
        /// a live render texture is part of its descriptor and cannot be changed underneath it. The
        /// instance itself is kept, which matters - <c>FramePainting.mat</c> holds a direct reference
        /// to this asset, so replacing the texture would leave the frame showing the old one.
        /// </summary>
        private static void Resize(RenderTexture target, AuthoredSize authored, int width, int height)
        {
            target.Release();
            target.width = width;
            target.height = height;
            target.Create();

            if (NoBarsSettings.DebugLogging)
            {
                NoBarsPlugin.Log?.LogInfo($"[NoBars] Oil painting render texture realigned to " +
                                          $"{width}x{height} for a {Screen.width}x{Screen.height} window " +
                                          $"(was {authored.Width}x{authored.Height}).");
            }
        }

        private static AuthoredSize AuthoredFor(RenderTexture target)
        {
            if (AuthoredSizes.TryGetValue(target, out AuthoredSize authored)) return authored;

            // Nothing sensible can be derived from a texture that has no size yet (a render texture
            // that failed to allocate); leave it alone rather than guessing.
            if (target.width <= 0 || target.height <= 0) return null;

            authored = new AuthoredSize(target.width, target.height);
            AuthoredSizes.Add(target, authored);
            return authored;
        }

        private sealed class AuthoredSize
        {
            internal readonly int Width;
            internal readonly int Height;

            /// <summary>780/600 = 1.3, the aspect <c>SetCameraAspect</c> gives the camera.</summary>
            internal readonly float Aspect;

            internal AuthoredSize(int width, int height)
            {
                Width = width;
                Height = height;
                Aspect = width / (float)height;
            }
        }
    }
}