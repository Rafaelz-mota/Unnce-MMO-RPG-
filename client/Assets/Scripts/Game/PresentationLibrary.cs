using System.Collections.Generic;
using Mmorpg.Core.Data;
using Mmorpg.Core.Presentation;
using UnityEngine;

namespace Mmorpg.Game
{
    /// <summary>Carrega (uma vez) o catálogo de aparência de Resources/GameData/presentation.json. Problemas viram avisos no Console, nunca exceção.</summary>
    public static class PresentationLibrary
    {
        private static PresentationCatalog _catalog;
        private static GameData _forData;

        public static PresentationCatalog Get(GameData data)
        {
            if (_catalog != null && ReferenceEquals(_forData, data)) return _catalog;
            var problems = new List<string>();
            TextAsset asset = Resources.Load<TextAsset>("GameData/presentation");
            if (asset == null) problems.Add("presentation.json ausente em Resources/GameData; usando aparência padrão");
            _catalog = PresentationCatalog.Parse(asset != null ? asset.text : "{\"schemaVersion\":1}", data, problems);
            _forData = data;
            for (int i = 0; i < problems.Count && i < 12; i++) Debug.LogWarning("Aparência: " + problems[i]);
            return _catalog;
        }
    }
}
