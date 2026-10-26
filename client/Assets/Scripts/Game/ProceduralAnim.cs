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

        private Vector3 _last;
        private bool _hasLast;
        private float _phase;
        private float _attack;
        private float _speed;

        public void Lunge() { _attack = 0.25f; }

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
