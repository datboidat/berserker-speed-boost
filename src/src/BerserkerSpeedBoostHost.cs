using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace BerserkerSpeedBoostHost
{
    [BepInPlugin("datboidat.BerserkerSpeedBoostHost", "Berserker Speed Boost Host (1.3x)", "1.3.5")]
    [BepInDependency("FNKTLabs.BerserkerEnemies", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        private Harmony _harmony;

        private void Awake()
        {
            // Host-only guard: skip on clients (non-master)
            if (IsClientNotHost())
            {
                Logger.LogInfo("BerserkerSpeedBoostHost: Client detected; skipping speed boost.");
                return;
            }

            _harmony = new Harmony("datboidat.BerserkerSpeedBoostHost");
            bool patched = false;

            // find berserker manager type
            var managerType = AccessTools.AllTypes().FirstOrDefault(t =>
                t.Name.IndexOf("Berserker", StringComparison.OrdinalIgnoreCase) >= 0 &&
                t.Name.IndexOf("Manager", StringComparison.OrdinalIgnoreCase) >= 0);

            if (managerType != null)
            {
                MethodInfo method = AccessTools.Method(managerType, "StartBerserker") ??
                                    AccessTools.Method(managerType, "SetBerserkerValues");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(Plugin).GetMethod(nameof(AfterBerserkerConfigured), BindingFlags.Static | BindingFlags.NonPublic));
                    _harmony.Patch(method, postfix: postfix);
                    Logger.LogInfo($"BerserkerSpeedBoostHost: Patched {managerType.FullName}.{method.Name}");
                    patched = true;
                }
            }

            if (!patched)
            {
                Logger.LogWarning("BerserkerSpeedBoostHost: Could not find Berserker manager type or method to patch.");
            }

            // Start coroutine to unpatch broken HurtCollider patches after load
            StartCoroutine(UnpatchBrokenHurtColliderCoroutine());
        }

        private static bool IsClientNotHost()
        {
            // Check PhotonNetwork.IsMasterClient if available
            var pn = AccessTools.TypeByName("Photon.Pun.PhotonNetwork") ?? AccessTools.TypeByName("PhotonNetwork");
            if (pn == null) return false;
            var prop = pn.GetProperty("IsMasterClient", BindingFlags.Public | BindingFlags.Static);
            if (prop == null) return false;
            var value = prop.GetValue(null, null);
            if (value is bool flag)
            {
                return !flag; // true if not master (client)
            }
            return false;
        }

        private IEnumerator UnpatchBrokenHurtColliderCoroutine()
        {
            // Delay across frames to allow other mods to patch first
            for (int i = 0; i < 5; i++)
            {
                TryUnpatchBrokenHurtCollider();
                yield return null;
            }
        }

        private static void TryUnpatchBrokenHurtCollider()
        {
            try
            {
                var hc = AccessTools.TypeByName("HurtCollider");
                if (hc == null)
                {
                    return;
                }

                var targets = hc.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                .Where(m => m.Name == "EnemyHurt")
                                .ToArray();
                if (targets.Length == 0) return;

                var harmony = new Harmony("datboidat.BerserkerSpeedBoostHost.Unpatcher");
                foreach (var target in targets)
                {
                    var info = Harmony.GetPatchInfo(target);
                    if (info == null) continue;
                    var patches = info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).ToArray();
                    foreach (var p in patches)
                    {
                        var mi = p.PatchMethod;
                        string owner = p.owner ?? "";
                        string asmName = mi?.DeclaringType?.Assembly?.GetName()?.Name ?? "";
                        string fullName = mi?.DeclaringType?.FullName + "." + mi?.Name;
                        bool looksBerserker =
                            owner.IndexOf("Berserker", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            asmName.IndexOf("Berserker", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            (fullName != null && fullName.IndexOf("ModPatch.EnemyHurt", StringComparison.OrdinalIgnoreCase) >= 0);

                        if (looksBerserker && mi != null)
                        {
                            harmony.Unpatch(target, mi);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private static void AfterBerserkerConfigured(object __instance)
        {
            try
            {
                // Try to get berserker transform from known fields/properties
                Transform t =
                    AccessTools.Field(__instance.GetType(), "berserkerChosenTransform")?.GetValue(__instance) as Transform
                    ?? (AccessTools.Field(__instance.GetType(), "berserkerChosen")?.GetValue(__instance) as GameObject)?.transform
                    ?? AccessTools.Property(__instance.GetType(), "BerserkerTransform")?.GetValue(__instance, null) as Transform;

                if (t == null)
                {
                    return;
                }

                var go = t.gameObject;
                var applier = go.GetComponent<BerserkerEnemies.BerserkerSpeedApplier>();
                if (applier == null)
                {
                    applier = go.AddComponent<BerserkerEnemies.BerserkerSpeedApplier>();
                }

                applier.Multiplier = 1.3f; // 30% increase
                applier.ReapplyEverySeconds = 1f;

            }
            catch (Exception)
            {
                // ignore
            }
        }
    }
}
