using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BoplModSyncer
{
	[BepInPlugin("com.almafa64.BoplModSyncer", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	public class Plugin : BaseUnityPlugin
	{
		public static string CHECKSUM { get => _checksum ?? throw new("CHECKSUM hasn't been calculated"); }
		
		internal static Harmony harmony;
		internal static ManualLogSource logger;
		internal static string _checksum;

		private TextMeshProUGUI checksumText;

		private void Awake()
		{
			harmony = new(Info.Metadata.GUID);
			logger = Logger;

			SceneManager.sceneLoaded += OnSceneLoaded;
		}

		private void Start()
		{
			_checksum = Utils.CombineHashes(GetModHashes());
			SetChecksumText();
		}

		private List<string> GetModHashes()
		{
			List<string> hashes = [];
			foreach (BepInEx.PluginInfo plugin in Chainloader.PluginInfos.Values)
			{
				string hash = Utils.Checksum(plugin.Location);
				hashes.Add(hash);
			}
			return hashes;
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
}