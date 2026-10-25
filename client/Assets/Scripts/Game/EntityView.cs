using Mmorpg.Core.Combat;
using Mmorpg.Core.Common;
using Mmorpg.Core.Data;
using UnityEngine;

namespace Mmorpg.Game
{
    /// <summary>
    /// Representação visual de um <see cref="Combatant"/> (jogador ou monstro). Só LÊ o estado da simulação;
    /// nunca decide resultado de combate. O modelo é um placeholder de primitivas (cápsula + "nariz" de direção).
    /// </summary>
    public sealed class EntityView : MonoBehaviour
    {
        private Transform _body;
        private Color _baseColor;
        private float _pulse;
        private bool _deadApplied;

        public Combatant Combatant { get; private set; }

        /// <summary>Altura (m) onde ficam nome e barra de vida.</summary>
        public float LabelHeight { get; private set; }

        public static EntityView Create(Combatant c, GameData data, Transform parent)
        {
            var go = new GameObject((c.IsPlayer ? "Jogador " : "Monstro ") + c.Name + " #" + c.Id);
            go.transform.SetParent(parent, false);
            go.transform.position = Conv.ToV3(c.Position);
            EntityView view = go.AddComponent<EntityView>();
            view.Init(c, data);
            return view;
        }

        private void Init(Combatant c, GameData data)
        {
            Combatant = c;
            float scale = 1f;
            if (c.IsMonster)
            {
                scale = (float)System.Math.Max(0.4, c.MonsterDef.Scale);
                _baseColor = Conv.Hex(c.Category != null ? c.Category.ColorHex : null, Color.gray);
            }
            else
            {
                _baseColor = new Color(0.25f, 0.55f, 0.95f);
            }

            var root = new GameObject("Corpo");
            root.transform.SetParent(transform, false);
            _body = root.transform;

            // Cápsula: 1 de largura x 2 de altura quando a escala é 1.
            Prims.Make(PrimitiveType.Capsule, "Capsula", _body, new Vector3(0f, scale, 0f), new Vector3(scale, scale, scale), _baseColor, true);
            // "Nariz": mostra para onde a entidade olha.
            Prims.Make(PrimitiveType.Sphere, "Nariz", _body, new Vector3(0f, scale * 1.4f, scale * 0.5f), Vector3.one * (0.35f * scale), Color.white, false);

            LabelHeight = scale * 2.3f;
        }

        public void Pulse() { _pulse = 0.2f; }

        private void Update()
        {
            if (Combatant == null) return;

            Vector3 target = Conv.ToV3(Combatant.Position);
            float dist = Vector3.Distance(transform.position, target);
            // Teletransporte (nascimento, dash longo, renascer) não deve "deslizar".
            transform.position = dist > 5f ? target : Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-25f * Time.deltaTime));

            Vec2 f = Combatant.Facing;
            if (f.SqrLength > 1e-6)
            {
                Quaternion want = Quaternion.LookRotation(new Vector3((float)f.X, 0f, (float)f.Z));
                transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-14f * Time.deltaTime));
            }

            if (_pulse > 0f)
            {
                _pulse -= Time.deltaTime;
                float k = 1f + 0.18f * Mathf.Clamp01(_pulse / 0.2f);
                _body.localScale = new Vector3(k, k, k);
            }
            else if (_body.localScale.x != 1f)
            {
                _body.localScale = Vector3.one;
            }

            if (Combatant.Dead && !_deadApplied)
            {
                _deadApplied = true;
                _body.localRotation = Quaternion.Euler(0f, 0f, 90f);
                _body.localPosition = new Vector3(0f, -0.35f, 0f);
                Prims.TintAll(gameObject, _baseColor * 0.35f);
            }
            else if (!Combatant.Dead && _deadApplied)
            {
                _deadApplied = false; // renasceu
                _body.localRotation = Quaternion.identity;
                _body.localPosition = Vector3.zero;
                Prims.Tint(_body.GetChild(0).gameObject, _baseColor);
                Prims.Tint(_body.GetChild(1).gameObject, Color.white);
            }
        }
    }
}
