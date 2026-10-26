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
