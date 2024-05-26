﻿using BepInEx;
using BepInEx.Logging;
using BoplFixedMath;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Wormhole
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

		private static readonly FieldInfo massField = typeof(BlackHole).GetField("mass", AccessTools.all);

		private static Fix GetMass(BlackHole blackHole) => (Fix) massField.GetValue(blackHole);

		private static void ConnectToEmptyPair(BlackHole blackHole, bool isWhitehole)
		{
			foreach (KeyValuePair<BlackHole, BlackHole> holePair in holePairs)
			{
				// skip if it's connected, connection is to self
				// or this is white and connection is white (dont connect white to white or black to black)
				if (holePair.Value != null || whiteHoles.Contains(holePair.Key) == isWhitehole || holePair.Key == blackHole)
					continue;

				holePairs[holePair.Key] = blackHole;
				holePairs[blackHole] = holePair.Key;
				break;
			}
		}

		private static void RemoveConnection(BlackHole blackHole)
		{
			try
			{
				BlackHole value = holePairs[blackHole];
				holePairs[blackHole] = null;
				if (value != null) holePairs[value] = null;
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

			Traverse traverse = new(__instance);

			if (collision.layer == traverse.Field("affectorLayer").GetValue<int>())
			{
				BlackHole component = collision.colliderPP.fixTrans.GetComponent<BlackHole>();
				Fix compMass = GetMass(component);
				if (selfMass > compMass || (compMass == selfMass && __instance.spriteRen.sortingOrder > component.spriteRen.sortingOrder))
				{
					// eated another blackhole
					RemoveConnection(component);
					whiteHoles.Remove(component);
					holePairs.Remove(component);
				}
				return true;
			}

			if (collision.layer != traverse.Field("playerLayer").GetValue<int>()) return true;

			// only run original code if this hole isnt paired
			if (!holePairs.TryGetValue(__instance, out BlackHole holePair) || holePair == null) return true;

			PlayerCollision playerCollision = collision.colliderPP.fixTrans.gameObject.GetComponent<PlayerCollision>();
			Traverse playerTraverse = new(playerCollision);
			PlayerBody body = playerTraverse.Field("body").GetValue<PlayerBody>();
			PlayerPhysics physics = playerTraverse.Field("physics").GetValue<PlayerPhysics>();

			physics.UnGround(true, false);
			// ToDo add velocity to forward not up
			body.position = holePair.dCircle.position + Vec2.up * __instance.dCircle.radius;

			return false;
		}

		internal static void BlackHoleGrow_Postfix(BlackHole __instance)
		{
			Traverse traverse = new(__instance);
			Fix mass = GetMass(__instance);

			if (mass >= (Fix)0)
			{
				if (!whiteHoles.Remove(__instance)) return;

				RemoveConnection(__instance);
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

			RemoveConnection(__instance);
			ConnectToEmptyPair(__instance, true);
		}

		internal static void BlackHoleInit_Postfix(BlackHole __instance)
		{
			holePairs.Add(__instance, null);
			ConnectToEmptyPair(__instance, false);
		}
	}
}