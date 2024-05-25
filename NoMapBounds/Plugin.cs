using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;

namespace NoMapBounds
{
	[BepInPlugin("com.almafa64.NoMapBounds", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	[BepInProcess("BoplBattle.exe")]
	public class Plugin : BaseUnityPlugin
	{
		internal static Harmony harmony;
		internal static ManualLogSource logger;
		internal static ConfigFile config;

		private void Awake()
		{
			harmony = new(Info.Metadata.GUID);
			logger = Logger;
			config = Config;

			harmony.Patch(
				AccessTools.Method(typeof(DestroyIfOutsideSceneBounds), nameof(DestroyIfOutsideSceneBounds.UpdateSim)),
				prefix: new(typeof(Patches), nameof(Patches.UpdateSim_Prefix))
			);
		}
	}

	class Patches
	{
		private static readonly FieldInfo fixTransField = typeof(DestroyIfOutsideSceneBounds).GetField("fixTrans", AccessTools.all);

		// only run original code if position is under water level
		internal static bool UpdateSim_Prefix(DestroyIfOutsideSceneBounds __instance) =>
			(fixTransField.GetValue(__instance) as FixTransform).position.y <= SceneBounds.WaterHeight;
	}
}