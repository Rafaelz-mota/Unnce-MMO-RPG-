using Mmorpg.Core.Combat;
using Mmorpg.Core.Data;
using Mmorpg.Core.Events;
using UnityEngine;

namespace Mmorpg.Game
{
    /// <summary>
    /// Efeitos visuais das habilidades e dos status, escolhidos pelo campo "element" e pela área da habilidade
    /// (skills.json). Tudo é feito de primitivas e só LÊ eventos da simulação: não decide dano nem alcance.
    /// </summary>
    public static class SkillVfx
    {
        private const float Golden = 2.399963f;
        /// <summary>Acima disto, efeitos secundários são descartados (ainda não há pooling de objetos).</summary>
        public const int MaxLive = 220;

        private static bool Busy { get { return Fx.Live > MaxLive; } }

        public static void Palette(string element, out Color main, out Color alt)
        {
            switch (element)
            {
                case "fire": main = new Color(1f, 0.45f, 0.1f); alt = new Color(1f, 0.85f, 0.25f); break;
                case "ice": main = new Color(0.55f, 0.85f, 1f); alt = new Color(0.92f, 0.98f, 1f); break;
                case "wind": main = new Color(0.7f, 1f, 0.85f); alt = new Color(1f, 1f, 1f); break;
                case "earth": main = new Color(0.55f, 0.4f, 0.25f); alt = new Color(0.45f, 0.65f, 0.3f); break;
                case "arcane": main = new Color(0.7f, 0.4f, 1f); alt = new Color(0.4f, 0.9f, 1f); break;
                case "light": main = new Color(1f, 0.9f, 0.45f); alt = new Color(1f, 1f, 0.92f); break;
                case "dark": main = new Color(0.5f, 0.15f, 0.65f); alt = new Color(0.2f, 0.08f, 0.28f); break;
                default: main = new Color(0.92f, 0.92f, 0.96f); alt = new Color(1f, 0.8f, 0.5f); break;
            }
        }

        // ------------------------------------------------------------------ peças básicas
        public static void Ring(Vector3 pos, float radius, Color color, Transform parent, float life, float delay)
        {
            GameObject go = Prims.Make(PrimitiveType.Cylinder, "Anel", parent, new Vector3(pos.x, 0.07f, pos.z), Vector3.one, color, false);
            Fx fx = go.AddComponent<Fx>();
            fx.Life = life;
            fx.Delay = delay;
            float d = Mathf.Max(1f, radius * 2f);
            fx.S0 = new Vector3(d * 0.3f, 0.02f, d * 0.3f);
            fx.S1 = new Vector3(d, 0.02f, d);
        }

        public static void Burst(Vector3 pos, Color color, int count, float speed, float size, Transform parent)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i * Golden;
                GameObject go = Prims.Make(PrimitiveType.Sphere, "Faisca", parent, pos, Vector3.one * size, color, false);
                Fx fx = go.AddComponent<Fx>();
                fx.Life = 0.35f + 0.05f * (i % 4);
                fx.Velocity = new Vector3(Mathf.Cos(a) * speed, speed * 0.6f, Mathf.Sin(a) * speed);
                fx.Gravity = new Vector3(0f, -9f, 0f);
                fx.S0 = Vector3.one * size;
            }
        }

        private static void Particle(PrimitiveType type, Transform parent, Vector3 pos, Vector3 size, Color color, Vector3 vel, Vector3 grav, float life, float delay, bool tumble, bool pop)
        {
            GameObject go = Prims.Make(type, "Particula", parent, pos, size, color, false);
            Fx fx = go.AddComponent<Fx>();
            fx.Life = life;
            fx.Delay = delay;
            fx.Velocity = vel;
            fx.Gravity = grav;
            fx.S0 = size;
            fx.S1 = pop ? size : Vector3.zero;
            fx.Pop = pop;
            if (tumble) { fx.Tumble = true; fx.Spin = 360f; }
        }

        public static void Projectile(Vector3 from, Vector3 to, Color main, Color alt, Transform parent, float size)
        {
            float life = 0.12f + 0.012f * Vector3.Distance(from, to);
            life = Mathf.Clamp(life, 0.15f, 0.5f);
            for (int i = 0; i < 4; i++)
            {
                GameObject go = Prims.Make(PrimitiveType.Sphere, i == 0 ? "Projetil" : "Rastro", parent, from, Vector3.one * size, i == 0 ? main : alt, false);
                Fx fx = go.AddComponent<Fx>();
                fx.Travel = true;
                fx.From = from;
                fx.To = to;
                fx.Life = life;
                fx.Delay = 0.035f * i;
                float s = size * (1f - 0.18f * i);
                fx.S0 = Vector3.one * s;
                fx.S1 = Vector3.one * s * 0.6f;
                if (i == 0) { fx.EndBurst = true; fx.BurstColor = main; }
            }
        }

        // ------------------------------------------------------------------ habilidades
        public static void OnSkillUsed(GameEvent e, SkillDef skill, EntityView source, EntityView target, bool byPlayer, Transform parent)
        {
            if (Busy && !byPlayer) return;
            string el = skill != null && !string.IsNullOrEmpty(skill.Element) ? skill.Element : "physical";
            Color main, alt;
            Palette(el, out main, out alt);
            if (!byPlayer && el == "physical") { main = new Color(1f, 0.35f, 0.25f); alt = new Color(1f, 0.7f, 0.3f); }

            Vector3 center = Conv.ToV3(e.Position);
            float radius = (float)e.Radius;

            if (radius > 0.01f)
            {
                AreaEffect(el, skill, center, radius, main, alt, parent);
                return;
            }

            Vector3 from = source != null ? source.transform.position + new Vector3(0f, 1.4f, 0f) : center + new Vector3(0f, 1.4f, 0f);
            bool hasEnemyTarget = target != null && source != null && target != source && skill != null
                && skill.Targeting == Targeting.Enemy;
            bool allyTarget = target != null && skill != null && skill.Targeting == Targeting.Ally;

            if (hasEnemyTarget)
            {
                Vector3 to = target.transform.position + new Vector3(0f, 1.2f, 0f);
                if (el == "physical" && skill.Range <= 4.0)
                {
                    MeleeSwing(source, to, main, alt, parent);
                }
                else if (el == "physical")
                {
                    Projectile(from, to, main, alt, parent, 0.18f); // flecha/tiro: projétil pequeno e rápido
                }
                else Projectile(from, to, main, alt, parent, el == "fire" ? 0.5f : 0.38f);
                return;
            }

            // Buff, escudo, cura em um aliado, ou em si mesmo: aura subindo em volta do alvo.
            Vector3 at = allyTarget ? target.transform.position : (source != null ? source.transform.position : center);
            Aura(at, main, alt, parent, el == "light" || skill != null && skill.Type == SkillType.Heal);
        }

        /// <summary>
        /// Golpe corpo a corpo: rastro em arco da arma na frente de quem ataca e, logo depois, o impacto no alvo.
        /// Dispara no evento SkillUsed (o dano real já foi decidido pelo núcleo); o atraso do impacto é só visual.
        /// </summary>
        private static void MeleeSwing(EntityView source, Vector3 to, Color main, Color alt, Transform parent)
        {
            if (source != null)
            {
                Vector3 fwd = source.transform.rotation * Vector3.forward;
                Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
                Vector3 c = source.transform.position + new Vector3(0f, 1.3f, 0f);
                const int n = 7;
                for (int i = 0; i < n; i++)
                {
                    float a = -1.1f + 2.2f * i / (n - 1);
                    Vector3 p = c + fwd * (Mathf.Cos(a) * 1.7f) + right * (Mathf.Sin(a) * 1.7f);
                    GameObject seg = Prims.Make(PrimitiveType.Cube, "Rastro", parent, p, new Vector3(0.3f, 0.06f, 0.3f), i % 2 == 0 ? main : alt, false);
                    seg.transform.rotation = Quaternion.Euler(0f, a * -Mathf.Rad2Deg, 0f);
                    Fx fx = seg.AddComponent<Fx>();
                    fx.Delay = 0.03f * i;
                    fx.Life = 0.16f;
                    fx.Pop = true;
                    fx.S0 = new Vector3(0.1f, 0.04f, 0.1f);
                    fx.S1 = new Vector3(0.55f, 0.07f, 0.35f);
                }
            }
            // impacto: clarão no alvo logo depois do arco, com faíscas ao terminar
            GameObject hit = Prims.Make(PrimitiveType.Sphere, "Impacto", parent, to, Vector3.one * 0.3f, alt, false);
            Fx hf = hit.AddComponent<Fx>();
            hf.Delay = 0.12f;
            hf.Life = 0.18f;
            hf.Pop = true;
            hf.S0 = Vector3.one * 0.2f;
            hf.S1 = Vector3.one * 1.0f;
            hf.EndBurst = true;
            hf.BurstColor = main;
            Ring(new Vector3(to.x, 0f, to.z), 1.1f, alt, parent, 0.3f, 0.12f);
        }

        private static void Aura(Vector3 at, Color main, Color alt, Transform parent, bool light)
        {
            Ring(at, 1.4f, main, parent, 0.5f, 0f);
            for (int i = 0; i < 10; i++)
            {
                float a = i * Golden;
                Vector3 p = at + new Vector3(Mathf.Cos(a) * 0.8f, 0.2f, Mathf.Sin(a) * 0.8f);
                Particle(PrimitiveType.Sphere, parent, p, Vector3.one * 0.14f, i % 2 == 0 ? main : alt, new Vector3(0f, 2.2f + 0.2f * (i % 3), 0f), Vector3.zero, 0.7f, 0.04f * i, false, false);
            }
            if (light) Pillar(at, 0.8f, 5f, alt, parent, 0.5f);
        }

        private static void Pillar(Vector3 at, float radius, float height, Color color, Transform parent, float life)
        {
            GameObject go = Prims.Make(PrimitiveType.Cylinder, "Pilar", parent, new Vector3(at.x, height * 0.5f, at.z), new Vector3(radius, height * 0.5f, radius), color, false);
            Fx fx = go.AddComponent<Fx>();
            fx.Life = life;
            fx.S0 = new Vector3(radius, height * 0.5f, radius);
            fx.S1 = new Vector3(0.05f, height * 0.5f, 0.05f);
        }

        private static void AreaEffect(string el, SkillDef skill, Vector3 center, float radius, Color main, Color alt, Transform parent)
        {
            Ring(center, radius, main, parent, 0.5f, 0f);
            Ring(center, radius * 0.65f, alt, parent, 0.45f, 0.08f);
            int count = Mathf.Clamp(Mathf.RoundToInt(8f + radius * 3f), 10, 28);
            bool ground = skill != null && skill.Targeting == Targeting.Ground;

            switch (el)
            {
                case "fire":
                    if (ground && radius >= 4.5f)
                    {
                        // chuva de meteoros: bolas caem do alto em pontos espalhados pela área
                        int meteors = Mathf.Clamp(Mathf.RoundToInt(radius), 4, 9);
                        for (int i = 0; i < meteors; i++)
                        {
                            float a = i * Golden;
                            float rr = radius * 0.85f * Mathf.Sqrt((i + 0.5f) / meteors);
                            Vector3 land = center + new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr);
                            GameObject m = Prims.Make(PrimitiveType.Sphere, "Meteoro", parent, land + new Vector3(0f, 16f, 0f), Vector3.one * 0.9f, main, false);
                            Fx fx = m.AddComponent<Fx>();
                            fx.Travel = true;
                            fx.From = land + new Vector3(0f, 16f, 0f);
                            fx.To = land + new Vector3(0f, 0.3f, 0f);
                            fx.Life = 0.32f;
                            fx.Delay = 0.1f * i;
                            fx.S0 = Vector3.one * 0.9f;
                            fx.S1 = Vector3.one * 0.7f;
                            fx.EndBurst = true;
                            fx.EndRing = true;
                            fx.EndRingRadius = 1.8f;
                            fx.BurstColor = alt;
                        }
                    }
                    for (int i = 0; i < count; i++)
                    {
                        float a = i * Golden;
                        float rr = radius * 0.9f * Mathf.Sqrt((i + 0.5f) / count);
                        Particle(PrimitiveType.Sphere, parent, center + new Vector3(Mathf.Cos(a) * rr, 0.2f, Mathf.Sin(a) * rr), Vector3.one * 0.3f, i % 2 == 0 ? main : alt, new Vector3(0f, 2.5f + 0.15f * (i % 5), 0f), Vector3.zero, 0.6f, 0.05f, false, false);
                    }
                    break;
                case "ice":
                    for (int i = 0; i < count; i++)
                    {
                        float a = i * Golden;
                        float rr = radius * 0.9f * Mathf.Sqrt((i + 0.5f) / count);
                        float h = 0.8f + 0.12f * (i % 5);
                        GameObject s = Prims.Make(PrimitiveType.Cube, "Estilhaco", parent, center + new Vector3(Mathf.Cos(a) * rr, h * 0.5f, Mathf.Sin(a) * rr), new Vector3(0.2f, 0.05f, 0.2f), i % 2 == 0 ? main : alt, false);
                        s.transform.rotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
                        Fx fx = s.AddComponent<Fx>();
                        fx.Life = 0.7f;
                        fx.Delay = 0.02f * i;
                        fx.Pop = true;
                        fx.S0 = new Vector3(0.1f, 0.05f, 0.1f);
                        fx.S1 = new Vector3(0.28f, h, 0.28f);
                    }
                    break;
                case "earth":
                    for (int i = 0; i < count; i++)
                    {
                        float a = i * Golden;
                        float rr = radius * 0.9f * Mathf.Sqrt((i + 0.5f) / count);
                        float sz = 0.3f + 0.07f * (i % 4);
                        Particle(PrimitiveType.Cube, parent, center + new Vector3(Mathf.Cos(a) * rr, 0.2f, Mathf.Sin(a) * rr), Vector3.one * sz, i % 3 == 0 ? alt : main, new Vector3(Mathf.Cos(a) * 1.2f, 6f + 0.5f * (i % 4), Mathf.Sin(a) * 1.2f), new Vector3(0f, -16f, 0f), 0.75f, 0.03f * (i % 5), true, false);
                    }
                    break;
                case "wind":
                    for (int i = 0; i < 4; i++)
                    {
                        GameObject w = Prims.Make(PrimitiveType.Cylinder, "Vento", parent, center + new Vector3(0f, 0.3f + 0.6f * i, 0f), Vector3.one, i % 2 == 0 ? main : alt, false);
                        Fx fx = w.AddComponent<Fx>();
                        fx.Life = 0.7f;
                        fx.Delay = 0.06f * i;
                        fx.Spin = 540f;
                        fx.Velocity = new Vector3(0f, 1.4f, 0f);
                        float d = radius * (0.9f - 0.12f * i) * 2f;
                        fx.S0 = new Vector3(d * 0.4f, 0.03f, d * 0.4f);
                        fx.S1 = new Vector3(d, 0.03f, d);
                    }
                    for (int i = 0; i < count; i++)
                    {
                        float a = i * Golden;
                        float rr = radius * 0.8f * Mathf.Sqrt((i + 0.5f) / count);
                        Particle(PrimitiveType.Sphere, parent, center + new Vector3(Mathf.Cos(a) * rr, 0.3f, Mathf.Sin(a) * rr), new Vector3(0.12f, 0.12f, 0.5f), alt, new Vector3(-Mathf.Sin(a) * 5f, 1.5f, Mathf.Cos(a) * 5f), Vector3.zero, 0.55f, 0.02f * (i % 6), false, false);
                    }
                    break;
                case "dark":
                    for (int i = 0; i < count; i++)
                    {
                        float a = i * Golden;
                        float rr = radius * 0.9f * Mathf.Sqrt((i + 0.5f) / count);
                        Particle(PrimitiveType.Cube, parent, center + new Vector3(Mathf.Cos(a) * rr, 0.2f, Mathf.Sin(a) * rr), Vector3.one * 0.3f, i % 2 == 0 ? main : alt, new Vector3(0f, 1.6f, 0f), Vector3.zero, 0.8f, 0.03f * (i % 6), true, false);
                    }
                    break;
                case "light":
                    Pillar(center, radius * 0.5f, 7f, alt, parent, 0.55f);
                    for (int i = 0; i < count; i++)
                    {
                        float a = i * Golden;
                        float rr = radius * 0.9f * Mathf.Sqrt((i + 0.5f) / count);
                        Particle(PrimitiveType.Sphere, parent, center + new Vector3(Mathf.Cos(a) * rr, 0.2f, Mathf.Sin(a) * rr), Vector3.one * 0.2f, main, new Vector3(0f, 2.2f, 0f), Vector3.zero, 0.75f, 0.03f * (i % 6), false, false);
                    }
                    break;
                case "arcane":
                    for (int i = 0; i < count; i++)
                    {
                        float a = i * Golden;
                        float rr = radius * 0.9f * Mathf.Sqrt((i + 0.5f) / count);
                        Particle(PrimitiveType.Sphere, parent, center + new Vector3(Mathf.Cos(a) * rr, 0.2f, Mathf.Sin(a) * rr), Vector3.one * 0.22f, i % 2 == 0 ? main : alt, new Vector3(Mathf.Cos(a) * 1.5f, 2.8f, Mathf.Sin(a) * 1.5f), new Vector3(0f, -3f, 0f), 0.65f, 0.02f * (i % 8), false, false);
                    }
                    if (ground) Pillar(center, radius * 0.35f, 9f, main, parent, 0.45f);
                    break;
                default:
                    for (int i = 0; i < count; i++)
                    {
                        float a = i * Golden;
                        Particle(PrimitiveType.Sphere, parent, center + new Vector3(0f, 0.3f, 0f), Vector3.one * 0.16f, i % 2 == 0 ? main : alt, new Vector3(Mathf.Cos(a) * radius * 2.2f, 2f, Mathf.Sin(a) * radius * 2.2f), new Vector3(0f, -8f, 0f), 0.4f, 0f, false, false);
                    }
                    break;
            }
        }

        /// <summary>Aviso vermelho (ou da cor do elemento, para o jogador) que cresce durante o tempo de conjuração.</summary>
        public static void Telegraph(Vector3 center, float radius, float castTime, bool byPlayer, string element, Transform parent)
        {
            Color main, alt;
            Palette(element, out main, out alt);
            Color c = byPlayer ? Dk(main, 0.7f) : new Color(0.9f, 0.15f, 0.1f);
            GameObject go = Prims.Make(PrimitiveType.Cylinder, "Aviso", parent, new Vector3(center.x, 0.05f, center.z), Vector3.one, c, false);
            Fx fx = go.AddComponent<Fx>();
            fx.Life = Mathf.Max(0.2f, castTime);
            float d = radius * 2f;
            fx.S0 = new Vector3(d * 0.15f, 0.015f, d * 0.15f);
            fx.S1 = new Vector3(d, 0.015f, d);
        }

        private static Color Dk(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k); }

        // ------------------------------------------------------------------ outros eventos
        public static void OnStatus(string statusId, Vector3 pos, Transform parent)
        {
            Color c;
            switch (statusId)
            {
                case "taunted": c = new Color(1f, 0.3f, 0.15f); break;
                case "stunned": c = new Color(1f, 0.9f, 0.2f); break;
                case "rooted": c = new Color(0.35f, 0.7f, 0.25f); break;
                case "silenced": c = new Color(0.6f, 0.5f, 0.75f); break;
                case "chilled": c = new Color(0.6f, 0.88f, 1f); break;
                case "burning": c = new Color(1f, 0.5f, 0.1f); break;
                case "bleeding": c = new Color(0.7f, 0.08f, 0.08f); break;
                case "corruption": c = new Color(0.55f, 0.2f, 0.7f); break;
                case "barrier": c = new Color(0.45f, 0.85f, 1f); break;
                case "war_cry": c = new Color(0.95f, 0.35f, 0.2f); break;
                case "regrowth": c = new Color(0.4f, 0.9f, 0.4f); break;
                default: return;
            }
            Ring(pos, 1.1f, c, parent, 0.45f, 0f);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Golden;
                Particle(PrimitiveType.Sphere, parent, pos + new Vector3(Mathf.Cos(a) * 0.5f, 0.3f, Mathf.Sin(a) * 0.5f), Vector3.one * 0.12f, c, new Vector3(0f, 2f, 0f), Vector3.zero, 0.6f, 0.03f * i, false, false);
            }
        }

        public static void OnHeal(Vector3 pos, Transform parent)
        {
            for (int i = 0; i < 6; i++)
            {
                float a = i * Golden;
                Particle(PrimitiveType.Sphere, parent, pos + new Vector3(Mathf.Cos(a) * 0.5f, 0.3f, Mathf.Sin(a) * 0.5f), Vector3.one * 0.13f, new Color(0.4f, 1f, 0.5f), new Vector3(0f, 1.8f, 0f), Vector3.zero, 0.7f, 0.04f * i, false, false);
            }
        }

        public static void OnDeath(Vector3 pos, Transform parent)
        {
            for (int i = 0; i < 10; i++)
            {
                float a = i * Golden;
                Particle(PrimitiveType.Sphere, parent, pos + new Vector3(0f, 0.6f, 0f), Vector3.one * 0.32f, new Color(0.55f, 0.52f, 0.48f), new Vector3(Mathf.Cos(a) * 1.6f, 1.2f, Mathf.Sin(a) * 1.6f), new Vector3(0f, -1f, 0f), 0.8f, 0f, false, false);
            }
        }

        public static void OnLevelUp(Vector3 pos, Transform parent)
        {
            Pillar(pos, 1.0f, 8f, new Color(1f, 0.9f, 0.4f), parent, 1.0f);
            Ring(pos, 2.5f, new Color(1f, 0.85f, 0.3f), parent, 0.8f, 0f);
            Burst(pos + new Vector3(0f, 1f, 0f), new Color(1f, 0.95f, 0.6f), 16, 5f, 0.18f, parent);
        }

        public static void OnDash(Vector3 a, Vector3 b, Color color, Transform parent)
        {
            for (int i = 0; i < 6; i++)
            {
                float k = i / 5f;
                Vector3 p = Vector3.Lerp(a, b, k) + new Vector3(0f, 0.9f, 0f);
                Particle(PrimitiveType.Sphere, parent, p, Vector3.one * 0.4f, color, Vector3.zero, Vector3.zero, 0.3f, 0f, false, false);
            }
        }
    }
}
