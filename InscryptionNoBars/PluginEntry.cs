using System;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace InscryptionNoBars
{
    /// <summary>
    /// Wires the patches together: settings, Harmony patch, watchdog. Kept separate from the
    /// BepInEx entry point so the startup order stays explicit.
    /// </summary>
    public static class PluginEntry
    {
        private static bool _initialized;
        private static Harmony _harmony;

        /// <summary>
        /// Removes every patch this mod applied. Patches belong to the Harmony instance that
        /// applied them, so the instance created in <see cref="Initialize"/> is kept for this:
        /// <c>UnpatchAll(string)</c> is obsolete (and an error) in HarmonyX 2.x.
        /// </summary>
        internal static void Unpatch()
        {
            _harmony?.UnpatchSelf();
        }

        internal static void Initialize(ConfigFile config)
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                Debug.Log("[NoBars] Loading Inscryption No Bars...");

                NoBarsSettings.Bind(config);

                var harmony = new Harmony(NoBarsPlugin.ModGUID);
                _harmony = harmony;
                bool patched = PixelCameraPatch.Apply(harmony);

                if (!patched)
                {
                    Debug.LogError("[NoBars] Nothing was patched, the black bars will stay.");
                }

                if (NoBarsSettings.ForceFullRectOnUnpatchedCameras)
                {
                    FullRectWatchdog.Install();
                    Debug.Log("[NoBars] Watchdog installed for cameras without a PixelCamera.");
                }

                Debug.Log($"[NoBars] Successfully loaded (window {Screen.width}x{Screen.height}, " +
                          $"mode {NoBarsSettings.Mode}, bars {(patched ? "removed" : "left alone")}).");
            }
            catch (Exception e)
            {
                Debug.LogError("[NoBars] Failed to initialize: " + e.Message);
                Debug.LogException(e);
            }
        }
    }
}