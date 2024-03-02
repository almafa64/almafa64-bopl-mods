using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;

namespace BoplModSyncer
{
	[BepInPlugin("com.almafa64.BoplModSyncer", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	public class Plugin : BaseUnityPlugin
	{
		internal static Harmony harmony;
		internal static ManualLogSource logger;
		internal static string _checksum;
		public static string CHECKSUM { get => _checksum ?? throw new("CHECKSUM hasn't been calculated"); }

		private void Awake()
		{
			harmony = new(Info.Metadata.GUID);
			logger = Logger;
		}

		private void Start()
		{
			_checksum = Utils.CombineHashes(GetModHashes());
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
	}
}