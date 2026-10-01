using System;
using BepInEx.Configuration;

namespace InscryptionNoBars
{
    /// <summary>
    /// Live view of the values in <c>BepInEx/config/inscryption.nobars.cfg</c>. Every value is
    /// re-read when the file changes, so edits apply without restarting the game.
    /// </summary>
    internal static class NoBarsSettings
    {
        private const float DefaultMaxExpansion = 1.6f;


        private static ConfigEntry<string> _mode;
        private static ConfigEntry<bool> _patchGbcCameras;
        private static ConfigEntry<float> _maxExpansion;
        private static ConfigEntry<bool> _forceFullRect;
        private static ConfigEntry<bool> _debug;

        internal static FramingMode Mode { get; private set; } = FramingMode.PreserveHorizontal;

        internal static bool PatchGbcCameras { get; private set; } = true;

        internal static bool ForceFullRectOnUnpatchedCameras { get; private set; } = true;

        internal static float MaxExpansion { get; private set; } = DefaultMaxExpansion;

        internal static bool DebugLogging { get; private set; }

        internal static void Bind(ConfigFile config)
        {
            _mode = config.Bind("Framing", "Mode", FramingMode.PreserveHorizontal.ToString(),
                "What to do with the space freed by the removed bars.\n" +
                "  PreserveHorizontal = reveal more of the world vertically so the game's horizontal framing is kept intact (default).\n" +
                "  PreserveVertical   = keep the vertical view and reveal more horizontally instead (classic ultrawide look).");

            _patchGbcCameras = config.Bind("Framing", "PatchGbcCameras", true,
                "Also fill the screen on the 2D Game Boy cameras (the ones with PixelCamera.gbcMode set). " +
                "Turn this off if the GBC scenes end up misaligned.");

            _maxExpansion = config.Bind("Framing", "MaxExpansion", DefaultMaxExpansion,
                "Safety cap on how much the vertical view may be widened. 1.0 disables widening entirely " +
                "(the screen is still filled, but the framing is then cropped horizontally).");

            _forceFullRect = config.Bind("Compatibility", "ForceFullRectOnUnpatchedCameras", true,
                "Safety net: force a full-screen viewport on any camera that still letterboxes and is not handled " +
                "by the PixelCamera patch. Cameras that render to a texture are never touched.");

            _debug = config.Bind("Debug", "Enabled", false,
                "Verbose logging of every framing decision.");

            Refresh();

            // BepInEx 5 exposes no per-entry file-changed event, so the hooks live on the
            // ConfigFile: ConfigReloaded fires when the file on disk changes, SettingChanged when
            // a value is set programmatically or from BepInEx's own settings window.
            config.ConfigReloaded += OnConfigReloaded;
            config.SettingChanged += OnSettingChanged;

            NoBarsPlugin.Log?.LogInfo($"[NoBars] Mode={Mode}, PatchGbcCameras={PatchGbcCameras}, MaxExpansion={MaxExpansion}");
        }

        private static void OnConfigReloaded(object sender, EventArgs e)
        {
            Refresh();
            LogReloaded();
        }

        private static void OnSettingChanged(object sender, SettingChangedEventArgs e)
        {
            Refresh();
            LogReloaded();
        }

        private static void LogReloaded()
        {
            NoBarsPlugin.Log?.LogInfo($"[NoBars] Reloaded settings: Mode={Mode}, PatchGbcCameras={PatchGbcCameras}, MaxExpansion={MaxExpansion}");
        }

        private static void Refresh()
        {
            if (_mode == null) return;

            Mode = ParseMode(_mode.Value);
            PatchGbcCameras = _patchGbcCameras.Value;
            ForceFullRectOnUnpatchedCameras = _forceFullRect.Value;
            DebugLogging = _debug.Value;

            // BepInEx already parses the float out of the config file (and falls back to the
            // default on garbage input), so only the sanity check is left to do here.
            MaxExpansion = _maxExpansion.Value > 0f ? _maxExpansion.Value : DefaultMaxExpansion;
        }

        private static FramingMode ParseMode(string value)
        {
            return string.Equals(value, FramingMode.PreserveVertical.ToString(), StringComparison.OrdinalIgnoreCase)
                ? FramingMode.PreserveVertical
                : FramingMode.PreserveHorizontal;
        }

        internal static void LogError(string source, Exception exception)
        {
            if (DebugLogging)
            {
                NoBarsPlugin.Log?.LogError($"[NoBars] {source} failed: {exception}");
                return;
            }

            NoBarsPlugin.Log?.LogError($"[NoBars] {source} failed: {exception.GetType().Name}: {exception.Message}. " +
                                       "Set Debug.Enabled=true in the config for the full trace.");
        }
    }
}