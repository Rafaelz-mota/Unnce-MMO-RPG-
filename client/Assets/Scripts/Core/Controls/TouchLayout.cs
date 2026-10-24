using System;

namespace Mmorpg.Core.Controls
{
    /// <summary>Retângulo em pixels de tela com Y para CIMA (origem no canto inferior esquerdo).</summary>
    public struct RectF
    {
        public double X;
        public double Y;
        public double Width;
        public double Height;

        public RectF(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public double Right { get { return X + Width; } }

        public double Top { get { return Y + Height; } }

        public bool Contains(double px, double py)
        {
            return px >= X && px <= Right && py >= Y && py <= Top;
        }

        public bool Overlaps(RectF o)
        {
            return X < o.Right && Right > o.X && Y < o.Top && Top > o.Y;
        }
    }

    /// <summary>
    /// Posição dos controles de toque (Android), calculada a partir do tamanho da tela. Sem dependência de Unity.
    /// Botões de habilidade em grade 3x3 no canto inferior direito (habilidade 1 no canto, ao alcance do polegar),
    /// joystick na metade esquerda, e botões de alvo, mochila e menu. O que não é controle é área de câmera.
    /// </summary>
    public sealed class TouchLayout
    {
        public const int SkillButtonCount = 9;

        public double ScreenWidth { get; private set; }
        public double ScreenHeight { get; private set; }
        public double ButtonSize { get; private set; }
        public double Margin { get; private set; }
        public double Gap { get; private set; }

        /// <summary>Região onde um toque inicia o joystick.</summary>
        public RectF JoystickZone { get; private set; }

        public RectF[] SkillButtons { get; private set; }
        public RectF TargetButton { get; private set; }
        public RectF BagButton { get; private set; }
        public RectF MenuButton { get; private set; }

        public static TouchLayout Compute(double screenWidth, double screenHeight, double uiScale)
        {
            if (screenWidth <= 0 || screenHeight <= 0) throw new ArgumentOutOfRangeException("screenWidth");
            double scale = uiScale <= 0 ? 1.0 : uiScale;
            double shortSide = Math.Min(screenWidth, screenHeight);

            double unit = shortSide * 0.11 * scale;
            unit = Math.Max(unit, 48.0);               // mínimo tocável
            unit = Math.Min(unit, shortSide * 0.16);   // máximo para caber na tela

            var l = new TouchLayout
            {
                ScreenWidth = screenWidth,
                ScreenHeight = screenHeight,
                ButtonSize = unit,
                Margin = unit * 0.35,
                Gap = unit * 0.15
            };

            l.SkillButtons = new RectF[SkillButtonCount];
            for (int i = 0; i < SkillButtonCount; i++)
            {
                int col = i % 3;
                int row = i / 3;
                l.SkillButtons[i] = new RectF(
                    screenWidth - l.Margin - unit - col * (unit + l.Gap),
                    l.Margin + row * (unit + l.Gap),
                    unit,
                    unit);
            }

            l.TargetButton = new RectF(screenWidth - l.Margin - unit, l.Margin + 3 * (unit + l.Gap), unit, unit);
            l.BagButton = new RectF(screenWidth - l.Margin - unit, screenHeight - l.Margin - unit, unit, unit);
            l.MenuButton = new RectF(screenWidth - l.Margin - 2 * unit - l.Gap, screenHeight - l.Margin - unit, unit, unit);
            l.JoystickZone = new RectF(0, 0, screenWidth * 0.35, screenHeight * 0.55);
            return l;
        }

        /// <summary>Índice (0..8) do botão de habilidade sob o ponto, ou -1.</summary>
        public int SkillButtonAt(double x, double y)
        {
            for (int i = 0; i < SkillButtons.Length; i++)
                if (SkillButtons[i].Contains(x, y)) return i;
            return -1;
        }

        /// <summary>True se o ponto está sobre qualquer botão (o toque não deve virar câmera nem seleção de alvo).</summary>
        public bool IsOverButton(double x, double y)
        {
            return SkillButtonAt(x, y) >= 0 || TargetButton.Contains(x, y) || BagButton.Contains(x, y) || MenuButton.Contains(x, y);
        }
    }
}
