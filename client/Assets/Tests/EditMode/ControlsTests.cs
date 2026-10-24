using System;
using NUnit.Framework;
using Mmorpg.Core.Common;
using Mmorpg.Core.Controls;

namespace Mmorpg.Core.Tests
{
    /// <summary>Matemática de entrada (PC e Android): zona morta, câmera, joystick virtual e posição dos botões de toque.</summary>
    [TestFixture]
    public class ControlsTests
    {
        private const double Eps = 1e-9;

        // ------------------------------------------------------------ MoveMath
        [Test]
        public void Deadzone_ZeroesSmallInput_AndRescalesTheRest()
        {
            Vec2 small = MoveMath.ApplyDeadzone(new Vec2(0.1, 0), 0.2);
            Assert.AreEqual(0.0, small.Length, Eps);

            Vec2 mid = MoveMath.ApplyDeadzone(new Vec2(0.6, 0), 0.2);
            Assert.AreEqual(0.5, mid.X, Eps); // (0,6 - 0,2) / 0,8
            Assert.AreEqual(0.0, mid.Z, Eps);

            Vec2 full = MoveMath.ApplyDeadzone(new Vec2(0, 1), 0.2);
            Assert.AreEqual(1.0, full.Length, Eps);

            Assert.AreEqual(0.0, MoveMath.ApplyDeadzone(Vec2.Zero, 0.2).Length, Eps);
        }

        [Test]
        public void CameraRelative_FollowsTheCameraYaw()
        {
            Vec2 forward = new Vec2(0, 1);
            Vec2 right = new Vec2(1, 0);

            Vec2 a = MoveMath.CameraRelative(forward, 0);
            Assert.AreEqual(0.0, a.X, Eps);
            Assert.AreEqual(1.0, a.Z, Eps);

            Vec2 b = MoveMath.CameraRelative(forward, 90); // câmera olhando para leste
            Assert.AreEqual(1.0, b.X, 1e-9);
            Assert.AreEqual(0.0, b.Z, 1e-9);

            Vec2 c = MoveMath.CameraRelative(right, 90);   // direita da câmera = sul
            Assert.AreEqual(0.0, c.X, 1e-9);
            Assert.AreEqual(-1.0, c.Z, 1e-9);

            Vec2 d = MoveMath.CameraRelative(new Vec2(1, 1), 37);
            Assert.LessOrEqual(d.Length, 1.0 + Eps, "a diagonal nunca pode ser mais rápida");
        }

        [Test]
        public void WrapDegrees_KeepsAnglesInZeroTo360()
        {
            Assert.AreEqual(270.0, MoveMath.WrapDegrees(-90), Eps);
            Assert.AreEqual(0.0, MoveMath.WrapDegrees(720), Eps);
            Assert.AreEqual(10.0, MoveMath.WrapDegrees(370), Eps);
        }

        // ------------------------------------------------------------ VirtualJoystick
        [Test]
        public void Joystick_IsZeroWhenInactive_AndFollowsTheFinger()
        {
            var j = new VirtualJoystick(100, 0.2);
            Assert.IsFalse(j.Active);
            Assert.AreEqual(0.0, j.Value.Length, Eps);
            j.Move(500, 500);
            Assert.AreEqual(0.0, j.Value.Length, Eps, "mover sem iniciar é ignorado");

            j.Begin(500, 500);
            Assert.IsTrue(j.Active);
            j.Move(550, 500); // metade do raio para a direita
            Assert.AreEqual(0.375, j.Value.X, 1e-9); // (0,5 - 0,2) / 0,8
            Assert.AreEqual(0.0, j.Value.Z, 1e-9);

            j.Move(500, 600); // para cima na tela = frente (+Z)
            Assert.AreEqual(1.0, j.Value.Z, 1e-9);

            j.Move(500, 400); // para baixo = trás
            Assert.AreEqual(-1.0, j.Value.Z, 1e-9);

            j.End();
            Assert.IsFalse(j.Active);
            Assert.AreEqual(0.0, j.Value.Length, Eps);
        }

        [Test]
        public void Joystick_ClampsFarDragsToUnitLength_AndKnobStaysInsideTheRing()
        {
            var j = new VirtualJoystick(100, 0.1);
            j.Begin(0, 0);
            j.Move(5000, 5000);
            Assert.AreEqual(1.0, j.Value.Length, 1e-9);
            Assert.AreEqual(100.0, j.KnobOffset.Length, 1e-9);
        }

        [Test]
        public void Joystick_RejectsInvalidRadius_AndCanBeResized()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new VirtualJoystick(0, 0.1));
            var j = new VirtualJoystick(100, 0.1);
            j.SetRadius(200);
            Assert.AreEqual(200.0, j.RadiusPixels, Eps);
            j.SetRadius(-5);
            Assert.AreEqual(200.0, j.RadiusPixels, Eps, "valor inválido é ignorado");
        }

        // ------------------------------------------------------------ TouchLayout
        private static readonly double[][] Screens =
        {
            new[] { 1920.0, 1080.0 }, new[] { 2400.0, 1080.0 }, new[] { 1280.0, 720.0 },
            new[] { 800.0, 480.0 }, new[] { 1024.0, 768.0 }, new[] { 1080.0, 1920.0 }
        };

        [Test]
        public void TouchLayout_FitsEveryCommonScreen_WithoutOverlapsOrClippedButtons()
        {
            foreach (double[] s in Screens)
            {
                foreach (double scale in new[] { 0.8, 1.0, 1.5 })
                {
                    string label = s[0] + "x" + s[1] + " escala " + scale;
                    TouchLayout l = TouchLayout.Compute(s[0], s[1], scale);

                    var rects = new System.Collections.Generic.List<RectF>(l.SkillButtons);
                    rects.Add(l.TargetButton);
                    rects.Add(l.BagButton);
                    rects.Add(l.MenuButton);
                    Assert.AreEqual(TouchLayout.SkillButtonCount + 3, rects.Count, label);

                    for (int i = 0; i < rects.Count; i++)
                    {
                        RectF r = rects[i];
                        Assert.GreaterOrEqual(r.X, 0.0, label + " botão " + i);
                        Assert.GreaterOrEqual(r.Y, 0.0, label + " botão " + i);
                        Assert.LessOrEqual(r.Right, s[0] + Eps, label + " botão " + i);
                        Assert.LessOrEqual(r.Top, s[1] + Eps, label + " botão " + i);
                        Assert.GreaterOrEqual(l.ButtonSize, 48.0 - Eps, label + " alvo de toque pequeno demais");
                        Assert.IsFalse(r.Overlaps(l.JoystickZone), label + " botão " + i + " invade a zona do joystick");
                        for (int k = i + 1; k < rects.Count; k++)
                            Assert.IsFalse(r.Overlaps(rects[k]), label + " botões " + i + " e " + k + " se sobrepõem");
                    }
                }
            }
        }

        [Test]
        public void TouchLayout_FindsTheButtonUnderAFinger()
        {
            TouchLayout l = TouchLayout.Compute(1920, 1080, 1.0);
            for (int i = 0; i < TouchLayout.SkillButtonCount; i++)
            {
                RectF b = l.SkillButtons[i];
                Assert.AreEqual(i, l.SkillButtonAt(b.X + b.Width / 2, b.Y + b.Height / 2), "botão " + i);
                Assert.IsTrue(l.IsOverButton(b.X + 1, b.Y + 1));
            }
            Assert.AreEqual(-1, l.SkillButtonAt(10, 10));
            Assert.IsFalse(l.IsOverButton(10, 10));
            Assert.IsTrue(l.IsOverButton(l.TargetButton.X + 2, l.TargetButton.Y + 2));
            Assert.IsTrue(l.JoystickZone.Contains(10, 10));
        }

        [Test]
        public void TouchLayout_RejectsInvalidScreenSize()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TouchLayout.Compute(0, 1080, 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => TouchLayout.Compute(1920, -1, 1.0));
        }
    }
}
