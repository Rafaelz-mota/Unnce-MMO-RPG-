using System.Collections.Generic;
using UnityEngine;

namespace Mmorpg.Game
{
    /// <summary>
    /// Animação por código dos modelos feitos de primitivas: balanço de pernas e braços ao andar, respiração parada,
    /// avanço do corpo ao atacar e peças que giram (aura, orbitais). Só apresentação: lê a posição na tela.
    /// </summary>
    public sealed class ProceduralAnim : MonoBehaviour
    {
        public Transform Body;
        public Transform[] Legs;      // pares alternados: índice par e ímpar balançam em sentidos opostos
        public Transform[] Arms;
        public Transform[] Spinners;  // giram em torno do eixo vertical
        public float StrideSpeed = 7f;
        public float LegSwing = 38f;
        public float ArmSwing = 30f;
        public bool Active = true;
        /// <summary>Animator de um modelo real: quando existe, ele anima no lugar do balanço por código.</summary>
        public Animator Rig;
        private HashSet<string> _rigParams;
        public bool HasRig { get { return Rig != null; } }

        /// <summary>True se o Animator tem o parâmetro (o controlador pode ter só alguns dos opcionais).</summary>
        private bool Has(string name)
        {
            if (Rig == null) return false;
            if (_rigParams == null)
            {
                _rigParams = new HashSet<string>();
                AnimatorControllerParameter[] ps = Rig.parameters;
                if (ps != null) for (int i = 0; i < ps.Length; i++) _rigParams.Add(ps[i].name);
            }
            return _rigParams.Contains(name);
        }

        public void Hit() { if (Has("Hit")) Rig.SetTrigger("Hit"); }

        public void SetDead(bool dead) { if (Has("Dead")) Rig.SetBool("Dead", dead); }

        /// <summary>True se o controlador do modelo tem a animação de morte (então não se deita o corpo por código).</summary>
        public bool RigHandlesDeath { get { return Has("Dead"); } }

        private Vector3 _last;
        private bool _hasLast;
        private float _phase;
        private float _attack;
        private float _speed;

        public void Lunge() { _attack = 0.25f; if (Has("Attack")) Rig.SetTrigger("Attack"); }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 pos = transform.position;
            if (_hasLast)
            {
                float dx = pos.x - _last.x, dz = pos.z - _last.z;
                float v = Mathf.Sqrt(dx * dx + dz * dz) / dt;
                _speed = Mathf.Lerp(_speed, Mathf.Clamp(v, 0f, 12f), 1f - Mathf.Exp(-12f * dt));
            }
            _last = pos;
            _hasLast = true;

            if (Rig != null)
            {
                if (Has("Speed")) Rig.SetFloat("Speed", _speed);
                return; // o Animator do modelo real cuida do resto
            }

            if (!Active) return;

            float walk = Mathf.Clamp01(_speed / 2f);
            _phase += _speed * StrideSpeed * 0.25f * dt * 3f;
            float swing = Mathf.Sin(_phase) * walk;

            if (Legs != null)
                for (int i = 0; i < Legs.Length; i++)
                    if (Legs[i] != null) Legs[i].localRotation = Quaternion.Euler((i % 2 == 0 ? swing : -swing) * LegSwing, 0f, 0f);

            float attackK = 0f;
            if (_attack > 0f) { _attack -= dt; attackK = Mathf.Clamp01(_attack / 0.25f); }

            if (Arms != null)
                for (int i = 0; i < Arms.Length; i++)
                    if (Arms[i] != null)
                    {
                        float a = (i % 2 == 0 ? -swing : swing) * ArmSwing - attackK * 85f * (i == 0 ? 1f : 0.5f);
                        Arms[i].localRotation = Quaternion.Euler(a, 0f, 0f);
                    }

            if (Body != null)
            {
                float bob = Mathf.Abs(Mathf.Sin(_phase)) * 0.07f * walk + Mathf.Sin(Time.time * 2f) * 0.012f * (1f - walk);
                Body.localPosition = new Vector3(0f, bob, attackK * 0.35f);
            }

            if (Spinners != null)
                for (int i = 0; i < Spinners.Length; i++)
                    if (Spinners[i] != null) Spinners[i].localRotation = Quaternion.Euler(0f, Time.time * (60f + 25f * i), 0f);
        }
    }
}
