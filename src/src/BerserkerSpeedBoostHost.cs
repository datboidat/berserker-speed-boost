using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace BerserkerSpeedBoostHost
{
    [BepInPlugin("datboidat.BerserkerSpeedBoostHost", "Berserker Speed Boost Host (1.3x)", "1.3.4")]
    [BepInDependency("FNKTLabs.BerserkerEnemies", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        private Harmony? _harmony;

        static bool IsClientNotHost()
        {
            var pn = AccessTools.TypeByName("Photon.Pun.PhotonNetwork") ?? AccessTools.TypeByName("PhotonNetwork");
            if (pn == null) return false;
            var prop = pn.GetProperty("IsMasterClient", BindingFlags.Public | BindingFlags.Static);
            if (prop == null) return false;
            var val = prop.GetValue(null, null);
            return val is bool b && b == false;
        }

        private void Awake()
        {
            if (IsClientNotHost())
            {
                Logger.LogInfo("[BerserkerSpeedBoostHost] Client detected; skipping speed boost.");
                return;
            }

            _harmony = new Harmony("datboidat.BerserkerSpeedBoostHost");
            bool patched = false;

            var managerType = AccessTools.AllTypes().FirstOrDefault(t =>
                t.Name.IndexOf("Berserker", StringComparison.OrdinalIgnoreCase) >= 0 &&
                t.Name.IndexOf("Manager", StringComparison.OrdinalIgnoreCase) >= 0);

            if (managerType != null)
            {
                MethodInfo target = AccessTools.Method(managerType, "StartBerserker") ??
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

        private void Start()
        {
            StartCoroutine(UnpatchHurtColliderOverFrames());
        }

        private IEnumerator UnpatchHurtColliderOverFrames()
        {
            for (int i = 0; i < 5; i++)
            {
                TryUnpatchBrokenHurtCollider();
                yield return null;
            }
        }

        private void TryUnpatchBrokenHurtCollider()
        {
            try
            {
                var hc = AccessTools.TypeByName("HurtCollider");
                if (hc == null)
                {
                    Logger.LogWarning("[BerserkerSpeedBoostHost] HurtCollider type not found.");
                    return;
                }

                var targets = hc.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                .Where(m => m.Name == "EnemyHurt")
                                .ToArray();
                if (targets.Length == 0)
                {
                    Logger.LogWarning("[BerserkerSpeedBoostHost] EnemyHurt method not found.");
                    return;
                }

                var unpatchHarmony = new Harmony("datboidat.BSB.UnpatchHurtCollider");
                int totalUnpatched = 0;

                foreach (var target in targets)
                {
                    var info = Harmony.GetPatchInfo(target);
                    if (info == null) continue;

                    var allPatches = info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).ToArray();
                    foreach (var p in allPatches)
                    {
                        var mi = p.Patch;
                        var owner = p.owner ?? "";
                        var asmName = mi?.DeclaringType?.Assembly?.GetName()?.Name ?? "";
                        var full = mi?.DeclaringType?.FullName + "." + mi?.Name;

                        bool looksBerserker =
                            owner.IndexOf("Berserker", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            asmName.IndexOf("Berserker", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            (full ?? "").IndexOf("ModPatch.EnemyHurt", StringComparison.OrdinalIgnoreCase) >= 0;

                        if (looksBerserker && mi != null)
                        {
                            unpatchHarmony.Unpatch(target, mi);
                            totalUnpatched++;
                            Logger.LogInfo($"[BerserkerSpeedBoostHost] Unpatched {full} (owner:{owner}) from {target.DeclaringType?.FullName}.{target.Name}");
                        }
                    }
                }

                if (totalUnpatched == 0)
                {
                    Logger.LogInfo("[BerserkerSpeedBoostHost] No matching HurtCollider patches found to unpatch.");
                }
                else
                {
                    Logger.LogInfo($"[BerserkerSpeedBoostHost] Unpatched {totalUnpatched} broken HurtCollider patch(es).");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BerserkerSpeedBoostHost] Unpatch attempt failed: {ex}");
            }
        }

        private static void AfterBerserkerConfigured(object __instance)
        {
            try
            {
                Transform t =
                    AccessTools.Field(__instance.GetType(), "berserkerChosenTransform")?.GetValue(__instance) as Transform
                    ?? (AccessTools.Field(__instance.GetType(), "berserkerChosen")?.GetValue(__instance) as GameObject)?.transform;

                t ??= AccessTools.Property(__instance.GetType(), "BerserkerTransform")?.GetValue(__instance, null) as Transform;

                if (t == null)
                {
                    var typeName = __instance.GetType().FullName;
                    BepInEx.Logging.Logger.CreateLogSource("BerserkerSpeedBoostHost")
                        .LogWarning($"Could not locate chosen berserker Transform on {typeName}. Speed boost not applied.");
                    return;
                }

                var go = t.gameObject;
                var applier = go.GetComponent<BerserkerEnemies.BerserkerSpeedApplier>();
                if (applier == null) applier = go.AddComponent<BerserkerEnemies.BerserkerSpeedApplier>();

                applier.Multiplier = 1.3f;
                applier.ReapplyEverySeconds = 1f;

                BepInEx.Logging.Logger.CreateLogSource("BerserkerSpeedBoostHost")
                    .LogInfo($"Applied 1.3x speed to berserker on {go.name}.");
            }
            catch (Exception ex)
            {
                BepInEx.Logging.Logger.CreateLogSource("BerserkerSpeedBoostHost")
                    .LogWarning($"Failed to apply speed boost: {ex}");
            }
        }
    }
}
