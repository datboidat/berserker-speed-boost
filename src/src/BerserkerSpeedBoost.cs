using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace BerserkerSpeedBoost
{
    [BepInPlugin("datboidat.BerserkerSpeedBoost", "Berserker Speed Boost (6x)", "1.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        private Harmony? _harmony;

        private void Awake()
        {
            _harmony = new Harmony("datboidat.BerserkerSpeedBoost");
            var patchedAny = false;

            // 1) Find a type whose name looks like the Berserker manager.
            var berserkerManagerType =
                AccessTools.AllTypes()
                    .FirstOrDefault(t =>
                        t.Name.Contains("Berserker", StringComparison.OrdinalIgnoreCase) &&
                        t.Name.Contains("Manager", StringComparison.OrdinalIgnoreCase));

            if (berserkerManagerType == null)
            {
                Logger.LogWarning("Could not find a Berserker *Manager* type to patch. Speed boost will not run.");
                return;
            }

            // 2) Try to patch one of the known methods that runs after the berserker is chosen/configured.
            //    Try StartBerserker first, then fall back to SetBerserkerValues.
            MethodInfo? target =
                AccessTools.Method(berserkerManagerType, "StartBerserker") ??
                AccessTools.Method(berserkerManagerType, "SetBerserkerValues");

            if (target != null)
            {
                var postfix = new HarmonyMethod(typeof(Plugin).GetMethod(nameof(AfterBerserkerConfigured),
                    BindingFlags.Static | BindingFlags.NonPublic));
                _harmony.Patch(target, postfix: postfix);
                Logger.LogInfo($"Patched {berserkerManagerType.FullName}.{target.Name}()");
                patchedAny = true;
            }

            if (!patchedAny)
            {
                Logger.LogWarning($"Found type {berserkerManagerType.FullName} but neither StartBerserker nor SetBerserkerValues were present.");
            }
        }

        private static void AfterBerserkerConfigured(object __instance)
        {
            try
            {
                Transform? t =
                    AccessTools.Field(__instance.GetType(), "berserkerChosenTransform")?.GetValue(__instance) as Transform
                    ?? (AccessTools.Field(__instance.GetType(), "berserkerChosen")?.GetValue(__instance) as GameObject)?.transform;

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
                applier.Multiplier = 6f;
                applier.ReapplyEverySeconds = 1f;

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
