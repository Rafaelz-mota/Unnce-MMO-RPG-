using System;
using System.Collections.Generic;
using System.IO;
using Mmorpg.Core.Characters;
using Mmorpg.Core.Common;
using Mmorpg.Core.Data;
using Mmorpg.Core.Session;
using UnityEngine;

namespace Mmorpg.Game
{
    /// <summary>
    /// Ponto de entrada da vertical slice. Cria-se sozinho ao dar Play em QUALQUER cena (inclusive vazia), então
    /// o projeto não depende de cena nem de prefabs montados à mão.
    ///
    /// Fluxo: carrega e valida os JSON (erro de dados vira uma tela legível, nunca uma exceção solta) -> retoma o último
    /// personagem salvo ou mostra a escolha de classe -> monta câmera, mundo, entrada e HUD -> a cada frame
    /// entrada -> simulação -> visual. Toda regra de jogo está no núcleo (Mmorpg.Core); aqui só há apresentação.
    ///
    /// Protótipo OFFLINE: o "servidor" é a própria GameSession local. No jogo online o cliente enviará os mesmos
    /// comandos a um servidor autoritativo.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private enum Stage { Loading, ClassSelect, Playing, Error }

        private const string PrefLastCharacter = "mmorpg.lastCharacter";
        private const string DefaultName = "Aventureiro";
        private const int ClassesPerPage = 5;

        private static bool _exists;

        private Stage _stage = Stage.Loading;
        private GameData _data;
        private ICharacterStore _store;
        private GameSession _session;
        private WorldView _world;
        private CameraRig _rig;
        private PlayerInput _input;
        private GameHud _hud;
        private string _regionId;

        private string _errorTitle = "";
        private string _errorText = "";
        private string _notice;
        private string _name = DefaultName;
        private int _page;
        private List<ClassDef> _classes;

        private GUIStyle _title;
        private GUIStyle _text;
        private GUIStyle _button;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _exists = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (_exists) return;
            new GameObject("MMORPG - Vertical Slice").AddComponent<GameBootstrap>();
        }

        private void Awake()
        {
            if (_exists) { Destroy(gameObject); return; }
            _exists = true;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            Application.targetFrameRate = 60;
            Initialize();
        }

        // ------------------------------------------------------------------ inicialização
        private void Initialize()
        {
            try
            {
                LoadResult result = GameDataLoader.Load(new ResourcesDataSource());
                if (!result.Success)
                {
                    Fail("Os dados de jogo (Assets/Resources/GameData) são inválidos", result.Report.ToText());
                    return;
                }
                if (result.Report.WarningCount > 0) Debug.LogWarning("Avisos nos dados de jogo:\n" + result.Report.ToText());
                _data = result.Data;

                _regionId = FirstImplementedRegion(_data);
                if (_regionId == null) { Fail("Nenhuma região implementada", "regions.json não marca nenhuma região com \"implemented\": true."); return; }

                string saveDir = Path.Combine(Application.persistentDataPath, "saves");
                _store = new JsonFileCharacterStore(saveDir);
                Debug.Log("Saves em: " + saveDir);

                _classes = new List<ClassDef>(_data.Classes.Values);
                // Classes com habilidades implementadas primeiro; empate por nome (ordem estável).
                _classes.Sort((a, b) =>
                {
                    int c = b.SkillIds.Count.CompareTo(a.SkillIds.Count);
                    return c != 0 ? c : string.CompareOrdinal(a.Name, b.Name);
                });

                string last = PlayerPrefs.GetString(PrefLastCharacter, "");
                if (!string.IsNullOrEmpty(last))
                {
                    string error;
                    PlayerCharacter pc = GameSession.TryLoadCharacter(_data, _store, last, out error);
                    if (pc != null) { BeginSession(pc); return; }
                    if (error != null)
                    {
                        // Não sobrescreve um save ilegível: o novo personagem usa outro nome e o arquivo antigo fica intacto.
                        _notice = "O personagem \"" + last + "\" não pôde ser carregado (" + error + "). O arquivo foi mantido. Crie um novo personagem.";
                        _name = FreeName(DefaultName);
                    }
                }
                _stage = Stage.ClassSelect;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Fail("Erro inesperado ao iniciar", ex.Message);
            }
        }

        private static string FirstImplementedRegion(GameData data)
        {
            RegionDef best = null;
            foreach (RegionDef r in data.Regions.Values)
                if (r.Implemented && (best == null || r.Order < best.Order)) best = r;
            return best == null ? null : best.Id;
        }

        private string FreeName(string baseName)
        {
            string n = baseName;
            for (int i = 2; _store.Exists(n) && i < 1000; i++) n = baseName + i;
            return n;
        }

        private void Fail(string title, string text)
        {
            _stage = Stage.Error;
            _errorTitle = title;
            _errorText = text;
        }

        // ------------------------------------------------------------------ início da partida
        private void BeginSession(PlayerCharacter pc)
        {
            _session = GameSession.Start(_data, pc, _regionId, new SystemRng(), _store);

            Camera cam = SetupCamera();
            SetupLight();

            _rig = new CameraRig(cam);
            _world = new WorldView(_session, transform);
            _world.Sync();
            _hud = new GameHud(_session, _rig);
            _input = new PlayerInput(_session, _rig, _hud);
            _hud.Bind(_world, _input);
            _rig.SnapTo(Conv.ToV3(_session.Player.Position));

            PlayerPrefs.SetString(PrefLastCharacter, pc.Name);
            PlayerPrefs.Save();
            _session.Save();
            _session.Notify("Bem-vindo ao " + _session.Sim.Region.Name + ". Boss da região: " + _session.Sim.Region.BossName + ".");
            _stage = Stage.Playing;
        }

        private static Camera SetupCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.72f, 0.9f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 250f;
            return cam;
        }

        private static void SetupLight()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.52f, 0.55f);
            if (RenderSettings.sun != null) return;
            var go = new GameObject("Sol");
            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.sun = light;
        }

        // ------------------------------------------------------------------ frame
        private void Update()
        {
            if (_stage != Stage.Playing) return;
            try
            {
                float dt = Time.deltaTime;
                _input.Update(dt);
                _session.Tick(dt);
                _world.Sync();
                _hud.Update(dt);
            }
            catch (Exception ex)
            {
                // Um erro de simulação não pode derrubar o app sem salvar nem explicar.
                Debug.LogException(ex);
                try { _session.Save(); } catch (Exception) { }
                Fail("Erro durante a simulação (o personagem foi salvo, se possível)", ex.GetType().Name + ": " + ex.Message);
            }
        }

        private void LateUpdate()
        {
            if (_stage != Stage.Playing) return;
            EntityView self = _world.ViewOf(_session.Player.Id);
            Vector3 focus = self != null ? self.transform.position : Conv.ToV3(_session.Player.Position);
            _rig.Follow(focus, Time.deltaTime);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && _stage == Stage.Playing) _session.Save();
        }

        private void OnApplicationQuit()
        {
            if (_stage == Stage.Playing) _session.Save();
        }

        private void OnDestroy()
        {
            if (_hud != null) _hud.Dispose();
            if (_world != null) _world.Destroy();
            _exists = false;
        }

        // ------------------------------------------------------------------ telas
        private void OnGUI()
        {
            EnsureStyles();
            float k = Mathf.Clamp(Screen.height / 720f, 0.75f, 2.5f);
            _title.fontSize = Mathf.RoundToInt(26f * k);
            _text.fontSize = Mathf.RoundToInt(15f * k);
            _button.fontSize = Mathf.RoundToInt(15f * k);

            switch (_stage)
            {
                case Stage.Loading:
                    GUI.Label(new Rect(0, Screen.height * 0.45f, Screen.width, 40f * k), "Carregando dados...", _title);
                    break;
                case Stage.ClassSelect:
                    DrawClassSelect(k);
                    break;
                case Stage.Playing:
                    _hud.Draw();
                    break;
                case Stage.Error:
                    DrawError(k);
                    break;
            }
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _text = new GUIStyle(GUI.skin.label) { wordWrap = true, alignment = TextAnchor.UpperLeft };
            _button = new GUIStyle(GUI.skin.button) { wordWrap = true, alignment = TextAnchor.MiddleLeft };
        }

        private void DrawError(float k)
        {
            float w = Mathf.Min(Screen.width - 20f, 760f * k);
            float x = (Screen.width - w) * 0.5f;
            GUI.color = new Color(1f, 0.6f, 0.55f);
            GUI.Label(new Rect(x, 20f * k, w, 80f * k), _errorTitle, _title);
            GUI.color = Color.white;

            string text = _errorText;
            if (text.Length > 3500) text = text.Substring(0, 3500) + "\n... (texto cortado; veja o Console da Unity)";
            GUI.Label(new Rect(x, 110f * k, w, Screen.height - 200f * k), text, _text);

            if (GUI.Button(new Rect(x, Screen.height - 70f * k, 220f * k, 44f * k), "Tentar novamente", _button))
            {
                _stage = Stage.Loading;
                Initialize();
            }
        }

        private void DrawClassSelect(float k)
        {
            float w = Mathf.Min(Screen.width - 20f, 760f * k);
            float x = (Screen.width - w) * 0.5f;
            float y = 12f * k;

            GUI.Label(new Rect(x, y, w, 40f * k), "Escolha sua classe", _title);
            y += 44f * k;

            if (_notice != null)
            {
                GUI.color = new Color(1f, 0.8f, 0.4f);
                GUI.Label(new Rect(x, y, w, 56f * k), _notice, _text);
                GUI.color = Color.white;
                y += 60f * k;
            }

            GUI.Label(new Rect(x, y, 90f * k, 30f * k), "Nome:", _text);
            _name = GUI.TextField(new Rect(x + 80f * k, y, 260f * k, 30f * k), _name ?? "", 24);
            y += 40f * k;

            int pages = (_classes.Count + ClassesPerPage - 1) / ClassesPerPage;
            _page = Mathf.Clamp(_page, 0, pages - 1);
            float cardH = Mathf.Min(86f * k, (Screen.height - y - 70f * k) / ClassesPerPage - 4f);

            for (int i = 0; i < ClassesPerPage; i++)
            {
                int index = _page * ClassesPerPage + i;
                if (index >= _classes.Count) break;
                ClassDef c = _classes[index];
                Rect r = new Rect(x, y + i * (cardH + 4f), w, cardH);

                string roles = string.Join(", ", c.Roles.ToArray());
                string skills = c.SkillIds.Count > 0
                    ? "habilidades nesta versão: " + c.SkillIds.Count + " de " + _data.Rules.SkillsPerClass
                    : "SEM habilidades nesta versão (classe ainda não implementada)";
                string label = c.Name + "   [" + FactionName(c.Faction) + "]   " + roles + "\n" + skills + "\n" + c.Description;
                GUI.color = c.SkillIds.Count > 0 ? Color.white : new Color(0.7f, 0.7f, 0.7f);
                if (GUI.Button(r, label, _button)) CreateAndStart(c);
                GUI.color = Color.white;
            }

            float by = Screen.height - 54f * k;
            if (GUI.Button(new Rect(x, by, 120f * k, 40f * k), "< Anterior", _button)) _page = Mathf.Max(0, _page - 1);
            GUI.Label(new Rect(x + 130f * k, by + 6f * k, 160f * k, 30f * k), "Página " + (_page + 1) + " / " + pages, _text);
            if (GUI.Button(new Rect(x + w - 120f * k, by, 120f * k, 40f * k), "Próxima >", _button)) _page = Mathf.Min(pages - 1, _page + 1);
        }

        private string FactionName(string id)
        {
            foreach (FactionDef f in _data.Rules.Factions)
                if (f.Id == id) return f.Name;
            return id;
        }

        private void CreateAndStart(ClassDef cls)
        {
            string name = string.IsNullOrWhiteSpace(_name) ? DefaultName : _name.Trim();
            try
            {
                if (_store.Exists(name))
                {
                    _notice = "Já existe um personagem chamado \"" + name + "\". Escolha outro nome (o existente não foi alterado).";
                    return;
                }
                PlayerCharacter pc = PlayerCharacter.CreateNew(_data, name, cls.Id);
                _notice = null;
                BeginSession(pc);
            }
            catch (ArgumentException ex)
            {
                _notice = ex.Message;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Fail("Não foi possível criar o personagem", ex.Message);
            }
        }
    }
}
