using System.Collections.Generic;
using Mmorpg.Core.Common;
using Mmorpg.Core.Controls;
using Mmorpg.Core.Hotbar;
using Mmorpg.Core.Session;
using UnityEngine;

namespace Mmorpg.Game
{
    /// <summary>
    /// Traduz teclado/mouse (PC) e toque (Android) em COMANDOS da <see cref="GameSession"/>. Não decide nada de
    /// combate: o mundo valida alcance, recarga, recurso e alvo. A matemática (zona morta, relativo à câmera,
    /// posição dos botões) vem do núcleo e é testada sem Unity.
    ///
    /// Usa o Input Manager clássico. Se o projeto estiver configurado só com o novo Input System
    /// (Player Settings > Active Input Handling), nada aqui compila em modo ativo e o HUD avisa
    /// para escolher "Both" — veja docs/EXECUTAR.md.
    /// </summary>
    public sealed class PlayerInput
    {
        private struct Pointer
        {
            public int Id;
            public Vector2 Pos;
            public int Phase; // 0 = começou, 1 = moveu, 2 = terminou
        }

        private const int MouseEmulationId = -1000;

        private readonly GameSession _session;
        private readonly CameraRig _camera;
        private readonly GameHud _hud;
        private readonly List<Pointer> _pointers = new List<Pointer>();

        private TouchLayout _layout;
        private int _layoutW, _layoutH;
        private float _layoutScale;

        private int _joyId = int.MinValue;
        private int _camId = int.MinValue;
        private int _cam2Id = int.MinValue;
        private Vector2 _camStart, _camLast, _cam2Last, _prevCamPos;
        private float _camStartTime;
        private bool _camMoved;
        private float _pinchDistance;

        private Vector2 _lastMouse;
        private bool _rotatingWithMouse;

        public VirtualJoystick Joystick { get; private set; }

        public bool TouchMode { get; set; }

        /// <summary>Escala da interface escolhida pelo jogador (menu). Afeta o tamanho dos botões de toque.</summary>
        public float UiScale { get; set; }

        /// <summary>False quando o projeto não tem o Input Manager clássico habilitado.</summary>
        public bool InputAvailable { get; private set; }

        public PlayerInput(GameSession session, CameraRig camera, GameHud hud)
        {
            _session = session;
            _camera = camera;
            _hud = hud;
            UiScale = 1f;
            Joystick = new VirtualJoystick(100.0, 0.15);
            TouchMode = Application.isMobilePlatform;
#if ENABLE_LEGACY_INPUT_MANAGER
            InputAvailable = true;
#endif
        }

        /// <summary>Retângulos dos botões de toque em pixels de tela (origem embaixo à esquerda), recalculados se a tela mudar.</summary>
        public TouchLayout Layout
        {
            get
            {
                if (_layout == null || _layoutW != Screen.width || _layoutH != Screen.height || _layoutScale != UiScale)
                {
                    _layoutW = Screen.width;
                    _layoutH = Screen.height;
                    _layoutScale = UiScale;
                    _layout = TouchLayout.Compute(Mathf.Max(64, Screen.width), Mathf.Max(64, Screen.height), UiScale);
                    Joystick.SetRadius(_layout.ButtonSize * 1.3);
                }
                return _layout;
            }
        }

        // ------------------------------------------------------------------ ações compartilhadas
        /// <summary>Botão/clique/tecla de uma habilidade (1..9). Habilidade paga ainda não comprada é COMPRADA ao clicar/tocar.</summary>
        public void HotbarPress(int number, bool fromKeyboard)
        {
            IList<HotbarSlot> slots = _session.Hotbar();
            if (number >= 1 && number <= slots.Count && slots[number - 1].State == HotbarSlotState.Purchasable)
            {
                if (fromKeyboard)
                    _session.Notify("Clique ou toque na habilidade para comprá-la (" + slots[number - 1].GoldCost + " ouro).");
                else
                    _session.LearnSkill(number);
                return;
            }
            _session.UseHotbarSlot(number);
        }

        public void TapSelect(Vector2 screenPosition)
        {
            Ray ray = _camera.ScreenRay(screenPosition);
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 300f)) return;
            EntityView view = hit.collider.GetComponentInParent<EntityView>();
            if (view != null && view.Combatant != null && view.Combatant.IsMonster) _session.SelectTarget(view.Combatant.Id);
        }

        // ------------------------------------------------------------------ frame
        public void Update(float dt)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchCount > 0) TouchMode = true; // tela de toque em notebook/tablet

            if (Input.GetKeyDown(KeyCode.F2)) TouchMode = !TouchMode; // alterna a interface de toque (para testar no PC)

            if (_session.Player.Dead)
            {
                _session.SetMove(Vec2.Zero);
                Joystick.End();
                _joyId = int.MinValue;
            }

            Keyboard();
            Mouse();
            Pointers();

            // Movimento: teclado e joystick se somam (o núcleo limita o comprimento a 1).
            Vec2 move = Vec2.Zero;
            float x = 0f, z = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) z += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) z -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (x != 0f || z != 0f) move = MoveMath.CameraRelative(new Vec2(x, z), _camera.Yaw);
            if (Joystick.Active) move = MoveMath.CameraRelative(Joystick.Value, _camera.Yaw);
            if (!_session.Player.Dead) _session.SetMove(move);
#endif
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        // ------------------------------------------------------------------ teclado
        private void Keyboard()
        {
            for (int i = 0; i < 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) HotbarPress(i + 1, true);

            if (Input.GetKeyDown(KeyCode.Tab)) _session.CycleTarget();
            if (Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.I)) _hud.ToggleInventory();

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_hud.AnyWindowOpen) _hud.CloseWindows();
                else if (_session.SelectedTargetId != 0) _session.ClearTarget();
                else _hud.ToggleMenu();
            }
        }

        // ------------------------------------------------------------------ mouse
        private void Mouse()
        {
            Vector2 pos = Input.mousePosition;

            if (Input.GetMouseButtonDown(1) && !_hud.IsOverUi(pos)) _rotatingWithMouse = true;
            if (Input.GetMouseButtonUp(1)) _rotatingWithMouse = false;
            if (_rotatingWithMouse && Input.GetMouseButton(1))
                _camera.Rotate(pos.x - _lastMouse.x, pos.y - _lastMouse.y);
            _lastMouse = pos;

            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0f && !_hud.IsOverUi(pos)) _camera.Zoom(-scroll * 1.5f);

            // No modo de toque o mouse é tratado como um dedo (em Pointers); aqui só o modo PC.
            if (!TouchMode && Input.GetMouseButtonDown(0) && !_hud.IsOverUi(pos)) TapSelect(pos);
        }

        // ------------------------------------------------------------------ toque (e mouse como dedo no modo de toque)
        private void Pointers()
        {
            _pointers.Clear();
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                int phase = t.phase == TouchPhase.Began ? 0 : (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled ? 2 : 1);
                _pointers.Add(new Pointer { Id = t.fingerId, Pos = t.position, Phase = phase });
            }
            if (Input.touchCount == 0 && TouchMode)
            {
                Vector2 m = Input.mousePosition;
                if (Input.GetMouseButtonDown(0)) _pointers.Add(new Pointer { Id = MouseEmulationId, Pos = m, Phase = 0 });
                else if (Input.GetMouseButtonUp(0)) _pointers.Add(new Pointer { Id = MouseEmulationId, Pos = m, Phase = 2 });
                else if (Input.GetMouseButton(0)) _pointers.Add(new Pointer { Id = MouseEmulationId, Pos = m, Phase = 1 });
            }

            for (int i = 0; i < _pointers.Count; i++)
            {
                Pointer p = _pointers[i];
                if (p.Phase == 0) OnBegan(p);
                else if (p.Phase == 1) OnMoved(p);
                else OnEnded(p);
            }
        }

        private void OnBegan(Pointer p)
        {
            TouchLayout l = Layout;

            int skill = l.SkillButtonAt(p.Pos.x, p.Pos.y);
            if (skill >= 0) { HotbarPress(skill + 1, false); return; }
            if (l.TargetButton.Contains(p.Pos.x, p.Pos.y)) { _session.CycleTarget(); return; }
            if (l.BagButton.Contains(p.Pos.x, p.Pos.y)) { _hud.ToggleInventory(); return; }
            if (l.MenuButton.Contains(p.Pos.x, p.Pos.y)) { _hud.ToggleMenu(); return; }
            if (_hud.IsOverUi(p.Pos)) return;

            if (_joyId == int.MinValue && l.JoystickZone.Contains(p.Pos.x, p.Pos.y))
            {
                _joyId = p.Id;
                Joystick.Begin(p.Pos.x, p.Pos.y);
                return;
            }

            if (_camId == int.MinValue)
            {
                _camId = p.Id;
                _camStart = p.Pos;
                _camLast = p.Pos;
                _prevCamPos = p.Pos;
                _camStartTime = Time.unscaledTime;
                _camMoved = false;
            }
            else if (_cam2Id == int.MinValue)
            {
                _cam2Id = p.Id;
                _cam2Last = p.Pos;
                _pinchDistance = Vector2.Distance(_camLast, _cam2Last);
            }
        }

        private void OnMoved(Pointer p)
        {
            if (p.Id == _joyId) { Joystick.Move(p.Pos.x, p.Pos.y); return; }

            if (p.Id == _camId) _camLast = p.Pos;
            else if (p.Id == _cam2Id) _cam2Last = p.Pos;
            else return;

            if (_cam2Id != int.MinValue)
            {
                float d = Vector2.Distance(_camLast, _cam2Last);
                _camera.Zoom(-(d - _pinchDistance) * 0.03f);
                _pinchDistance = d;
                _camMoved = true;
                return;
            }

            Vector2 delta = p.Pos - _camStart;
            if (!_camMoved && delta.magnitude > 14f * Mathf.Max(1f, UiScale)) _camMoved = true;
            // Rotação só depois de passar do limiar de toque, para um toque simples não girar a câmera.
            if (_camMoved && p.Id == _camId)
            {
                Vector2 step = p.Pos - _prevCamPos;
                _camera.Rotate(step.x, step.y);
            }
            _prevCamPos = p.Pos;
        }

        private void OnEnded(Pointer p)
        {
            if (p.Id == _joyId)
            {
                Joystick.End();
                _joyId = int.MinValue;
                return;
            }

            if (p.Id == _cam2Id)
            {
                _cam2Id = int.MinValue;
                return;
            }

            if (p.Id == _camId)
            {
                bool tap = !_camMoved && Time.unscaledTime - _camStartTime < 0.35f && _cam2Id == int.MinValue;
                if (tap) TapSelect(p.Pos);
                _camId = _cam2Id; // se havia um segundo dedo, ele vira o principal
                _camLast = _cam2Last;
                _camStart = _cam2Last;
                _prevCamPos = _cam2Last;
                _cam2Id = int.MinValue;
                _camMoved = true; // o dedo restante só gira a câmera
            }
        }
#endif
    }
}
