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

			harmony.Patch(
				AccessTools.Method(typeof(Teleport), nameof(Teleport.CastAbility)),
				postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.CastTeleport_Postfix))
			);
		}
	}

	class Patches
	{
		private static GameObject go;
		public static bool CastTeleport_Prefix(Teleport __instance)
		{
			Traverse traverse = new(__instance);
			TeleportIndicator indicator = traverse.Field("teleportIndicator").GetValue<TeleportIndicator>();
			if (indicator == null || indicator.IsDestroyed)
			{
				InstantAbility ability = traverse.Field("instantAbility").GetValue<InstantAbility>();
				int index = ability.GetSlimeController().abilities.IndexOf(ability);
				
				go = new();
				TextMeshPro text = go.AddComponent<TextMeshPro>();

				switch(index)
				{
					case 0: text.text = "<"; break;
					case 1: text.text = ">"; break;
					case 2: text.text = "^"; break;
				}

				text.fontSize = 30;
				text.fontStyle = FontStyles.Bold;
				text.font = LocalizedText.localizationTable.GetFont(Language.EN, true);
				text.outlineWidth = 0.2f;
				text.outlineColor = Color.black;
				text.color = ability.GetSlimeController().GetPlayerMaterial().GetColor("_ShadowColor");
			}

			return true;
		}

		public static void CastTeleport_Postfix(Teleport __instance)
		{
			if (!go) return;

			Traverse traverse = new(__instance);
			TeleportIndicator indicator = traverse.Field("teleportIndicator").GetValue<TeleportIndicator>();
			go.transform.SetParent(indicator.transform, false);
			go.transform.position = indicator.transform.position + new Vector3(15, 3);

			go = null;
		}
	}
}