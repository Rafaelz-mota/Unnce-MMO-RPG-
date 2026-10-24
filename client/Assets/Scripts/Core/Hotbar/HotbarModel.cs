using System.Collections.Generic;
using Mmorpg.Core.Characters;
using Mmorpg.Core.Data;

namespace Mmorpg.Core.Hotbar
{
    public enum HotbarSlotState
    {
        /// <summary>Não há habilidade definida nesta posição ainda (o catálogo das 216 habilidades é preenchido por etapas).</summary>
        Empty,
        /// <summary>Aprendida e utilizável.</summary>
        Ready,
        /// <summary>Ainda não atingiu o nível exigido.</summary>
        LockedByLevel,
        /// <summary>Habilidade paga (slots 6-9) com nível suficiente: pode ser comprada com Gold.</summary>
        Purchasable
    }

    public sealed class HotbarSlot
    {
        /// <summary>1..9 (a mesma numeração de skills.json).</summary>
        public int Number { get; set; }

        public SkillDef Skill { get; set; }

        public HotbarSlotState State { get; set; }

        public int RequiredLevel { get; set; }

        public long GoldCost { get; set; }

        public bool CanAfford { get; set; }
    }

    /// <summary>Visão da barra de habilidades de um personagem (9 posições). Só lê regras do jogo; não muda nada.</summary>
    public static class HotbarModel
    {
        public static IList<HotbarSlot> Build(PlayerCharacter pc)
        {
            GameData data = pc.Data;
            int count = data.Rules.SkillsPerClass;
            IList<SkillDef> skills = pc.ClassSkills();
            var result = new List<HotbarSlot>(count);

            for (int number = 1; number <= count; number++)
            {
                SkillDef skill = null;
                for (int i = 0; i < skills.Count; i++)
                {
                    if (skills[i].Slot == number) { skill = skills[i]; break; }
                }

                var slot = new HotbarSlot { Number = number, Skill = skill };
                if (skill == null)
                {
                    slot.State = HotbarSlotState.Empty;
                }
                else if (pc.IsSkillLearned(skill.Id))
                {
                    slot.State = HotbarSlotState.Ready;
                }
                else
                {
                    slot.RequiredLevel = skill.RequiresLevel;
                    slot.GoldCost = skill.GoldCost;
                    slot.CanAfford = pc.Gold >= skill.GoldCost;
                    bool paid = skill.Slot > data.Rules.FreeSkillSlots;
                    slot.State = (pc.Level >= skill.RequiresLevel && paid) ? HotbarSlotState.Purchasable : HotbarSlotState.LockedByLevel;
                }
                result.Add(slot);
            }
            return result;
        }
    }
}
