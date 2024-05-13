using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Steamworks.Data;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BoplUtils
{
	[BepInPlugin($"com.almafa64.{PluginInfo.PLUGIN_NAME}", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	[BepInProcess("BoplBattle.exe")]
	public class Plugin : BaseUnityPlugin
	{
		internal static Harmony harmony;
		internal static ManualLogSource logger;
		internal static ConfigFile config;

		public static Dictionary<string, string[]> json;
		public static string yaml;

		private void Awake()
		{
			harmony = new(Info.Metadata.GUID);
			logger = Logger;
			config = Config;
			SceneManager.sceneLoaded += OnSceneLoad;

			/*json = JsonSerializer.Deserialize<Dictionary<string, string[]>>(@"{""test"":[""1"",""2"",""3""],""test2"":[""apple"",""banana"",""cock""]}");
			foreach(KeyValuePair<string, string[]> kvp in json)
			{
				logger.LogWarning(kvp.Key + ":\n\t- " + string.Join("\n\t- ", kvp.Value));
			}*/
			
			/*var node = JsonNode.Parse(@"{""test"":[""1"",""2"",""3""],""test2"":[""apple"",""banana"",""cock""]}");
			node.AsObject().Do(kvp =>
			{
				logger.LogWarning(kvp.Key + ":\n\t- " + string.Join("\n\t- ", kvp.Value.AsArray()));
			});*/

			/*var serializer = new YamlDotNet.Serialization.SerializerBuilder()
				.WithNamingConvention(YamlDotNet.Serialization.NamingConventions.UnderscoredNamingConvention.Instance)
				.Build();
			yaml = serializer.Serialize(new Dictionary<string, string[]>
			{
				{ "test", ["1", "2", "3"] },
				{ "test2", ["apple", "banana", "cock"] },
			});
			logger.LogWarning(yaml);*/

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
		public static void SpawnPlayers_Postfix()
		{
			
		}

		public static void OnEnterLobby_Postfix(Lobby lobby)
		{
			
		}

		public static void GameSessionInit_Postfix()
		{
			
		}
	}
}