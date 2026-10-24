using System;

namespace Mmorpg.Core.Common
{
    /// <summary>Vetor 2D no plano do chão (X, Z). Independente de Unity para rodar também no servidor.</summary>
    public struct Vec2 : IEquatable<Vec2>
    {
        public double X;
        public double Z;

        public Vec2(double x, double z)
        {
            X = x;
            Z = z;
        }

        public static Vec2 Zero { get { return new Vec2(0, 0); } }

        public double Length { get { return Math.Sqrt(X * X + Z * Z); } }

        public double SqrLength { get { return X * X + Z * Z; } }

        public Vec2 Normalized()
        {
            double l = Length;
            if (l < 1e-9) return Zero;
            return new Vec2(X / l, Z / l);
        }

        public static double Distance(Vec2 a, Vec2 b)
        {
            double dx = a.X - b.X;
            double dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        public static Vec2 operator +(Vec2 a, Vec2 b) { return new Vec2(a.X + b.X, a.Z + b.Z); }
        public static Vec2 operator -(Vec2 a, Vec2 b) { return new Vec2(a.X - b.X, a.Z - b.Z); }
        public static Vec2 operator *(Vec2 a, double s) { return new Vec2(a.X * s, a.Z * s); }

        public bool Equals(Vec2 other) { return X == other.X && Z == other.Z; }
        public override bool Equals(object obj) { return obj is Vec2 && Equals((Vec2)obj); }

        public override int GetHashCode()
        {
            unchecked { return (X.GetHashCode() * 397) ^ Z.GetHashCode(); }
        }

        public override string ToString() { return "(" + X.ToString("0.##") + ", " + Z.ToString("0.##") + ")"; }
    }
}
