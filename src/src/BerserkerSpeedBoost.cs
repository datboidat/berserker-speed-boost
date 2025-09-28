using System;
using System.Collections;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace BerserkerSpeedBoost
{
    [BepInPlugin("datboidat.BerserkerSpeedBoost", "Berserker Speed Boost (6x)", "1.3.0")]
    [BepInDependency("BerserkerEnemies", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin
    {
        private Harmony _harmony;
        private Coroutine _patchCoroutine;

        private void Awake()
        {
            _harmony = new Harmony("datboidat.BerserkerSpeedBoost");
            _patchCoroutine = StartCoroutine(PatchWhenLoaded());
            Logger.LogInfo("BerserkerSpeedBoost plugin loaded; waiting for BerserkerController...");
        }

        private IEnumerator PatchWhenLoaded()
        {
            Type controllerType = null;
            for (int i = 0; i < 60; i++)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    controllerType = assembly.GetType("BerserkerController")
                        ?? assembly.GetType("BerserkerEnemies.BerserkerController")
                        ?? assembly.GetType("hypn.BerserkerEnemies.BerserkerController");
                    if (controllerType != null)
                        break;
                }
                if (controllerType != null)
                    break;
                yield return null;
            }

            if (controllerType == null)
            {
                Logger.LogWarning("Could not find BerserkerController to patch. Speed boost will not run.");
                yield break;
            }

            MethodInfo target = AccessTools.Method(controllerType, "Awake")
                ?? AccessTools.Method(controllerType, "Start");

            if (target != null)
            {
                MethodInfo postfix = typeof(Plugin).GetMethod(nameof(ControllerAwakePostfix),
                    BindingFlags.NonPublic | BindingFlags.Static);
                _harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                Logger.LogInfo($"Patched {controllerType.FullName}.{target.Name}()");
            }
            else
            {
                Logger.LogWarning($"Could not find Awake/Start on {controllerType.FullName}");
            }
        }

        private static void ControllerAwakePostfix(MonoBehaviour __instance)
        {
            try
            {
                var go = __instance.gameObject;
                var applier = go.GetComponent<BerserkerEnemies.BerserkerSpeedApplier>();
                if (applier == null) applier = go.AddComponent<BerserkerEnemies.BerserkerSpeedApplier>();
                applier.Multiplier = 6f;
                applier.ReapplyEverySeconds = 1f;
                BepInEx.Logging.Logger.CreateLogSource("BerserkerSpeedBoost")
                    .LogInfo($"Attached speed applier to {go.name}");
            }
            catch (Exception ex)
            {
                BepInEx.Logging.Logger.CreateLogSource("BerserkerSpeedBoost")
                    .LogWarning($"Failed to apply speed boost: {ex}");
            }
        }
    }
}
