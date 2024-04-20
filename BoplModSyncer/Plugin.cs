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
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BoplModSyncer
{
	[BepInPlugin("com.almafa64.BoplModSyncer", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	[BepInProcess("BoplBattle.exe")]
	public class Plugin : BaseUnityPlugin
	{
		public static string CHECKSUM { get => _checksum ?? throw new("CHECKSUM hasn't been calculated"); }
		public static readonly string MOD_LIST_API = "https://api.github.com/repos/ShAdowDev16/BoplMods/contents/AllAvailableMods.txt";
		public static readonly string MOD_LIST = "https://raw.githubusercontent.com/ShAdowDev16/BoplMods/main/AllAvailableMods.txt";
		internal static readonly Dictionary<string, Mod> _mods = [];
		public static readonly ReadOnlyDictionary<string, Mod> mods = new(_mods);

		internal static Harmony harmony;
		internal static ManualLogSource logger;
		internal static string _checksum;
		internal static readonly HashSet<string> _clientOnlyGuids = [
			"com.Melon_David.MapPicker",
			"me.antimality.TimeStopTimer",
			"me.antimality.SuddenDeathTimer",
			"com.almafa64.BoplTranslator",
			"com.WackyModer.ModNames",
		];

		internal static GameObject genericPanel;
		internal static GameObject missingModsPanel;
		internal static GameObject noSyncerPanel;
		internal static GameObject installingPanel;
		internal static GameObject restartPanel;

		private TextMeshProUGUI checksumText;

		private void Awake()
		{
			harmony = new(Info.Metadata.GUID);
			logger = Logger;

			SceneManager.sceneLoaded += OnSceneLoaded;

			harmony.Patch(
				AccessTools.Method(typeof(SteamManager), "OnLobbyEnteredCallback"),
				prefix: new(typeof(Patches), nameof(Patches.OnEnterLobby_Prefix))
			);

			AssetBundle bundle = AssetBundle.LoadFromStream(Utils.GetResourceStream(PluginInfo.PLUGIN_NAME, "PanelBundle"));
			genericPanel = bundle.LoadAsset<GameObject>("GenericPanel");
		}

		private void Start()
		{
			// Get all released mod links
			WebClient wc = new();
			string modList = wc.DownloadString(MOD_LIST);
			string[] modLines = modList.TrimEnd().Split('\n');

			Dictionary<string, Mod> officalMods = [];

			// save links for every released mod
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
				if (_clientOnlyGuids.Contains(plugin.Metadata.GUID)) continue;

				string hash = Utils.ChecksumFile(plugin.Location);
				logger.LogInfo($"{plugin.Metadata.GUID} - {hash}");
				hashes.Add(hash);

				officalMods.TryGetValue(plugin.Location.Split(Path.DirectorySeparatorChar).Last(), out Mod mod);
				mod.Plugin = plugin;
				mod.Hash = hash;

				_mods.Add(plugin.Metadata.GUID, mod);
			}

			SetChecksumText(Utils.CombineHashes(hashes));

			PanelMaker.MakeGenericPanel(ref genericPanel);
			noSyncerPanel = PanelMaker.MakeNoSyncerPanel(genericPanel);
			missingModsPanel = PanelMaker.MakeMissingModsPanel(genericPanel);
			installingPanel = PanelMaker.MakeInstallingPanel(genericPanel);
			restartPanel = PanelMaker.MakeRestartPanel(genericPanel);
			Destroy(genericPanel);
			genericPanel = null;
		}

		private void SetChecksumText(string text)
		{
			_checksum = text;
			checksumText.text = CHECKSUM;
			checksumText.fontSize -= 5;

			// move checksum text to the bottom of screen + 10 pixel
			Vector3 pos = Camera.main.WorldToScreenPoint(checksumText.transform.position);
			pos.y = 10;
			checksumText.transform.position = Camera.main.ScreenToWorldPoint(pos);
		}

		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			if (scene.name == "MainMenu") OnMainMenuloaded();
		}

		private void OnMainMenuloaded()
		{
			// create checksum on center of screen
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
		private static readonly string checksumField = "almafa64>checksum";
		private static readonly string hostModListField = "almafa64>modlist";

		public static void MySetData(this Lobby lobby, string key, string value) => 
			lobby.SetData("almafa64>" + key, value);

		public static string MyGetData(this Lobby lobby, string key) => 
			lobby.GetData("almafa64>" + key);

		private struct ModNetData(string guid, string version, string link, string hash)
		{
			public string guid = guid;
			public string version = version;
			public string link = link;
			public string hash = hash;
		}

		public static void OnEnterLobby_Prefix(Lobby lobby)
		{
			string lobbyHash = lobby.MyGetData(checksumField);

			if (SteamManager.LocalPlayerIsLobbyOwner)
				OnHostJoin(lobby);                                // you are host
			else if (lobbyHash == Plugin.CHECKSUM)
				SyncConfigs(lobby);                               // you have same mods as host
			else if (lobbyHash == null || lobbyHash == "")
				HostDoesntHaveSyncer(lobby);                      // host didnt install syncer
			else
				ModMismatch(lobby);                               // you dont have same mods as host
		}

		private static void HostDoesntHaveSyncer(Lobby lobby)
		{
			Plugin.logger.LogWarning("host doesnt have syncer");

			SteamManager.instance.LeaveLobby();

			// --- no host syncer panel ---

			// get canvas in main menu or selector scenes
			Transform canvas;
			if (SceneManager.GetActiveScene().name == "MainMenu")
				canvas = GameObject.Find("Canvas (1)").transform;
			else
				canvas = GameObject.Find("Canvas").transform;

			GameObject noSyncerPanel = Object.Instantiate(Plugin.noSyncerPanel, canvas);
			PanelMaker.MakeCloseButton(noSyncerPanel);
		}

		private static void ModMismatch(Lobby lobby)
		{
			// ToDo maybe download configs too now so game doesnt need to restart twice

			List<ModNetData> missingMods = [];
			foreach (string hostMod in lobby.MyGetData(hostModListField).Split('|'))
			{
				// guid,ver,link,hash
				string[] datas = hostMod.Split(',');
				ModNetData modData = new(datas[0], datas[1], datas[2], datas[3]);

				// if guid isnt found in local mods or hash isnt same then its missing
				if (!Plugin.mods.TryGetValue(modData.guid, out Mod mod) || mod.Hash != modData.hash)
					missingMods.Add(modData);
			}

			Plugin.logger.LogWarning("missing: " + string.Join("\n", missingMods.Select(m => $"{m.guid} (v{m.version})")));

			SteamManager.instance.LeaveLobby();

			// --- missing mods panel ---

			// get canvas in main menu or selector scenes
			Transform canvas;
			if (SceneManager.GetActiveScene().name == "MainMenu")
				canvas = GameObject.Find("Canvas (1)").transform;
			else
				canvas = GameObject.Find("Canvas").transform;

			GameObject missingModsPanel = Object.Instantiate(Plugin.missingModsPanel, canvas);
			Transform row = missingModsPanel.transform.Find("NeededModList/Viewport/Content/Toggle");

			LinkClicker linkClicker = missingModsPanel.AddComponent<LinkClicker>();

			// close panel with button click
			// ToDo download selected mods
			PanelMaker.MakeCloseButton(missingModsPanel);

			void makeRow(ModNetData modData, bool copyRow)
			{
				// create new row if there is still more mods
				Transform rowCopy = copyRow ? Object.Instantiate(row, row.parent) : null;

				// com.test.testmod v1.0.0 (a4bf) link
				StringBuilder sb = new StringBuilder(modData.guid)
					.Append(" v").Append(modData.version)
					.Append(" (").Append(modData.hash.Substring(0, 4)).Append(")");

				if(modData.link != "")
				{
					// get release PAGE of mod
					modData.link = modData.link.Substring(0, modData.link.LastIndexOf("/"));
					sb.Append(" <link=").Append(modData.link).Append("><color=blue><b>link</b></color></link>");
				}

				TextMeshProUGUI label = row.Find("Background/Label").GetComponent<TextMeshProUGUI>();
				label.text = sb.ToString();
				linkClicker.textMeshes.Add(label);

				// dont give option to download mod if isnt offical
				// set toggle default to checked
				Toggle toggle = row.GetComponent<Toggle>();
				if (modData.link != "") toggle.isOn = true;
				else toggle.interactable = false;

				row = rowCopy;
			}

			for (int i = 0, n = missingMods.Count - 1; i < n; i++)
			{
				makeRow(missingMods[i], true);
			}
			makeRow(missingMods[missingMods.Count - 1], false);
		}

		private static void SyncConfigs(Lobby lobby)
		{
			foreach (KeyValuePair<string, Mod> mod in Plugin.mods)
			{
				ConfigFile config = mod.Value.Plugin.Instance.Config;

				// turn off auto saving to keep users own settings in file
				bool saveOnSet = config.SaveOnConfigSet;
				config.SaveOnConfigSet = false;

				foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> entryDir in config)
				{
					ConfigEntryBase entry = entryDir.Value;
					string data = lobby.MyGetData($"{mod.Key}|{entry.Definition}");
					entry.SetSerializedValue(data);
				}

				config.SaveOnConfigSet = saveOnSet;
			}
		}

		private static void OnHostJoin(Lobby lobby)
		{
			lobby.MySetData(checksumField, Plugin.CHECKSUM);

			StringBuilder sb = new();
			foreach (KeyValuePair<string, Mod> modDir in Plugin.mods)
			{
				Mod mod = modDir.Value;

				ConfigFile config = mod.Plugin.Instance.Config;

				// no built in json lib + cant embed dlls without project restructure = custom format
				// guid,ver,link,hash
				sb.Append(modDir.Key).Append(',')
					.Append(mod.Plugin.Metadata.Version).Append(',')
					.Append(mod.Link).Append(',')
					.Append(mod.Hash)
					.Append("|");

				foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> entryDir in config)
				{
					ConfigEntryBase entry = entryDir.Value;
					lobby.MySetData($"{modDir.Key}|{entry.Definition}", entry.GetSerializedValue());
				}
			}

			sb.Remove(sb.Length - 1, 1); // remove last '|'
			lobby.MySetData(hostModListField, sb.ToString());
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