using UnityEngine;

namespace Mmorpg.Game
{
    /// <summary>
    /// Peça de efeito visual de curta duração (partícula, faísca, projétil, anel). Move, escala e destrói a si mesma.
    /// Sem Shader especial: o "sumir" é feito encolhendo a peça. Só apresentação.
    /// </summary>
    public sealed class Fx : MonoBehaviour
    {
        public float Delay;
        public float Life = 0.5f;
        public float Spin;          // graus/s
        public bool Tumble;         // gira também no eixo X (pedras); sem isso gira só no vertical (anéis)
        public Vector3 Velocity;
        public Vector3 Gravity;
        public Vector3 S0 = Vector3.one;
        public Vector3 S1 = Vector3.zero;
        public bool Pop;            // cresce até a metade da vida e depois encolhe
        public bool Travel;
        public Vector3 From, To;    // projétil: vai de From a To durante a vida
        public bool EndBurst;
        public Color BurstColor;
        public bool EndRing;
        public float EndRingRadius = 1.5f;

        private float _age;

        /// <summary>Quantidade de peças de efeito vivas; SkillVfx usa para limitar efeitos e proteger o FPS no celular.</summary>
        public static int Live;

        private void Awake() { Live++; }

        private void OnDestroy() { Live--; }

        private void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            if (_age < Delay)
            {
                transform.localScale = Vector3.zero;
                return;
            }
            float t = _age - Delay;
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, Life));

            if (Travel) transform.position = Vector3.Lerp(From, To, k);
            else
            {
                transform.position = transform.position + Velocity * dt;
                Velocity = Velocity + Gravity * dt;
            }

            if (Pop)
            {
                float h = k < 0.5f ? k * 2f : (1f - k) * 2f;
                transform.localScale = Vector3.Lerp(S0, S1, h);
            }
            else transform.localScale = Vector3.Lerp(S0, S1, k);

            if (Spin != 0f)
                transform.rotation = Quaternion.Euler(Tumble ? Spin * 0.7f * t : 0f, Spin * t, 0f);

            if (k >= 1f)
            {
                if (EndBurst) SkillVfx.Burst(transform.position, BurstColor, 7, 4f, 0.16f, transform.parent);
                if (EndRing) SkillVfx.Ring(new Vector3(transform.position.x, 0f, transform.position.z), EndRingRadius, BurstColor, transform.parent, 0.4f, 0f);
                UnityEngine.Object.Destroy(gameObject);
            }
        }
    }
}
