using System.Collections.Generic;
using UnityEngine;

namespace InscryptionNoBars
{
    /// <summary>
    /// Cameras that render a <c>ScreenSpaceCamera</c> canvas, which must keep the game's own
    /// letterboxed viewport.
    ///
    /// The interface is not drawn by hand: the UI canvases in the game are
    /// <c>ScreenSpaceCamera</c> canvases rendered by <c>PerspectiveUICamera</c> and
    /// <c>OrthographicUICamera</c>, and their <c>CanvasScaler</c> runs in <c>ScaleWithScreenSize</c>
    /// mode with <c>MatchWidthOrHeight = MatchHeight</c> against a 1920x1080 reference. Unity's
    /// <c>CanvasScaler</c> takes its screen size from <c>Camera.pixelRect</c> for those canvases,
    /// so the letterbox is what pins the interface to a fixed pixel size.
    ///
    /// Widening the viewport of such a camera therefore rescales the whole interface by
    /// <c>screenHeight / 1080</c> instead of <c>letterboxedHeight / 1080</c>: every glyph is
    /// resampled at a scale factor the glyph atlas was not authored for, which is exactly the
    /// "text looks blurry and bigger than the original" symptom. Those cameras are therefore
    /// skipped entirely, in every framing mode.
    ///
    /// Skipping them does not bring the bars back. These cameras do not clear colour outside their
    /// viewport, so the band they occupy stays the game's own UI layout while the world camera,
    /// which does fill the screen, remains visible in the freed rows and columns.
    /// </summary>
    internal static class UiCameraCache
    {
        private const float RescanInterval = 1f;

        private static readonly List<Camera> Cameras = new List<Camera>();

        private static float _nextScan;

        /// <summary>
        /// True when <paramref name="camera"/> renders a <c>ScreenSpaceCamera</c> canvas, meaning
        /// its viewport drives the interface scale and has to be left alone.
        /// </summary>
        internal static bool Contains(Camera camera)
        {
            if (camera == null) return false;

            // Scanned once per second, and immediately when the cache is cold (first camera of a
            // new scene, before the next scan is due).
            if (Cameras.Count == 0 || Time.unscaledTime >= _nextScan)
                Rebuild();

            for (int i = 0; i < Cameras.Count; i++)
            {
                if (ReferenceEquals(Cameras[i], camera)) return true;
            }

            return false;
        }

        private static void Rebuild()
        {
            _nextScan = Time.unscaledTime + RescanInterval;
            Cameras.Clear();

            Canvas[] canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null) continue;
                if (canvas.renderMode != RenderMode.ScreenSpaceCamera) continue;

                Camera worldCamera = canvas.worldCamera;
                if (worldCamera != null && !Cameras.Contains(worldCamera))
                    Cameras.Add(worldCamera);
            }
        }
    }
}