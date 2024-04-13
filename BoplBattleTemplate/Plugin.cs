using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine.SceneManagement;

namespace BoplBattleTemplate
{
	[BepInPlugin("com.almafa64.BoplBattleTemplate", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
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
			SceneManager.sceneLoaded += OnSceneLoad;

			Logger.LogMessage($"guid: {Info.Metadata.GUID}, name: {Info.Metadata.Name}, version: {Info.Metadata.Version}");

			harmony.Patch(
				AccessTools.Method(typeof(GameSessionHandler), "SpawnPlayers"),
				postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.SpawnPlayers_Postfix))
			);
		}

		private void OnSceneLoad(Scene scene, LoadSceneMode mode)
		{
			if(scene.name == "MainMenu")
			{

				return;
			}
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