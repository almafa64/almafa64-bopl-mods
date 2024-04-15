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
		internal static GameObject missingModsPrefab;

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
			missingModsPrefab = bundle.LoadAsset<GameObject>("Panel");
			DontDestroyOnLoad(missingModsPrefab);
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

			// add text components into panel
			TextMeshProUGUI title = missingModsPrefab.transform.Find("Title").gameObject.AddComponent<TextMeshProUGUI>();
			TextMeshProUGUI info = missingModsPrefab.transform.Find("Info").gameObject.AddComponent<TextMeshProUGUI>();
			TextMeshProUGUI ok = missingModsPrefab.transform.Find("OK Button/Text (TMP)").gameObject.AddComponent<TextMeshProUGUI>();
			Text rowLabel = missingModsPrefab.transform.Find("NeededModList/Viewport/Content/Toggle/Background/Label").gameObject.AddComponent<Text>();

			// text mesh pro settings
			title.fontSize = 56;
			title.color = UnityEngine.Color.black;
			title.font = LocalizedText.localizationTable.GetFont(Language.EN, false);
			title.alignment = TextAlignmentOptions.BaselineLeft;
			title.fontStyle = FontStyles.Bold;

			ok.fontSize = 50;
			ok.color = UnityEngine.Color.black;
			ok.font = LocalizedText.localizationTable.GetFont(Language.EN, false);
			ok.alignment = TextAlignmentOptions.Center;
			ok.fontStyle = FontStyles.Bold;

			info.fontSize = 50;
			info.color = UnityEngine.Color.black;
			info.font = LocalizedText.localizationTable.GetFont(Language.EN, false);
			info.alignment = TextAlignmentOptions.BaselineLeft;

			// text settings
			rowLabel.fontSize = 50;
			rowLabel.color = new Color32(50, 50, 50, 255);
			rowLabel.font = Font.GetDefault();
			rowLabel.alignment = TextAnchor.MiddleLeft;

			// texts
			title.text = "Missing mods";
			info.text = "Install | GUID";
			ok.text = "OK";
			rowLabel.text = "placeholder";
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
		private static readonly string modListField = "almafa64>modlist";

		public static void MySetData(this Lobby lobby, string key, string value) => 
			lobby.SetData("almafa64>" + key, value);

		public static string MyGetData(this Lobby lobby, string key) => 
			lobby.GetData("almafa64>" + key);

		public static void OnEnterLobby_Prefix(Lobby lobby)
		{
			if (SteamManager.LocalPlayerIsLobbyOwner)
				OnHostJoin(lobby);                                       // you are host
			else if (lobby.MyGetData(checksumField) == Plugin.CHECKSUM)
				SyncConfigs(lobby);                                      // you have same mods as host
			else
				ModMismatch(lobby);                                      // you dont have same mods as host
		}

		private static void ModMismatch(Lobby lobby)
		{
			// ToDo maybe download configs too now so game doesnt need to restart twice

			// ToDo use checksum
			List<string> modList = lobby.MyGetData(modListField).Split('|').ToList();
			foreach (string modGUID in Plugin.mods.Keys)
			{
				modList.Remove(modGUID);
			}

			SteamManager.instance.LeaveLobby();

			// --- missing mods panel ---

			// get canvas in main menu or selector scenes
			Transform canvas;
			if (SceneManager.GetActiveScene().name == "MainMenu")
				canvas = GameObject.Find("Canvas (1)").transform;
			else
				canvas = GameObject.Find("Canvas").transform;

			GameObject missingModsPanel = Object.Instantiate(Plugin.missingModsPrefab, canvas);
			Transform row = missingModsPanel.transform.Find("NeededModList/Viewport/Content/Toggle");

			// close panel with button click
			Button button = missingModsPanel.transform.Find("OK Button").GetComponent<Button>();
			button.onClick.AddListener(() => Object.Destroy(missingModsPanel));

			if (lobby.MyGetData(checksumField) == null) return;

			void makeRow(string text, bool copyRow)
			{
				// create new row if there is still more mods
				Transform rowCopy = copyRow ? Object.Instantiate(row, row.parent) : null;

				row.Find("Background/Label").GetComponent<Text>().text = text;

				// ToDo only stop interaction if there is no offical link
				row.GetComponent<Toggle>().interactable = false;

				row = rowCopy;
			}

			for (int i = 0, n = modList.Count - 1; i < n; i++)
			{
				makeRow(modList[i], true);
			}
			makeRow(modList[modList.Count - 1], false);
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
			foreach (KeyValuePair<string, Mod> mod in Plugin.mods)
			{
				ConfigFile config = mod.Value.Plugin.Instance.Config;
				sb.Append(mod.Key).Append("|");

				foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> entryDir in config)
				{
					ConfigEntryBase entry = entryDir.Value;
					lobby.MySetData($"{mod.Key}|{entry.Definition}", entry.GetSerializedValue());
				}
			}

			sb.Remove(sb.Length - 1, 1); // remove last '|'
			// ToDo send json {'guid': <guid>, 'checksum': <checksum>, 'version': <version>, 'link': <link>}
			lobby.MySetData(modListField, sb.ToString());
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