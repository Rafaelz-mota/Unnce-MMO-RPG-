using UnityEngine;

namespace Mmorpg.Game
{
    /// <summary>Anel que cresce e some: marca a área de uma habilidade (raio vem do JSON da habilidade).</summary>
    public sealed class RingEffect : MonoBehaviour
    {
        private float _age;
        private float _life = 0.45f;
        private float _radius = 1f;

        public static void Spawn(Vector3 position, float radius, Color color, Transform parent)
        {
            GameObject go = Prims.Make(PrimitiveType.Cylinder, "Anel", parent, position + new Vector3(0f, 0.06f, 0f), new Vector3(radius * 2f, 0.02f, radius * 2f), color, false);
            RingEffect fx = go.AddComponent<RingEffect>();
            fx._radius = Mathf.Max(0.5f, radius);
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float k = Mathf.Clamp01(_age / _life);
            float d = _radius * 2f * (0.35f + 0.65f * k);
            transform.localScale = new Vector3(d, 0.02f, d);
            if (_age >= _life) UnityEngine.Object.Destroy(gameObject);
        }
    }
}
