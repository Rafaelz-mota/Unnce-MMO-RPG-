using Mmorpg.Core.Combat;
using Mmorpg.Core.Data;
using Mmorpg.Core.Events;

namespace Mmorpg.Core.Presentation
{
    /// <summary>
    /// Converte eventos da simulação e recusas de habilidade em mensagens em português para o registro da interface.
    /// Sem dependência de Unity (testável). A raridade aparece como texto ([Raro]) e não só como cor.
    /// Devolve null quando o evento não deve aparecer para o jogador local.
    /// </summary>
    public static class EventLog
    {
        public static string ItemLabel(GameData data, string itemId)
        {
            ItemDef item;
            if (itemId == null || !data.Items.TryGetValue(itemId, out item)) return itemId ?? "?";
            RarityDef rarity;
            string tag = data.Rarities.TryGetValue(item.Rarity, out rarity) ? rarity.Tag : null;
            return string.IsNullOrEmpty(tag) ? item.Name : item.Name + " " + tag;
        }

        public static string Format(GameData data, GameEvent e, int localPlayerId)
        {
            switch (e.Type)
            {
                case GameEventType.XpGained:
                    return e.TargetId == localPlayerId ? "+" + ((long)e.Amount) + " XP" : null;

                case GameEventType.GoldGained:
                    return e.TargetId == localPlayerId ? "+" + ((long)e.Amount) + " ouro" : null;

                case GameEventType.ItemLooted:
                    return e.TargetId == localPlayerId ? "Obteve " + e.Count + "x " + ItemLabel(data, e.ItemId) : null;

                case GameEventType.InventoryFull:
                    return e.TargetId == localPlayerId ? "Inventário cheio: " + ItemLabel(data, e.ItemId) + " foi perdido." : null;

                case GameEventType.LevelUp:
                    return e.TargetId == localPlayerId ? "Você subiu para o nível " + ((int)e.Amount) + "!" : null;

                case GameEventType.BossPhase:
                case GameEventType.Announcement:
                    return string.IsNullOrEmpty(e.Text) ? null : e.Text;

                case GameEventType.Died:
                    if (e.TargetId == localPlayerId) return "Você foi derrotado.";
                    if (e.SourceId == localPlayerId) return "Você derrotou " + e.Text + ".";
                    return null;

                case GameEventType.Respawned:
                    return e.TargetId == localPlayerId ? "Você renasceu." : null;

                case GameEventType.CastInterrupted:
                    return e.SourceId == localPlayerId ? "Conjuração interrompida (" + e.Text + ")." : null;

                case GameEventType.StatusResisted:
                    return e.SourceId == localPlayerId ? "O alvo resistiu (" + e.Text + ")." : null;
            }
            return null;
        }

        /// <summary>Texto para uma habilidade recusada; null quando não vale a pena avisar (recarga global é frequente).</summary>
        public static string Describe(UseSkillResult result)
        {
            switch (result)
            {
                case UseSkillResult.Ok:
                case UseSkillResult.GlobalCooldown:
                case UseSkillResult.AlreadyCasting:
                    return null;
                case UseSkillResult.UnknownSkill: return "Habilidade desconhecida.";
                case UseSkillResult.Dead: return "Você está caído.";
                case UseSkillResult.NotLearned: return "Habilidade ainda não aprendida.";
                case UseSkillResult.Stunned: return "Você está atordoado.";
                case UseSkillResult.Silenced: return "Você está silenciado.";
                case UseSkillResult.Rooted: return "Você está imobilizado.";
                case UseSkillResult.OnCooldown: return "Habilidade em recarga.";
                case UseSkillResult.NotEnoughResource: return "Recurso insuficiente.";
                case UseSkillResult.NoTarget: return "Nenhum alvo (use Tab ou toque em um inimigo).";
                case UseSkillResult.InvalidTarget: return "Alvo inválido.";
                case UseSkillResult.OutOfRange: return "Alvo fora de alcance.";
            }
            return null;
        }
    }
}
