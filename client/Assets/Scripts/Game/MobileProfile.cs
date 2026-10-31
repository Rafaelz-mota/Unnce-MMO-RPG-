using UnityEngine;

namespace Mmorpg.Game
{
    /// <summary>
    /// Ajustes de desempenho só para celular (Android/iOS). No PC não muda nada. Valores conservadores:
    /// sombras mais curtas, uma luz por pixel e agrupamento (static batching) dos enfeites do mapa.
    /// <b>Nunca foi medido em um celular real</b>: são pontos de partida, não resultados.
    /// </summary>
    public static class MobileProfile
    {
        public const float ShadowDistance = 45f;

        public static void Apply()
        {
            if (!Application.isMobilePlatform) return;
            QualitySettings.shadowDistance = ShadowDistance;
            QualitySettings.pixelLightCount = 1;
            QualitySettings.vSyncCount = 0;
            QualitySettings.lodBias = 0.8f;
        }

        /// <summary>Junta os enfeites estáticos em poucos meshes (menos chamadas de desenho). Só no celular.</summary>
        public static void BatchStatic(GameObject root)
        {
            if (!Application.isMobilePlatform || root == null) return;
            StaticBatchingUtility.Combine(root);
        }
    }
}
