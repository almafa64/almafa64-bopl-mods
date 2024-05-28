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

		private static bool TeleportPlayer(GameObject collidedObject, BlackHole pair)
		{
			PlayerCollision playerCollision = collidedObject.GetComponent<PlayerCollision>();

			if (playerCollision == null)
			{
				// player is ball
				BoplBody body = collidedObject.GetComponent<BoplBody>();
				body.position = pair.dCircle.position + Vec2.NormalizedSafe(body.velocity) * pair.dCircle.radius;
				return false;
			}

			PlayerBody playerBody = bodyField.GetValue(playerCollision) as PlayerBody;
			PlayerPhysics physics = physicsField.GetValue(playerCollision) as PlayerPhysics;

			playerBody.ropeBody?.Dettach(true); // dettach rope
			physics?.UnGround(true, false); // if physics null then player is drilling else unground player

			playerBody.position = pair.dCircle.position + Vec2.NormalizedSafe(playerBody.Velocity) * pair.dCircle.radius;

			// exit roll if rolling
			//collidedObject.GetComponent<Roll>()?.ExitAbility(default);

			return false;
		}

		private static bool TeleportObject(GameObject collidedObject, BlackHole pair)
		{
			// let blackhole destroy spike
			if (collidedObject.GetComponent<SpikeAttack>() != null) return true;

			BoplBody body = collidedObject.GetComponent<BoplBody>();
			Vec2 normVel = Vec2.NormalizedSafe(body.velocity);
			
			// ToDo change influenceRadiusMultiplier, it gets too big on big holes
			body.position = pair.dCircle.position + normVel * pair.dCircle.radius * (pair.influenceRadiusMultiplier / (Fix)1.5);
			body.velocity /= pair.influenceRadiusMultiplier;
			return false;
		}

		private static bool TeleportPlatform(GameObject collidedObject, BlackHole pair)
		{
			BoplBody body = collidedObject.GetComponent<BoplBody>();

			Vec2 pos = pair.dCircle.position + Vec2.NormalizedSafe(body.velocity) * pair.dCircle.radius;

			PlatformApi.PlatformApi.SetPos(collidedObject, pos);
			PlatformApi.PlatformApi.SetHome(collidedObject, pos);

			return false;
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
				return TeleportPlatform(collidedObject, holePair);

			if (collision.layer == (int)playerLayerField.GetValue(__instance))
				return TeleportPlayer(collidedObject, holePair);

			if (collision.colliderPP.monobehaviourCollider.inverseMass > Fix.Zero)
				return TeleportObject(collidedObject, holePair);

			return true;
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

		internal static bool SmokeOnCollide_Prefix(CollisionInformation collision)
		{
			BlackHole blackHole = collision.colliderPP.fixTrans.GetComponent<BlackHole>();
			if (blackHole == null) return true;

			// only run original if collided is a hole or isnt connected
			return !holePairs.TryGetValue(blackHole, out BlackHole holePair) || holePair == null;
		}
	}
}
