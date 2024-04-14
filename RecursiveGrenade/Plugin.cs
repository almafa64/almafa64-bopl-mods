using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace RecursiveGrenade
{
	[BepInPlugin("com.almafa64.RecursiveGrenade", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	[BepInProcess("BoplBattle.exe")]
	public class Plugin : BaseUnityPlugin
	{
		internal static Harmony harmony;
		internal static ManualLogSource logger;
		internal static ConfigFile config;

		internal static ConfigEntry<int> recursionCount;

		private void Awake()
		{
			harmony = new(Info.Metadata.GUID);
			logger = Logger;
			config = Config;

			recursionCount = config.Bind("RecursiveGrenade", "recursion count", 1, new ConfigDescription("", new AcceptableValueRange<int>(0, 1000)));

			harmony.Patch(
				AccessTools.Method(typeof(GameSessionHandler), "SpawnPlayers"),
				postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.SpawnPlayers_Postfix))
			);
		}
	}

	class Patches
	{
		public static void SpawnPlayers_Postfix()
		{
			Plugin.logger.LogMessage("Spawned players");
		}
	}
}