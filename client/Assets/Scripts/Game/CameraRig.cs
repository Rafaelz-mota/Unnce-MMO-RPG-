using UnityEngine;

namespace Mmorpg.Game
{
    /// <summary>
    /// Câmera em terceira pessoa com rotação (yaw), inclinação (pitch) e zoom. Os limites são só de apresentação;
    /// o ângulo (Yaw) é o que converte a entrada de movimento em direção de mundo (MoveMath.CameraRelative).
    /// </summary>
    public sealed class CameraRig
    {
        public const float MinPitch = 10f;
        public const float MaxPitch = 80f;
        public const float MinDistance = 4f;
        public const float MaxDistance = 30f;

        private readonly Camera _camera;

        public float Yaw;
        public float Pitch = 38f;
        public float Distance = 13f;
        /// <summary>Graus por pixel arrastado (ajustável no menu).</summary>
        public float Sensitivity = 0.22f;

        public CameraRig(Camera camera)
        {
            _camera = camera;
        }

        public Camera Camera { get { return _camera; } }

        public void Rotate(float dxPixels, float dyPixels)
        {
            Yaw += dxPixels * Sensitivity;
            Pitch = Mathf.Clamp(Pitch - dyPixels * Sensitivity, MinPitch, MaxPitch);
        }

        public void Zoom(float delta)
        {
            Distance = Mathf.Clamp(Distance + delta, MinDistance, MaxDistance);
        }

        public Ray ScreenRay(Vector2 screenPosition)
        {
            return _camera.ScreenPointToRay(screenPosition);
        }

        /// <summary>Segue o alvo suavemente. Chame após a atualização do movimento do jogador.</summary>
        public void Follow(Vector3 target, float dt)
        {
            Quaternion rot = Quaternion.Euler(Pitch, Yaw, 0f);
            Vector3 focus = target + new Vector3(0f, 1.6f, 0f);
            Vector3 desired = focus - rot * Vector3.forward * Distance;
            Transform t = _camera.transform;
            t.position = Vector3.Lerp(t.position, desired, 1f - Mathf.Exp(-18f * dt));
            t.rotation = Quaternion.LookRotation(focus - t.position, Vector3.up);
        }

        public void SnapTo(Vector3 target)
        {
            Quaternion rot = Quaternion.Euler(Pitch, Yaw, 0f);
            Vector3 focus = target + new Vector3(0f, 1.6f, 0f);
            Transform t = _camera.transform;
            t.position = focus - rot * Vector3.forward * Distance;
            t.rotation = Quaternion.LookRotation(focus - t.position, Vector3.up);
        }
    }
}
