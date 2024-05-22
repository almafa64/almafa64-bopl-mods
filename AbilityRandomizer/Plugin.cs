using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;

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
		}
	}

	class Patches
	{
		internal static void ExitAbility_Postfix(Ability __instance, AbilityExitInfo exitInfo)
		{
			IAbilityComponent[] components = new Traverse(__instance).Field("abilityComponents").GetValue<IAbilityComponent[]>();
			
			// when ability is tesla coil only run randomizer after placing 2. tesla coil
			if (components[0] is PlantObjectOnPlatform placer &&
				new Traverse(placer).Field("placeObject").GetValue<IPlaceObject>() is PlaceSparkNode sparkNode &&
				new Traverse(sparkNode).Field("plantBuffer").GetValue<RingBuffer<SimpleSparkNode>>()[1] == null)
			{
				return;
			}

			Plugin.logger.LogWarning($"used: '{__instance.name}'");
			foreach(var a in typeof(AbilityExitInfo).GetFields())
			{
				Plugin.logger.LogWarning("\t-" + a.Name + ": " + a.GetValue(exitInfo));
			}
			Plugin.logger.LogWarning("components:");
			foreach(var a in __instance.GetComponents<IAbilityComponent>())
			{
				Plugin.logger.LogWarning("\t-" + a);
			}
		}
	}
}