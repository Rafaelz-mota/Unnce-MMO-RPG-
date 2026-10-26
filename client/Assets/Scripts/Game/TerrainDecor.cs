using Mmorpg.Core.Data;
using UnityEngine;

namespace Mmorpg.Game
{
    /// <summary>
    /// Enfeites do mapa feitos de primitivas: manchas de terreno, caminhos, grama, flores, floresta ao redor, pedras da
    /// arena do chefe e o acampamento do ponto de nascimento. Nada aqui tem colisor nem afeta o jogo; os obstáculos reais
    /// (regions.json) continuam sendo os da simulação. Posições vêm de um gerador determinístico (mesmo mapa sempre).
    /// </summary>
    public static class TerrainDecor
    {
        private struct Lcg
        {
            private uint _s;
            public Lcg(uint seed) { _s = seed * 2654435761u + 12345u; }
            public float Next()
            {
                unchecked { _s = _s * 1664525u + 1013904223u; }
                return (_s >> 8) / 16777216f;
            }
            public float Range(float a, float b) { return a + (b - a) * Next(); }
        }

        private static Color Dk(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k); }

        private static uint Hash(string s)
        {
            uint h = 17;
            if (s != null) for (int i = 0; i < s.Length; i++) h = h * 31 + s[i];
            return h;
        }

        public static void Build(RegionDef region, Transform root)
        {
            var holder = new GameObject("Enfeites");
            holder.transform.SetParent(root, false);
            Transform t = holder.transform;
            var rng = new Lcg(Hash(region.Id));
            BoundsDef b = region.Bounds;
            float minX = (float)b.MinX, maxX = (float)b.MaxX, minZ = (float)b.MinZ, maxZ = (float)b.MaxZ;
            Vector3 spawn = new Vector3((float)region.PlayerSpawn.X, 0f, (float)region.PlayerSpawn.Z);

            Patches(t, rng, minX, maxX, minZ, maxZ);
            Paths(t, rng, region, spawn);
            Grass(t, rng, minX, maxX, minZ, maxZ, spawn);
            Flowers(t, rng, minX, maxX, minZ, maxZ, spawn);
            Forest(t, rng, minX, maxX, minZ, maxZ);
            Camp(t, spawn);
            foreach (SpawnZoneDef z in region.SpawnZones)
                if (z.Id != null && z.Id.StartsWith("arena")) { Arena(t, new Vector3((float)z.CenterX, 0f, (float)z.CenterZ)); break; }
        }

        // ------------------------------------------------------------------ chão
        private static void Patches(Transform t, Lcg rng, float minX, float maxX, float minZ, float maxZ)
        {
            Color[] cols =
            {
                new Color(0.36f, 0.55f, 0.3f), new Color(0.27f, 0.45f, 0.24f),
                new Color(0.42f, 0.57f, 0.29f), new Color(0.31f, 0.5f, 0.33f)
            };
            for (int i = 0; i < 36; i++)
            {
                int level = i % 4; // mesma altura = mesma cor: manchas que se sobrepõem não "brigam" na tela
                float r = rng.Range(5f, 13f);
                float x = rng.Range(minX + r * 0.5f, maxX - r * 0.5f);
                float z = rng.Range(minZ + r * 0.5f, maxZ - r * 0.5f);
                Prims.MakeShared(PrimitiveType.Cylinder, "Mancha", t, new Vector3(x, 0.004f + 0.003f * level, z), new Vector3(r * 2f, 0.001f, r * 2f * rng.Range(0.7f, 1f)), cols[level]);
            }
        }

        private static void Paths(Transform t, Lcg rng, RegionDef region, Vector3 spawn)
        {
            Color dirt = new Color(0.63f, 0.55f, 0.4f);
            foreach (SpawnZoneDef z in region.SpawnZones)
            {
                Vector3 to = new Vector3((float)z.CenterX, 0f, (float)z.CenterZ);
                float dx = to.x - spawn.x, dz = to.z - spawn.z;
                float len = Mathf.Sqrt(dx * dx + dz * dz);
                if (len < 6f) continue;
                float ang = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
                int steps = Mathf.Max(2, Mathf.RoundToInt(len / 4f));
                float nx = dz / len, nz = -dx / len; // perpendicular, para o caminho serpentear
                for (int i = 1; i < steps; i++)
                {
                    float k = i / (float)steps;
                    float wob = Mathf.Sin(k * 9f + len) * 2.2f * Mathf.Sin(k * Mathf.PI);
                    Vector3 p = new Vector3(spawn.x + dx * k + nx * wob, 0.04f, spawn.z + dz * k + nz * wob);
                    GameObject seg = Prims.MakeShared(PrimitiveType.Cube, "Caminho", t, p, new Vector3(rng.Range(2.2f, 3.2f), 0.02f, 5f), dirt);
                    seg.transform.rotation = Quaternion.Euler(0f, ang + Mathf.Sin(k * 9f + len) * 14f, 0f);
                }
            }
        }

        private static bool NearSpawn(float x, float z, Vector3 spawn, float r)
        {
            float dx = x - spawn.x, dz = z - spawn.z;
            return dx * dx + dz * dz < r * r;
        }

        private static void Grass(Transform t, Lcg rng, float minX, float maxX, float minZ, float maxZ, Vector3 spawn)
        {
            Color[] cols = { new Color(0.22f, 0.5f, 0.2f), new Color(0.3f, 0.58f, 0.24f), new Color(0.18f, 0.4f, 0.18f) };
            for (int i = 0; i < 130; i++)
            {
                float x = rng.Range(minX + 2f, maxX - 2f), z = rng.Range(minZ + 2f, maxZ - 2f);
                if (NearSpawn(x, z, spawn, 4.5f)) continue;
                float h = rng.Range(0.35f, 0.7f);
                Prims.MakeShared(PrimitiveType.Sphere, "Grama", t, new Vector3(x, h * 0.35f, z), new Vector3(rng.Range(0.4f, 0.8f), h, rng.Range(0.4f, 0.8f)), cols[i % 3]);
            }
        }

        private static void Flowers(Transform t, Lcg rng, float minX, float maxX, float minZ, float maxZ, Vector3 spawn)
        {
            Color[] cols = { new Color(1f, 0.9f, 0.3f), new Color(1f, 1f, 1f), new Color(0.95f, 0.5f, 0.7f), new Color(0.6f, 0.5f, 1f) };
            Color stem = new Color(0.2f, 0.45f, 0.18f);
            for (int i = 0; i < 48; i++)
            {
                float x = rng.Range(minX + 3f, maxX - 3f), z = rng.Range(minZ + 3f, maxZ - 3f);
                if (NearSpawn(x, z, spawn, 5f)) continue;
                Prims.MakeShared(PrimitiveType.Cylinder, "Haste", t, new Vector3(x, 0.15f, z), new Vector3(0.04f, 0.15f, 0.04f), stem);
                Prims.MakeShared(PrimitiveType.Sphere, "Flor", t, new Vector3(x, 0.34f, z), new Vector3(0.2f, 0.12f, 0.2f), cols[i % 4]);
            }
        }

        // ------------------------------------------------------------------ árvores e pedras
        public static void Tree(Transform parent, Vector3 pos, float size, int variant)
        {
            Color bark = new Color(0.4f, 0.28f, 0.16f);
            if (variant % 3 == 2)
            {
                // pinheiro: copas empilhadas, cada uma menor
                Prims.MakeShared(PrimitiveType.Cylinder, "Tronco", parent, pos + new Vector3(0f, size * 0.9f, 0f), new Vector3(size * 0.35f, size * 0.9f, size * 0.35f), bark);
                Color pine = new Color(0.12f, 0.36f, 0.2f);
                for (int i = 0; i < 4; i++)
                {
                    float w = size * (2.2f - 0.5f * i);
                    Prims.MakeShared(PrimitiveType.Cylinder, "Copa", parent, pos + new Vector3(0f, size * (1.7f + 0.9f * i), 0f), new Vector3(w, size * 0.32f, w), i % 2 == 0 ? pine : Dk(pine, 1.25f));
                }
                return;
            }
            Prims.MakeShared(PrimitiveType.Cylinder, "Tronco", parent, pos + new Vector3(0f, size * 1.4f, 0f), new Vector3(size * 0.5f, size * 1.4f, size * 0.5f), bark);
            Color leaf = variant % 3 == 0 ? new Color(0.15f, 0.42f, 0.18f) : new Color(0.24f, 0.52f, 0.2f);
            Prims.MakeShared(PrimitiveType.Sphere, "Copa", parent, pos + new Vector3(0f, size * 3.4f, 0f), new Vector3(size * 3f, size * 2.5f, size * 3f), leaf);
            Prims.MakeShared(PrimitiveType.Sphere, "Copa", parent, pos + new Vector3(size * 0.9f, size * 2.8f, size * 0.3f), new Vector3(size * 2f, size * 1.7f, size * 2f), Dk(leaf, 0.85f));
            Prims.MakeShared(PrimitiveType.Sphere, "Copa", parent, pos + new Vector3(-size * 0.8f, size * 3.0f, -size * 0.4f), new Vector3(size * 2.1f, size * 1.8f, size * 2.1f), Dk(leaf, 1.15f));
        }

        public static void Rock(Transform parent, Vector3 pos, float r)
        {
            Color g = new Color(0.5f, 0.5f, 0.52f);
            Prims.MakeShared(PrimitiveType.Sphere, "Pedra", parent, pos + new Vector3(0f, r * 0.45f, 0f), new Vector3(r * 2f, r * 1.1f, r * 1.8f), g);
            Prims.MakeShared(PrimitiveType.Sphere, "Pedra", parent, pos + new Vector3(r * 0.9f, r * 0.25f, r * 0.3f), new Vector3(r * 1.1f, r * 0.7f, r * 1.0f), Dk(g, 0.88f));
            Prims.MakeShared(PrimitiveType.Sphere, "Musgo", parent, pos + new Vector3(-r * 0.2f, r * 0.9f, 0f), new Vector3(r * 1.1f, r * 0.25f, r * 1.0f), new Color(0.32f, 0.5f, 0.26f));
        }

        private static void Forest(Transform t, Lcg rng, float minX, float maxX, float minZ, float maxZ)
        {
            // Árvores fora dos limites (o jogador não chega lá): moldura de floresta ao redor do vale.
            for (int i = 0; i < 70; i++)
            {
                int side = i % 4;
                float off = rng.Range(2f, 22f);
                float x, z;
                if (side == 0) { x = rng.Range(minX - 10f, maxX + 10f); z = maxZ + off; }
                else if (side == 1) { x = rng.Range(minX - 10f, maxX + 10f); z = minZ - off; }
                else if (side == 2) { x = maxX + off; z = rng.Range(minZ, maxZ); }
                else { x = minX - off; z = rng.Range(minZ, maxZ); }
                Tree(t, new Vector3(x, 0f, z), rng.Range(1.1f, 2.0f), i);
            }
        }

        // ------------------------------------------------------------------ pontos de interesse
        private static void Camp(Transform t, Vector3 spawn)
        {
            Color stone = new Color(0.58f, 0.58f, 0.6f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 0.7854f;
                Prims.MakeShared(PrimitiveType.Cube, "PedraDoCirculo", t, spawn + new Vector3(Mathf.Cos(a) * 3.2f, 0.35f, Mathf.Sin(a) * 3.2f), new Vector3(0.7f, 0.7f, 0.7f), stone);
            }
            Prims.MakeShared(PrimitiveType.Cylinder, "Fogueira", t, spawn + new Vector3(0f, 0.12f, 0f), new Vector3(0.9f, 0.12f, 0.9f), new Color(0.25f, 0.2f, 0.18f));
            Prims.MakeShared(PrimitiveType.Sphere, "Chama", t, spawn + new Vector3(0f, 0.55f, 0f), new Vector3(0.5f, 0.8f, 0.5f), new Color(1f, 0.55f, 0.12f));
            Prims.MakeShared(PrimitiveType.Sphere, "ChamaInterna", t, spawn + new Vector3(0f, 0.5f, 0f), new Vector3(0.28f, 0.5f, 0.28f), new Color(1f, 0.9f, 0.35f));
            // marco de pedra com tocha: ajuda a achar o ponto de nascimento de longe
            Prims.MakeShared(PrimitiveType.Cylinder, "Marco", t, spawn + new Vector3(4.5f, 1.2f, 0f), new Vector3(0.35f, 1.2f, 0.35f), stone);
            Prims.MakeShared(PrimitiveType.Sphere, "Tocha", t, spawn + new Vector3(4.5f, 2.6f, 0f), new Vector3(0.45f, 0.45f, 0.45f), new Color(1f, 0.75f, 0.25f));
        }

        private static void Arena(Transform t, Vector3 c)
        {
            Color stone = new Color(0.32f, 0.3f, 0.34f);
            Color glow = new Color(0.85f, 0.2f, 0.15f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 0.7854f + 0.2f;
                GameObject s = Prims.MakeShared(PrimitiveType.Cube, "MonolitoDaArena", t, c + new Vector3(Mathf.Cos(a) * 10f, 2.0f + 0.3f * (i % 3), Mathf.Sin(a) * 10f), new Vector3(1.2f, 4f + 0.6f * (i % 3), 1.0f), stone);
                s.transform.rotation = Quaternion.Euler((i % 3 - 1) * 5f, -a * Mathf.Rad2Deg, (i % 2 == 0 ? 4f : -4f));
                Prims.MakeShared(PrimitiveType.Cube, "RunaDaArena", t, c + new Vector3(Mathf.Cos(a) * 9.35f, 3.0f, Mathf.Sin(a) * 9.35f), new Vector3(0.25f, 0.8f, 0.1f), glow).transform.rotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
            }
            for (int i = 0; i < 28; i++)
            {
                float a = i * 0.2244f;
                GameObject d = Prims.MakeShared(PrimitiveType.Cube, "Marca", t, c + new Vector3(Mathf.Cos(a) * 8.6f, 0.06f, Mathf.Sin(a) * 8.6f), new Vector3(1.4f, 0.05f, 0.25f), glow);
                d.transform.rotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
            }
        }
    }
}
