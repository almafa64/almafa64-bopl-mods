using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Steamworks.Data;
using UnityEngine.SceneManagement;

namespace BoplBattleTemplate
{
	[BepInPlugin($"com.almafa64.{PluginInfo.PLUGIN_NAME}", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
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

			harmony.Patch(
				AccessTools.Method(typeof(SteamManager), "OnLobbyEnteredCallback"),
				postfix: new(typeof(Patches), nameof(Patches.OnEnterLobby_Postfix))
			);

			harmony.Patch(
				AccessTools.Method(typeof(GameSession), nameof(GameSession.Init)),
				postfix: new(typeof(Patches), nameof(Patches.GameSessionInit_Postfix))
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
		// called on every level
		internal static void SpawnPlayers_Postfix()
		{
			Plugin.logger.LogMessage("Spawned players");
		}

		// called after each joined player
		internal static void OnEnterLobby_Postfix(Lobby lobby)
		{
			Plugin.logger.LogWarning($"you are {(SteamManager.LocalPlayerIsLobbyOwner ? "" : "not ")}the owner");
		}

		// called at the beginning of first level
		internal static void GameSessionInit_Postfix()
		{
			Plugin.logger.LogWarning($"lobby is {(GameLobby.isOnlineGame ? "" : "not ")}online, and you are {(SteamManager.LocalPlayerIsLobbyOwner ? "" : "not ")}the owner");
		}
	}
}