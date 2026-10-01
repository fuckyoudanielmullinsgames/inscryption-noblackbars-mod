using UnityEngine;

namespace InscryptionNoBars
{
    /// <summary>
    /// Framing math, kept free of any game type so it stays easy to reason about (and to test).
    ///
    /// Inscryption's <c>PixelCamera.LateUpdate</c> letterboxes by shrinking the normalized camera
    /// viewport: it picks a vertical pixel scale, snaps it to an integer number of screen rows and
    /// centres whatever is left over, i.e. the black bars. In a window taller than the 1.75 aspect
    /// ratio <c>ReferenceResolutionSetter</c> enforces, <c>rect.width</c> stays 1 (the full window
    /// width is used) while <c>rect.height</c> drops below 1, so the discarded rows are the bars.
    /// </summary>
    internal static class CameraFraming
    {
        internal const float FullThreshold = 0.9999f;

        /// <summary>
        /// Differences below this are rounding noise from the game's integer pixel scaling (the
        /// even-rounding in PixelCamera can drop a single column), not real bars worth widening
        /// the view for.
        /// </summary>
        internal const float NegligibleExpansion = 1.001f;

        internal static readonly Rect FullRect = new Rect(0f, 0f, 1f, 1f);

        internal static bool IsFullScreen(Rect rect)
        {
            return rect.width >= FullThreshold && rect.height >= FullThreshold;
        }

        /// <summary>
        /// How much taller the view has to become once the viewport is filled, so that the
        /// horizontal world extent stays exactly where the game put it.
        ///
        /// Filling the viewport changes the camera aspect from <c>(width * screenW) / (height *
        /// screenH)</c> to <c>screenW / screenH</c>, and the horizontal extent of a camera is
        /// <c>2 * depth * tan(fov / 2) * aspect</c>. Keeping it identical therefore needs the
        /// vertical extent multiplied by <c>width / height</c>.
        ///
        /// Returns 1.0 when the game already fills the window, and also for windows *wider* than
        /// the game's aspect cap (bars on the sides, <c>width / height &lt; 1</c>): filling those
        /// just reveals more of the scene sideways, which is what ultrawide users expect, so the
        /// projection is left alone instead of being narrowed.
        /// </summary>
        internal static float ExpansionFactor(Rect authored)
        {
            float width = Mathf.Clamp(authored.width, 1e-4f, 1f);
            float height = Mathf.Clamp(authored.height, 1e-4f, 1f);

            float k = width / height;
            return k > NegligibleExpansion ? k : 1f;
        }

        /// <summary>
        /// Vertical FOV that widens the vertical view by <paramref name="k"/> while keeping the
        /// horizontal view (tan(fov/2) * aspect) exactly where it was. Done in tangent space
        /// because that is what the projection actually uses.
        /// </summary>
        internal static float ScaleVerticalFov(float fov, float k)
        {
            if (k <= NegligibleExpansion) return fov;

            float halfTan = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float widened = 2f * Mathf.Atan(halfTan * k) * Mathf.Rad2Deg;

            return Mathf.Clamp(widened, 0.5f, 179f);
        }
    }
}