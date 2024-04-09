using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine.SceneManagement;

namespace WhoseThisTeleport
{
	[BepInPlugin("com.almafa64.WhoseThisTeleport", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
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
				AccessTools.Method(typeof(Teleport), nameof(Teleport.CastAbility)),
				prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.CastTeleport_Prefix))
			);
		}
	}

	class Patches
	{
		public static bool CastTeleport_Prefix(Teleport __instance)
		{
			TeleportIndicator indicator = new Traverse(__instance).Field("teleportIndicator").GetValue<TeleportIndicator>();
			if (indicator == null || indicator.IsDestroyed)
			{
				
			}

			return true;
		}
	}
}