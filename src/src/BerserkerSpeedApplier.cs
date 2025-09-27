using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

namespace BerserkerEnemies
{
    /// Applies movement & animation speed multipliers and re-applies periodically.
    [DisallowMultipleComponent]
    public class BerserkerSpeedApplier : MonoBehaviour
    {
        public float Multiplier = 6f;              // 500% increase (6×)
        public float ReapplyEverySeconds = 1f;

        NavMeshAgent[] _agents = Array.Empty<NavMeshAgent>();
        float[] _baseSpeed = Array.Empty<float>();
        float[] _baseAccel = Array.Empty<float>();
        float[] _baseAngular = Array.Empty<float>();

        Animator[] _anims = Array.Empty<Animator>();
        float[] _baseAnimSpeed = Array.Empty<float>();

        Component? _enemyComponent = null;
        readonly List<(FieldInfo field, float baseValue)> _speedFields = new();

        void Awake()
        {
            // Cache agents
            _agents = GetComponentsInChildren<NavMeshAgent>(true);
            _baseSpeed = new float[_agents.Length];
            _baseAccel = new float[_agents.Length];
            _baseAngular = new float[_agents.Length];
            for (int i = 0; i < _agents.Length; i++)
            {
                _baseSpeed[i]   = _agents[i].speed;
                _baseAccel[i]   = _agents[i].acceleration;
                _baseAngular[i] = _agents[i].angularSpeed;
            }

            // Cache animators
            _anims = GetComponentsInChildren<Animator>(true);
            _baseAnimSpeed = new float[_anims.Length];
            for (int i = 0; i < _anims.Length; i++)
                _baseAnimSpeed[i] = _anims[i].speed;

            // Try to find an "Enemy-like" component and cache any float fields that look like speeds
            foreach (var comp in GetComponentsInChildren<Component>(true))
            {
                var t = comp.GetType();
                var name = t.Name.ToLowerInvariant();
                if (name.Contains("enemy") || name.Contains("ai"))
                {
                    _enemyComponent = comp;
                    foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        if (f.FieldType == typeof(float))
                        {
                            string n = f.Name.ToLowerInvariant();
                            if (n.Contains("speed") || n.Contains("move"))
                            {
                                try
                                {
                                    float val = (float)f.GetValue(comp);
                                    _speedFields.Add((f, val));
                                }
                                catch { /* ignore */ }
                            }
                        }
                    }
                    break;
                }
            }
        }

        void OnEnable()
        {
            Apply();
            if (ReapplyEverySeconds > 0f)
                InvokeRepeating(nameof(Apply), ReapplyEverySeconds, ReapplyEverySeconds);
        }

        void OnDisable()
        {
            CancelInvoke();
        }

        void Apply()
        {
            // Agents
            for (int i = 0; i < _agents.Length; i++)
            {
                var a = _agents[i];
                if (!a) continue;
                a.speed        = _baseSpeed[i]   * Multiplier;
                a.acceleration = _baseAccel[i]   * Multiplier;
                a.angularSpeed = Mathf.Min(_baseAngular[i] * Multiplier, 1080f);
            }

            // Animations
            for (int i = 0; i < _anims.Length; i++)
            {
                var an = _anims[i];
                if (!an) continue;
                an.speed = _baseAnimSpeed[i] * Multiplier;
            }

            // Enemy custom fields (fallback)
            if (_enemyComponent != null)
            {
                foreach (var (field, baseVal) in _speedFields)
                {
                    try
                    {
                        field.SetValue(_enemyComponent, baseVal * Multiplier);
                    }
                    catch { /* ignore */ }
                }
            }
        }
    }
}
