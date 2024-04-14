using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BoplFixedMath;
using HarmonyLib;

namespace NoAutoShrink
{
	[BepInPlugin($"com.almafa64.{PluginInfo.PLUGIN_NAME}", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	[BepInProcess("BoplBattle.exe")]
	public class Plugin : BaseUnityPlugin
	{
		internal static Harmony harmony;
		internal static ManualLogSource logger;
		internal static ConfigFile config;

		internal static ConfigEntry<int> timeToShrink;

		private void Awake()
		{
			harmony = new(Info.Metadata.GUID);
			logger = Logger;
			config = Config;

			timeToShrink = config.Bind(PluginInfo.PLUGIN_NAME, "time to shrink", (int)Fix.MaxValue, new ConfigDescription("time until platforms shrink back in seconds", new AcceptableValueRange<int>(0, (int)Fix.MaxValue)));

			Constants.timeUntilPlatformsReturnToOriginalSize = (Fix)timeToShrink.Value;
		}
	}
}