using BepInEx;
using BepInEx.Logging;
using BoplFixedMath;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
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
				LineRenderer lineRenderer = whiteHole.gameObject.GetComponent<LineRenderer>();
				lineRenderer.SetPosition(0, Vector3.zero);
				lineRenderer.SetPosition(1, Vector3.zero);

				BlackHole pair = Patches.holePairs[whiteHole];
				if (pair == null) continue;
				lineRenderer.SetPosition(0, pair.transform.transform.position);
				lineRenderer.SetPosition(1, whiteHole.transform.transform.position);
			}
		}
	}

	class Patches
	{
		internal static readonly HashSet<BlackHole> whiteHoles = [];
		internal static readonly Dictionary<BlackHole, BlackHole> holePairs = [];

		private static readonly FieldInfo massField = GetField<BlackHole>("mass");
		private static readonly FieldInfo affectorLayerField = GetField<BlackHole>("affectorLayer");
		private static readonly FieldInfo playerLayerField = GetField<BlackHole>("playerLayer");

		private static readonly FieldInfo bodyField = GetField<PlayerCollision>("body");
		private static readonly FieldInfo physicsField = GetField<PlayerCollision>("physics");

		private static FieldInfo GetField<T>(string name) => typeof(T).GetField(name, AccessTools.all);

		private static Fix GetMass(BlackHole blackHole) => (Fix) massField.GetValue(blackHole);

		private static void ConnectToEmptyPair(BlackHole blackHole, bool isWhitehole)
		{
			foreach (KeyValuePair<BlackHole, BlackHole> holePair in holePairs)
			{
				// skip if it's connected or connection is self
				// or this is white and connection is white (dont connect white to white or black to black)
				if (holePair.Value != null || whiteHoles.Contains(holePair.Key) == isWhitehole || holePair.Key == blackHole)
					continue;

				holePairs[holePair.Key] = blackHole;
				holePairs[blackHole] = holePair.Key;
				break;
			}
		}

		private static void RemoveConnection(BlackHole blackHole, bool tryReconnect)
		{
			try
			{
				BlackHole value = holePairs[blackHole];
				if (value != null)
				{
					holePairs[value] = null;
					if(tryReconnect) ConnectToEmptyPair(value, whiteHoles.Contains(value));
				}
				holePairs[blackHole] = null;
			}
			catch (KeyNotFoundException) { }
		}

		internal static void SpawnPlayers_Postfix()
		{
			holePairs.Clear();
			whiteHoles.Clear();
		}

		internal static bool BlackHoleCollide_Prefix(BlackHole __instance, CollisionInformation collision)
		{
			Fix selfMass = GetMass(__instance);
			if (selfMass < (Fix)0) return true;

			if (collision.layer == (int)affectorLayerField.GetValue(__instance))
			{
				BlackHole component = collision.colliderPP.fixTrans.GetComponent<BlackHole>();
				Fix compMass = GetMass(component);
				if (selfMass > compMass || (compMass == selfMass && __instance.spriteRen.sortingOrder > component.spriteRen.sortingOrder))
				{
					// eated another blackhole
					RemoveConnection(component, false);
					whiteHoles.Remove(component);
					holePairs.Remove(component);
				}
				return true;
			}

			/*// ToDo set platform position
			PlatformApi.PlatformApi.SetPos();
			PlatformApi.PlatformApi.SetHome();*/

			if (collision.layer != (int)playerLayerField.GetValue(__instance)) return true;

			// only run original code if this hole isnt paired
			if (!holePairs.TryGetValue(__instance, out BlackHole holePair) || holePair == null) return true;

			PlayerCollision playerCollision = collision.colliderPP.fixTrans.gameObject.GetComponent<PlayerCollision>();
			PlayerBody body = bodyField.GetValue(playerCollision) as PlayerBody;
			PlayerPhysics physics = physicsField.GetValue(playerCollision) as PlayerPhysics;

			physics.UnGround(true, false);
			body.position = holePair.dCircle.position + Vec2.NormalizedSafe(body.Velocity) * __instance.dCircle.radius;

			return false;
		}

		internal static void BlackHoleGrow_Postfix(BlackHole __instance)
		{
			Fix mass = GetMass(__instance);

			if (mass >= (Fix)0)
			{
				if (!whiteHoles.Remove(__instance)) return;

				RemoveConnection(__instance, true);
				ConnectToEmptyPair(__instance, false);
				Object.Destroy(__instance.gameObject.GetComponent<LineRenderer>());

				return;
			}

			if (!whiteHoles.Add(__instance)) return;

			LineRenderer lineRenderer = __instance.gameObject.AddComponent<LineRenderer>();
			lineRenderer.material = new Material(Shader.Find("Hidden/Internal-Colored"));
			lineRenderer.endColor = lineRenderer.startColor = Color.red;
			lineRenderer.endWidth = lineRenderer.startWidth = 0.2f;
			lineRenderer.positionCount = 2;

			RemoveConnection(__instance, true);
			ConnectToEmptyPair(__instance, true);
		}

		internal static void BlackHoleInit_Postfix(BlackHole __instance)
		{
			holePairs.Add(__instance, null);
			ConnectToEmptyPair(__instance, false);
		}
	}
}