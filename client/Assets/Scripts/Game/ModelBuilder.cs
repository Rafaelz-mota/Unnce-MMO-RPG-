using System.Collections.Generic;
using Mmorpg.Core.Data;
using Mmorpg.Core.Presentation;
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
        /// <summary>Altura relativa do personagem (presentation.json); só para jogadores.</summary>
        public float HeightScale = 1f;
        /// <summary>True enquanto o corpo é feito de primitivas (nenhum prefab real instalado).</summary>
        public bool IsPlaceholder = true;
        public Vector3 Torso = new Vector3(0.62f, 0.78f, 0.36f);
        /// <summary>Encaixes por nome: hand_r, hand_l, head, chest.</summary>
        public Dictionary<string, Transform> Sockets = new Dictionary<string, Transform>();
        /// <summary>Adorno da classe na cabeça; fica oculto quando há item de cabeça equipado.</summary>
        public Transform ClassHeadgear;
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
        /// <summary>
        /// Corpo do jogador a partir da aparência da classe (presentation.json). Se a classe tem um prefab real em
        /// Resources, ele é usado; senão monta o visual provisório de primitivas (marcado em IsPlaceholder).
        /// Arma e armadura NÃO vêm daqui: são colocadas por EquipmentVisuals nos encaixes (Sockets).
        /// </summary>
        public static ModelParts BuildPlayer(ClassLookDef look, Transform parent)
        {
            if (look == null) look = new ClassLookDef { Id = "?", Cloth = "#4080F0", Accent = "#F2CC4D" };
            ModelParts prefab = TryPrefab(look, parent);
            if (prefab != null) return prefab;
            return Humanoid(parent, look);
        }

        private static ModelParts TryPrefab(ClassLookDef look, Transform parent)
        {
            if (string.IsNullOrEmpty(look.Prefab)) return null;
            GameObject asset = Resources.Load<GameObject>(look.Prefab);
            if (asset == null)
            {
                Debug.LogWarning("Prefab da classe " + look.Id + " não encontrado em Resources/" + look.Prefab + "; usando o visual provisório.");
                return null;
            }
            GameObject inst = UnityEngine.Object.Instantiate(asset);
            ModelParts m = NewParts(parent);
            inst.transform.SetParent(m.Root, false);
            m.IsPlaceholder = false;
            m.HeightScale = (float)look.Height;
            m.Height = 2.2f;
            // Encaixes: objetos filhos do prefab com estes nomes (veja docs/VISUAL.md). Sem rig/Animator configurado, fica em pose estática.
            string[] names = { "hand_r", "hand_l", "head", "chest" };
            for (int i = 0; i < names.Length; i++)
            {
                Transform f = FindDeep(inst.transform, names[i]);
                if (f != null) m.Sockets[names[i]] = f;
            }
            Prims.Hitbox(m.Root, 2.2f, 0.9f);
            return m;
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t == null) return null;
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                Transform f = FindDeep(t.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }

        private static Transform Anchor(Transform parent, string name, float x, float y, float z)
        {
            return Prims.Pivot(parent, name, new Vector3(x, y, z));
        }

        private static ModelParts Humanoid(Transform parent, ClassLookDef look)
        {
            Color cloth = Conv.Hex(look.Cloth, Color.gray);
            Color accent = Conv.Hex(look.Accent, Color.white);
            Color skin = Conv.Hex(look.Skin, new Color(0.9f, 0.74f, 0.6f));
            float tw = 0.62f, td = 0.36f, limb = 1f;
            if (look.Build == "heavy") { tw = 0.8f; td = 0.44f; limb = 1.25f; }
            else if (look.Build == "slim") { tw = 0.5f; td = 0.3f; limb = 0.85f; }

            ModelParts m = NewParts(parent);
            m.HeightScale = (float)look.Height;
            m.Torso = new Vector3(tw, 0.78f, td);
            Transform r = m.Root;
            float armX = tw * 0.5f + 0.11f * limb;

            Transform legL = Prims.Pivot(r, "PernaE", new Vector3(-0.17f * limb, 0.9f, 0f));
            Transform legR = Prims.Pivot(r, "PernaD", new Vector3(0.17f * limb, 0.9f, 0f));
            P(legL, PrimitiveType.Cylinder, "Perna", 0f, -0.42f, 0f, 0.22f * limb, 0.42f, 0.22f * limb, Dk(cloth, 0.65f));
            P(legR, PrimitiveType.Cylinder, "Perna", 0f, -0.42f, 0f, 0.22f * limb, 0.42f, 0.22f * limb, Dk(cloth, 0.65f));
            P(legL, PrimitiveType.Cube, "Bota", 0f, -0.86f, 0.05f, 0.24f * limb, 0.12f, 0.34f, Dk(accent, 0.5f));
            P(legR, PrimitiveType.Cube, "Bota", 0f, -0.86f, 0.05f, 0.24f * limb, 0.12f, 0.34f, Dk(accent, 0.5f));

            P(r, PrimitiveType.Cube, "Tronco", 0f, 1.25f, 0f, tw, 0.78f, td, cloth);
            P(r, PrimitiveType.Cube, "Cinto", 0f, 0.92f, 0f, tw + 0.04f, 0.1f, td + 0.04f, accent);
            if (look.Robe)
            {
                P(r, PrimitiveType.Cylinder, "Tunica", 0f, 0.5f, 0f, tw * 1.35f, 0.5f, td * 2.6f, cloth);
                P(r, PrimitiveType.Cube, "BarraDaTunica", 0f, 0.04f, 0f, tw * 1.45f, 0.06f, td * 2.7f, accent);
            }
            if (look.Cape) P(r, PrimitiveType.Cube, "Capa", 0f, 1.12f, -td * 0.5f - 0.07f, tw * 0.85f, 0.95f, 0.05f, Dk(accent, 0.8f));

            P(r, PrimitiveType.Sphere, "Cabeca", 0f, 1.85f, 0f, 0.42f, 0.44f, 0.42f, skin);
            P(r, PrimitiveType.Sphere, "OlhoE", -0.09f, 1.88f, 0.17f, 0.07f, 0.07f, 0.05f, Color.white);
            P(r, PrimitiveType.Sphere, "OlhoD", 0.09f, 1.88f, 0.17f, 0.07f, 0.07f, 0.05f, Color.white);
            Transform gear = Prims.Pivot(r, "AdornoDaClasse", new Vector3(0f, 0f, 0f));
            Headgear(gear, look.Headgear, cloth, accent);
            m.ClassHeadgear = gear;

            Transform armR = Prims.Pivot(r, "BracoD", new Vector3(armX, 1.6f, 0f));
            Transform armL = Prims.Pivot(r, "BracoE", new Vector3(-armX, 1.6f, 0f));
            P(armR, PrimitiveType.Cylinder, "Braco", 0f, -0.32f, 0f, 0.16f * limb, 0.34f, 0.16f * limb, cloth);
            P(armL, PrimitiveType.Cylinder, "Braco", 0f, -0.32f, 0f, 0.16f * limb, 0.34f, 0.16f * limb, cloth);
            P(armR, PrimitiveType.Sphere, "Mao", 0f, -0.68f, 0f, 0.14f, 0.14f, 0.14f, skin);
            P(armL, PrimitiveType.Sphere, "Mao", 0f, -0.68f, 0f, 0.14f, 0.14f, 0.14f, skin);
            Shoulders(armR, armL, look.Shoulders, accent, limb);

            if (look.OffHand == "shield")
                P(armL, PrimitiveType.Cylinder, "Escudo", -0.14f, -0.5f, 0.14f, 0.66f, 0.05f, 0.66f, accent).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            switch (look.Back)
            {
                case "quiver": P(r, PrimitiveType.Cube, "Aljava", 0.12f, 1.35f, -td * 0.5f - 0.1f, 0.14f, 0.55f, 0.14f, new Color(0.35f, 0.24f, 0.13f)); break;
                case "backpack":
                    P(r, PrimitiveType.Cube, "Mochila", 0f, 1.3f, -td * 0.5f - 0.15f, tw * 0.8f, 0.6f, 0.26f, new Color(0.45f, 0.32f, 0.18f));
                    P(r, PrimitiveType.Cylinder, "Rolo", 0f, 1.65f, -td * 0.5f - 0.15f, 0.14f, tw * 0.4f, 0.14f, Dk(accent, 0.9f)).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    break;
                case "tome": P(r, PrimitiveType.Cube, "Livro", tw * 0.5f + 0.05f, 0.85f, 0.05f, 0.1f, 0.26f, 0.2f, accent); break;
            }

            m.Sockets["hand_r"] = Anchor(armR, "hand_r", 0f, -0.68f, 0f);
            m.Sockets["hand_l"] = Anchor(armL, "hand_l", 0f, -0.68f, 0f);
            m.Sockets["head"] = Anchor(r, "head", 0f, 1.85f, 0f);
            m.Sockets["chest"] = Anchor(r, "chest", 0f, 1.25f, 0f);

            Prims.Hitbox(r, 2.2f, 0.9f);
            m.Legs = new[] { legL, legR };
            m.Arms = new[] { armR, armL };
            m.Height = 2.3f;
            return m;
        }

        private static void Shoulders(Transform armR, Transform armL, string style, Color accent, float limb)
        {
            Transform[] arms = { armR, armL };
            for (int i = 0; i < 2; i++)
            {
                Transform a = arms[i];
                switch (style)
                {
                    case "round": P(a, PrimitiveType.Sphere, "Ombreira", 0f, 0.02f, 0f, 0.34f, 0.26f, 0.34f, accent); break;
                    case "large": P(a, PrimitiveType.Cube, "Ombreira", 0f, 0.05f, 0f, 0.44f, 0.18f, 0.4f, accent); break;
                    case "spiked":
                        P(a, PrimitiveType.Sphere, "Ombreira", 0f, 0.02f, 0f, 0.32f, 0.24f, 0.32f, Dk(accent, 0.85f));
                        P(a, PrimitiveType.Cube, "Espinho", 0f, 0.26f, 0f, 0.09f, 0.3f, 0.09f, Lt(accent, 0.2f));
                        break;
                    default: P(a, PrimitiveType.Sphere, "Ombro", 0f, 0f, 0f, 0.22f * limb, 0.22f * limb, 0.22f * limb, Dk(accent, 0.9f)); break;
                }
            }
        }

        private static void Headgear(Transform t, string kind, Color cloth, Color accent)
        {
            Color bone = new Color(0.9f, 0.87f, 0.78f);
            switch (kind)
            {
                case "helm":
                    P(t, PrimitiveType.Sphere, "Elmo", 0f, 1.95f, -0.01f, 0.48f, 0.4f, 0.48f, Dk(cloth, 0.9f));
                    P(t, PrimitiveType.Cube, "Crista", 0f, 2.18f, -0.02f, 0.06f, 0.2f, 0.34f, accent);
                    break;
                case "hat":
                    P(t, PrimitiveType.Cylinder, "Aba", 0f, 2.03f, 0f, 0.7f, 0.025f, 0.7f, Dk(cloth, 0.8f));
                    P(t, PrimitiveType.Cylinder, "Copa", 0f, 2.25f, 0f, 0.34f, 0.22f, 0.34f, Dk(cloth, 0.8f));
                    P(t, PrimitiveType.Cylinder, "Fita", 0f, 2.08f, 0f, 0.36f, 0.03f, 0.36f, accent);
                    break;
                case "hood":
                    P(t, PrimitiveType.Sphere, "Capuz", 0f, 1.93f, -0.05f, 0.5f, 0.46f, 0.5f, Dk(cloth, 0.85f));
                    break;
                case "crown":
                    P(t, PrimitiveType.Cylinder, "Coroa", 0f, 2.1f, 0f, 0.4f, 0.06f, 0.4f, accent);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 1.2566f;
                        P(t, PrimitiveType.Cube, "PontaDaCoroa", Mathf.Cos(a) * 0.18f, 2.22f, Mathf.Sin(a) * 0.18f, 0.06f, 0.14f, 0.06f, accent);
                    }
                    break;
                case "horns":
                    for (int s = -1; s <= 1; s += 2)
                    {
                        GameObject h = P(t, PrimitiveType.Cube, "Chifre", s * 0.24f, 2.12f, 0f, 0.08f, 0.36f, 0.08f, bone);
                        h.transform.localRotation = Quaternion.Euler(0f, 0f, -s * 28f);
                    }
                    break;
                case "goggles":
                    P(t, PrimitiveType.Cube, "Tira", 0f, 1.95f, 0.02f, 0.46f, 0.07f, 0.42f, Dk(cloth, 0.5f));
                    P(t, PrimitiveType.Sphere, "LenteE", -0.1f, 1.96f, 0.2f, 0.13f, 0.13f, 0.08f, new Color(0.5f, 0.85f, 1f));
                    P(t, PrimitiveType.Sphere, "LenteD", 0.1f, 1.96f, 0.2f, 0.13f, 0.13f, 0.08f, new Color(0.5f, 0.85f, 1f));
                    break;
                case "headband":
                    P(t, PrimitiveType.Cylinder, "Faixa", 0f, 1.98f, 0f, 0.44f, 0.04f, 0.44f, accent);
                    break;
                case "plume":
                    P(t, PrimitiveType.Sphere, "Gorro", 0f, 1.98f, -0.02f, 0.45f, 0.26f, 0.45f, Dk(cloth, 0.9f));
                    GameObject f = P(t, PrimitiveType.Cube, "Pena", 0.12f, 2.2f, -0.08f, 0.05f, 0.4f, 0.14f, accent);
                    f.transform.localRotation = Quaternion.Euler(20f, 0f, -25f);
                    break;
                case "mask":
                    P(t, PrimitiveType.Cube, "Mascara", 0f, 1.78f, 0.17f, 0.3f, 0.2f, 0.06f, bone);
                    P(t, PrimitiveType.Sphere, "Capuz", 0f, 1.95f, -0.06f, 0.5f, 0.4f, 0.5f, Dk(cloth, 0.8f));
                    break;
                case "skull":
                    P(t, PrimitiveType.Sphere, "Cranio", 0f, 2.05f, 0.1f, 0.34f, 0.3f, 0.34f, bone);
                    P(t, PrimitiveType.Sphere, "OrbitaE", -0.07f, 2.07f, 0.26f, 0.08f, 0.08f, 0.05f, new Color(0.1f, 0.05f, 0.1f));
                    P(t, PrimitiveType.Sphere, "OrbitaD", 0.07f, 2.07f, 0.26f, 0.08f, 0.08f, 0.05f, new Color(0.1f, 0.05f, 0.1f));
                    break;
                case "circlet":
                    P(t, PrimitiveType.Cylinder, "Diadema", 0f, 2.02f, 0f, 0.44f, 0.03f, 0.44f, accent);
                    P(t, PrimitiveType.Sphere, "Gema", 0f, 2.04f, 0.22f, 0.1f, 0.1f, 0.06f, Lt(accent, 0.5f));
                    break;
            }
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
