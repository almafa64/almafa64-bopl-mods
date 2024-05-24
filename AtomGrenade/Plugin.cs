using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BoplFixedMath;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

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
		internal static Sprite atomSheet;

		private void Awake()
		{
			harmony = new(Info.Metadata.GUID);
			logger = Logger;
			config = Config;

			grenadePower = config.Bind("Settings", "grenade power multiplier", 5d, "Minimum is 0.0 (negative will set it to 1.0). Maximum somewhere around 5");
			if (grenadePower.Value < 1d) grenadePower.Value = 1d;

			SceneManager.sceneLoaded += OnSceneLoaded;

			harmony.Patch(
				AccessTools.Method(typeof(GrenadeExplode), nameof(GrenadeExplode.Detonate)), 
				prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.Detonate_Prefix))
			);
			harmony.Patch(
				AccessTools.Method(typeof(GameSessionHandler), "SpawnPlayers"), 
				postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.SpawnPlayers_Postfix))
			);

			/*harmony.Patch(
				AccessTools.Method(typeof(), nameof()),
				postfix: new(typeof(Patches), nameof(Patches.Test_Postfix))
			);*/

			Texture2D atomTexture = Utils.LoadDLLTexture("AtomGrenade.atom_grenade.png");
			atomSprite = Sprite.Create(atomTexture, new Rect(0, 0, atomTexture.width, atomTexture.height), new Vector2(0.5f, 0.5f), 45);

			Texture2D atomSheetTexture = Utils.LoadDLLTexture("AtomGrenade.atom_sheet.png");
			atomSheet = Sprite.Create(atomSheetTexture, new Rect(0, 0, atomSheetTexture.width, atomSheetTexture.height), new Vector2(0.5f, 0.5f), 45);
		}

		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			if (scene.name.Contains("Select")) ChSelectSceneLoaded();
		}

		private void ChSelectSceneLoaded()
		{
			// change og sprite sheet to mine
			foreach(AbilityGrid grid in FindObjectsOfType<AbilityGrid>())
			{
				new Traverse(grid).Field<AbilityGridEntry[]>("grid").Value[2].SetSprite(atomSprite);
			}
		}
	}

	class Patches
	{
		public static bool Detonate_Prefix(GrenadeExplode __instance)
		{
			// make hitbox bigger/smaller
			new Traverse(__instance).Field<IPhysicsCollider>("hitbox").Value.Scale *= (Fix)Plugin.grenadePower.Value;
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

		public static void Test_Postfix()
		{
			Plugin.logger.LogWarning($"something");
		}
	}
}