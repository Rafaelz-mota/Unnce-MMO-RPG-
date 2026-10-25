using Mmorpg.Core.Data;
using UnityEngine;

namespace Mmorpg.Game
{
    /// <summary>
    /// Lê os JSON de balanceamento de Assets/Resources/GameData (embutidos no build, inclusive Android).
    /// Arquivo ausente vira exceção de E/S; o <see cref="GameDataLoader"/> a converte em erro de validação.
    /// </summary>
    public sealed class ResourcesDataSource : IDataSource
    {
        public string ReadAllText(string name)
        {
            TextAsset asset = Resources.Load<TextAsset>("GameData/" + name);
            if (asset == null)
                throw new System.IO.FileNotFoundException("Arquivo ausente em Resources/GameData: " + name + ".json");
            return asset.text;
        }
    }
}
