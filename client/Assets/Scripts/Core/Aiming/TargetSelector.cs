using System.Collections.Generic;
using Mmorpg.Core.Combat;
using Mmorpg.Core.Common;

namespace Mmorpg.Core.Aiming
{
    /// <summary>
    /// Seleção de alvo (Tab, alvo mais próximo). Só considera inimigos vivos, hostis e que podem ser atacados
    /// (monstros voltando para casa ficam invulneráveis e são ignorados). Sem dependência de Unity.
    /// </summary>
    public static class TargetSelector
    {
        public static bool IsValid(CombatWorld world, Combatant from, int targetId)
        {
            if (world == null || from == null || targetId == 0) return false;
            Combatant t = world.Get(targetId);
            return t != null && !t.Dead && !t.Invulnerable && world.IsHostile(from, t);
        }

        /// <summary>Inimigo válido mais próximo dentro de 'maxDistance', ou null.</summary>
        public static Combatant Nearest(CombatWorld world, Combatant from, double maxDistance)
        {
            List<Combatant> list = Candidates(world, from, maxDistance);
            return list.Count > 0 ? list[0] : null;
        }

        /// <summary>
        /// Próximo alvo ao pressionar Tab: percorre os inimigos válidos do mais perto para o mais longe e volta ao começo.
        /// Devolve 0 se não há nenhum.
        /// </summary>
        public static int CycleNext(CombatWorld world, Combatant from, int currentId, double maxDistance)
        {
            List<Combatant> list = Candidates(world, from, maxDistance);
            if (list.Count == 0) return 0;
            int index = -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Id == currentId) { index = i; break; }
            }
            return list[(index + 1) % list.Count].Id;
        }

        private static List<Combatant> Candidates(CombatWorld world, Combatant from, double maxDistance)
        {
            var list = new List<Combatant>();
            if (world == null || from == null) return list;
            IList<Combatant> all = world.All;
            for (int i = 0; i < all.Count; i++)
            {
                Combatant c = all[i];
                if (c.Dead || c.Invulnerable || !world.IsHostile(from, c)) continue;
                if (Vec2.Distance(from.Position, c.Position) > maxDistance) continue;
                list.Add(c);
            }
            list.Sort((a, b) =>
            {
                int byDistance = Vec2.Distance(from.Position, a.Position).CompareTo(Vec2.Distance(from.Position, b.Position));
                return byDistance != 0 ? byDistance : a.Id.CompareTo(b.Id);
            });
            return list;
        }
    }
}
