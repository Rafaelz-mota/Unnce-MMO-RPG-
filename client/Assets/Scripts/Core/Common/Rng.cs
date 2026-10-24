using System;

namespace Mmorpg.Core.Common
{
    /// <summary>Fonte de aleatoriedade injetável (permite testes determinísticos e RNG do servidor).</summary>
    public interface IRng
    {
        /// <summary>Valor em [0, 1).</summary>
        double NextDouble();

        /// <summary>Inteiro em [minInclusive, maxExclusive).</summary>
        int NextInt(int minInclusive, int maxExclusive);
    }

    public sealed class SystemRng : IRng
    {
        private readonly Random _random;

        public SystemRng(int seed) { _random = new Random(seed); }

        public SystemRng() { _random = new Random(); }

        public double NextDouble() { return _random.NextDouble(); }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return _random.Next(minInclusive, maxExclusive);
        }
    }
}
