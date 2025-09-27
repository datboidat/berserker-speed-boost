using System;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace BerserkerSpeedBoost
{
    [BepInPlugin("datboidat.BerserkerSpeedBoost", "Berserker Speed Boost", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            var harmony = new Harmony("datboidat.BerserkerSpeedBoost");
            harmony.PatchAll();
            Logger.LogInfo("Berserker Speed Boost loaded");
        }

        [HarmonyPatch]
        private class BerserkerSetValuesPatch
        {
            static MethodBase TargetMethod()
            {
                var managerType = AccessTools.TypeByName("BerserkerManager");
                return AccessTools.Method(managerType, "SetBerserkerValues", new Type[] { AccessTools.TypeByName("EnemyParent"), typeof(Transform), typeof(int) });
            }

            static void Postfix(Transform enemy)
            {
                ApplySpeedBoost(enemy.gameObject);
            }
        }

        private static void ApplySpeedBoost(GameObject enemyObj)
        {
            var berserkerType = AccessTools.TypeByName("BerserkerController");
            if (berserkerType == null) return;

            var berserkerComp = enemyObj.GetComponent(berserkerType);
            if (berserkerComp == null) return;

            bool isBerserker = false;
            var field = AccessTools.Field(berserkerType, "isBerserkerFlag");
            if (field != null)
            {
                isBerserker = (bool)field.GetValue(berserkerComp);
            }
            if (!isBerserker) return;

            var agent = enemyObj.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.speed *= 1.3f;
                agent.acceleration *= 1.3f;
            }

            foreach (var anim in enemyObj.GetComponentsInChildren<Animator>())
            {
                anim.speed *= 1.3f;
            }
        }
    }
}
