using System.Collections.Generic;
using UnityEngine;

namespace Mmorpg.Game
{
    /// <summary>
    /// Formas primitivas coloridas (placeholder visual até existirem modelos 3D). Usa o material padrão do
    /// primitivo do pipeline atual (Built-in ou URP) e só troca a cor, sem depender de Shader.Find (que falha em builds).
    /// </summary>
    public static class Prims
    {
        public static GameObject Make(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Color color, bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            Tint(go, color);
            if (!keepCollider)
            {
                Collider col = go.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.Destroy(col);
            }
            return go;
        }

        private static readonly Dictionary<int, Material> Shared = new Dictionary<int, Material>();

        /// <summary>
        /// Como Make, mas reaproveita um material por cor (sem colisor). Usado nos enfeites do mapa: poucos materiais
        /// permitem à Unity agrupar as chamadas de desenho, importante no celular.
        /// </summary>
        public static GameObject MakeShared(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            Collider col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                int key = ((int)(Mathf.Clamp01(color.r) * 255f) << 16) | ((int)(Mathf.Clamp01(color.g) * 255f) << 8) | (int)(Mathf.Clamp01(color.b) * 255f);
                Material m;
                if (!Shared.TryGetValue(key, out m) || m == null)
                {
                    m = new Material(r.sharedMaterial);
                    m.color = color;
                    Shared[key] = m;
                }
                r.sharedMaterial = m;
            }
            return go;
        }

        /// <summary>Ponto de articulação vazio (para balançar pernas e braços girando o pivô).</summary>
        public static Transform Pivot(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        /// <summary>Cápsula invisível com colisor, usada para clicar/tocar na entidade (o corpo desenhado não tem colisor).</summary>
        public static void Hitbox(Transform parent, float height, float width)
        {
            GameObject go = Make(PrimitiveType.Capsule, "Hitbox", parent, new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height * 0.5f, width), Color.white, true);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
        }

        public static void Tint(GameObject go, Color color)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.material.color = color;
        }

        public static void TintAll(GameObject go, Color color)
        {
            Renderer[] all = go.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < all.Length; i++) all[i].material.color = color;
        }
    }
}
