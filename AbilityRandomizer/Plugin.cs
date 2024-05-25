using BepInEx;
using BepInEx.Logging;
using BoplFixedMath;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
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
				AccessTools.Method(typeof(HookshotInstant), "FireHook"),
				postfix: new(typeof(Patches), nameof(Patches.FireHook_Postfix))
			);

			harmony.Patch(
				AccessTools.Method(typeof(Rope), "CheckIfDestroyed"),
				postfix: new(typeof(Patches), nameof(Patches.RopeCheckIfDestroyed_Postfix))
			);

			harmony.Patch(
				AccessTools.Method(typeof(Rope), nameof(Rope.Initialize)),
				postfix: new(typeof(Patches), nameof(Patches.RopeInitialize_Postfix))
			);

			harmony.Patch(
				AccessTools.Method(typeof(GameSessionHandler), "SpawnPlayers"),
				postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.SpawnPlayers_Postfix))
			);

			harmony.Patch(
				AccessTools.Method(typeof(Invisibility), nameof(Invisibility.OnInvisibiltyEnded)),
				postfix: new(typeof(Patches), nameof(Patches.OnInvisibiltyEnded_Postfix))
			);
		}
	}

	class Patches
	{
		private static FieldInfo MyGetField<T>(string name) => typeof(T).GetField(name, AccessTools.all);

		private static readonly Dictionary<RopeBody, HookshotInstant> ropeSlots = [];
		private static readonly Dictionary<Rope, RopeBody> ropeBodies = [];

		private static readonly FieldInfo invisAbilityField = MyGetField<Invisibility>("ia");

		private static readonly FieldInfo hookAbilityField = MyGetField<HookshotInstant>("instantAbility");
		private static readonly FieldInfo hookRopeBodyField = MyGetField<HookshotInstant>("ropeBody");

		private static readonly FieldInfo teleportAbilityField = MyGetField<Teleport>("instantAbility");
		private static readonly FieldInfo teleportIndicatorField = MyGetField<Teleport>("teleportIndicator");

		private static readonly FieldInfo abilityComponentsField = MyGetField<Ability>("abilityComponents");
		private static readonly FieldInfo abilityCooldownField = MyGetField<SlimeController>("abilityCooldownTimers");

		private static readonly FieldInfo placeObjectField = MyGetField<PlantObjectOnPlatform>("placeObject");
		private static readonly FieldInfo plantBufferField = MyGetField<PlaceSparkNode>("plantBuffer");

		private static void ChangeAbility(InstantAbility instantAbility)
		{
			SlimeController controller = instantAbility.GetSlimeController();
			int index = controller.abilities.IndexOf(instantAbility);
			if (index == -1) return;

			ChangeAbility(controller, index);
		}

		private static void ChangeAbility(SlimeController controller, int index)
		{
			AbilityReadyIndicator indicator = controller.AbilityReadyIndicators[index];

			NamedSprite namedSprite = RandomAbility.GetRandomAbilityPrefab(
				controller.abilityIconsFull,
				controller.abilityIconsDemo,
				indicator.GetPrimarySprite());

			GameObject abilityPrefab = namedSprite.associatedGameObject;
			AbilityMonoBehaviour ability = FixTransform.InstantiateFixed(abilityPrefab, Vec2.zero, Fix.Zero).GetComponent<AbilityMonoBehaviour>();

			controller.abilities[index] = ability;
			PlayerHandler.Get().GetPlayer(controller.playerNumber).CurrentAbilities[index] = abilityPrefab;
			indicator.SetSprite(namedSprite.sprite, true);
			indicator.ResetAnimation();
			(abilityCooldownField.GetValue(controller) as Fix[])[index] = (Fix)100000L;
			AudioManager.Get().Play("abilityPickup");
		}

		internal static void SpawnPlayers_Postfix()
		{
			ropeBodies.Clear();
			ropeSlots.Clear();
		}

		// General randomizing
		internal static void ExitAbility_Postfix(Ability __instance, AbilityExitInfo exitInfo)
		{
			SlimeController controller = __instance.GetSlimeController();
			int index = controller.abilities.IndexOf(__instance);
			if (index == -1) return;

			IAbilityComponent[] components = abilityComponentsField.GetValue(__instance) as IAbilityComponent[];

			// when ability is tesla coil only run randomizer after placing 2. tesla coil
			if (components.Length == 1 &&
				components[0] is PlantObjectOnPlatform placer &&
				placeObjectField.GetValue(placer) as IPlaceObject is PlaceSparkNode sparkNode &&
				(plantBufferField.GetValue(sparkNode) as RingBuffer<SimpleSparkNode>)[1] == null)
			{
				return;
			}

			// ToDo invis

			ChangeAbility(controller, index);
		}

		// Teleport randomizing
		internal static void TeleportCastAbility_Prefix(Teleport __instance)
		{
			TeleportIndicator indicator = teleportIndicatorField.GetValue(__instance) as TeleportIndicator;
			if (indicator == null || indicator.IsDestroyed) return;

			ChangeAbility(teleportAbilityField.GetValue(__instance) as InstantAbility);
		}

		// Hook randomizing
		internal static void FireHook_Postfix(HookshotInstant __instance)
		{
			ropeSlots.Add(hookRopeBodyField.GetValue(__instance) as RopeBody, __instance);
		}

		internal static void RopeCheckIfDestroyed_Postfix(Rope __instance)
		{
			if (__instance.enabled) return;

			if (!ropeBodies.TryGetValue(__instance, out RopeBody ropeBody)) return;
			ropeBodies.Remove(__instance);

			if (!ropeSlots.TryGetValue(ropeBody, out HookshotInstant hookshotInstant)) return;
			ropeSlots.Remove(ropeBody);

			ChangeAbility(hookAbilityField.GetValue(hookshotInstant) as InstantAbility);
		}

		internal static void RopeInitialize_Postfix(Rope __instance, ref RopeBody __result)
		{
			ropeBodies.Add(__instance, __result);
		}

		// Invisibility randomizing
		internal static void OnInvisibiltyEnded_Postfix(Invisibility __instance)
		{
			ChangeAbility(invisAbilityField.GetValue(__instance) as InstantAbility);
		}
	}
}