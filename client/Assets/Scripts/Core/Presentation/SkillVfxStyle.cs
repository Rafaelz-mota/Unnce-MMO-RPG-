using System.Linq;
using Mmorpg.Core.Data;

namespace Mmorpg.Core.Presentation
{
    /// <summary>
    /// Assinatura visual de uma habilidade, calculada só a partir dos dados dela (id + mecânicas). É determinística,
    /// sem Unity, para ser testada: duas habilidades da mesma classe nunca têm o mesmo visual. A camada Unity
    /// (SkillVfx) só desenha o que este estilo manda.
    /// </summary>
    public sealed class SkillVfxStyle
    {
        /// <summary>0 esfera, 1 cubo, 2 cilindro.</summary>
        public int Shape;
        /// <summary>0 radial, 1 espiral, 2 colunas na borda, 3 convergente.</summary>
        public int Pattern;
        /// <summary>Deslocamento de matiz em passos de -3 a +3 (a cor do elemento muda um pouco por habilidade).</summary>
        public int HueStep;
        public float SizeMul;
        public int Trails;
        public int Bursts;
        public int Rings;
        public int Hits;
        public bool Leech, Execute, BonusVsStatus, Cleanse, Dispel, Shield, Dash, Heal, Dot, Stun, Root, Slow, Silence, Taunt, Haste, BuffAlly, Debuff;

        public const float HueStepSize = 0.02f;

        public static uint Hash(string s)
        {
            uint h = 2166136261u;
            foreach (char c in s ?? "") { h ^= c; h *= 16777619u; }
            return h;
        }

        public static SkillVfxStyle For(SkillDef skill, GameData data)
        {
            uint h = Hash(skill.Id);
            var st = new SkillVfxStyle
            {
                Shape = (int)(h % 3),
                Pattern = (int)((h >> 3) % 4),
                HueStep = (int)((h >> 6) % 7) - 3,
                SizeMul = 0.8f + 0.15f * ((h >> 9) % 4),
                Trails = 3 + (int)((h >> 11) % 4),
                Bursts = 8 + 4 * (int)((h >> 13) % 3),
                Rings = 1 + (int)((h >> 15) % 3),
                Hits = 1
            };
            foreach (SkillEffectDef e in skill.Effects)
            {
                switch (e.Kind)
                {
                    case EffectKind.Damage:
                        st.Hits = System.Math.Max(st.Hits, e.Hits);
                        if (e.Leech > 0) st.Leech = true;
                        if (e.ExecuteBelowHpPercent > 0) st.Execute = true;
                        if (e.BonusIfStatusId != null) st.BonusVsStatus = true;
                        break;
                    case EffectKind.Heal: st.Heal = true; break;
                    case EffectKind.Shield: st.Shield = true; break;
                    case EffectKind.Dash: st.Dash = true; break;
                    case EffectKind.Cleanse:
                        if (e.ResolvedSide() == EffectSide.Enemy) st.Dispel = true; else st.Cleanse = true;
                        break;
                    case EffectKind.ApplyStatus:
                        StatusDef sd;
                        if (e.StatusId == null || data == null || !data.Statuses.TryGetValue(e.StatusId, out sd)) break;
                        bool hostile = e.ResolvedSide() == EffectSide.Enemy;
                        switch (sd.Kind)
                        {
                            case StatusKind.Stun: st.Stun = true; break;
                            case StatusKind.Root: st.Root = true; break;
                            case StatusKind.Slow: st.Slow = true; break;
                            case StatusKind.Silence: st.Silence = true; break;
                            case StatusKind.Taunt: st.Taunt = true; break;
                            case StatusKind.Dot: st.Dot = true; break;
                            case StatusKind.Hot: st.Heal = true; break;
                            case StatusKind.Debuff: st.Debuff = true; break;
                            case StatusKind.Buff:
                                if (sd.Id == "haste") st.Haste = true;
                                if (!hostile) st.BuffAlly = true;
                                break;
                            case StatusKind.Shield: st.Shield = true; break;
                        }
                        break;
                }
            }
            return st;
        }

        /// <summary>Texto único do visual, usado nos testes e no registro de diagnóstico.</summary>
        public string Signature()
        {
            return string.Join("|", new object[] { Shape, Pattern, HueStep, SizeMul, Trails, Bursts, Rings, Hits,
                Flag(Leech), Flag(Execute), Flag(BonusVsStatus), Flag(Cleanse), Flag(Dispel), Flag(Shield), Flag(Dash), Flag(Heal),
                Flag(Dot), Flag(Stun), Flag(Root), Flag(Slow), Flag(Silence), Flag(Taunt), Flag(Haste), Flag(BuffAlly), Flag(Debuff) });
        }

        private static int Flag(bool b) { return b ? 1 : 0; }
    }
}
