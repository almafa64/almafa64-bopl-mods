using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace WhoseThisTeleport
{
	[BepInPlugin("com.almafa64.WhoseThisTeleport", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	[BepInProcess("BoplBattle.exe")]
	public class Plugin : BaseUnityPlugin
	{
		internal static Harmony harmony;
		internal static ManualLogSource logger;
		internal static ConfigFile config;

		private void Awake()
		{
			harmony = new(Info.Metadata.GUID);
			logger = Logger;
			config = Config;

			harmony.Patch(
				AccessTools.Method(typeof(Teleport), nameof(Teleport.CastAbility)),
				prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.CastTeleport_Prefix))
			);
		}
	}

	class Patches
	{
		public static bool CastTeleport_Prefix(Teleport __instance)
		{
			Traverse traverse = new(__instance);
			TeleportIndicator indicator = traverse.Field("teleportIndicator").GetValue<TeleportIndicator>();
			if (indicator == null || indicator.IsDestroyed)
			{
				InstantAbility ability = traverse.Field("instantAbility").GetValue<InstantAbility>();
				int index = ability.GetSlimeController().abilities.IndexOf(ability);
				
				GameObject go = new();
				TextMeshPro text = go.AddComponent<TextMeshPro>();

				Vector2 indPos = (Vector2)ability.GetSlimeController().body.fixtrans.position;
				go.transform.position = new Vector3(indPos.x + 13, indPos.y + 5, 1);

				switch(index)
				{
					case 0: text.text = "<"; break;
					case 1: text.text = ">"; break;
					case 2: text.text = "^"; break;
				}

				text.fontSize = 30;
				text.fontStyle = FontStyles.Bold;
				
			}

			return true;
		}
	}
}