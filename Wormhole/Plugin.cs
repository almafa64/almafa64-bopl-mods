using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Wormhole
{
	[BepInPlugin($"com.almafa64.{PluginInfo.PLUGIN_NAME}", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	[BepInDependency("com.David_Loves_JellyCar_Worlds.PlatformApi")]
	[BepInProcess("BoplBattle.exe")]
	public class Plugin : BaseUnityPlugin
	{
		internal static Harmony harmony;
		internal static ManualLogSource logger;

		private void Awake()
		{
			harmony = new(Info.Metadata.GUID);
			logger = Logger;

			harmony.Patch(
				AccessTools.Method(typeof(GameSessionHandler), "SpawnPlayers"),
				postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.SpawnPlayers_Postfix))
			);

			harmony.Patch(
				AccessTools.Method(typeof(BlackHole), nameof(BlackHole.OnCollide)),
				prefix: new(typeof(Patches), nameof(Patches.BlackHoleCollide_Prefix))
			);

			harmony.Patch(
				AccessTools.Method(typeof(BlackHole), nameof(BlackHole.Grow)),
				postfix: new(typeof(Patches), nameof(Patches.BlackHoleGrow_Postfix))
			);

			harmony.Patch(
				AccessTools.Method(typeof(BlackHole), nameof(BlackHole.Init)),
				postfix: new(typeof(Patches), nameof(Patches.BlackHoleInit_Postfix))
			);
		}

		void Update()
		{
			foreach (var whiteHole in Patches.whiteHoles)
			{
				try
				{
					LineRenderer lineRenderer = whiteHole.GetComponent<LineRenderer>();

					lineRenderer.SetPosition(0, Vector3.zero);
					lineRenderer.SetPosition(1, Vector3.zero);

					BlackHole pair = Patches.holePairs[whiteHole];
					if (pair == null) continue;
					lineRenderer.SetPosition(0, pair.transform.transform.position);
					lineRenderer.SetPosition(1, whiteHole.transform.transform.position);
				}
				catch { }
			}
		}
	}
}