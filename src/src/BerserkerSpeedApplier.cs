using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

namespace BerserkerEnemies
{
    /// <summary>
    /// Applies speed multipliers continuously so AI state changes can't revert them.
    /// </summary>
    [DisallowMultipleComponent]
    public class BerserkerSpeedApplier : MonoBehaviour
    {
        /// <summary>
        /// Speed multiplier. 6f = 500% increase (six times default).
        /// </summary>
        public float Multiplier = 6f;

        /// <summary>
        /// Unused in the continuous update mode but left for compatibility with other mods that expect this field.
        /// </summary>
        public float ReapplyEverySeconds = 1f;

        NavMeshAgent[] _agents = Array.Empty<NavMeshAgent>();
        Animator[] _anims = Array.Empty<Animator>();

        // Per-agent raw+applied tracking to avoid compounding multipliers.
        float[] _agentRawSpeed = Array.Empty<float>();
        float[] _agentAppliedSpeed = Array.Empty<float>();
        float[] _agentRawAccel = Array.Empty<float>();
        float[] _agentAppliedAccel = Array.Empty<float>();
        float[] _agentRawAngular = Array.Empty<float>();
        float[] _agentAppliedAngular = Array.Empty<float>();

        // Animator speed tracking
        float[] _animRaw = Array.Empty<float>();
        float[] _animApplied = Array.Empty<float>();

        /// <summary>
        /// Animator float parameters that look like speeds (e.g., "chaseSpeed", "attack_speed").
        /// </summary>
        readonly struct ParamRef { public readonly int Hash; public ParamRef(int h) { Hash = h; } }
        Dictionary<Animator, (List<ParamRef> p, List<float> raw, List<float> applied)> _animSpeedParams
            = new Dictionary<Animator, (List<ParamRef>, List<float>, List<float>)>();

        /// <summary>
        /// Enemy component custom float fields that look like speeds or movement rates.
        /// </summary>
        sealed class FieldState
        {
            public Component Comp = null!;
            public FieldInfo Field = null!;
            public float Raw;
            public float Applied;
        }
        readonly List<FieldState> _fieldStates = new List<FieldState>();

        void Awake()
        {
            // Cache NavMeshAgents
            _agents = GetComponentsInChildren<NavMeshAgent>(true);
            int nA = _agents.Length;
            _agentRawSpeed    = new float[nA];
            _agentAppliedSpeed= new float[nA];
            _agentRawAccel    = new float[nA];
            _agentAppliedAccel= new float[nA];
            _agentRawAngular  = new float[nA];
            _agentAppliedAngular = new float[nA];

            for (int i = 0; i < nA; i++)
            {
                var a = _agents[i];
                _agentRawSpeed[i]   = a ? a.speed        : 0f;
                _agentRawAccel[i]   = a ? a.acceleration : 0f;
                _agentRawAngular[i] = a ? a.angularSpeed : 0f;
                _agentAppliedSpeed[i] = float.NaN;
                _agentAppliedAccel[i] = float.NaN;
                _agentAppliedAngular[i] = float.NaN;
            }

            // Cache Animators
            _anims = GetComponentsInChildren<Animator>(true);
            int nAn = _anims.Length;
            _animRaw = new float[nAn];
            _animApplied = new float[nAn];

            for (int i = 0; i < nAn; i++)
            {
                var an = _anims[i];
                _animRaw[i] = an ? an.speed : 0f;
                _animApplied[i] = float.NaN;

                if (!_animSpeedParams.ContainsKey(an))
                {
                    var list = new List<ParamRef>();
                    foreach (var p in an.parameters)
                    {
                        if (p.type == AnimatorControllerParameterType.Float &&
                            p.name.IndexOf("speed", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            list.Add(new ParamRef(Animator.StringToHash(p.name)));
                        }
                    }
                    var raw = new List<float>(new float[list.Count]);
                    var applied = new List<float>(new float[list.Count]);
                    for (int j = 0; j < applied.Count; j++)
                        applied[j] = float.NaN;
                    _animSpeedParams[an] = (list, raw, applied);
                }
            }

            // Cache enemy/AI component float fields containing "speed" or "move"
            foreach (var comp in GetComponentsInChildren<Component>(true))
            {
                var t = comp.GetType();
                string tn = t.Name.ToLowerInvariant();
                if (!(tn.Contains("enemy") || tn.Contains("ai") || tn.Contains("berserker"))) continue;

                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (f.FieldType != typeof(float)) continue;
                    var n = f.Name.ToLowerInvariant();
                    if (n.Contains("speed") || n.Contains("move"))
                    {
                        try
                        {
                            float v = (float)f.GetValue(comp);
                            _fieldStates.Add(new FieldState { Comp = comp, Field = f, Raw = v, Applied = float.NaN });
                        }
                        catch { /* ignore */ }
                    }
                }
            }
        }

        void Update()
        {
            const float EPS = 0.0001f;

            // NavMeshAgents (AI often overrides these each frame; re-apply each frame)
            for (int i = 0; i < _agents.Length; i++)
            {
                var a = _agents[i];
                if (!a) continue;

                // speed
                float cur = a.speed;
                if (float.IsNaN(_agentAppliedSpeed[i]) || Math.Abs(cur - _agentAppliedSpeed[i]) > EPS)
                    _agentRawSpeed[i] = cur;
                float want = _agentRawSpeed[i] * Multiplier;
                if (Math.Abs(cur - want) > EPS) a.speed = want;
                _agentAppliedSpeed[i] = want;

                // acceleration
                cur = a.acceleration;
                if (float.IsNaN(_agentAppliedAccel[i]) || Math.Abs(cur - _agentAppliedAccel[i]) > EPS)
                    _agentRawAccel[i] = cur;
                want = _agentRawAccel[i] * Multiplier;
                if (Math.Abs(cur - want) > EPS) a.acceleration = want;
                _agentAppliedAccel[i] = want;

                // angular speed
                cur = a.angularSpeed;
                if (float.IsNaN(_agentAppliedAngular[i]) || Math.Abs(cur - _agentAppliedAngular[i]) > EPS)
                    _agentRawAngular[i] = cur;
                want = Mathf.Min(_agentRawAngular[i] * Multiplier, 1080f);
                if (Math.Abs(cur - want) > EPS) a.angularSpeed = want;
                _agentAppliedAngular[i] = want;
            }

            // Animator global speed and speed-like float parameters
            for (int i = 0; i < _anims.Length; i++)
            {
                var an = _anims[i];
                if (!an) continue;

                // Animator.speed
                float cur = an.speed;
                if (float.IsNaN(_animApplied[i]) || Math.Abs(cur - _animApplied[i]) > EPS)
                    _animRaw[i] = cur;
                float want = _animRaw[i] * Multiplier;
                if (Math.Abs(cur - want) > EPS) an.speed = want;
                _animApplied[i] = want;

                // Float parameters that include "speed" (often used for attack/chase)
                var pack = _animSpeedParams[an];
                for (int j = 0; j < pack.p.Count; j++)
                {
                    int hash = pack.p[j].Hash;
                    cur = an.GetFloat(hash);
                    if (float.IsNaN(pack.applied[j]) || Math.Abs(cur - pack.applied[j]) > EPS)
                        pack.raw[j] = cur;
                    want = pack.raw[j] * Multiplier;
                    if (Math.Abs(cur - want) > EPS) an.SetFloat(hash, want);
                    pack.applied[j] = want;
                }
                _animSpeedParams[an] = pack; // struct copy back
            }

            // Custom enemy fields
            foreach (var fs in _fieldStates)
            {
                try
                {
                    float cur = (float)fs.Field.GetValue(fs.Comp);
                    if (float.IsNaN(fs.Applied) || Math.Abs(cur - fs.Applied) > EPS)
                        fs.Raw = cur;
                    float want = fs.Raw * Multiplier;
                    if (Math.Abs(cur - want) > EPS) fs.Field.SetValue(fs.Comp, want);
                    fs.Applied = want;
                }
                catch { /* ignore individual failures */ }
            }
        }
    }
}