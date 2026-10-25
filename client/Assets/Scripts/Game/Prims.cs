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
