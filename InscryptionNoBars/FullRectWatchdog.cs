using UnityEngine;
using UnityEngine.SceneManagement;

namespace InscryptionNoBars
{
    /// <summary>
    /// Safety net for letterboxed cameras the <see cref="PixelCameraPatch"/> does not own.
    ///
    /// Every letterboxed camera shipped in the game has a <c>PixelCamera</c> and is therefore
    /// handled by the Harmony postfix, but cameras also show up at runtime (scenes spawned on the
    /// fly, other mods, the <c>PixelCamera</c> component being disabled). This sweeps twice a
    /// second and on every scene change, forcing a full viewport on anything still letterboxed.
    ///
    /// Cameras that render into a texture are left alone: their viewport is an authoring choice
    /// (card snapshots, in-world TV screens, scanner screens, ...), not screen letterboxing.
    /// </summary>
    public sealed class FullRectWatchdog : MonoBehaviour
    {
        private const float ScanInterval = 0.5f;

        private float _nextScan;

        internal static void Install()
        {
            GameObject host = new GameObject("InscryptionNoBars.Watchdog");
            DontDestroyOnLoad(host);
            host.AddComponent<FullRectWatchdog>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            SceneManager.activeSceneChanged += OnSceneChanged;
            Scan(logFixes: false);
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextScan)
                Scan(logFixes: false);
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
        }

        private void OnSceneChanged(Scene previous, Scene current)
        {
            Scan(logFixes: true);
        }

        private void Scan(bool logFixes)
        {
            _nextScan = Time.unscaledTime + ScanInterval;

            // Re-read every scan so disabling the safety net from the config takes effect without a
            // restart, like every other setting.
            if (!NoBarsSettings.ForceFullRectOnUnpatchedCameras) return;

            Camera[] cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null || camera.targetTexture != null) continue;

                // An interface camera's viewport drives the CanvasScaler, so widening it would
                // rescale the whole UI. It already has a PixelCamera, so the postfix owns it
                // anyway; this only documents the invariant for cameras spawned without one.
                if (UiCameraCache.Contains(camera)) continue;

                Rect authored = camera.rect;
                if (CameraFraming.IsFullScreen(authored)) continue;

                // A live PixelCamera owns this camera: the postfix already runs for it every frame.
                if (HasPixelCamera(camera, requireEnabled: true)) continue;

                // Honour the opt-out of the 2D Game Boy cameras. A disabled PixelCamera means
                // nobody is patching them, so the opt-out has to be enforced here as well.
                if (!NoBarsSettings.PatchGbcCameras && HasPixelCamera(camera, requireEnabled: false))
                {
                    MonoBehaviour pixelCamera = FindPixelCamera(camera, requireEnabled: false);
                    if (pixelCamera != null && PixelCameraPatch.IsGbcMode(pixelCamera)) continue;
                }

                camera.rect = CameraFraming.FullRect;

                if (logFixes || NoBarsSettings.DebugLogging)
                {
                    NoBarsPlugin.Log?.LogInfo(
                        $"[NoBars] Filled letterboxed camera '{camera.name}' " +
                        $"(was {authored.width:F3}x{authored.height:F3}).");
                }
            }
        }

        private static bool HasPixelCamera(Camera camera, bool requireEnabled)
        {
            return FindPixelCamera(camera, requireEnabled) != null;
        }

        private static MonoBehaviour FindPixelCamera(Camera camera, bool requireEnabled)
        {
            MonoBehaviour[] behaviours = camera.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null) continue;
                if (requireEnabled && !behaviour.enabled) continue;
                if (behaviour.GetType().Name == PixelCameraPatch.TargetTypeName) return behaviour;
            }

            return null;
        }
    }
}