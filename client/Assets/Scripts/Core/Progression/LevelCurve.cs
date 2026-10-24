using System;
using Mmorpg.Core.Common;
using Mmorpg.Core.Data;

namespace Mmorpg.Core.Progression
{
    /// <summary>Curva de experiência e regras de XP, 100% guiadas por progression.json.</summary>
    public static class LevelCurve
    {
        /// <summary>XP necessário para ir de 'level' para 'level+1'. 0 no nível máximo.</summary>
        public static long XpToNext(GameData d, int level)
        {
            if (level >= d.Progression.MaxLevel) return 0;
            XpCurveConfig c = d.Progression.XpCurve;
            long v = (long)Math.Round(c.A * Math.Pow(Math.Max(1, level), c.B) + c.C);
            return Math.Max(1, v);
        }

        /// <summary>Fator de XP conforme a diferença de nível (jogador muito acima rende pouco; muito abaixo rende bônus limitado).</summary>
        public static double LevelDifferenceFactor(GameData d, int playerLevel, int monsterLevel)
        {
            LevelDifferenceConfig c = d.Progression.LevelDifference;
            int diff = monsterLevel - playerLevel;
            if (Math.Abs(diff) <= c.FreeRange) return 1.0;
            if (diff < 0)
            {
                double f = 1.0 - c.PenaltyPerLevel * (-diff - c.FreeRange);
                return Math.Max(c.MinFactor, f);
            }
            double bonus = 1.0 + c.BonusPerLevel * (diff - c.FreeRange);
            return Math.Min(c.MaxBonus, bonus);
        }

        public static long MonsterXp(GameData d, MonsterDef m, int playerLevel)
        {
            MonsterCategoryDef cat = d.MonsterCategories[m.CategoryId];
            double baseXp = d.Progression.MonsterXp.Base + d.Progression.MonsterXp.PerLevel * m.Level;
            double xp = baseXp * cat.XpMultiplier * LevelDifferenceFactor(d, playerLevel, m.Level);
            return Math.Max(1L, (long)Math.Round(xp));
        }

        /// <summary>True se o jogador está acima do nível de corte: não recebe itens nem ouro (anti-farm de conteúdo inicial).</summary>
        public static bool IsAboveLootCutoff(GameData d, int playerLevel, int monsterLevel)
        {
            return playerLevel - monsterLevel >= d.Progression.LevelDifference.LootCutoffLevels;
        }
    }
}
