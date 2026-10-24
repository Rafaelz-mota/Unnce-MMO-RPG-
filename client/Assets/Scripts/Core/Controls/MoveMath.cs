using System;
using Mmorpg.Core.Common;

namespace Mmorpg.Core.Controls
{
    /// <summary>
    /// Matemática de entrada de movimento, sem dependência de Unity (testável em EditMode e reutilizável no servidor).
    /// Convenção: vetor de entrada com X = direita e Z = frente; mundo com X = leste e Z = norte; yaw 0 = olhando para +Z,
    /// yaw 90 = olhando para +X (mesma convenção do Unity).
    /// </summary>
    public static class MoveMath
    {
        /// <summary>
        /// Zona morta radial com reescala: abaixo de 'deadzone' devolve zero; acima, o comprimento é remapeado de
        /// [deadzone, 1] para [0, 1], de modo que o movimento começa suave em vez de "pular" para a velocidade da borda.
        /// </summary>
        public static Vec2 ApplyDeadzone(Vec2 v, double deadzone)
        {
            double len = v.Length;
            if (len < 1e-9) return Vec2.Zero;
            double dz = MathUtil.Clamp(deadzone, 0.0, 0.99);
            if (len <= dz) return Vec2.Zero;
            double scaled = MathUtil.Clamp((len - dz) / (1.0 - dz), 0.0, 1.0);
            return v * (scaled / len);
        }

        /// <summary>Converte a entrada relativa à câmera (X direita, Z frente) em direção de mundo. O comprimento nunca passa de 1.</summary>
        public static Vec2 CameraRelative(Vec2 input, double cameraYawDegrees)
        {
            double r = cameraYawDegrees * Math.PI / 180.0;
            double sin = Math.Sin(r);
            double cos = Math.Cos(r);
            // frente da câmera no mundo = (sin, cos); direita da câmera = (cos, -sin)
            var world = new Vec2(input.X * cos + input.Z * sin, -input.X * sin + input.Z * cos);
            double len = world.Length;
            return len > 1.0 ? world * (1.0 / len) : world;
        }

        /// <summary>Normaliza um ângulo para [0, 360).</summary>
        public static double WrapDegrees(double degrees)
        {
            double d = degrees % 360.0;
            return d < 0 ? d + 360.0 : d;
        }
    }
}
