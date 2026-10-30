using System.Collections.Generic;
using Mmorpg.Core.Combat;
using Mmorpg.Core.Data;
using Mmorpg.Core.Events;
using Mmorpg.Core.Session;
using UnityEngine;

namespace Mmorpg.Game
{
    /// <summary>
    /// Cena da região em execução: terreno, obstáculos (de regions.json), marcação das zonas de monstros e das
    /// entidades. Reconcilia a cada frame com a simulação, então nunca fica fora de sincronia com o núcleo.
    /// </summary>
    public sealed class WorldView
    {
        private readonly GameSession _session;
        private readonly GameObject _root;
        private readonly Dictionary<int, EntityView> _views = new Dictionary<int, EntityView>();
        private readonly List<int> _stale = new List<int>();

        public WorldView(GameSession session, Transform parent)
        {
            _session = session;
            _root = new GameObject("Mundo - " + session.Sim.Region.Name);
            _root.transform.SetParent(parent, false);
            SkillVfx.Data = session.Data;
            BuildTerrain(session.Sim.Region);
            session.EventRaised += OnEvent;
        }

        public void Destroy()
        {
            _session.EventRaised -= OnEvent;
            UnityEngine.Object.Destroy(_root);
            _views.Clear();
        }

        public EntityView ViewOf(int combatantId)
        {
            EntityView v;
            return _views.TryGetValue(combatantId, out v) ? v : null;
        }

        public IEnumerable<EntityView> Views { get { return _views.Values; } }

        /// <summary>Cria views para quem acabou de nascer e remove as de quem saiu do mundo. Chame uma vez por frame.</summary>
        public void Sync()
        {
            IList<Combatant> all = _session.Sim.World.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (!_views.ContainsKey(all[i].Id))
                    _views[all[i].Id] = EntityView.Create(all[i], _session.Data, _root.transform);
            }

            _stale.Clear();
            foreach (KeyValuePair<int, EntityView> kv in _views)
                if (_session.Sim.World.Get(kv.Key) == null) _stale.Add(kv.Key);
            for (int i = 0; i < _stale.Count; i++)
            {
                EntityView v = _views[_stale[i]];
                if (v != null) UnityEngine.Object.Destroy(v.gameObject);
                _views.Remove(_stale[i]);
            }
        }

        private void OnEvent(GameEvent e)
        {
            Transform fxParent = _root.transform;
            switch (e.Type)
            {
                case GameEventType.Damage:
                {
                    EntityView hit = ViewOf(e.TargetId);
                    if (hit != null) hit.Flash();
                    return;
                }
                case GameEventType.Heal:
                {
                    EntityView healed = ViewOf(e.TargetId);
                    if (healed != null) SkillVfx.OnHeal(healed.transform.position, fxParent);
                    return;
                }
                case GameEventType.StatusApplied:
                {
                    EntityView t = ViewOf(e.TargetId);
                    if (t != null && e.StatusId != null) SkillVfx.OnStatus(e.StatusId, t.transform.position, fxParent);
                    return;
                }
                case GameEventType.Died:
                {
                    EntityView dead = ViewOf(e.TargetId);
                    if (dead != null && dead.Combatant.IsMonster) SkillVfx.OnDeath(dead.transform.position, fxParent);
                    return;
                }
                case GameEventType.LevelUp:
                {
                    EntityView up = ViewOf(e.TargetId != 0 ? e.TargetId : _session.Player.Id);
                    if (up != null) SkillVfx.OnLevelUp(up.transform.position, fxParent);
                    return;
                }
                case GameEventType.Dash:
                {
                    SkillDef ds;
                    string del = _session.Data.Skills.TryGetValue(e.SkillId ?? "", out ds) ? ds.Element : null;
                    Color m, a;
                    SkillVfx.Palette(del, out m, out a);
                    SkillVfx.OnDash(Conv.ToV3(e.Position), Conv.ToV3(e.Position2), a, fxParent);
                    return;
                }
                case GameEventType.CastStarted:
                {
                    if (e.Radius > 0.01)
                    {
                        SkillDef cs;
                        string cel = _session.Data.Skills.TryGetValue(e.SkillId ?? "", out cs) ? cs.Element : null;
                        SkillVfx.Telegraph(Conv.ToV3(e.Position), (float)e.Radius, (float)e.Amount, e.SourceId == _session.Player.Id, cel, fxParent);
                    }
                    return;
                }
                case GameEventType.SkillUsed:
                {
                    SkillDef skill;
                    _session.Data.Skills.TryGetValue(e.SkillId ?? "", out skill);
                    EntityView src = ViewOf(e.SourceId);
                    EntityView tgt = ViewOf(e.TargetId);
                    if (src != null) src.Pulse();
                    SkillVfx.OnSkillUsed(e, skill, src, tgt, e.SourceId == _session.Player.Id, fxParent);
                    return;
                }
            }
        }

        // ------------------------------------------------------------------ terreno
        private void BuildTerrain(RegionDef region)
        {
            BoundsDef b = region.Bounds;
            float w = (float)(b.MaxX - b.MinX);
            float d = (float)(b.MaxZ - b.MinZ);
            float cx = (float)(b.MinX + b.MaxX) * 0.5f;
            float cz = (float)(b.MinZ + b.MaxZ) * 0.5f;

            // Plane padrão mede 10 x 10.
            Prims.Make(PrimitiveType.Plane, "Chao", _root.transform, new Vector3(cx, 0f, cz), new Vector3(w / 10f, 1f, d / 10f), new Color(0.32f, 0.5f, 0.28f), false);

            // Cerca baixa nas bordas (a simulação já impede de sair; isto só mostra o limite).
            Color fence = new Color(0.35f, 0.27f, 0.2f);
            Prims.Make(PrimitiveType.Cube, "CercaN", _root.transform, new Vector3(cx, 0.6f, (float)b.MaxZ), new Vector3(w, 1.2f, 0.6f), fence, false);
            Prims.Make(PrimitiveType.Cube, "CercaS", _root.transform, new Vector3(cx, 0.6f, (float)b.MinZ), new Vector3(w, 1.2f, 0.6f), fence, false);
            Prims.Make(PrimitiveType.Cube, "CercaL", _root.transform, new Vector3((float)b.MaxX, 0.6f, cz), new Vector3(0.6f, 1.2f, d), fence, false);
            Prims.Make(PrimitiveType.Cube, "CercaO", _root.transform, new Vector3((float)b.MinX, 0.6f, cz), new Vector3(0.6f, 1.2f, d), fence, false);

            // Zonas de monstros: círculos no chão (a arena do chefe em vermelho).
            string bossCat = _session.Data.Rules.BossCategoryId;
            foreach (SpawnZoneDef z in region.SpawnZones)
            {
                MonsterDef m = _session.Data.Monsters[z.MonsterId];
                bool boss = m.CategoryId == bossCat;
                float r = boss ? 9f : (float)z.Radius + 1.5f;
                Color col = boss ? new Color(0.45f, 0.2f, 0.18f) : new Color(0.26f, 0.42f, 0.23f);
                Prims.Make(PrimitiveType.Cylinder, "Zona " + z.Id, _root.transform, new Vector3((float)z.CenterX, 0.02f, (float)z.CenterZ), new Vector3(r * 2f, 0.01f, r * 2f), col, false);
            }

            // Ponto de nascimento.
            Prims.Make(PrimitiveType.Cylinder, "Nascimento", _root.transform, new Vector3((float)region.PlayerSpawn.X, 0.03f, (float)region.PlayerSpawn.Z), new Vector3(5f, 0.01f, 5f), new Color(0.75f, 0.7f, 0.45f), false);

            TerrainDecor.Build(region, _root.transform);
            foreach (NpcDef n in region.Npcs) BuildNpc(n);
            foreach (ObstacleDef o in region.Obstacles) BuildObstacle(o);
        }

        /// <summary>Mercador montado com primitivas: corpo, cabeça, chapéu, balcão com caixas e placa. Aparência provisória.</summary>
        private void BuildNpc(NpcDef n)
        {
            Transform root = Prims.Pivot(_root.transform, "NPC - " + n.Name, new Vector3((float)n.X, 0f, (float)n.Z));
            root.localRotation = Quaternion.Euler(0f, 200f, 0f);
            Color robe = new Color(0.55f, 0.18f, 0.2f), skin = new Color(0.88f, 0.7f, 0.55f), wood = new Color(0.45f, 0.3f, 0.17f);
            Prims.Make(PrimitiveType.Capsule, "Corpo", root, new Vector3(0f, 1.0f, 0f), new Vector3(0.9f, 1.0f, 0.9f), robe, false);
            Prims.Make(PrimitiveType.Sphere, "Cabeca", root, new Vector3(0f, 2.2f, 0f), new Vector3(0.6f, 0.6f, 0.6f), skin, false);
            Prims.Make(PrimitiveType.Cylinder, "ChapeuAba", root, new Vector3(0f, 2.45f, 0f), new Vector3(0.95f, 0.04f, 0.95f), new Color(0.25f, 0.18f, 0.1f), false);
            Prims.Make(PrimitiveType.Cylinder, "ChapeuTopo", root, new Vector3(0f, 2.65f, 0f), new Vector3(0.5f, 0.2f, 0.5f), new Color(0.25f, 0.18f, 0.1f), false);
            Prims.Make(PrimitiveType.Cube, "Balcao", root, new Vector3(0f, 0.5f, 1.3f), new Vector3(3.2f, 1.0f, 0.9f), wood, false);
            Prims.Make(PrimitiveType.Cube, "Caixa1", root, new Vector3(-1.6f, 0.45f, -0.5f), new Vector3(0.9f, 0.9f, 0.9f), wood, false);
            Prims.Make(PrimitiveType.Cube, "Caixa2", root, new Vector3(1.7f, 0.35f, -0.6f), new Vector3(0.7f, 0.7f, 0.7f), new Color(0.5f, 0.36f, 0.2f), false);
            Prims.Make(PrimitiveType.Cylinder, "Poste", root, new Vector3(-1.5f, 1.5f, 1.3f), new Vector3(0.12f, 1.5f, 0.12f), wood, false);
            Prims.Make(PrimitiveType.Cylinder, "Poste2", root, new Vector3(1.5f, 1.5f, 1.3f), new Vector3(0.12f, 1.5f, 0.12f), wood, false);
            Prims.Make(PrimitiveType.Cube, "Toldo", root, new Vector3(0f, 3.05f, 1.3f), new Vector3(3.6f, 0.12f, 1.4f), new Color(0.85f, 0.65f, 0.2f), false);
            Prims.Make(PrimitiveType.Sphere, "Marcador", root, new Vector3(0f, 3.6f, 0f), new Vector3(0.35f, 0.35f, 0.35f), new Color(1f, 0.85f, 0.2f), false);
        }

        private void BuildObstacle(ObstacleDef o)
        {
            float r = (float)o.Radius;
            Vector3 p = new Vector3((float)o.X, 0f, (float)o.Z);
            var holder = new GameObject(o.Kind + " " + o.X + "," + o.Z);
            holder.transform.SetParent(_root.transform, false);
            holder.transform.position = p;
            Transform t = holder.transform;
            int variant = (int)(System.Math.Abs(o.X * 7.0 + o.Z * 13.0)) % 3;

            switch (o.Kind)
            {
                case "tree":
                    TerrainDecor.Tree(t, Vector3.zero, Mathf.Max(0.5f, r * 0.9f), variant);
                    break;
                case "rock":
                    TerrainDecor.Rock(t, Vector3.zero, r);
                    break;
                default: // ruin e qualquer tipo novo: base de pedra com pilares e um arco quebrado
                    Prims.MakeShared(PrimitiveType.Cylinder, "Base", t, new Vector3(0f, 0.2f, 0f), new Vector3(r * 2f, 0.2f, r * 2f), new Color(0.55f, 0.55f, 0.58f));
                    Prims.MakeShared(PrimitiveType.Cube, "Pilar1", t, new Vector3(-r * 0.4f, 1.8f, 0f), new Vector3(r * 0.5f, 3.4f, r * 0.5f), new Color(0.65f, 0.65f, 0.68f));
                    Prims.MakeShared(PrimitiveType.Cube, "Pilar2", t, new Vector3(r * 0.4f, 1.1f, r * 0.2f), new Vector3(r * 0.5f, 2.0f, r * 0.5f), new Color(0.62f, 0.62f, 0.65f));
                    Prims.MakeShared(PrimitiveType.Cube, "Verga", t, new Vector3(-r * 0.15f, 3.5f, 0f), new Vector3(r * 1.0f, 0.4f, r * 0.45f), new Color(0.6f, 0.6f, 0.63f));
                    Prims.MakeShared(PrimitiveType.Sphere, "Hera", t, new Vector3(-r * 0.4f, 2.2f, r * 0.28f), new Vector3(r * 0.35f, 1.4f, r * 0.15f), new Color(0.25f, 0.5f, 0.22f));
                    break;
            }
        }
    }
}
