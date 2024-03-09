using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BoplFixedMath;
using HarmonyLib;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace AtomGrenade
{
	[BepInPlugin("com.almafa64.AtomGrenade", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	[BepInProcess("BoplBattle.exe")]
	public class Plugin : BaseUnityPlugin
	{
		internal static Harmony harmony;
		internal static ManualLogSource logger;
		internal static ConfigFile config;
		internal static ConfigEntry<double> grenadePower;

		internal static Sprite atomSprite;

		private void Awake()
		{
			harmony = new(Info.Metadata.GUID);
			logger = Logger;
			config = Config;

			grenadePower = config.Bind("Settings", "grenade power multiplier", 1d, "Minimum is 0.0 (negative will set it to 1.0). Maximum somewhere around 5");
			if (grenadePower.Value < 0d) grenadePower.Value = 1d;

			harmony.Patch(
				AccessTools.Method(typeof(GrenadeExplode), nameof(GrenadeExplode.Detonate)), 
				prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.Detonate_Prefix))
			);
			harmony.Patch(
				AccessTools.Method(typeof(GameSessionHandler), "SpawnPlayers"), 
				postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.SpawnPlayers_Postfix))
			);
			
			using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AtomGrenade.atom_grenade.png");
			byte[] buffer = new byte[stream.Length];
			stream.Read(buffer, 0, buffer.Length);
			Texture2D atomTexture = new(256, 256);
			atomTexture.LoadImage(buffer);
			atomSprite = Sprite.Create(atomTexture, new Rect(0, 0, atomTexture.width, atomTexture.height), new Vector2(0.5f, 0.5f), 45);
		}
	}

	class Patches
	{
		public static bool Detonate_Prefix(GrenadeExplode __instance)
		{
			Traverse t = Traverse.Create(__instance);
			t.Field<IPhysicsCollider>("hitbox").Value.Scale *= (Fix)Plugin.grenadePower.Value;
			return true;
		}

		public static void SpawnPlayers_Postfix()
		{
			if (Plugin.grenadePower.Value <= 1d) return;

			// change sprite on prefab and dummy
			foreach (SpriteRenderer renderer in Resources.FindObjectsOfTypeAll<SpriteRenderer>())
			{
				if (renderer.name != "dummyGrenade") continue;
				ThrowItem2 throwItem2 = renderer.transform.parent.GetComponent<ThrowItem2>();
				if(throwItem2.name.Contains("Grenade"))
				{
					throwItem2.ItemPrefab.GetComponent<SpriteRenderer>().sprite = Plugin.atomSprite;
				renderer.sprite = Plugin.atomSprite;
				}
			}
		}
	}
}