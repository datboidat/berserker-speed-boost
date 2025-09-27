using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace BerserkerSpeedBoost
{
    /// <summary>
    /// BepInEx plugin that multiplies berserker movement and animation speeds by attaching a
    /// BerserkerSpeedApplier component to the chosen berserker when it is started.
    /// </summary>
    [BepInPlugin("datboidat.BerserkerSpeedBoost", "Berserker Speed Boost", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            var harmony = new Harmony("datboidat.BerserkerSpeedBoost");
            harmony.PatchAll();
            Logger.LogInfo("Berserker Speed Boost plugin loaded");
        }

        /// <summary>
        /// Harmony patch targeting BerserkerManager.StartBerserker. After a berserker is started,
        /// this Postfix attaches the BerserkerSpeedApplier component to the chosen transform and
        /// configures its multiplier.
        /// </summary>
        [HarmonyPatch]
        private static class StartBerserkerPatch
        {
            static MethodBase TargetMethod()
            {
                var managerType = AccessTools.TypeByName("BerserkerManager");
                return AccessTools.Method(managerType, "StartBerserker");
            }

            static void Postfix()
            {
                // Obtain BerserkerManager type and the static field that holds the chosen transform
                var managerType = AccessTools.TypeByName("BerserkerManager");
                if (managerType == null) return;

                var field = AccessTools.Field(managerType, "berserkerChosenTransform");
                if (field == null) return;

                var chosenTransform = field.GetValue(null) as Transform;
                if (chosenTransform == null) return;

                // Attach or retrieve the BerserkerSpeedApplier on the chosen transform
                var applier = chosenTransform.gameObject.GetComponent<BerserkerEnemies.BerserkerSpeedApplier>();
                if (applier == null)
                {
                    applier = chosenTransform.gameObject.AddComponent<BerserkerEnemies.BerserkerSpeedApplier>();
                }

                // Set the multiplier to 6f (500% increase) and optionally adjust reapply interval
                applier.Multiplier = 6f;
                applier.ReapplyEverySeconds = 1f;
            }
        }
    }
}