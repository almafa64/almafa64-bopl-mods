using BepInEx;
using BepInEx.Logging;
using BoplFixedMath;
using HarmonyLib;
using UnityEngine;

namespace AbilityRandomizer
{
	[BepInPlugin($"com.almafa64.{PluginInfo.PLUGIN_NAME}", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
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
				AccessTools.Method(typeof(Ability), nameof(Ability.ExitAbility)),
				postfix: new(typeof(Patches), nameof(Patches.ExitAbility_Postfix))
			);

			harmony.Patch(
				AccessTools.Method(typeof(Teleport), nameof(Teleport.CastAbility)),
				prefix: new(typeof(Patches), nameof(Patches.TeleportCastAbility_Prefix))
			);

			harmony.Patch(
				AccessTools.Method(typeof(HookshotInstant), nameof(HookshotInstant.UseAbility)),
				postfix: new(typeof(Patches), nameof(Patches.HookUseAbility_Postfix))
			);
		}
	}

	class Patches
	{
		private static void ChangeAbility(SlimeController controller, int index)
		{
			NamedSprite namedSprite = RandomAbility.GetRandomAbilityPrefab(controller.abilityIconsFull, controller.abilityIconsDemo);

			GameObject abilityPrefab = namedSprite.associatedGameObject;
			AbilityMonoBehaviour ability = FixTransform.InstantiateFixed(abilityPrefab, Vec2.zero, Fix.Zero).GetComponent<AbilityMonoBehaviour>();

			controller.abilities[index] = ability;
			PlayerHandler.Get().GetPlayer(controller.playerNumber).CurrentAbilities[index] = abilityPrefab;
			controller.AbilityReadyIndicators[index].SetSprite(namedSprite.sprite, true);
			controller.AbilityReadyIndicators[index].ResetAnimation();
			new Traverse(controller).Field("abilityCooldownTimers").GetValue<Fix[]>()[index] = (Fix)100000L;
			AudioManager.Get().Play("abilityPickup");
		}

		internal static void ExitAbility_Postfix(Ability __instance, AbilityExitInfo exitInfo)
		{
			SlimeController controller = __instance.GetSlimeController();
			int index = controller.abilities.IndexOf(__instance);
			if (index == -1) return;

			IAbilityComponent[] components = new Traverse(__instance).Field("abilityComponents").GetValue<IAbilityComponent[]>();
			
			// when ability is tesla coil only run randomizer after placing 2. tesla coil
			if (components.Length == 1 &&
				components[0] is PlantObjectOnPlatform placer &&
				new Traverse(placer).Field("placeObject").GetValue<IPlaceObject>() is PlaceSparkNode sparkNode &&
				new Traverse(sparkNode).Field("plantBuffer").GetValue<RingBuffer<SimpleSparkNode>>()[1] == null)
			{
				return;
			}

			// ToDo invis

			ChangeAbility(controller, index);
		}

		internal static void TeleportCastAbility_Prefix(Teleport __instance)
		{
			Traverse traverse = new(__instance);
			TeleportIndicator indicator = traverse.Field("teleportIndicator").GetValue<TeleportIndicator>();
			if (indicator == null || indicator.IsDestroyed) return;

			InstantAbility ability = traverse.Field("instantAbility").GetValue<InstantAbility>();
			SlimeController controller = ability.GetSlimeController();
			int index = controller.abilities.IndexOf(ability);
			if (index == -1) return;

			ChangeAbility(controller, index);
		}

		internal static void HookUseAbility_Postfix(HookshotInstant __instance)
		{
			Traverse traverse = new(__instance);

			InstantAbility ability = traverse.Field("instantAbility").GetValue<InstantAbility>();
			SlimeController controller = ability.GetSlimeController();
			int index = controller.abilities.IndexOf(ability);
			if (index == -1) return;

			RopeBody ropeBody = traverse.Field("ropeBody").GetValue<RopeBody>();
			if(ropeBody == null)
			{
				ChangeAbility(controller, index);
				return;
			}
		
			// ToDo
			// 1. rope missed
			// 2. rope on player
		}
	}
}