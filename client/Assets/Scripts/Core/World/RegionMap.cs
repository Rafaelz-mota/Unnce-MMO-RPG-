using System;
using Mmorpg.Core.Common;
using Mmorpg.Core.Data;

namespace Mmorpg.Core.World
{
    /// <summary>Região com limites retangulares e obstáculos circulares (regions.json).</summary>
    public sealed class RegionMap : IWorldMap
    {
        private readonly RegionDef _region;

        public RegionMap(RegionDef region)
        {
            _region = region;
        }

        public RegionDef Region { get { return _region; } }

        public Vec2 PlayerSpawn { get { return new Vec2(_region.PlayerSpawn.X, _region.PlayerSpawn.Z); } }

        public Vec2 ClampToBounds(Vec2 p, double radius)
        {
            BoundsDef b = _region.Bounds;
            return new Vec2(
                MathUtil.Clamp(p.X, b.MinX + radius, b.MaxX - radius),
                MathUtil.Clamp(p.Z, b.MinZ + radius, b.MaxZ - radius));
        }

        public bool IsBlocked(Vec2 point, double radius)
        {
            BoundsDef b = _region.Bounds;
            if (point.X < b.MinX + radius || point.X > b.MaxX - radius || point.Z < b.MinZ + radius || point.Z > b.MaxZ - radius)
                return true;
            foreach (ObstacleDef o in _region.Obstacles)
            {
                double dx = point.X - o.X, dz = point.Z - o.Z;
                double r = o.Radius + radius;
                if (dx * dx + dz * dz < r * r) return true;
            }
            return false;
        }

        public Vec2 Move(Vec2 from, Vec2 to, double radius)
        {
            Vec2 delta = to - from;
            double length = delta.Length;
            if (length < 1e-9) return from;
            // Passos curtos evitam atravessar obstáculos finos em velocidades altas (dash).
            int steps = Math.Max(1, (int)Math.Ceiling(length / Math.Max(0.1, radius * 0.5)));
            Vec2 step = delta * (1.0 / steps);
            Vec2 pos = from;
            for (int i = 0; i < steps; i++) pos = PushOut(ClampToBounds(pos + step, radius), radius);
            return pos;
        }

        private Vec2 PushOut(Vec2 p, double radius)
        {
            foreach (ObstacleDef o in _region.Obstacles)
            {
                double dx = p.X - o.X, dz = p.Z - o.Z;
                double r = o.Radius + radius;
                double sq = dx * dx + dz * dz;
                if (sq >= r * r) continue;
                double d = Math.Sqrt(sq);
                if (d < 1e-6) { dx = 1; dz = 0; d = 1; }
                p = new Vec2(o.X + dx / d * r, o.Z + dz / d * r);
            }
            return ClampToBounds(p, radius);
        }
    }
}
