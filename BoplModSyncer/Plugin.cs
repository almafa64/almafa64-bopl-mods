using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
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

				Utils.GetConfigEntries(plugin);

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

	public struct Mod(string link)
	{
		public string Link { get; internal set; } = link;

		public string Hash { get; internal set; }
		public BepInEx.PluginInfo Plugin { get; internal set; }

		public override readonly string ToString() =>
			$"name: '{Plugin.Metadata.Name}', version: '{Plugin.Metadata.Version}', link: '{Link}', guid: '{Plugin.Metadata.GUID}', hash: '{Hash}'";
	}
}