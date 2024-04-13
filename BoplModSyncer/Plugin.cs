using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Steamworks.Data;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BoplModSyncer
{
	[BepInPlugin("com.almafa64.BoplModSyncer", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	[BepInProcess("BoplBattle.exe")]
	public class Plugin : BaseUnityPlugin
	{
		public static string CHECKSUM { get => _checksum ?? throw new("CHECKSUM hasn't been calculated"); }
		public static readonly string MOD_LIST_API = "https://api.github.com/repos/ShAdowDev16/BoplMods/contents/AllAvailableMods.txt";
		public static readonly string MOD_LIST = "https://raw.githubusercontent.com/ShAdowDev16/BoplMods/main/AllAvailableMods.txt";

		internal static Harmony harmony;
		internal static ManualLogSource logger;
		internal static string _checksum;
		internal static readonly Dictionary<string, Mod> _mods = [];

		public static ReadOnlyDictionary<string, Mod> mods = new(_mods);

		private TextMeshProUGUI checksumText;

		private void Awake()
		{
			harmony = new(Info.Metadata.GUID);
			logger = Logger;

			SceneManager.sceneLoaded += OnSceneLoaded;

			harmony.Patch(
				AccessTools.Method(typeof(SteamManager), "OnLobbyEnteredCallback"),
				postfix: new(typeof(Patches), nameof(Patches.OnEnterLobby_Postfix))
			);

			harmony.Patch(
				AccessTools.Method(typeof(GameSession), nameof(GameSession.Init)),
				postfix: new(typeof(Patches), nameof(Patches.GameSessionInit_Postfix))
			);
		}

		private void Start()
		{
			// Get all released mod links
			WebClient wc = new();
			string modList = wc.DownloadString(MOD_LIST);
			string[] modLines = modList.TrimEnd().Split('\n');

			Dictionary<string, Mod> officalMods = [];

			foreach (string modline in modLines)
			{
				string[] datas = modline.Replace("\"", "").Split(',');
				for (int i = 0; i < datas.Length; i++)
				{
					datas[i] = datas[i].Trim();
				}
				Mod mod = new(datas[2]);
				officalMods.Add(datas[2].Split('/').Last(), mod);
			}

			// Get all downloaded mods (and add link if it's released)
			List<string> hashes = [];
			foreach (BepInEx.PluginInfo plugin in Chainloader.PluginInfos.Values)
			{
				string hash = Utils.ChecksumFile(plugin.Location);
				hashes.Add(hash);

				officalMods.TryGetValue(plugin.Location.Split(Path.DirectorySeparatorChar).Last(), out Mod mod);
				mod.Plugin = plugin;
				mod.Hash = hash;

				_mods.Add(plugin.Metadata.GUID, mod);
			}

			SetChecksumText(Utils.CombineHashes(hashes));
		}

		private void SetChecksumText(string text)
		{
			_checksum = text;
			checksumText.text = CHECKSUM;
			checksumText.fontSize -= 5;
			Vector3 pos = Camera.main.WorldToScreenPoint(checksumText.transform.position);
			pos.y = 10;
			checksumText.transform.position = Camera.main.ScreenToWorldPoint(pos);
		}

		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			if (scene.name != "MainMenu") return;
			Transform canvas = GameObject.Find("Canvas (1)").transform;
			GameObject exitText = GameObject.Find("ExitText");
			GameObject hashObj = Instantiate(exitText, canvas);
			Destroy(hashObj.GetComponent<LocalizedText>());
			checksumText = hashObj.GetComponent<TextMeshProUGUI>();
			checksumText.transform.position = exitText.transform.position;
			if (_checksum != null) SetChecksumText(_checksum);
		}
	}

	static class Patches
	{
		public static void MySetData(this Lobby lobby, string key, string value) => 
			lobby.SetData("almafa64>" + key, value);

		public static string MyGetData(this Lobby lobby, string key) => 
			lobby.GetData("almafa64>" + key);

		[System.Obsolete]
		public static void OnEnterLobby_Postfix(Lobby lobby)
		{
			string checksumField = "checksum";

			if (!SteamManager.LocalPlayerIsLobbyOwner)
			{
				if (lobby.MyGetData(checksumField) != Plugin.CHECKSUM)
				{
					// ToDo print out needed mods (maybe download released automaticly)
					SteamManager.instance.LeaveLobby();
				}
				return;
			}

			lobby.MySetData(checksumField, Plugin.CHECKSUM);
			foreach (KeyValuePair<string, Mod> mod in Plugin.mods)
			{
				ConfigFile config = mod.Value.Plugin.Instance.Config;

				foreach (ConfigEntryBase entry in config.GetConfigEntries())
				{
					lobby.MySetData($"{mod.Key}|{entry.Definition}", entry.GetSerializedValue());
				}
			}
		}

		[System.Obsolete]
		public static void GameSessionInit_Postfix()
		{
			if (!GameLobby.isOnlineGame || SteamManager.LocalPlayerIsLobbyOwner) return;
			// load host's config settings
			Lobby lobby = SteamManager.instance.currentLobby;
			foreach (KeyValuePair<string, Mod> mod in Plugin.mods)
			{
				ConfigFile config = mod.Value.Plugin.Instance.Config;

				// turn off auto saving to keep users own settings in file
				bool saveOnSet = config.SaveOnConfigSet;
				config.SaveOnConfigSet = false;
				
				foreach (ConfigEntryBase entry in config.GetConfigEntries())
				{
					string data = lobby.MyGetData($"{mod.Key}|{entry.Definition}");
					entry.SetSerializedValue(data);
				}

				config.SaveOnConfigSet = saveOnSet;
			}
		}
	}

	public struct Mod(string link)
	{
		public string Link { get; internal set; } = link;

		public string Hash { get; internal set; }
		public BepInEx.PluginInfo Plugin { get; internal set; }

		public override readonly string ToString() =>
			$"name: '{Plugin.Metadata.Name}', version: '{Plugin.Metadata.Version}', link: '{Link}', guid: '{Plugin.Metadata.GUID}', hash: '{Hash}'";
	}
}