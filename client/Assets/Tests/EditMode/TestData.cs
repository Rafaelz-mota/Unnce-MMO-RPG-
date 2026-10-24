using System;
using System.IO;
using Mmorpg.Core.Data;

namespace Mmorpg.Core.Tests
{
    /// <summary>
    /// Localiza e carrega os JSON reais do projeto para os testes. Funciona no Unity Test Runner
    /// (diretório atual = raiz do projeto Unity) e no harness (variável MMORPG_DATA_DIR).
    /// </summary>
    public static class TestData
    {
        private static GameData _cached;

        public static string Directory()
        {
            string env = Environment.GetEnvironmentVariable("MMORPG_DATA_DIR");
            if (!string.IsNullOrEmpty(env) && System.IO.Directory.Exists(env)) return env;
            string dir = System.IO.Directory.GetCurrentDirectory();
            for (int i = 0; i < 8 && dir != null; i++)
            {
                string a = Path.Combine(dir, "Assets", "Resources", "GameData");
                if (System.IO.Directory.Exists(a)) return a;
                string b = Path.Combine(dir, "client", "Assets", "Resources", "GameData");
                if (System.IO.Directory.Exists(b)) return b;
                dir = Path.GetDirectoryName(dir);
            }
            throw new DirectoryNotFoundException("Pasta GameData não encontrada. Defina MMORPG_DATA_DIR.");
        }

        public static IDataSource Source() { return new FileDataSource(Directory()); }

        /// <summary>Dados já validados; compartilhados entre testes (são somente leitura).</summary>
        public static GameData Data()
        {
            if (_cached == null) _cached = GameDataLoader.LoadOrThrow(Source());
            return _cached;
        }
    }
}
