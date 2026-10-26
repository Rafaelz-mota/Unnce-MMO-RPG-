using Mmorpg.Core.Combat;
using Mmorpg.Core.Common;
using Mmorpg.Core.Data;
using UnityEngine;

namespace Mmorpg.Game
{
    /// <summary>
    /// Representação visual de um <see cref="Combatant"/> (jogador ou monstro). Só LÊ o estado da simulação;
    /// nunca decide resultado de combate. O modelo é montado por código com primitivas (ver ModelBuilder), com animação simples.
    /// </summary>
    public sealed class EntityView : MonoBehaviour
    {
        private Transform _body;
        private ProceduralAnim _anim;
        private Renderer[] _renderers;
        private Color[] _original;
        private float _pulse;
        private float _flash;
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
            var root = new GameObject("Corpo");
            root.transform.SetParent(transform, false);
            _body = root.transform;

            ModelParts parts;
            float scale = 1f;
            if (c.IsMonster)
            {
                Color cat = Conv.Hex(c.Category != null ? c.Category.ColorHex : null, Color.gray);
                parts = ModelBuilder.BuildMonster(c.MonsterDef, cat, _body);
                scale = (float)System.Math.Max(0.4, c.MonsterDef.Scale) / Mathf.Max(0.1f, parts.BaseScale);
            }
            else
            {
                string classId = c.Character != null && c.Character.Class != null ? c.Character.Class.Id : null;
                parts = ModelBuilder.BuildPlayer(classId, c.Faction, _body);
            }
            parts.Root.localScale = new Vector3(scale, scale, scale);
            LabelHeight = parts.Height * scale + 0.3f;

            _anim = gameObject.AddComponent<ProceduralAnim>();
            _anim.Body = parts.Body;
            _anim.Legs = parts.Legs;
            _anim.Arms = parts.Arms;
            _anim.Spinners = parts.Spinners;
            _anim.StrideSpeed = parts.StrideSpeed;
            _anim.LegSwing = parts.LegSwing;
            _anim.ArmSwing = parts.ArmSwing;

            _renderers = parts.Root.GetComponentsInChildren<Renderer>();
            _original = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++) _original[i] = _renderers[i].material.color;
        }

        /// <summary>Ao usar uma habilidade: o corpo dá um pequeno impulso e os braços golpeiam.</summary>
        public void Pulse()
        {
            _pulse = 0.2f;
            if (_anim != null) _anim.Lunge();
        }

        /// <summary>Ao receber dano: o corpo pisca em branco por um instante.</summary>
        public void Flash() { _flash = 0.12f; }

        private void SetColors(float whiteMix, float darken)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                Color o = _original[i];
                Color c = new Color(o.r * darken, o.g * darken, o.b * darken);
                if (whiteMix > 0f) c = new Color(c.r + (1f - c.r) * whiteMix, c.g + (1f - c.g) * whiteMix, c.b + (1f - c.b) * whiteMix);
                _renderers[i].material.color = c;
            }
        }

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
                float k = 1f + 0.1f * Mathf.Clamp01(_pulse / 0.2f);
                _body.localScale = new Vector3(k, k, k);
            }
            else if (_body.localScale.x != 1f)
            {
                _body.localScale = Vector3.one;
            }

            if (Combatant.Dead && !_deadApplied)
            {
                _deadApplied = true;
                _anim.Active = false;
                _body.localRotation = Quaternion.Euler(90f, 0f, 0f);
                _body.localPosition = new Vector3(0f, 0.3f, 0f);
                SetColors(0f, 0.35f);
            }
            else if (!Combatant.Dead && _deadApplied)
            {
                _deadApplied = false; // renasceu
                _anim.Active = true;
                _body.localRotation = Quaternion.identity;
                _body.localPosition = Vector3.zero;
                SetColors(0f, 1f);
            }
            else if (!Combatant.Dead)
            {
                if (_flash > 0f)
                {
                    _flash -= Time.deltaTime;
                    SetColors(Mathf.Clamp01(_flash / 0.12f) * 0.7f, 1f);
                }
            }
        }
    }
}
