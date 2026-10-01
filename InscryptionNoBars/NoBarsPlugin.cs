using BepInEx;
using BepInEx.Logging;

namespace InscryptionNoBars
{
    /// <summary>
    /// BepInEx entry point. Its only job is to hand control to <see cref="PluginEntry"/>,
    /// which holds the actual patch logic.
    /// </summary>
    [BepInPlugin(ModGUID, ModName, ModVer)]
    public class NoBarsPlugin : BaseUnityPlugin
    {
        public const string ModGUID = "inscryption.nobars";
        public const string ModName = "InscryptionNoBars";
        public const string ModVer = "0.1.0";

        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo($"{ModName} {ModVer} starting");
            PluginEntry.Initialize(Config);
        }

        private void OnDestroy()
        {
            PluginEntry.Unpatch();
        }
    }
}