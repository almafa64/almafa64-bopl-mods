using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BoplFixedMath;
using HarmonyLib;
using System.Reflection;
using UnityEngine.SceneManagement;

namespace AtomGrenade
{
	[BepInPlugin("com.almafa64.AtomGrenade", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	public class Plugin : BaseUnityPlugin
	{
		internal static Harmony harmony;
		internal static ManualLogSource logger;
		internal static ConfigFile config;
		internal static ConfigEntry<int> grenadePower;

		private void Awake()
		{
			harmony = new(Info.Metadata.GUID);
			logger = Logger;
			config = Config;
			SceneManager.sceneLoaded += OnSceneLoad;

			grenadePower = config.Bind("Settings", "grenade power multiplier", 1, "Maximum somewhere around 5");

			MethodInfo detonate = AccessTools.Method(typeof(GrenadeExplode), nameof(GrenadeExplode.Detonate));
			HarmonyMethod detonatePatch = new(typeof(Patches), nameof(Patches.Detonate_Prefix));
			harmony.Patch(detonate, prefix: detonatePatch);
		}

		private void OnSceneLoad(Scene scene, LoadSceneMode mode)
		{

		}
	}

	class Patches
	{
		public static bool Detonate_Prefix(GrenadeExplode __instance)
		{
			Traverse t = Traverse.Create(__instance);
			t.Field<IPhysicsCollider>("hitbox").Value.Scale *= new Fix(Plugin.grenadePower.Value);
			return true;
		}
	}
}