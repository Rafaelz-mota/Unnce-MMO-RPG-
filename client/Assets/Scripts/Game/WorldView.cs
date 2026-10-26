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
            if (e.Type == GameEventType.Damage)
            {
                EntityView hit = ViewOf(e.TargetId);
                if (hit != null) hit.Flash();
                return;
            }
            if (e.Type != GameEventType.SkillUsed) return;
            EntityView src = ViewOf(e.SourceId);
            if (src != null) src.Pulse();
            if (e.Radius > 0.01)
            {
                bool byPlayer = e.SourceId == _session.Player.Id;
                Color c = byPlayer ? new Color(0.3f, 0.85f, 1f) : new Color(1f, 0.3f, 0.2f);
                RingEffect.Spawn(Conv.ToV3(e.Position), (float)e.Radius, c, _root.transform);
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

            foreach (ObstacleDef o in region.Obstacles) BuildObstacle(o);
        }

        private void BuildObstacle(ObstacleDef o)
        {
            float r = (float)o.Radius;
            Vector3 p = new Vector3((float)o.X, 0f, (float)o.Z);
            var holder = new GameObject(o.Kind + " " + o.X + "," + o.Z);
            holder.transform.SetParent(_root.transform, false);
            holder.transform.position = p;
            Transform t = holder.transform;

            switch (o.Kind)
            {
                case "tree":
                    Prims.Make(PrimitiveType.Cylinder, "Tronco", t, new Vector3(0f, 1.6f, 0f), new Vector3(r * 0.8f, 1.6f, r * 0.8f), new Color(0.4f, 0.28f, 0.16f), false);
                    Prims.Make(PrimitiveType.Sphere, "Copa", t, new Vector3(0f, 4f, 0f), new Vector3(r * 3f, r * 2.6f, r * 3f), new Color(0.15f, 0.42f, 0.18f), false);
                    break;
                case "rock":
                    Prims.Make(PrimitiveType.Sphere, "Pedra", t, new Vector3(0f, r * 0.45f, 0f), new Vector3(r * 2f, r * 1.1f, r * 2f), new Color(0.5f, 0.5f, 0.52f), false);
                    break;
                default: // ruin e qualquer tipo novo: base de pedra com dois pilares
                    Prims.Make(PrimitiveType.Cylinder, "Base", t, new Vector3(0f, 0.2f, 0f), new Vector3(r * 2f, 0.2f, r * 2f), new Color(0.55f, 0.55f, 0.58f), false);
                    Prims.Make(PrimitiveType.Cube, "Pilar1", t, new Vector3(-r * 0.4f, 1.8f, 0f), new Vector3(r * 0.5f, 3.4f, r * 0.5f), new Color(0.65f, 0.65f, 0.68f), false);
                    Prims.Make(PrimitiveType.Cube, "Pilar2", t, new Vector3(r * 0.4f, 1.1f, r * 0.2f), new Vector3(r * 0.5f, 2.0f, r * 0.5f), new Color(0.62f, 0.62f, 0.65f), false);
                    break;
            }
        }
    }
}
