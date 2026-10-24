using System;
using Mmorpg.Core.Common;

namespace Mmorpg.Core.Controls
{
    /// <summary>
    /// Joystick virtual flutuante (o centro é onde o dedo toca). Sem dependência de Unity.
    /// Coordenadas de tela em pixels com Y para CIMA (mesma convenção de UnityEngine.Input.touches[i].position).
    /// Value: X = direita, Z = frente (para cima na tela), comprimento 0..1, já com zona morta.
    /// </summary>
    public sealed class VirtualJoystick
    {
        public double RadiusPixels { get; private set; }

        public double Deadzone { get; private set; }

        public bool Active { get; private set; }

        public Vec2 Origin { get; private set; }

        public Vec2 Current { get; private set; }

        public VirtualJoystick(double radiusPixels, double deadzone)
        {
            if (radiusPixels <= 0) throw new ArgumentOutOfRangeException("radiusPixels");
            RadiusPixels = radiusPixels;
            Deadzone = MathUtil.Clamp(deadzone, 0.0, 0.9);
        }

        /// <summary>Atualiza o raio (por exemplo, ao girar a tela ou mudar a escala da interface).</summary>
        public void SetRadius(double radiusPixels)
        {
            if (radiusPixels > 0) RadiusPixels = radiusPixels;
        }

        public void Begin(double x, double y)
        {
            Active = true;
            Origin = new Vec2(x, y);
            Current = Origin;
        }

        public void Move(double x, double y)
        {
            if (!Active) return;
            Current = new Vec2(x, y);
        }

        public void End()
        {
            Active = false;
            Origin = Vec2.Zero;
            Current = Vec2.Zero;
        }

        public Vec2 Value
        {
            get
            {
                if (!Active) return Vec2.Zero;
                Vec2 d = ClampedOffset() * (1.0 / RadiusPixels);
                return MoveMath.ApplyDeadzone(d, Deadzone);
            }
        }

        /// <summary>Deslocamento do botão em pixels, limitado ao raio (para desenhar).</summary>
        public Vec2 KnobOffset
        {
            get { return Active ? ClampedOffset() : Vec2.Zero; }
        }

        private Vec2 ClampedOffset()
        {
            Vec2 d = Current - Origin;
            double len = d.Length;
            if (len > RadiusPixels) d = d * (RadiusPixels / len);
            return d;
        }
    }
}
