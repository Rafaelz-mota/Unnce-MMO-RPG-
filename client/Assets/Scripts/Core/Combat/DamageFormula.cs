using System;
using Mmorpg.Core.Common;
using Mmorpg.Core.Data;
using Mmorpg.Core.Stats;

namespace Mmorpg.Core.Combat
{
    /// <summary>
    /// Fórmulas de combate. Todos os coeficientes vêm de combat.json.
    ///   bruto      = base + Σ(escala[atributo] * atributo do atacante)
    ///   mitigação  = defesa / (defesa + K * nívelAtacante + C), limitada a maxMitigation
    ///   final      = bruto * (1 - mitigação), no mínimo 1
    /// </summary>
    public static class DamageFormula
    {
        public static double Scale(SkillEffectDef effect, StatSheet attacker)
        {
            double v = effect.Base;
            foreach (var kv in effect.Scaling) v += kv.Value * attacker.Get(kv.Key);
            return v;
        }

        public static double Mitigation(GameData d, StatSheet defender, int attackerLevel, DamageType type)
        {
            double def = defender.Get(type == DamageType.Physical ? "physDefense" : "magicResist");
            if (def <= 0) return 0;
            MitigationConfig m = d.Combat.Mitigation;
            double mit = def / (def + m.K * Math.Max(1, attackerLevel) + m.C);
            return MathUtil.Clamp(mit, 0, m.MaxMitigation);
        }

        public static double CritChance(GameData d, StatSheet attacker)
        {
            return MathUtil.Clamp(attacker.Get("critChance"), 0, d.Combat.CritChanceCap);
        }

        /// <summary>Multiplicador de dano em área: acima de 'freeTargets' alvos, cada alvo extra reduz o dano (com piso).</summary>
        public static double AoeMultiplier(GameData d, int targetsHit)
        {
            AoeFalloffConfig a = d.Combat.AoeFalloff;
            if (targetsHit <= a.FreeTargets) return 1.0;
            return Math.Max(a.MinMultiplier, 1.0 - a.PerExtraTarget * (targetsHit - a.FreeTargets));
        }
    }
}
