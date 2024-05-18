using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using BoplModSyncer.Utils;
using HarmonyLib;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using TinyJson;
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
		public const string THUNDERSTORE_BOPL_MODS = "https://thunderstore.io/c/bopl-battle/api/v1/package";

		internal static readonly Dictionary<string, LocalModData> _mods = [];
		public static readonly ReadOnlyDictionary<string, LocalModData> mods = new(_mods);
		public static readonly bool IsDemo = Path.GetFileName(Paths.GameRootPath) == "Bopl Battle Demo";

		internal static Harmony harmony;
		internal static ManualLogSource logger;
		internal static ConfigFile config;
		internal static Plugin plugin;

		internal static ConfigEntry<ulong> lastLobbyId;

		internal static string _checksum;
		internal static readonly HashSet<string> _clientOnlyGuids = [
			"com.Melon_David.MapPicker",
			"me.antimality.TimeStopTimer",
			"me.antimality.SuddenDeathTimer",
			"com.almafa64.BoplTranslator",
			"com.almafa64.BoplModSyncer",
			"com.WackyModer.ModNames",
		];

		internal static GameObject genericPanel;
		internal static GameObject missingModsPanel;
		internal static GameObject noSyncerPanel;
		internal static GameObject installingPanel;
		internal static GameObject restartPanel;

		private static GameObject checksumTextObj;

		private void Awake()
		{
			harmony = new(Info.Metadata.GUID);
			logger = Logger;
			config = Config;
			plugin = this;

			lastLobbyId = config.Bind("BoplModSyncer", "last lobby id", 0ul);

			SceneManager.sceneLoaded += OnSceneLoaded;

			harmony.Patch(
				AccessTools.Method(typeof(SteamManager), "OnLobbyEnteredCallback"),
				prefix: new(typeof(Patches), nameof(Patches.OnEnterLobby_Prefix))
			);

			harmony.Patch(
				AccessTools.Method(typeof(SteamManager), "OnLobbyMemberJoinedCallback"),
				postfix: new(typeof(Patches), nameof(Patches.OnLobbyMemberJoinedCallback_Postfix))
			);

			AssetBundle bundle = AssetBundle.LoadFromStream(BaseUtils.GetResourceStream(PluginInfo.PLUGIN_NAME, "PanelBundle"));
			genericPanel = bundle.LoadAsset<GameObject>("GenericPanel");
			bundle.Unload(false);

			GameUtils.Init();
			Directory.CreateDirectory(GameUtils.MyCachePath);
		}

		private void Start()
		{
			// Get all released mod links
			WebClient wc = new();
			List<object> modsJSON = (List<object>)wc.DownloadString(THUNDERSTORE_BOPL_MODS).FromJson<object>();

			Dictionary<string, Dictionary<string, string>> downloadLinks = [];

			// get all download link for version for all mod
			foreach(var modObj in modsJSON)
			{
				Dictionary<string, object> mod = modObj as Dictionary<string, object>;
				Dictionary<string, string> modLinks = [];
				foreach(var versionObj in (List<object>)mod["versions"])
				{
					Dictionary<string, object> version = versionObj as Dictionary<string, object>;
					modLinks.Add((string)version["version_number"], version["download_url"] as string);
				}
				downloadLinks.Add((string)mod["name"], modLinks);
			}

			// Get all downloaded mods (and add link if it's released)
			List<string> hashes = [];
			foreach (BepInEx.PluginInfo plugin in Chainloader.PluginInfos.Values)
			{
				if (_clientOnlyGuids.Contains(plugin.Metadata.GUID)) continue;

				string hash = BaseUtils.ChecksumFile(plugin.Location);
				logger.LogInfo($"{plugin.Metadata.GUID} - {hash}");
				hashes.Add(hash);

				Manifest manifest = GameUtils.GetManifest(plugin);

				string link = manifest == null ? "" : downloadLinks.GetValueSafe(manifest.Name).GetValueSafe(manifest.Version);
				LocalModData mod = new(link)
				{
					Manifest = manifest,
					Plugin = plugin,
					Hash = hash
				};

				_mods.Add(plugin.Metadata.GUID, mod);
			}

			MakeChecksumText(BaseUtils.CombineHashes(hashes));

			PanelMaker.MakeGenericPanel(ref genericPanel);
			noSyncerPanel = PanelMaker.MakeNoSyncerPanel(genericPanel);
			missingModsPanel = PanelMaker.MakeMissingModsPanel(genericPanel);
			installingPanel = PanelMaker.MakeInstallingPanel(genericPanel);
			restartPanel = PanelMaker.MakeRestartPanel(genericPanel);
			Destroy(genericPanel);
			genericPanel = null;
		}

		private void MakeChecksumText(string checksum)
		{
			if (_checksum != null) throw new("Checksum text was already made!");
			_checksum = checksum;

			Transform canvas = PanelUtils.GetCanvas();
			GameObject text = GameObject.Find("ExitText");
			GameObject hashObj = Instantiate(text, canvas);
			hashObj.name = "hashText";
			Destroy(hashObj.GetComponent<LocalizedText>());

			TextMeshProUGUI checksumText = hashObj.GetComponent<TextMeshProUGUI>();
			checksumText.transform.position = text.transform.position;
			checksumText.text = checksum;

			checksumTextObj = Instantiate(hashObj);
			checksumTextObj.hideFlags = HideFlags.HideInHierarchy;
			DontDestroyOnLoad(checksumTextObj);
			Destroy(hashObj);

			ShowChecksumText(10);
		}

		private void ShowChecksumText(int ypos)
		{
			if (checksumTextObj == null) return;

			GameObject hashObj = Instantiate(checksumTextObj, PanelUtils.GetCanvas());

			// move checksum text to the bottom of screen + 10 pixel up
			Vector3 pos = Camera.main.WorldToScreenPoint(hashObj.transform.position);
			pos.y = ypos;
			hashObj.transform.position = Camera.main.ScreenToWorldPoint(pos);
		}

		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			if (scene.name == "MainMenu") OnMainMenuloaded();
			else if (scene.name.Contains("Select")) OnSelectMenuLoaded();
		}

		private void OnMainMenuloaded()
		{
			ShowChecksumText(10);
		}

		private void OnSelectMenuLoaded()
		{
			// idk why this needs to be so big
			ShowChecksumText(1500);
		}
	}
}