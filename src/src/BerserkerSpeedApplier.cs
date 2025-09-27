using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

namespace BerserkerEnemies
{
    /// <summary>
    /// A component that multiplies an enemy's movement and animation speeds by a configurable multiplier.
    /// It caches base values on Awake and reapplies them periodically to ensure other scripts don't
    /// overwrite the speed boost.
    /// </summary>
    [DisallowMultipleComponent]
    public class BerserkerSpeedApplier : MonoBehaviour
    {
        /// <summary>
        /// Multiplier applied to all movement and animation speeds. 6f corresponds to a 500% increase (six times).
        /// </summary>
        public float Multiplier = 6f;

        /// <summary>
        /// How often, in seconds, to reapply the speed boost. Set to zero to apply only once.
        /// </summary>
        public float ReapplyEverySeconds = 1f;

        private NavMeshAgent[] _agents;
        private float[] _baseSpeed;
        private float[] _baseAccel;
        private float[] _baseAngular;

        private Animator[] _anims;
        private float[] _baseAnimSpeed;

        private Component _enemyComponent;
        private readonly List<(FieldInfo field, float baseValue)> _speedFields = new();

        /// <summary>
        /// Cache base values from NavMeshAgents, Animators, and any custom speed-related fields.
        /// </summary>
        private void Awake()
        {
            // Cache NavMeshAgent base values
            _agents = GetComponentsInChildren<NavMeshAgent>(true);
            _baseSpeed = new float[_agents.Length];
            _baseAccel = new float[_agents.Length];
            _baseAngular = new float[_agents.Length];
            for (int i = 0; i < _agents.Length; i++)
            {
                _baseSpeed[i] = _agents[i].speed;
                _baseAccel[i] = _agents[i].acceleration;
                _baseAngular[i] = _agents[i].angularSpeed;
            }

            // Cache Animator base speeds
            _anims = GetComponentsInChildren<Animator>(true);
            _baseAnimSpeed = new float[_anims.Length];
            for (int i = 0; i < _anims.Length; i++)
            {
                _baseAnimSpeed[i] = _anims[i].speed;
            }

            // Attempt to find an "enemy" component and any relevant float fields for movement speed
            foreach (var comp in GetComponentsInChildren<Component>(true))
            {
                var type = comp.GetType();
                if (type.Name.IndexOf("Enemy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    type.Name.IndexOf("AI", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _enemyComponent = comp;
                    foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        if (field.FieldType == typeof(float))
                        {
                            var nameLower = field.Name.ToLower();
                            if (nameLower.Contains("speed") || nameLower.Contains("move"))
                            {
                                try
                                {
                                    float val = (float)field.GetValue(comp);
                                    _speedFields.Add((field, val));
                                }
                                catch
                                {
                                    // ignore fields that cannot be read
                                }
                            }
                        }
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Apply the speed multipliers when the component is enabled and start periodic reapplication if configured.
        /// </summary>
        private void OnEnable()
        {
            Apply();
            if (ReapplyEverySeconds > 0f)
            {
                InvokeRepeating(nameof(Apply), ReapplyEverySeconds, ReapplyEverySeconds);
            }
        }

        /// <summary>
        /// Stop periodic reapplication when disabled.
        /// </summary>
        private void OnDisable()
        {
            CancelInvoke();
        }

        /// <summary>
        /// Apply the multipliers to agents, animators, and custom speed fields.
        /// </summary>
        private void Apply()
        {
            // Apply to NavMeshAgents
            for (int i = 0; i < _agents.Length; i++)
            {
                var agent = _agents[i];
                if (agent == null) continue;
                agent.speed = _baseSpeed[i] * Multiplier;
                agent.acceleration = _baseAccel[i] * Multiplier;
                agent.angularSpeed = Mathf.Min(_baseAngular[i] * Multiplier, 1080f);
            }

            // Apply to Animators
            for (int i = 0; i < _anims.Length; i++)
            {
                var anim = _anims[i];
                if (anim == null) continue;
                anim.speed = _baseAnimSpeed[i] * Multiplier;
            }

            // Apply to other speed-related fields on the enemy component
            if (_enemyComponent != null)
            {
                foreach (var (field, baseVal) in _speedFields)
                {
                    try
                    {
                        field.SetValue(_enemyComponent, baseVal * Multiplier);
                    }
                    catch
                    {
                        // ignore fields that cannot be set
                    }
                }
            }
        }
    }
}