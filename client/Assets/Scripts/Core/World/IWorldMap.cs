using Mmorpg.Core.Common;

namespace Mmorpg.Core.World
{
    /// <summary>Colisão e limites do mundo. O servidor usa a mesma implementação que o cliente.</summary>
    public interface IWorldMap
    {
        /// <summary>Move de 'from' até 'to' (em passos curtos), deslizando em obstáculos e respeitando os limites.</summary>
        Vec2 Move(Vec2 from, Vec2 to, double radius);

        bool IsBlocked(Vec2 point, double radius);
    }

    /// <summary>Mapa aberto sem obstáculos (testes).</summary>
    public sealed class OpenMap : IWorldMap
    {
        public Vec2 Move(Vec2 from, Vec2 to, double radius) { return to; }
        public bool IsBlocked(Vec2 point, double radius) { return false; }
    }
}
