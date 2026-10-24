namespace Mmorpg.Core.Common
{
    public static class MathUtil
    {
        public static double Clamp(double v, double min, double max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        public static int Clamp(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        public static long Clamp(long v, long min, long max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
    }
}
