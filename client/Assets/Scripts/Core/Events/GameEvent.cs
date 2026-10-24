using Mmorpg.Core.Common;

namespace Mmorpg.Core.Events
{
    public enum GameEventType
    {
        Spawned,
        Despawned,
        Damage,
        Heal,
        Absorbed,
        StatusApplied,
        StatusRemoved,
        StatusResisted,
        SkillUsed,
        CastStarted,
        CastInterrupted,
        Dash,
        Died,
        Respawned,
        XpGained,
        LevelUp,
        GoldGained,
        ItemLooted,
        InventoryFull,
        BossPhase,
        SkillLearned,
        Announcement
    }

    /// <summary>
    /// Evento produzido pela simulação. O cliente (Unity) só LÊ eventos para animar, mostrar números de dano
    /// e sons; nunca decide resultado de combate. Em uma versão online, o servidor envia estes eventos.
    /// </summary>
    public sealed class GameEvent
    {
        public double Time;
        public GameEventType Type;
        /// <summary>Quem causou (0 = ninguém/ambiente).</summary>
        public int SourceId;
        public int TargetId;
        public string SkillId;
        public string StatusId;
        public double Amount;
        public bool Crit;
        public string Text;
        public Vec2 Position;
        /// <summary>Segundo ponto (destino do dash).</summary>
        public Vec2 Position2;
        public double Radius;
        public int Count;
        public string ItemId;

        public override string ToString()
        {
            return "[" + Time.ToString("0.00") + "] " + Type + " src=" + SourceId + " dst=" + TargetId
                + (SkillId != null ? " skill=" + SkillId : "")
                + (StatusId != null ? " status=" + StatusId : "")
                + (ItemId != null ? " item=" + ItemId + "x" + Count : "")
                + (Amount != 0 ? " amount=" + Amount.ToString("0.#") : "")
                + (Crit ? " CRIT" : "")
                + (Text != null ? " \"" + Text + "\"" : "");
        }
    }
}
