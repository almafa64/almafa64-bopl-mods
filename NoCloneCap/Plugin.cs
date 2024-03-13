using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine.SceneManagement;

namespace NoCloneCap
{
	[BepInPlugin("com.almafa64.NoCloneCap", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	[BepInProcess("BoplBattle.exe")]
	public class Plugin : BaseUnityPlugin
	{
		internal static Harmony harmony;
		internal static ManualLogSource logger;
		internal static ConfigFile config;
		internal static ConfigEntry<int> maxClones;

		private void Awake()
		{
			harmony = new(Info.Metadata.GUID);
			logger = Logger;
			config = Config;

			maxClones = config.Bind("NoCloneCap", "max clone count", int.MaxValue, "original default is 17, new default (and max) is 2147483647");

			harmony.Patch(
				AccessTools.Method(typeof(PlayerCollision), "Awake"),
				postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.PlayerCollisionAwake_Postfix))
			);
		}
	}

	class Patches
	{
		public static void PlayerCollisionAwake_Postfix(PlayerCollision __instance)
		{
			Traverse.Create(__instance).Field<int>("maxAllowedClonesAndBodies").Value = Plugin.maxClones.Value;
		}
	}
}