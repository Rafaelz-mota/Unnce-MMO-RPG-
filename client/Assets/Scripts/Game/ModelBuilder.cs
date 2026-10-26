using Mmorpg.Core.Data;
using UnityEngine;

namespace Mmorpg.Game
{
    /// <summary>Peças de um modelo montado por código (para animar e para escolher a altura do nome e da barra de vida).</summary>
    public sealed class ModelParts
    {
        public Transform Root;
        public Transform Body;
        public Transform[] Legs = new Transform[0];
        public Transform[] Arms = new Transform[0];
        public Transform[] Spinners = new Transform[0];
        public float Height = 2f;
        /// <summary>Escala (monsters.json "scale") com a qual o modelo já está desenhado no tamanho final.</summary>
        public float BaseScale = 1f;
        public float StrideSpeed = 7f;
        public float LegSwing = 38f;
        public float ArmSwing = 30f;
    }

    /// <summary>
    /// Monta personagens e monstros com primitivas (cápsulas, esferas, cubos), com forma e cor próprias por classe e por
    /// criatura. É um visual provisório melhor que a cápsula única, não um modelo final. Só apresentação.
    /// </summary>
    public static class ModelBuilder
    {
        private static Color Dk(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k); }
        private static Color Lt(Color c, float k) { return new Color(c.r + (1f - c.r) * k, c.g + (1f - c.g) * k, c.b + (1f - c.b) * k); }

        private static GameObject P(Transform parent, PrimitiveType t, string name, float x, float y, float z, float sx, float sy, float sz, Color c)
        {
            return Prims.Make(t, name, parent, new Vector3(x, y, z), new Vector3(sx, sy, sz), c, false);
        }

        private static ModelParts NewParts(Transform parent)
        {
            var go = new GameObject("Modelo");
            go.transform.SetParent(parent, false);
            var m = new ModelParts();
            m.Root = go.transform;
            m.Body = go.transform;
            return m;
        }

        // ------------------------------------------------------------------ jogadores
        public static ModelParts BuildPlayer(string classId, string faction, Transform parent)
        {
            Color cloth, accent, skin = new Color(0.9f, 0.74f, 0.6f);
            string weapon, headgear = "none";
            switch (classId)
            {
                case "guerreiro": cloth = new Color(0.55f, 0.58f, 0.65f); accent = new Color(0.75f, 0.2f, 0.18f); weapon = "sword"; headgear = "helm"; break;
                case "mago": cloth = new Color(0.2f, 0.3f, 0.75f); accent = new Color(0.4f, 0.9f, 1f); weapon = "staff"; headgear = "hat"; break;
                case "sacerdote": cloth = new Color(0.93f, 0.93f, 0.88f); accent = new Color(0.95f, 0.8f, 0.3f); weapon = "mace"; break;
                case "arqueiro": cloth = new Color(0.25f, 0.5f, 0.25f); accent = new Color(0.55f, 0.38f, 0.2f); weapon = "bow"; headgear = "hood"; break;
                case "assassino": cloth = new Color(0.2f, 0.2f, 0.24f); accent = new Color(0.7f, 0.15f, 0.2f); weapon = "dagger"; headgear = "hood"; break;
                case "bruxo": cloth = new Color(0.3f, 0.12f, 0.4f); accent = new Color(0.75f, 0.3f, 0.95f); weapon = "darkstaff"; headgear = "hood"; break;
                default:
                    bool dusk = faction != null && faction.IndexOf("crep", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    cloth = dusk ? new Color(0.45f, 0.2f, 0.4f) : new Color(0.25f, 0.55f, 0.95f);
                    accent = dusk ? new Color(0.9f, 0.4f, 0.3f) : new Color(0.95f, 0.8f, 0.3f);
                    weapon = "staff";
                    break;
            }
            return Humanoid(parent, cloth, accent, skin, weapon, headgear);
        }

        private static ModelParts Humanoid(Transform parent, Color cloth, Color accent, Color skin, string weapon, string headgear)
        {
            ModelParts m = NewParts(parent);
            Transform r = m.Root;

            // pernas (pivô no quadril)
            Transform legL = Prims.Pivot(r, "PernaE", new Vector3(-0.17f, 0.9f, 0f));
            Transform legR = Prims.Pivot(r, "PernaD", new Vector3(0.17f, 0.9f, 0f));
            P(legL, PrimitiveType.Cylinder, "Perna", 0f, -0.42f, 0f, 0.22f, 0.42f, 0.22f, Dk(cloth, 0.65f));
            P(legR, PrimitiveType.Cylinder, "Perna", 0f, -0.42f, 0f, 0.22f, 0.42f, 0.22f, Dk(cloth, 0.65f));
            P(legL, PrimitiveType.Cube, "Bota", 0f, -0.86f, 0.05f, 0.24f, 0.12f, 0.34f, Dk(accent, 0.5f));
            P(legR, PrimitiveType.Cube, "Bota", 0f, -0.86f, 0.05f, 0.24f, 0.12f, 0.34f, Dk(accent, 0.5f));

            // tronco, cinto, capa
            P(r, PrimitiveType.Cube, "Tronco", 0f, 1.25f, 0f, 0.62f, 0.78f, 0.36f, cloth);
            P(r, PrimitiveType.Cube, "Cinto", 0f, 0.92f, 0f, 0.66f, 0.1f, 0.4f, accent);
            P(r, PrimitiveType.Cube, "Capa", 0f, 1.12f, -0.24f, 0.5f, 0.95f, 0.05f, Dk(accent, 0.8f));

            // cabeça
            P(r, PrimitiveType.Sphere, "Cabeca", 0f, 1.85f, 0f, 0.42f, 0.44f, 0.42f, skin);
            P(r, PrimitiveType.Sphere, "OlhoE", -0.09f, 1.88f, 0.17f, 0.07f, 0.07f, 0.05f, Color.white);
            P(r, PrimitiveType.Sphere, "OlhoD", 0.09f, 1.88f, 0.17f, 0.07f, 0.07f, 0.05f, Color.white);
            switch (headgear)
            {
                case "helm":
                    P(r, PrimitiveType.Sphere, "Elmo", 0f, 1.95f, -0.01f, 0.48f, 0.4f, 0.48f, Dk(cloth, 0.9f));
                    P(r, PrimitiveType.Cube, "Crista", 0f, 2.18f, -0.02f, 0.06f, 0.2f, 0.34f, accent);
                    break;
                case "hat":
                    P(r, PrimitiveType.Cylinder, "Aba", 0f, 2.03f, 0f, 0.7f, 0.025f, 0.7f, Dk(cloth, 0.8f));
                    P(r, PrimitiveType.Cylinder, "Copa", 0f, 2.25f, 0f, 0.34f, 0.22f, 0.34f, Dk(cloth, 0.8f));
                    P(r, PrimitiveType.Cylinder, "Fita", 0f, 2.08f, 0f, 0.36f, 0.03f, 0.36f, accent);
                    break;
                case "hood":
                    P(r, PrimitiveType.Sphere, "Capuz", 0f, 1.93f, -0.05f, 0.5f, 0.46f, 0.5f, Dk(cloth, 0.85f));
                    break;
            }

            // braços (pivô no ombro)
            Transform armR = Prims.Pivot(r, "BracoD", new Vector3(0.42f, 1.6f, 0f));
            Transform armL = Prims.Pivot(r, "BracoE", new Vector3(-0.42f, 1.6f, 0f));
            P(armR, PrimitiveType.Cylinder, "Braco", 0f, -0.32f, 0f, 0.16f, 0.34f, 0.16f, cloth);
            P(armL, PrimitiveType.Cylinder, "Braco", 0f, -0.32f, 0f, 0.16f, 0.34f, 0.16f, cloth);
            P(armR, PrimitiveType.Sphere, "Mao", 0f, -0.68f, 0f, 0.14f, 0.14f, 0.14f, skin);
            P(armL, PrimitiveType.Sphere, "Mao", 0f, -0.68f, 0f, 0.14f, 0.14f, 0.14f, skin);
            P(armR, PrimitiveType.Sphere, "Ombro", 0f, 0f, 0f, 0.24f, 0.24f, 0.24f, accent);
            P(armL, PrimitiveType.Sphere, "Ombro", 0f, 0f, 0f, 0.24f, 0.24f, 0.24f, accent);

            // arma na mão direita (e escudo na esquerda, quando houver)
            Color steel = new Color(0.82f, 0.84f, 0.9f);
            Color wood = new Color(0.45f, 0.3f, 0.16f);
            switch (weapon)
            {
                case "sword":
                    P(armR, PrimitiveType.Cube, "Lamina", 0f, -0.68f, 0.5f, 0.07f, 0.12f, 0.95f, steel);
                    P(armR, PrimitiveType.Cube, "Guarda", 0f, -0.68f, 0.05f, 0.3f, 0.06f, 0.07f, accent);
                    P(armL, PrimitiveType.Cylinder, "Escudo", -0.12f, -0.5f, 0.12f, 0.62f, 0.05f, 0.62f, accent);
                    armL.localRotation = Quaternion.Euler(0f, 0f, 0f);
                    break;
                case "staff":
                    P(armR, PrimitiveType.Cylinder, "Cajado", 0f, -0.5f, 0.15f, 0.07f, 0.95f, 0.07f, wood);
                    P(armR, PrimitiveType.Sphere, "Orbe", 0f, 0.5f, 0.15f, 0.26f, 0.26f, 0.26f, accent);
                    break;
                case "darkstaff":
                    P(armR, PrimitiveType.Cylinder, "Cajado", 0f, -0.5f, 0.15f, 0.07f, 0.95f, 0.07f, new Color(0.2f, 0.12f, 0.2f));
                    P(armR, PrimitiveType.Sphere, "Orbe", 0f, 0.5f, 0.15f, 0.28f, 0.28f, 0.28f, accent);
                    P(armR, PrimitiveType.Cube, "Espinho", 0f, 0.33f, 0.15f, 0.07f, 0.16f, 0.07f, new Color(0.1f, 0.05f, 0.12f));
                    break;
                case "mace":
                    P(armR, PrimitiveType.Cylinder, "Cabo", 0f, -0.55f, 0.15f, 0.07f, 0.6f, 0.07f, wood);
                    P(armR, PrimitiveType.Sphere, "Cabeca", 0f, 0.1f, 0.15f, 0.26f, 0.26f, 0.26f, accent);
                    break;
                case "bow":
                    P(armL, PrimitiveType.Cube, "Arco", 0f, -0.6f, 0.2f, 0.05f, 1.2f, 0.06f, wood);
                    P(armL, PrimitiveType.Cube, "Corda", 0f, -0.6f, 0.08f, 0.015f, 1.1f, 0.015f, Color.white);
                    P(r, PrimitiveType.Cube, "Aljava", 0.12f, 1.35f, -0.28f, 0.14f, 0.55f, 0.14f, Dk(wood, 0.8f));
                    break;
                case "dagger":
                    P(armR, PrimitiveType.Cube, "Adaga", 0f, -0.68f, 0.28f, 0.05f, 0.08f, 0.5f, steel);
                    P(armL, PrimitiveType.Cube, "Adaga", 0f, -0.68f, 0.28f, 0.05f, 0.08f, 0.5f, steel);
                    break;
            }

            Prims.Hitbox(r, 2.2f, 0.9f);
            m.Legs = new[] { legL, legR };
            m.Arms = new[] { armR, armL };
            m.Height = 2.3f;
            return m;
        }

        // ------------------------------------------------------------------ monstros
        public static ModelParts BuildMonster(MonsterDef def, Color categoryColor, Transform parent)
        {
            switch (def.Id)
            {
                case "lobo_selvagem": return Wolf(parent, new Color(0.45f, 0.4f, 0.34f), new Color(1f, 0.85f, 0.2f), false);
                case "lobo_cinzento": return Wolf(parent, new Color(0.5f, 0.52f, 0.56f), new Color(1f, 0.6f, 0.2f), false);
                case "lobo_lunar_palido": return Wolf(parent, new Color(0.88f, 0.9f, 1f), new Color(0.5f, 0.8f, 1f), true);
                case "javali_do_vale": return Boar(parent, new Color(0.42f, 0.28f, 0.2f));
                case "broto_ancestral": return Sprout(parent);
                case "sentinela_de_pedra_rachada": return StoneGolem(parent);
                case "colosso_da_raiz": return RootColossus(parent);
                default: return Blob(parent, categoryColor);
            }
        }

        private static ModelParts Blob(Transform parent, Color c)
        {
            ModelParts m = NewParts(parent);
            P(m.Root, PrimitiveType.Sphere, "Corpo", 0f, 0.8f, 0f, 1.3f, 1.2f, 1.3f, c);
            P(m.Root, PrimitiveType.Sphere, "OlhoE", -0.25f, 1.0f, 0.55f, 0.18f, 0.18f, 0.12f, Color.white);
            P(m.Root, PrimitiveType.Sphere, "OlhoD", 0.25f, 1.0f, 0.55f, 0.18f, 0.18f, 0.12f, Color.white);
            Prims.Hitbox(m.Root, 1.6f, 1.3f);
            m.Height = 1.8f;
            return m;
        }

        private static ModelParts Wolf(Transform parent, Color fur, Color eyes, bool lunar)
        {
            ModelParts m = NewParts(parent);
            Transform r = m.Root;
            P(r, PrimitiveType.Sphere, "Corpo", 0f, 0.78f, 0f, 0.62f, 0.58f, 1.3f, fur);
            P(r, PrimitiveType.Sphere, "Peito", 0f, 0.82f, 0.4f, 0.7f, 0.66f, 0.7f, Dk(fur, 0.95f));
            P(r, PrimitiveType.Sphere, "Cabeca", 0f, 0.98f, 0.82f, 0.46f, 0.42f, 0.5f, fur);
            P(r, PrimitiveType.Cube, "Focinho", 0f, 0.9f, 1.1f, 0.2f, 0.17f, 0.34f, Dk(fur, 0.85f));
            P(r, PrimitiveType.Sphere, "Nariz", 0f, 0.95f, 1.28f, 0.1f, 0.09f, 0.09f, new Color(0.08f, 0.08f, 0.08f));
            P(r, PrimitiveType.Cube, "OrelhaE", -0.15f, 1.27f, 0.72f, 0.12f, 0.22f, 0.08f, Dk(fur, 0.8f));
            P(r, PrimitiveType.Cube, "OrelhaD", 0.15f, 1.27f, 0.72f, 0.12f, 0.22f, 0.08f, Dk(fur, 0.8f));
            P(r, PrimitiveType.Sphere, "OlhoE", -0.12f, 1.05f, 1.02f, 0.07f, 0.06f, 0.05f, eyes);
            P(r, PrimitiveType.Sphere, "OlhoD", 0.12f, 1.05f, 1.02f, 0.07f, 0.06f, 0.05f, eyes);

            Transform tail = Prims.Pivot(r, "Cauda", new Vector3(0f, 0.85f, -0.6f));
            tail.localRotation = Quaternion.Euler(-55f, 0f, 0f);
            P(tail, PrimitiveType.Cylinder, "Cauda", 0f, -0.28f, 0f, 0.14f, 0.3f, 0.14f, Dk(fur, 0.9f));

            Transform fl = Prims.Pivot(r, "PataFE", new Vector3(-0.22f, 0.6f, 0.45f));
            Transform fr = Prims.Pivot(r, "PataFD", new Vector3(0.22f, 0.6f, 0.45f));
            Transform bl = Prims.Pivot(r, "PataTE", new Vector3(-0.22f, 0.6f, -0.4f));
            Transform br = Prims.Pivot(r, "PataTD", new Vector3(0.22f, 0.6f, -0.4f));
            Transform[] legs = { fl, br, fr, bl }; // diagonais juntas
            for (int i = 0; i < legs.Length; i++)
                P(legs[i], PrimitiveType.Cylinder, "Pata", 0f, -0.3f, 0f, 0.15f, 0.3f, 0.15f, Dk(fur, 0.75f));

            if (lunar)
            {
                // aura lunar: três luas pequenas orbitando
                Transform orbit = Prims.Pivot(r, "Orbita", new Vector3(0f, 1.1f, 0f));
                for (int i = 0; i < 3; i++)
                {
                    float a = i * 2.094f;
                    P(orbit, PrimitiveType.Sphere, "Lua", Mathf.Cos(a) * 1.2f, 0.4f * Mathf.Sin(a * 2f), Mathf.Sin(a) * 1.2f, 0.16f, 0.16f, 0.16f, new Color(0.75f, 0.9f, 1f));
                }
                m.Spinners = new[] { orbit };
            }

            Prims.Hitbox(r, 1.5f, 1.3f);
            m.Legs = legs;
            m.LegSwing = 42f;
            m.Height = 1.7f;
            return m;
        }

        private static ModelParts Boar(Transform parent, Color hide)
        {
            ModelParts m = NewParts(parent);
            Transform r = m.Root;
            P(r, PrimitiveType.Sphere, "Corpo", 0f, 0.8f, 0f, 0.95f, 0.85f, 1.35f, hide);
            P(r, PrimitiveType.Sphere, "Ombros", 0f, 0.92f, 0.4f, 1.0f, 0.95f, 0.8f, Dk(hide, 0.9f));
            P(r, PrimitiveType.Sphere, "Cabeca", 0f, 0.82f, 0.95f, 0.6f, 0.55f, 0.6f, hide);
            GameObject snout = P(r, PrimitiveType.Cylinder, "Focinho", 0f, 0.74f, 1.3f, 0.3f, 0.06f, 0.3f, new Color(0.7f, 0.5f, 0.45f));
            snout.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            P(r, PrimitiveType.Cube, "PresaE", -0.2f, 0.72f, 1.22f, 0.07f, 0.2f, 0.07f, new Color(0.95f, 0.93f, 0.8f));
            P(r, PrimitiveType.Cube, "PresaD", 0.2f, 0.72f, 1.22f, 0.07f, 0.2f, 0.07f, new Color(0.95f, 0.93f, 0.8f));
            P(r, PrimitiveType.Cube, "OrelhaE", -0.22f, 1.14f, 0.85f, 0.14f, 0.2f, 0.08f, Dk(hide, 0.7f));
            P(r, PrimitiveType.Cube, "OrelhaD", 0.22f, 1.14f, 0.85f, 0.14f, 0.2f, 0.08f, Dk(hide, 0.7f));
            P(r, PrimitiveType.Sphere, "OlhoE", -0.17f, 0.92f, 1.2f, 0.07f, 0.07f, 0.05f, new Color(0.9f, 0.2f, 0.1f));
            P(r, PrimitiveType.Sphere, "OlhoD", 0.17f, 0.92f, 1.2f, 0.07f, 0.07f, 0.05f, new Color(0.9f, 0.2f, 0.1f));
            P(r, PrimitiveType.Cube, "Cerdas", 0f, 1.32f, 0.1f, 0.12f, 0.2f, 1.0f, Dk(hide, 0.55f));

            Transform fl = Prims.Pivot(r, "PataFE", new Vector3(-0.3f, 0.5f, 0.5f));
            Transform fr = Prims.Pivot(r, "PataFD", new Vector3(0.3f, 0.5f, 0.5f));
            Transform bl = Prims.Pivot(r, "PataTE", new Vector3(-0.3f, 0.5f, -0.45f));
            Transform br = Prims.Pivot(r, "PataTD", new Vector3(0.3f, 0.5f, -0.45f));
            Transform[] legs = { fl, br, fr, bl };
            for (int i = 0; i < legs.Length; i++)
                P(legs[i], PrimitiveType.Cylinder, "Pata", 0f, -0.25f, 0f, 0.22f, 0.25f, 0.22f, Dk(hide, 0.7f));

            Prims.Hitbox(r, 1.5f, 1.4f);
            m.Legs = legs;
            m.LegSwing = 32f;
            m.Height = 1.7f;
            return m;
        }

        private static ModelParts Sprout(Transform parent)
        {
            ModelParts m = NewParts(parent);
            Transform r = m.Root;
            Color bark = new Color(0.42f, 0.3f, 0.18f);
            Color leaf = new Color(0.3f, 0.7f, 0.25f);
            P(r, PrimitiveType.Cylinder, "Tronco", 0f, 0.65f, 0f, 0.8f, 0.65f, 0.8f, bark);
            P(r, PrimitiveType.Sphere, "Topo", 0f, 1.25f, 0f, 0.85f, 0.5f, 0.85f, Dk(bark, 1.1f));
            P(r, PrimitiveType.Sphere, "OlhoE", -0.18f, 0.95f, 0.36f, 0.16f, 0.2f, 0.1f, new Color(1f, 0.95f, 0.5f));
            P(r, PrimitiveType.Sphere, "OlhoD", 0.18f, 0.95f, 0.36f, 0.16f, 0.2f, 0.1f, new Color(1f, 0.95f, 0.5f));
            P(r, PrimitiveType.Cube, "Boca", 0f, 0.62f, 0.4f, 0.28f, 0.07f, 0.05f, new Color(0.15f, 0.08f, 0.04f));
            for (int i = 0; i < 5; i++)
            {
                float a = i * 1.2566f;
                GameObject l = P(r, PrimitiveType.Sphere, "Folha", Mathf.Cos(a) * 0.32f, 1.62f, Mathf.Sin(a) * 0.32f, 0.3f, 0.12f, 0.55f, leaf);
                l.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 25f);
            }
            P(r, PrimitiveType.Sphere, "Broto", 0f, 1.7f, 0f, 0.2f, 0.3f, 0.2f, Lt(leaf, 0.3f));

            Transform legL = Prims.Pivot(r, "RaizE", new Vector3(-0.25f, 0.3f, 0f));
            Transform legR = Prims.Pivot(r, "RaizD", new Vector3(0.25f, 0.3f, 0f));
            P(legL, PrimitiveType.Cylinder, "Raiz", 0f, -0.15f, 0f, 0.2f, 0.17f, 0.2f, Dk(bark, 0.8f));
            P(legR, PrimitiveType.Cylinder, "Raiz", 0f, -0.15f, 0f, 0.2f, 0.17f, 0.2f, Dk(bark, 0.8f));
            Transform armL = Prims.Pivot(r, "GalhoE", new Vector3(-0.42f, 0.95f, 0f));
            Transform armR = Prims.Pivot(r, "GalhoD", new Vector3(0.42f, 0.95f, 0f));
            P(armL, PrimitiveType.Cylinder, "Galho", 0f, -0.25f, 0f, 0.12f, 0.28f, 0.12f, bark);
            P(armR, PrimitiveType.Cylinder, "Galho", 0f, -0.25f, 0f, 0.12f, 0.28f, 0.12f, bark);

            Prims.Hitbox(r, 1.9f, 1.0f);
            m.Legs = new[] { legL, legR };
            m.Arms = new[] { armR, armL };
            m.StrideSpeed = 9f;
            m.Height = 2.1f;
            return m;
        }

        private static ModelParts StoneGolem(Transform parent)
        {
            ModelParts m = NewParts(parent);
            Transform r = m.Root;
            Color stone = new Color(0.52f, 0.52f, 0.56f);
            Color dark = new Color(0.3f, 0.3f, 0.33f);
            Color glow = new Color(1f, 0.8f, 0.25f);
            Transform legL = Prims.Pivot(r, "PernaE", new Vector3(-0.35f, 1.0f, 0f));
            Transform legR = Prims.Pivot(r, "PernaD", new Vector3(0.35f, 1.0f, 0f));
            P(legL, PrimitiveType.Cube, "Perna", 0f, -0.5f, 0f, 0.55f, 1.0f, 0.6f, dark);
            P(legR, PrimitiveType.Cube, "Perna", 0f, -0.5f, 0f, 0.55f, 1.0f, 0.6f, dark);
            P(r, PrimitiveType.Cube, "Tronco", 0f, 1.7f, 0f, 1.5f, 1.3f, 0.95f, stone);
            P(r, PrimitiveType.Cube, "Peitoral", 0f, 1.75f, 0.5f, 0.9f, 0.7f, 0.1f, dark);
            P(r, PrimitiveType.Cube, "Rachadura", 0.2f, 1.8f, 0.56f, 0.07f, 0.9f, 0.04f, glow);
            P(r, PrimitiveType.Cube, "Rachadura2", -0.3f, 1.55f, 0.56f, 0.4f, 0.06f, 0.04f, glow);
            P(r, PrimitiveType.Cube, "Cabeca", 0f, 2.6f, 0f, 0.7f, 0.6f, 0.7f, stone);
            P(r, PrimitiveType.Cube, "OlhoE", -0.17f, 2.65f, 0.36f, 0.15f, 0.08f, 0.04f, glow);
            P(r, PrimitiveType.Cube, "OlhoD", 0.17f, 2.65f, 0.36f, 0.15f, 0.08f, 0.04f, glow);
            P(r, PrimitiveType.Sphere, "MusgoE", -0.55f, 2.4f, -0.1f, 0.4f, 0.2f, 0.4f, new Color(0.3f, 0.5f, 0.25f));
            Transform armL = Prims.Pivot(r, "BracoE", new Vector3(-1.0f, 2.1f, 0f));
            Transform armR = Prims.Pivot(r, "BracoD", new Vector3(1.0f, 2.1f, 0f));
            P(armL, PrimitiveType.Cube, "Braco", 0f, -0.7f, 0f, 0.5f, 1.4f, 0.5f, stone);
            P(armR, PrimitiveType.Cube, "Braco", 0f, -0.7f, 0f, 0.5f, 1.4f, 0.5f, stone);
            P(armL, PrimitiveType.Cube, "Punho", 0f, -1.5f, 0f, 0.7f, 0.6f, 0.7f, dark);
            P(armR, PrimitiveType.Cube, "Punho", 0f, -1.5f, 0f, 0.7f, 0.6f, 0.7f, dark);
            Prims.Hitbox(r, 3.0f, 1.8f);
            m.Legs = new[] { legL, legR };
            m.Arms = new[] { armR, armL };
            m.StrideSpeed = 5f;
            m.LegSwing = 22f;
            m.ArmSwing = 18f;
            m.Height = 3.2f;
            m.BaseScale = 1.8f;
            return m;
        }

        private static ModelParts RootColossus(Transform parent)
        {
            ModelParts m = NewParts(parent);
            Transform r = m.Root;
            Color bark = new Color(0.36f, 0.25f, 0.15f);
            Color leaf = new Color(0.18f, 0.5f, 0.2f);
            Color glow = new Color(1f, 0.25f, 0.15f);
            Transform legL = Prims.Pivot(r, "PernaE", new Vector3(-0.5f, 1.2f, 0f));
            Transform legR = Prims.Pivot(r, "PernaD", new Vector3(0.5f, 1.2f, 0f));
            P(legL, PrimitiveType.Cylinder, "Raiz", 0f, -0.6f, 0f, 0.7f, 0.6f, 0.7f, Dk(bark, 0.85f));
            P(legR, PrimitiveType.Cylinder, "Raiz", 0f, -0.6f, 0f, 0.7f, 0.6f, 0.7f, Dk(bark, 0.85f));
            for (int i = 0; i < 5; i++)
            {
                float a = i * 1.2566f;
                GameObject root = P(r, PrimitiveType.Cylinder, "RaizSolo", Mathf.Cos(a) * 1.3f, 0.15f, Mathf.Sin(a) * 1.3f, 0.25f, 0.55f, 0.25f, Dk(bark, 0.75f));
                root.transform.localRotation = Quaternion.Euler(Mathf.Sin(a) * 70f, 0f, -Mathf.Cos(a) * 70f);
            }
            P(r, PrimitiveType.Cylinder, "Tronco", 0f, 2.4f, 0f, 1.7f, 1.3f, 1.5f, bark);
            P(r, PrimitiveType.Sphere, "Ombros", 0f, 3.5f, 0f, 2.2f, 0.9f, 1.5f, Dk(bark, 1.1f));
            P(r, PrimitiveType.Sphere, "Cabeca", 0f, 4.2f, 0.1f, 1.1f, 1.0f, 1.0f, Dk(bark, 1.15f));
            P(r, PrimitiveType.Sphere, "OlhoE", -0.3f, 4.3f, 0.55f, 0.28f, 0.2f, 0.15f, glow);
            P(r, PrimitiveType.Sphere, "OlhoD", 0.3f, 4.3f, 0.55f, 0.28f, 0.2f, 0.15f, glow);
            P(r, PrimitiveType.Sphere, "NucleoDoPeito", 0f, 2.9f, 0.72f, 0.5f, 0.5f, 0.25f, glow);
            P(r, PrimitiveType.Sphere, "Copa1", 0f, 5.1f, 0f, 2.0f, 1.0f, 2.0f, leaf);
            P(r, PrimitiveType.Sphere, "Copa2", -0.9f, 4.9f, 0.3f, 1.2f, 0.8f, 1.2f, Lt(leaf, 0.1f));
            P(r, PrimitiveType.Sphere, "Copa3", 0.9f, 4.9f, -0.2f, 1.3f, 0.8f, 1.3f, Dk(leaf, 0.85f));
            for (int i = 0; i < 6; i++)
            {
                float a = i * 1.0472f;
                GameObject thorn = P(r, PrimitiveType.Cube, "Espinho", Mathf.Cos(a) * 0.85f, 3.9f, Mathf.Sin(a) * 0.7f - 0.3f, 0.12f, 0.5f, 0.12f, new Color(0.25f, 0.18f, 0.1f));
                thorn.transform.localRotation = Quaternion.Euler(Mathf.Sin(a) * 25f, 0f, -Mathf.Cos(a) * 25f);
            }
            Transform armL = Prims.Pivot(r, "BracoE", new Vector3(-1.5f, 3.4f, 0f));
            Transform armR = Prims.Pivot(r, "BracoD", new Vector3(1.5f, 3.4f, 0f));
            P(armL, PrimitiveType.Cylinder, "Braco", 0f, -1.1f, 0f, 0.55f, 1.1f, 0.55f, bark);
            P(armR, PrimitiveType.Cylinder, "Braco", 0f, -1.1f, 0f, 0.55f, 1.1f, 0.55f, bark);
            P(armL, PrimitiveType.Sphere, "Punho", 0f, -2.3f, 0f, 1.0f, 0.9f, 1.0f, Dk(bark, 0.9f));
            P(armR, PrimitiveType.Sphere, "Punho", 0f, -2.3f, 0f, 1.0f, 0.9f, 1.0f, Dk(bark, 0.9f));
            Prims.Hitbox(r, 5.2f, 3.0f);
            m.Legs = new[] { legL, legR };
            m.Arms = new[] { armR, armL };
            m.StrideSpeed = 4f;
            m.LegSwing = 16f;
            m.ArmSwing = 14f;
            m.Height = 5.8f;
            m.BaseScale = 3.2f;
            return m;
        }
    }
}
