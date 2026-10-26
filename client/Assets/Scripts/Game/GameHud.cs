using System;
using System.Collections.Generic;
using System.Text;
using Mmorpg.Core.Characters;
using Mmorpg.Core.Combat;
using Mmorpg.Core.Controls;
using Mmorpg.Core.Data;
using Mmorpg.Core.Events;
using Mmorpg.Core.Hotbar;
using Mmorpg.Core.Items;
using Mmorpg.Core.Session;
using UnityEngine;

namespace Mmorpg.Game
{
    /// <summary>
    /// Interface do jogo em IMGUI (sem prefabs nem cena autoral: funciona em qualquer cena vazia, PC e Android).
    /// Só LÊ o estado da <see cref="GameSession"/> e envia COMANDOS por ela; nunca calcula resultado de combate.
    /// Desenhada a partir de <see cref="GameBootstrap"/>.OnGUI.
    ///
    /// Coordenadas: o núcleo e a entrada usam pixels com Y para CIMA; o IMGUI usa Y para BAIXO. A conversão fica em ToGui.
    /// </summary>
    public sealed class GameHud
    {
        private sealed class Floater
        {
            public Vector3 World;
            public string Text;
            public Color Color;
            public float Age;
        }

        private const float FloaterLife = 1.2f;
        private const int MaxFloaters = 40;
        private const int LogLinesShown = 6;
        private const float LabelRange = 45f;

        private static readonly string[] EquipLabels = { "weapon", "head", "chest", "legs", "feet", "accessory" };

        private readonly GameSession _session;
        private readonly CameraRig _camera;
        private WorldView _world;
        private PlayerInput _input;

        private readonly List<Floater> _floaters = new List<Floater>();
        private readonly List<Rect> _uiRects = new List<Rect>(); // coordenadas do IMGUI (Y para baixo)
        private readonly StringBuilder _sb = new StringBuilder();

        private bool _inventoryOpen;
        private bool _menuOpen;
        private int _selectedSlot = -1;
        private string _banner;
        private float _bannerAge;

        private GUIStyle _label;
        private GUIStyle _button;
        private Texture2D _circle;

        public GameHud(GameSession session, CameraRig camera)
        {
            _session = session;
            _camera = camera;
            _session.EventRaised += OnEvent;
        }

        /// <summary>Liga o HUD ao mundo visual e à entrada (criados depois dele, por causa da dependência circular).</summary>
        public void Bind(WorldView world, PlayerInput input)
        {
            _world = world;
            _input = input;
        }

        public void Dispose()
        {
            _session.EventRaised -= OnEvent;
            if (_circle != null) UnityEngine.Object.Destroy(_circle);
        }

        // ------------------------------------------------------------------ estado das janelas (usado pela entrada)
        public bool AnyWindowOpen { get { return _inventoryOpen || _menuOpen; } }

        public void ToggleInventory() { _inventoryOpen = !_inventoryOpen; if (_inventoryOpen) _menuOpen = false; }

        public void ToggleMenu() { _menuOpen = !_menuOpen; if (_menuOpen) _inventoryOpen = false; }

        public void CloseWindows() { _inventoryOpen = false; _menuOpen = false; }

        /// <summary>True se o ponto (pixels, Y para cima) está sobre uma janela ou a barra de habilidades do HUD.</summary>
        public bool IsOverUi(Vector2 screenPosition)
        {
            Vector2 p = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            for (int i = 0; i < _uiRects.Count; i++)
                if (_uiRects[i].Contains(p)) return true;
            return false;
        }

        // ------------------------------------------------------------------ eventos e tempo
        private void OnEvent(GameEvent e)
        {
            switch (e.Type)
            {
                case GameEventType.Damage:
                case GameEventType.Heal:
                    if (e.Amount < 0.5) return;
                    Combatant target = _session.Sim.World.Get(e.TargetId);
                    Vector3 pos = target != null ? Conv.ToV3(target.Position) : Conv.ToV3(e.Position);
                    bool heal = e.Type == GameEventType.Heal;
                    bool onPlayer = target != null && target.Id == _session.Player.Id;
                    var f = new Floater
                    {
                        World = pos + new Vector3(0f, 2.4f, 0f),
                        Text = (heal ? "+" : "") + ((long)Math.Round(e.Amount)) + (e.Crit ? "!" : ""),
                        Color = heal ? new Color(0.4f, 1f, 0.5f) : (onPlayer ? new Color(1f, 0.35f, 0.3f) : (e.Crit ? new Color(1f, 0.9f, 0.2f) : Color.white))
                    };
                    _floaters.Add(f);
                    if (_floaters.Count > MaxFloaters) _floaters.RemoveAt(0);
                    break;

                case GameEventType.BossPhase:
                case GameEventType.Announcement:
                    if (!string.IsNullOrEmpty(e.Text)) { _banner = e.Text; _bannerAge = 0f; }
                    break;

                case GameEventType.LevelUp:
                    if (e.TargetId == _session.Player.Id) { _banner = "Nível " + ((int)e.Amount) + "!"; _bannerAge = 0f; }
                    break;
            }
        }

        public void Update(float dt)
        {
            for (int i = _floaters.Count - 1; i >= 0; i--)
            {
                _floaters[i].Age += dt;
                if (_floaters[i].Age >= FloaterLife) _floaters.RemoveAt(i);
            }
            if (_banner != null)
            {
                _bannerAge += dt;
                if (_bannerAge > 4f) _banner = null;
            }
        }

        // ------------------------------------------------------------------ desenho
        public void Draw()
        {
            if (_world == null || _input == null) return;
            EnsureStyles();
            if (Event.current.type == EventType.Layout) _uiRects.Clear();

            float k = Scale();
            _label.fontSize = Mathf.RoundToInt(14f * k);
            _button.fontSize = Mathf.RoundToInt(13f * k);

            DrawWorldLabels(k);
            DrawFloaters(k);
            DrawPlayerFrame(k);
            DrawTargetFrame(k);
            DrawXpBar(k);
            DrawLog(k);
            if (_input.TouchMode) DrawTouchControls(k); else DrawHotbar(k);
            DrawBanner(k);
            DrawDeath(k);
            if (_inventoryOpen) DrawInventory(k);
            if (_menuOpen) DrawMenu(k);

            if (!_input.InputAvailable)
                Text(new Rect(10, Screen.height * 0.5f, Screen.width - 20, 80f * k), "Entrada desativada: em Player Settings > Active Input Handling escolha \"Both\" (veja docs/EXECUTAR.md).", Color.red, TextAnchor.MiddleCenter);
        }

        private float Scale()
        {
            return Mathf.Clamp(Screen.height / 720f, 0.75f, 2.5f) * Mathf.Max(0.5f, _input.UiScale);
        }

        private void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { wordWrap = false, richText = false };
            _button = new GUIStyle(GUI.skin.button) { wordWrap = true };
        }

        private static Rect ToGui(RectF r)
        {
            return new Rect((float)r.X, Screen.height - (float)r.Y - (float)r.Height, (float)r.Width, (float)r.Height);
        }

        private void RegisterUi(Rect r)
        {
            if (Event.current.type == EventType.Repaint) _uiRects.Add(r);
        }

        private void Text(Rect r, string text, Color color, TextAnchor anchor)
        {
            _label.alignment = anchor;
            _label.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, _label);
            _label.normal.textColor = color;
            GUI.Label(r, text, _label);
        }

        private static void Fill(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void Bar(Rect r, float fraction, Color color, string text)
        {
            Fill(r, new Color(0f, 0f, 0f, 0.65f));
            fraction = Mathf.Clamp01(fraction);
            Fill(new Rect(r.x + 1, r.y + 1, (r.width - 2) * fraction, r.height - 2), color);
            if (!string.IsNullOrEmpty(text)) Text(r, text, Color.white, TextAnchor.MiddleCenter);
        }

        // ------------------------------------------------------------------ quadros do jogador e do alvo
        private void DrawPlayerFrame(float k)
        {
            Combatant p = _session.Player;
            PlayerCharacter pc = _session.Character;
            CombatWorld w = _session.Sim.World;

            float x = 10f * k, y = 10f * k, width = 230f * k, h = 20f * k;
            Rect frame = new Rect(x - 4, y - 4, width + 8, h * 2 + 38f * k);
            Fill(frame, new Color(0f, 0f, 0f, 0.35f));
            RegisterUi(frame);

            Text(new Rect(x, y, width, 22f * k), pc.Name + "  -  Nv " + pc.Level + " " + pc.Class.Name, Color.white, TextAnchor.MiddleLeft);
            y += 24f * k;
            Bar(new Rect(x, y, width, h), (float)(p.Hp / Math.Max(1.0, p.MaxHp)), new Color(0.8f, 0.15f, 0.15f), ((long)Math.Ceiling(p.Hp)) + " / " + ((long)p.MaxHp));
            y += h + 3f * k;

            if (p.ResourceId != null)
            {
                double max = w.ResourceMax(p);
                ResourceDef def;
                string name = _session.Data.Resources.TryGetValue(p.ResourceId, out def) && !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : p.ResourceId;
                Bar(new Rect(x, y, width, h), max > 0 ? (float)(p.Resource / max) : 0f, Conv.ResourceColor(p.ResourceId), name + " " + ((long)p.Resource) + " / " + ((long)max));
            }

            // Ouro
            Text(new Rect(x, y + h + 2f * k, width, 20f * k), "Ouro: " + pc.Gold, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleLeft);
        }

        private void DrawTargetFrame(float k)
        {
            Combatant t = _session.SelectedTarget;
            if (t == null || t.Dead) return;

            float width = 260f * k, h = 20f * k;
            float x = (Screen.width - width) * 0.5f, y = 10f * k;
            Rect frame = new Rect(x - 4, y - 4, width + 8, h + 30f * k);
            Fill(frame, new Color(0f, 0f, 0f, 0.35f));
            RegisterUi(frame);

            string tag = t.Category != null && !string.IsNullOrEmpty(t.Category.Tag) ? " " + t.Category.Tag : "";
            Text(new Rect(x, y, width, 22f * k), t.Name + "  -  Nv " + t.Level + tag, Conv.Hex(t.Category != null ? t.Category.ColorHex : null, Color.white), TextAnchor.MiddleCenter);
            Bar(new Rect(x, y + 24f * k, width, h), (float)(t.Hp / Math.Max(1.0, t.MaxHp)), new Color(0.85f, 0.3f, 0.2f), ((long)Math.Ceiling(t.Hp)) + " / " + ((long)t.MaxHp));
        }

        private void DrawXpBar(float k)
        {
            PlayerCharacter pc = _session.Character;
            float h = 8f * k;
            float frac = pc.IsMaxLevel ? 1f : (float)pc.Xp / Math.Max(1L, pc.XpToNext);
            Fill(new Rect(0, Screen.height - h, Screen.width, h), new Color(0f, 0f, 0f, 0.6f));
            Fill(new Rect(0, Screen.height - h, Screen.width * Mathf.Clamp01(frac), h), new Color(0.55f, 0.35f, 0.9f));
        }

        // ------------------------------------------------------------------ rótulos no mundo e números de dano
        private void DrawWorldLabels(float k)
        {
            Camera cam = _camera.Camera;
            foreach (EntityView v in _world.Views)
            {
                if (v == null || v.Combatant == null) continue;
                Combatant c = v.Combatant;
                if (c.Dead || c.Id == _session.Player.Id) continue;
                if ((c.Position - _session.Player.Position).Length > LabelRange) continue;

                Vector3 sp = cam.WorldToScreenPoint(v.transform.position + new Vector3(0f, v.LabelHeight, 0f));
                if (sp.z <= 0f) continue;
                float gy = Screen.height - sp.y;
                float width = 70f * k;
                string tag = c.Category != null && !string.IsNullOrEmpty(c.Category.Tag) ? " " + c.Category.Tag : "";
                Text(new Rect(sp.x - 90f * k, gy - 22f * k, 180f * k, 18f * k), c.Name + " " + c.Level + tag, Conv.Hex(c.Category != null ? c.Category.ColorHex : null, Color.white), TextAnchor.MiddleCenter);
                Bar(new Rect(sp.x - width * 0.5f, gy - 4f * k, width, 7f * k), (float)(c.Hp / Math.Max(1.0, c.MaxHp)), new Color(0.85f, 0.2f, 0.15f), null);
            }
        }

        private void DrawFloaters(float k)
        {
            Camera cam = _camera.Camera;
            for (int i = 0; i < _floaters.Count; i++)
            {
                Floater f = _floaters[i];
                Vector3 sp = cam.WorldToScreenPoint(f.World);
                if (sp.z <= 0f) continue;
                float t = f.Age / FloaterLife;
                Color c = f.Color;
                c.a = 1f - t * t;
                Text(new Rect(sp.x - 50f * k, Screen.height - sp.y - 30f * k * t - 12f * k, 100f * k, 24f * k), f.Text, c, TextAnchor.MiddleCenter);
            }
        }

        private void DrawLog(float k)
        {
            IReadOnlyList<string> log = _session.Log;
            int n = Math.Min(LogLinesShown, log.Count);
            if (n == 0) return;
            float lineH = 18f * k;
            float x = 10f * k;
            float bottom = Screen.height - (_input.TouchMode ? 10f * k : 80f * k);
            if (_input.TouchMode) bottom = Screen.height - 10f * k - (float)_input.Layout.ButtonSize * 2.4f; // acima do joystick
            float y = bottom - n * lineH;
            Rect bg = new Rect(x - 4, y - 2, 420f * k, n * lineH + 4);
            Fill(bg, new Color(0f, 0f, 0f, 0.25f));
            for (int i = 0; i < n; i++)
                Text(new Rect(x, y + i * lineH, 420f * k, lineH), log[log.Count - n + i], Color.white, TextAnchor.MiddleLeft);
        }

        private void DrawBanner(float k)
        {
            if (_banner == null) return;
            Color c = new Color(1f, 0.9f, 0.5f, Mathf.Clamp01(1f - (_bannerAge - 3f)));
            int old = _label.fontSize;
            _label.fontSize = Mathf.RoundToInt(26f * k);
            Text(new Rect(0, Screen.height * 0.18f, Screen.width, 40f * k), _banner, c, TextAnchor.MiddleCenter);
            _label.fontSize = old;
        }

        private void DrawDeath(float k)
        {
            if (!_session.Player.Dead) return;
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.3f, 0f, 0f, 0.25f));
            int old = _label.fontSize;
            _label.fontSize = Mathf.RoundToInt(30f * k);
            Text(new Rect(0, Screen.height * 0.4f, Screen.width, 50f * k), "Você foi derrotado. Renascendo...", new Color(1f, 0.5f, 0.45f), TextAnchor.MiddleCenter);
            _label.fontSize = old;
        }

        // ------------------------------------------------------------------ habilidades
        private float CooldownFraction(SkillDef skill, out float remaining)
        {
            remaining = 0f;
            Combatant p = _session.Player;
            double until;
            if (skill == null || skill.Cooldown <= 0 || !p.CooldownUntil.TryGetValue(skill.Id, out until)) return 0f;
            double left = until - _session.Sim.World.Time;
            if (left <= 0) return 0f;
            remaining = (float)left;
            return Mathf.Clamp01((float)(left / skill.Cooldown));
        }

        private string SlotCaption(HotbarSlot s)
        {
            if (s.Skill == null) return "-";
            switch (s.State)
            {
                case HotbarSlotState.LockedByLevel: return s.Skill.Name + "\nNv " + s.RequiredLevel;
                case HotbarSlotState.Purchasable: return s.Skill.Name + "\n" + s.GoldCost + " ouro";
                default: return s.Skill.Name;
            }
        }

        private Color SlotColor(HotbarSlot s)
        {
            switch (s.State)
            {
                case HotbarSlotState.Ready: return new Color(0.15f, 0.25f, 0.4f, 0.9f);
                case HotbarSlotState.Purchasable: return s.CanAfford ? new Color(0.45f, 0.35f, 0.1f, 0.9f) : new Color(0.3f, 0.2f, 0.1f, 0.9f);
                case HotbarSlotState.LockedByLevel: return new Color(0.2f, 0.2f, 0.2f, 0.9f);
                default: return new Color(0.1f, 0.1f, 0.1f, 0.5f);
            }
        }

        private void DrawSlotFace(Rect r, HotbarSlot s, float k)
        {
            Fill(r, SlotColor(s));
            float remaining;
            float frac = CooldownFraction(s.Skill, out remaining);
            if (frac > 0f)
            {
                Fill(new Rect(r.x, r.y, r.width, r.height * frac), new Color(0f, 0f, 0f, 0.6f));
                Text(r, remaining.ToString("0.0"), Color.white, TextAnchor.MiddleCenter);
            }
            else
            {
                int old = _label.fontSize;
                _label.fontSize = Mathf.RoundToInt(Mathf.Max(9f, 11f * k));
                Text(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), SlotCaption(s), Color.white, TextAnchor.MiddleCenter);
                _label.fontSize = old;
            }
            Text(new Rect(r.x + 3, r.y, 20f * k, 16f * k), s.Number.ToString(), new Color(1f, 1f, 1f, 0.7f), TextAnchor.UpperLeft);
        }

        /// <summary>Barra de habilidades do PC: 9 slots embaixo, ao centro. Clique usa (ou compra) a habilidade.</summary>
        private void DrawHotbar(float k)
        {
            IList<HotbarSlot> slots = _session.Hotbar();
            float size = 56f * k, gap = 4f * k;
            float total = slots.Count * size + (slots.Count - 1) * gap;
            float x0 = (Screen.width - total) * 0.5f, y = Screen.height - size - 16f * k;
            RegisterUi(new Rect(x0 - 4, y - 4, total + 8, size + 8));

            for (int i = 0; i < slots.Count; i++)
            {
                Rect r = new Rect(x0 + i * (size + gap), y, size, size);
                DrawSlotFace(r, slots[i], k);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) _input.HotbarPress(slots[i].Number, false);
            }
            Text(new Rect(x0, y - 20f * k, total, 18f * k), "1-9 habilidades  |  Tab alvo  |  B inventário  |  botão direito gira a câmera  |  Esc menu  |  F2 modo toque", new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter);
        }

        /// <summary>Controles de toque (Android): apenas desenho; os toques são tratados por <see cref="PlayerInput"/> com o mesmo TouchLayout.</summary>
        private void DrawTouchControls(float k)
        {
            TouchLayout l = _input.Layout;
            IList<HotbarSlot> slots = _session.Hotbar();

            for (int i = 0; i < TouchLayout.SkillButtonCount && i < slots.Count; i++)
                DrawSlotFace(ToGui(l.SkillButtons[i]), slots[i], k);

            DrawButton(ToGui(l.TargetButton), "Alvo");
            DrawButton(ToGui(l.BagButton), "Bolsa");
            DrawButton(ToGui(l.MenuButton), "Menu");

            // Joystick: base e botão onde o dedo está; quando solto, uma dica fraca na zona.
            if (_circle == null) _circle = MakeCircle(64);
            VirtualJoystick j = _input.Joystick;
            float radius = (float)j.RadiusPixels;
            Color old = GUI.color;
            if (j.Active)
            {
                Vector2 o = new Vector2((float)j.Origin.X, Screen.height - (float)j.Origin.Z);
                GUI.color = new Color(1f, 1f, 1f, 0.2f);
                GUI.DrawTexture(new Rect(o.x - radius, o.y - radius, radius * 2f, radius * 2f), _circle);
                GUI.color = new Color(1f, 1f, 1f, 0.6f);
                float kr = radius * 0.4f;
                Vector2 off = new Vector2((float)j.KnobOffset.X, -(float)j.KnobOffset.Z);
                GUI.DrawTexture(new Rect(o.x + off.x - kr, o.y + off.y - kr, kr * 2f, kr * 2f), _circle);
            }
            else
            {
                float hint = (float)l.ButtonSize * 1.3f;
                Vector2 c = new Vector2((float)l.Margin + hint, Screen.height - ((float)l.Margin + hint));
                GUI.color = new Color(1f, 1f, 1f, 0.1f);
                GUI.DrawTexture(new Rect(c.x - hint, c.y - hint, hint * 2f, hint * 2f), _circle);
            }
            GUI.color = old;
        }

        private void DrawButton(Rect r, string text)
        {
            Fill(r, new Color(0.1f, 0.1f, 0.15f, 0.8f));
            Text(r, text, Color.white, TextAnchor.MiddleCenter);
        }

        private static Texture2D MakeCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, d <= r - 1f ? 1f : 0f));
                }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        // ------------------------------------------------------------------ inventário e equipamento
        private string RarityTag(ItemDef item)
        {
            RarityDef r;
            return _session.Data.Rarities.TryGetValue(item.Rarity, out r) && !string.IsNullOrEmpty(r.Tag) ? r.Tag : "";
        }

        private Color RarityColor(ItemDef item)
        {
            RarityDef r;
            return _session.Data.Rarities.TryGetValue(item.Rarity, out r) ? Conv.Hex(r.ColorHex, Color.white) : Color.white;
        }

        private void DrawInventory(float k)
        {
            GameData data = _session.Data;
            PlayerCharacter pc = _session.Character;
            Inventory inv = pc.Inventory;

            float w = Mathf.Min(Screen.width - 20f, 640f * k), h = Mathf.Min(Screen.height - 20f, 460f * k);
            Rect win = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            Fill(win, new Color(0.05f, 0.05f, 0.08f, 0.95f));
            RegisterUi(win);
            GUI.Box(win, GUIContent.none);

            float pad = 10f * k, line = 24f * k;
            Text(new Rect(win.x + pad, win.y + pad, w - 2 * pad - 90f * k, line), "Inventário  (" + (inv.Capacity - inv.FreeSlots) + "/" + inv.Capacity + ")   Ouro: " + pc.Gold, Color.white, TextAnchor.MiddleLeft);
            if (GUI.Button(new Rect(win.xMax - pad - 80f * k, win.y + pad, 80f * k, line), "Fechar", _button)) _inventoryOpen = false;

            // Equipamento (esquerda)
            float colW = 210f * k;
            float y = win.y + pad + line + 6f * k;
            Text(new Rect(win.x + pad, y, colW, line), "Equipado", new Color(1f, 0.9f, 0.5f), TextAnchor.MiddleLeft);
            y += line;
            IList<string> slotsOrder = data.Rules.EquipmentSlots;
            for (int i = 0; i < slotsOrder.Count; i++)
            {
                string slot = slotsOrder[i];
                string id = pc.EquippedItemId(slot);
                ItemDef item;
                bool has = id != null && data.Items.TryGetValue(id, out item);
                string label = slot + ": ";
                Color col = new Color(0.7f, 0.7f, 0.7f);
                if (has)
                {
                    ItemDef it = data.Items[id];
                    label += it.Name + " " + RarityTag(it);
                    col = RarityColor(it);
                }
                else label += "-";
                Text(new Rect(win.x + pad, y, colW - 60f * k, line), label, col, TextAnchor.MiddleLeft);
                if (has && GUI.Button(new Rect(win.x + pad + colW - 56f * k, y + 1, 56f * k, line - 2), "Tirar", _button))
                    _session.Unequip(slot);
                y += line;
            }

            // Bônus de conjunto ativos
            IList<SetBonusDef> bonuses = pc.ActiveSetBonuses();
            if (bonuses.Count > 0)
            {
                y += 6f * k;
                Text(new Rect(win.x + pad, y, colW, line), "Conjunto ativo:", new Color(0.6f, 0.9f, 1f), TextAnchor.MiddleLeft);
                y += line;
                for (int i = 0; i < bonuses.Count; i++)
                {
                    Text(new Rect(win.x + pad, y, colW, line), bonuses[i].Pieces + " peças: " + bonuses[i].Description, new Color(0.6f, 0.9f, 1f), TextAnchor.MiddleLeft);
                    y += line;
                }
            }

            // Grade de itens (direita)
            float gx = win.x + pad + colW + 12f * k, gy = win.y + pad + line + 6f * k;
            int cols = 5;
            float cell = Mathf.Min(64f * k, (win.xMax - gx - pad) / cols - 4f);
            for (int s = 0; s < inv.Capacity; s++)
            {
                Rect r = new Rect(gx + (s % cols) * (cell + 4f), gy + (s / cols) * (cell + 4f), cell, cell);
                ItemStack st = inv.Get(s);
                bool sel = s == _selectedSlot;
                Fill(r, sel ? new Color(0.3f, 0.3f, 0.5f, 0.95f) : new Color(0.15f, 0.15f, 0.2f, 0.9f));
                if (st != null)
                {
                    ItemDef it;
                    if (data.Items.TryGetValue(st.ItemId, out it))
                    {
                        int old = _label.fontSize;
                        _label.fontSize = Mathf.RoundToInt(Mathf.Max(8f, 10f * k));
                        Text(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), it.Name + (st.Count > 1 ? "\nx" + st.Count : ""), RarityColor(it), TextAnchor.MiddleCenter);
                        _label.fontSize = old;
                    }
                }
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) _selectedSlot = s;
            }

            // Detalhe do item selecionado (parte de baixo)
            float dy = win.yMax - 96f * k;
            ItemStack chosen = _selectedSlot >= 0 && _selectedSlot < inv.Capacity ? inv.Get(_selectedSlot) : null;
            ItemDef chosenDef;
            if (chosen != null && data.Items.TryGetValue(chosen.ItemId, out chosenDef))
            {
                _sb.Length = 0;
                _sb.Append(chosenDef.Name).Append("  ").Append(RarityTag(chosenDef));
                if (chosenDef.Type == ItemType.Equipment)
                {
                    _sb.Append("   [").Append(chosenDef.Slot).Append("]  Nv ").Append(chosenDef.RequiredLevel);
                    foreach (KeyValuePair<string, double> kv in chosenDef.Stats) _sb.Append("   ").Append(kv.Key).Append(" +").Append(kv.Value);
                }
                _sb.Append("\nVenda: ").Append(chosenDef.SellValue).Append(" ouro");
                if (!string.IsNullOrEmpty(chosenDef.Description)) _sb.Append("\n").Append(chosenDef.Description);
                Text(new Rect(win.x + pad, dy, w - 2 * pad - 110f * k, 90f * k), _sb.ToString(), RarityColor(chosenDef), TextAnchor.UpperLeft);
                if (chosenDef.Type == ItemType.Equipment && GUI.Button(new Rect(win.xMax - pad - 100f * k, dy, 100f * k, 30f * k), "Equipar", _button))
                    _session.Equip(_selectedSlot);
            }
            else
            {
                Text(new Rect(win.x + pad, dy, w - 2 * pad, 24f * k), "Toque em um item para ver os detalhes.", new Color(0.7f, 0.7f, 0.7f), TextAnchor.MiddleLeft);
            }
        }

        // ------------------------------------------------------------------ menu
        private void DrawMenu(float k)
        {
            float w = Mathf.Min(Screen.width - 20f, 380f * k), h = 372f * k;
            Rect win = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            Fill(win, new Color(0.05f, 0.05f, 0.08f, 0.95f));
            RegisterUi(win);
            GUI.Box(win, GUIContent.none);

            float pad = 14f * k, line = 28f * k, y = win.y + pad;
            Text(new Rect(win.x + pad, y, w - 2 * pad, line), "Menu", Color.white, TextAnchor.MiddleCenter);
            y += line + 4f * k;

            Text(new Rect(win.x + pad, y, w - 2 * pad, line), "Tamanho da interface: " + _input.UiScale.ToString("0.0") + "x", Color.white, TextAnchor.MiddleLeft);
            y += line;
            _input.UiScale = GUI.HorizontalSlider(new Rect(win.x + pad, y + 4f * k, w - 2 * pad, 20f * k), _input.UiScale, 0.7f, 2f);
            y += line;

            Text(new Rect(win.x + pad, y, w - 2 * pad, line), "Sensibilidade da câmera: " + _camera.Sensitivity.ToString("0.00"), Color.white, TextAnchor.MiddleLeft);
            y += line;
            _camera.Sensitivity = GUI.HorizontalSlider(new Rect(win.x + pad, y + 4f * k, w - 2 * pad, 20f * k), _camera.Sensitivity, 0.05f, 0.6f);
            y += line;

            _session.AutoTarget = GUI.Toggle(new Rect(win.x + pad, y, w - 2 * pad, line), _session.AutoTarget, " Mirar automaticamente no inimigo mais próximo", _button);
            y += line + 2f * k;
            _input.TouchMode = GUI.Toggle(new Rect(win.x + pad, y, w - 2 * pad, line), _input.TouchMode, " Controles de toque na tela", _button);
            y += line + 8f * k;

            if (_session.DevToolsEnabled)
            {
                if (GUI.Button(new Rect(win.x + pad, y, w - 2 * pad, 32f * k), "MODO TESTE: nível máximo + todas as habilidades", _button))
                {
                    _session.DevUnlockAll();
                    _menuOpen = false;
                }
                y += 38f * k;
            }

            float bw = (w - 3 * pad) * 0.5f;
            if (GUI.Button(new Rect(win.x + pad, y, bw, 32f * k), "Continuar", _button)) _menuOpen = false;
            if (GUI.Button(new Rect(win.x + 2 * pad + bw, y, bw, 32f * k), "Salvar", _button))
                _session.Notify(_session.Save() ? "Personagem salvo." : "Não foi possível salvar (veja o log).");
            y += 38f * k;
            if (GUI.Button(new Rect(win.x + pad, y, w - 2 * pad, 30f * k), "Salvar e sair do jogo", _button))
            {
                _session.Save();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }
    }
}
