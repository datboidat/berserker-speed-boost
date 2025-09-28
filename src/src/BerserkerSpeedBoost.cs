using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace BerserkerSpeedBoost
{
    [BepInPlugin("datboidat.BerserkerSpeedBoost", "Berserker Speed Boost (6x)", "1.3.1")]
    [BepInDependency("FNKTLabs.BerserkerEnemies", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        private Harmony? _harmony;

        private void Awake()
        {
            _harmony = new Harmony("datboidat.BerserkerSpeedBoost");
            bool patched = false;

            // Find a type whose name looks like the Berserker manager
            var managerType = AccessTools.AllTypes().FirstOrDefault(t =>
                t.Name.Contains("Berserker", StringComparison.OrdinalIgnoreCase) &&
                t.Name.Contains("Manager", StringComparison.OrdinalIgnoreCase));

            if (managerType != null)
            {
                // Try StartBerserker first, then SetBerserkerValues
                MethodInfo? target = AccessTools.Method(managerType, "StartBerserker") ??
                                     AccessTools.Method(managerType, "SetBerserkerValues");

                if (target != null)
                {
                    var postfix = new HarmonyMethod(typeof(Plugin).GetMethod(nameof(AfterBerserkerConfigured), BindingFlags.NonPublic | BindingFlags.Static));
                    _harmony.Patch(target, postfix: postfix);
                    Logger.LogInfo($"Patched {managerType.FullName}.{target.Name}()");
                    patched = true;
                }
            }

            if (!patched)
            {
                Logger.LogWarning("Could not find a Berserker *Manager* type to patch. Speed boost will not run.");
            }
        }

        // This runs after the target method; we attach our applier onto the chosen berserker
        private static void AfterBerserkerConfigured(object __instance)
        {
            try
            {
                // Try common field names first
                Transform? t =
                    AccessTools.Field(__instance.GetType(), "berserkerChosenTransform")?.GetValue(__instance) as Transform
                    ?? (AccessTools.Field(__instance.GetType(), "berserkerChosen")?.GetValue(__instance) as GameObject)?.transform;

                // If we still didn't get a transform, try a property
                t ??= AccessTools.Property(__instance.GetType(), "BerserkerTransform")?.GetValue(__instance, null) as Transform;

                if (t == null)
                {
                    var typeName = __instance.GetType().FullName;
                    BepInEx.Logging.Logger.CreateLogSource("BerserkerSpeedBoost")
                        .LogWarning($"Could not locate chosen berserker Transform on {typeName}. Speed boost not applied.");
                    return;
                }

                var go = t.gameObject;
                var applier = go.GetComponent<BerserkerEnemies.BerserkerSpeedApplier>();
                if (applier == null) applier = go.AddComponent<BerserkerEnemies.BerserkerSpeedApplier>();
                applier.Multiplier = 6f;                 // 500% increase (6x)
                applier.ReapplyEverySeconds = 1f;        // keep overrides sticky

                // Optional: log once when applied so the user can confirm in console
                BepInEx.Logging.Logger.CreateLogSource("BerserkerSpeedBoost")
                    .LogInfo($"Applied 6x speed to berserker on {go.name}.");
            }
            catch (Exception ex)
            {
                BepInEx.Logging.Logger.CreateLogSource("BerserkerSpeedBoost")
                    .LogWarning($"Failed to apply speed boost: {ex}");
            }
        }
    }
}
