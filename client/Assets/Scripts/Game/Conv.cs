using Mmorpg.Core.Common;
using UnityEngine;

namespace Mmorpg.Game
{
    /// <summary>Conversões entre o núcleo (Vec2: X leste, Z norte, em double) e a Unity (Vector3, y para cima).</summary>
    public static class Conv
    {
        public static Vector3 ToV3(Vec2 v) { return new Vector3((float)v.X, 0f, (float)v.Z); }

        public static Vector3 ToV3(Vec2 v, float y) { return new Vector3((float)v.X, y, (float)v.Z); }

        public static Vec2 ToVec2(Vector3 v) { return new Vec2(v.x, v.z); }

        /// <summary>Cor a partir de "#RRGGBB"; usa 'fallback' se o texto for inválido.</summary>
        public static Color Hex(string hex, Color fallback)
        {
            Color c;
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out c)) return c;
            return fallback;
        }

        /// <summary>Cor da barra de recurso (fúria, mana, energia...). Só apresentação.</summary>
        public static Color ResourceColor(string resourceId)
        {
            switch (resourceId)
            {
                case "rage": return new Color(0.85f, 0.25f, 0.15f);
                case "mana": return new Color(0.25f, 0.5f, 0.95f);
                case "energy": return new Color(0.95f, 0.85f, 0.2f);
            }
            return new Color(0.6f, 0.6f, 0.6f);
        }
    }
}
