using BoplFixedMath;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Wormhole
{
	internal class Patches
	{
		internal static readonly HashSet<BlackHole> whiteHoles = [];
		internal static readonly Dictionary<BlackHole, BlackHole> holePairs = [];

		private static readonly FieldInfo massField = GetField<BlackHole>("mass");
		private static readonly FieldInfo affectorLayerField = GetField<BlackHole>("affectorLayer");
		private static readonly FieldInfo playerLayerField = GetField<BlackHole>("playerLayer");

		private static readonly FieldInfo bodyField = GetField<PlayerCollision>("body");
		private static readonly FieldInfo physicsField = GetField<PlayerCollision>("physics");

		private static FieldInfo GetField<T>(string name) => typeof(T).GetField(name, AccessTools.all);

		private static Fix GetMass(BlackHole blackHole) => (Fix)massField.GetValue(blackHole);

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
					if (tryReconnect) ConnectToEmptyPair(value, whiteHoles.Contains(value));
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

		private static void TeleportPlayer(GameObject collidedObject, BlackHole pair)
		{
			PlayerCollision playerCollision = collidedObject.GetComponent<PlayerCollision>();

			if (playerCollision == null)
			{
				// player is ball
				BoplBody body = collidedObject.GetComponent<BoplBody>();
				body.position = pair.dCircle.position + Vec2.NormalizedSafe(body.velocity) * pair.dCircle.radius;
				return;
			}

			PlayerBody playerBody = bodyField.GetValue(playerCollision) as PlayerBody;
			PlayerPhysics physics = physicsField.GetValue(playerCollision) as PlayerPhysics;

			physics.UnGround(true, false);
			playerBody.position = pair.dCircle.position + Vec2.NormalizedSafe(playerBody.Velocity) * pair.dCircle.radius;
		}

		private static void TeleportObject(GameObject collidedObject, BlackHole pair)
		{
			BoplBody body = collidedObject.GetComponent<BoplBody>();
			body.position = pair.dCircle.position + Vec2.NormalizedSafe(body.velocity) * pair.dCircle.radius * (pair.influenceRadiusMultiplier / (Fix)1.5);
			body.velocity = body.StartVelocity;
		}

		private static void EatOtherHole(GameObject collidedObject, BlackHole blackHole, Fix mass)
		{
			BlackHole component = collidedObject.GetComponent<BlackHole>();
			Fix compMass = GetMass(component);
			if (mass > compMass || (compMass == mass && blackHole.spriteRen.sortingOrder > component.spriteRen.sortingOrder))
			{
				// eated another blackhole
				RemoveConnection(component, false);
				whiteHoles.Remove(component);
				holePairs.Remove(component);
			}
		}

		private static void TeleportPlatform(GameObject collidedObject, BlackHole blackHole)
		{
			// --- walls ---
			/*// ToDo set platform position
			PlatformApi.PlatformApi.SetPos();
			PlatformApi.PlatformApi.SetHome();*/
		}

		internal static bool BlackHoleCollide_Prefix(BlackHole __instance, CollisionInformation collision)
		{
			Fix mass = GetMass(__instance);
			if (mass < Fix.Zero) return true;

			GameObject collidedObject = collision.colliderPP.fixTrans.gameObject;

			if (collision.layer == (int)affectorLayerField.GetValue(__instance))
			{
				EatOtherHole(collidedObject, __instance, mass);
				return true;
			}

			// only run original code if this hole isnt paired
			if (!holePairs.TryGetValue(__instance, out BlackHole holePair) || holePair == null) return true;

			if (collision.layer == LayerMask.NameToLayer("wall"))
			{
				TeleportPlatform(collidedObject, __instance);
				return false;
			}

			Plugin.logger.LogWarning($"{collidedObject}: {string.Join("\n", collidedObject.GetComponents<object>())}");

			if (collision.layer != (int)playerLayerField.GetValue(__instance))
			{
				if (collision.colliderPP.monobehaviourCollider.inverseMass <= Fix.Zero) return true;

				TeleportObject(collidedObject, holePair);
				return false;
			}

			TeleportPlayer(collidedObject, holePair);
			return false;
		}

		internal static void BlackHoleGrow_Postfix(BlackHole __instance)
		{
			Fix mass = GetMass(__instance);

			if (mass >= Fix.Zero)
			{
				if (!whiteHoles.Remove(__instance)) return;

				RemoveConnection(__instance, true);
				ConnectToEmptyPair(__instance, false);
				Object.Destroy(__instance.gameObject.GetComponent<LineRenderer>());

				return;
			}

			if (!whiteHoles.Add(__instance)) return;

			// ---- debug ----
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
