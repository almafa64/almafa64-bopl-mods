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
			// Get all released mod
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
				Mod mod = new(datas[0], datas[1], datas[2]);
				officalMods.Add(datas[2].Split('/').Last(), mod);
			}

			// Get all downloaded released mods and non released
			List<string> hashes = [];
			foreach (BepInEx.PluginInfo plugin in Chainloader.PluginInfos.Values)
			{
				string hash = Utils.Checksum(plugin.Location);
				hashes.Add(hash);
				if (!officalMods.TryGetValue(plugin.Location.Split(Path.DirectorySeparatorChar).Last(), out Mod mod))
				{
					mod.Name = plugin.Metadata.Name;
					mod.Version = plugin.Metadata.Version.ToString();
				}
				mod.Hash = hash;
				mod.Guid = plugin.Metadata.GUID;
				mod.Path = plugin.Location;
				_mods.Add(plugin.Metadata.GUID, mod);
			}

			_checksum = Utils.CombineHashes(hashes);
			SetChecksumText();
		}

		private void SetChecksumText()
		{
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
			if (_checksum != null) SetChecksumText();
		}
	}

	public struct Mod(string name, string version, string link)
	{
		public string Name { get; internal set; } = name;
		public string Version { get; internal set; } = version;
		public string Link { get; internal set; } = link;
	
		public string Guid { get; internal set; }
		public string Path { get; internal set; }
		public string Hash { get; internal set; }

		public override readonly string ToString() =>
			$"name: {Name}, version: {Version}, link: {Link}, guid: {Guid}, hash: {Hash}";
	}
}