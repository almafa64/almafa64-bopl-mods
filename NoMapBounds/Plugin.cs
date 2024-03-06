using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BoplFixedMath;
using HarmonyLib;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace NoMapBounds
{
	[BepInPlugin("com.almafa64.NoMapBounds", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
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
		public static bool UpdateSim_Prefix(DestroyIfOutsideSceneBounds __instance)
		{
			if (Traverse.Create(__instance).Field("fixTrans").GetValue<FixTransform>().position.y < SceneBounds.WaterHeight) return true; 
			return false;
		}
	}
}