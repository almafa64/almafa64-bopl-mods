using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Steamworks;
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

		internal static ConfigEntry<float> fontSize;
		internal static ConfigEntry<bool> showEveryTeleport;
		internal static ConfigEntry<bool> whiteOnly;
		internal static ConfigEntry<string> textType;
		internal static ConfigEntry<Vector2> textOffset;

		private void Awake()
		{
			harmony = new(Info.Metadata.GUID);
			logger = Logger;
			config = Config;

			fontSize = config.Bind("WhoseThisTeleport", "font size", 30f);
			showEveryTeleport = config.Bind("WhoseThisTeleport", "show for every teleport", false);
			whiteOnly = config.Bind("WhoseThisTeleport", "white only", false, "Turns off text coloring");
			textType = config.Bind("WhoseThisTeleport", "text type", "arrow", "values: arrow, number");
			textOffset = config.Bind("WhoseThisTeleport", "text offset", new Vector2(15, 3), "offset from left of the portal");

			harmony.Patch(
				AccessTools.Method(typeof(Teleport), nameof(Teleport.CastAbility)),
				prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.CastTeleport_Prefix))
			);

			harmony.Patch(
				AccessTools.Method(typeof(Teleport), nameof(Teleport.CastAbility)),
				postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.CastTeleport_Postfix))
			);

			harmony.Patch(
				AccessTools.Method(typeof(GameSessionHandler), "SpawnPlayers"),
				postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.SpawnPlayers_Postfix))
			);
		}
	}

	class Patches
	{
		private static string GetSymbolForAbility(int index)
		{
			return Plugin.textType.Value switch
			{
				"number" => index switch
				{
					0 => "1",
					1 => "3",
					2 => "2",
					_ => "something bad happened lol",
				},
				_ => index switch
				{
					0 => "<",
					1 => ">",
					2 => "^",
					_ => "something bad happened lol",
				},
			};
		}

		private static GameObject go;
		private static int thisId;
		public static bool CastTeleport_Prefix(Teleport __instance)
		{
			Traverse traverse = new(__instance);
			TeleportIndicator indicator = traverse.Field("teleportIndicator").GetValue<TeleportIndicator>();
			if (indicator == null || indicator.IsDestroyed)
			{
				InstantAbility ability = traverse.Field("instantAbility").GetValue<InstantAbility>();
				SlimeController controller = ability.GetSlimeController();

				if (!Plugin.showEveryTeleport.Value && controller.GetPlayerId() != thisId) return true;
				
				int index = controller.abilities.IndexOf(ability);

				go = new();
				TextMeshPro text = go.AddComponent<TextMeshPro>();

				text.text = GetSymbolForAbility(index);

				text.fontSize = Plugin.fontSize.Value;
				text.fontStyle = FontStyles.Bold;
				text.font = LocalizedText.localizationTable.GetFont(Language.EN, true);
				text.outlineWidth = 0.2f;
				text.outlineColor = Color.black;
				if (Plugin.whiteOnly.Value) text.color = Color.white;
				else text.color = controller.GetPlayerMaterial().GetColor("_ShadowColor");
			}

			return true;
		}

		public static void CastTeleport_Postfix(Teleport __instance)
		{
			if (!go) return;

			Traverse traverse = new(__instance);
			TeleportIndicator indicator = traverse.Field("teleportIndicator").GetValue<TeleportIndicator>();
			go.transform.SetParent(indicator.transform, false);
			go.transform.position = indicator.transform.position + new Vector3(Plugin.textOffset.Value.x, Plugin.textOffset.Value.y);

			go = null;
		}

		public static void SpawnPlayers_Postfix()
		{
			thisId = Object.FindObjectOfType<InputUpdater>().GetClaimerId();
		}
	}
}