using System.Collections.Generic;
using System.Text;
using Mmorpg.Core.Characters;
using Mmorpg.Core.Data;
using Mmorpg.Core.Presentation;
using UnityEngine;

namespace Mmorpg.Game
{
    /// <summary>
    /// Mostra no corpo o que está equipado. Lê <see cref="PlayerCharacter.Equipped"/> (a fonte da verdade, que já
    /// calcula os atributos) e refaz só a aparência quando muda; NÃO altera atributos, inventário nem salvamento.
    /// Cada item usa seu prefab real (presentation.json, campo "prefab") ou uma forma provisória de primitivas.
    /// </summary>
    public sealed class EquipmentVisuals : MonoBehaviour
    {
        private PlayerCharacter _pc;
        private GameData _data;
        private PresentationCatalog _catalog;
        private ModelParts _parts;
        private EntityView _view;
        private string _signature = "<nunca>";
        private readonly List<GameObject> _spawned = new List<GameObject>();

        public void Init(PlayerCharacter pc, GameData data, PresentationCatalog catalog, ModelParts parts, EntityView view)
        {
            _pc = pc; _data = data; _catalog = catalog; _parts = parts; _view = view;
        }

        private string Signature()
        {
            var keys = new List<string>();
            foreach (KeyValuePair<string, string> kv in _pc.Equipped) keys.Add(kv.Key + "=" + kv.Value);
            keys.Sort();
            var sb = new StringBuilder();
            for (int i = 0; i < keys.Count; i++) sb.Append(keys[i]).Append(';');
            return sb.ToString();
        }

        private void Update()
        {
            if (_pc == null || _parts == null) return;
            string sig = Signature();
            if (sig == _signature) return;
            _signature = sig;
            Rebuild();
        }

        private void Rebuild()
        {
            for (int i = 0; i < _spawned.Count; i++) if (_spawned[i] != null) UnityEngine.Object.Destroy(_spawned[i]);
            _spawned.Clear();

            bool headItem = false;
            foreach (KeyValuePair<string, string> kv in _pc.Equipped)
            {
                ItemDef def;
                if (!_data.Items.TryGetValue(kv.Value, out def)) continue;
                ItemLookDef look = _catalog.ItemLook(def);
                if (look == null || look.Shape == "none") continue;
                if (def.Slot == "head") headItem = true;
                Spawn(def, look);
            }
            // Item de cabeça substitui o adorno da classe (evita dois chapéus).
            if (_parts.ClassHeadgear != null) _parts.ClassHeadgear.gameObject.SetActive(!headItem);
            if (_view != null) _view.RefreshRenderers();
        }

        private void Spawn(ItemDef def, ItemLookDef look)
        {
            Color c = Conv.Hex(look.Color, Color.gray);
            Color glow = string.IsNullOrEmpty(look.Glow) ? c : Conv.Hex(look.Glow, c);
            bool hasGlow = !string.IsNullOrEmpty(look.Glow);

            if (!string.IsNullOrEmpty(look.Prefab))
            {
                GameObject asset = Resources.Load<GameObject>(look.Prefab);
                if (asset != null)
                {
                    Transform at = SocketFor(def, look);
                    if (at != null)
                    {
                        GameObject inst = UnityEngine.Object.Instantiate(asset);
                        inst.transform.SetParent(at, false);
                        _spawned.Add(inst);
                        return;
                    }
                }
                else Debug.LogWarning("Prefab do item " + def.Id + " não encontrado em Resources/" + look.Prefab + "; usando a forma provisória.");
            }

            switch (def.Slot)
            {
                case "weapon":
                {
                    Transform hand = SocketFor(def, look);
                    if (hand != null) _spawned.Add(Weapon(hand, look.Shape, c, glow, hasGlow));
                    break;
                }
                case "head": Head(look.Shape, c, glow, hasGlow); break;
                case "chest": Chest(look.Shape, c, glow, hasGlow); break;
                case "legs": Legs(c); break;
                case "feet": Feet(c); break;
            }
        }

        private Transform SocketFor(ItemDef def, ItemLookDef look)
        {
            string name = look.Socket;
            if (string.IsNullOrEmpty(name))
            {
                switch (def.Slot)
                {
                    case "weapon": name = "hand_r"; break;
                    case "head": name = "head"; break;
                    default: name = "chest"; break;
                }
            }
            Transform t;
            return _parts.Sockets.TryGetValue(name, out t) ? t : null;
        }

        private static Color Dk(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k); }

        private static GameObject P(Transform parent, PrimitiveType t, string name, float x, float y, float z, float sx, float sy, float sz, Color c)
        {
            return Prims.Make(t, name, parent, new Vector3(x, y, z), new Vector3(sx, sy, sz), c, false);
        }

        // ------------------------------------------------------------------ armas (origem = mão; +z à frente, +y para cima)
        private static GameObject Weapon(Transform hand, string shape, Color c, Color glow, bool hasGlow)
        {
            var root = new GameObject("Arma_" + shape);
            root.transform.SetParent(hand, false);
            Transform r = root.transform;
            Color wood = new Color(0.45f, 0.3f, 0.16f);
            switch (shape)
            {
                case "sword":
                    P(r, PrimitiveType.Cube, "Lamina", 0f, 0f, 0.55f, 0.07f, 0.13f, 0.95f, c);
                    if (hasGlow) P(r, PrimitiveType.Cube, "Fio", 0f, 0f, 0.6f, 0.085f, 0.04f, 0.8f, glow);
                    P(r, PrimitiveType.Cube, "Guarda", 0f, 0f, 0.05f, 0.32f, 0.06f, 0.07f, Dk(c, 0.6f));
                    P(r, PrimitiveType.Cylinder, "Cabo", 0f, 0f, -0.08f, 0.06f, 0.08f, 0.06f, wood).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                case "dagger":
                    P(r, PrimitiveType.Cube, "Lamina", 0f, 0f, 0.32f, 0.05f, 0.09f, 0.5f, c);
                    P(r, PrimitiveType.Cube, "Guarda", 0f, 0f, 0.05f, 0.2f, 0.05f, 0.05f, Dk(c, 0.6f));
                    if (hasGlow) P(r, PrimitiveType.Sphere, "Brilho", 0f, 0f, 0.58f, 0.07f, 0.07f, 0.07f, glow);
                    break;
                case "axe":
                    P(r, PrimitiveType.Cylinder, "Cabo", 0f, 0.1f, 0.2f, 0.07f, 0.55f, 0.07f, wood).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    P(r, PrimitiveType.Cube, "Lamina", 0f, 0.05f, 0.75f, 0.06f, 0.4f, 0.3f, c);
                    break;
                case "mace":
                case "scepter":
                    P(r, PrimitiveType.Cylinder, "Cabo", 0f, 0.35f, 0f, 0.07f, 0.55f, 0.07f, wood);
                    P(r, PrimitiveType.Sphere, "Cabeca", 0f, 0.95f, 0f, 0.26f, 0.26f, 0.26f, hasGlow ? glow : c);
                    if (shape == "mace") P(r, PrimitiveType.Cube, "Aro", 0f, 0.95f, 0f, 0.34f, 0.05f, 0.34f, Dk(c, 0.7f));
                    break;
                case "spear":
                    P(r, PrimitiveType.Cylinder, "Haste", 0f, 0.5f, 0f, 0.06f, 1.3f, 0.06f, wood);
                    P(r, PrimitiveType.Cube, "Ponta", 0f, 1.9f, 0f, 0.1f, 0.3f, 0.04f, c);
                    break;
                case "bow":
                    P(r, PrimitiveType.Cube, "Arco", 0f, 0.3f, 0.12f, 0.05f, 1.2f, 0.07f, c);
                    P(r, PrimitiveType.Cube, "Corda", 0f, 0.3f, 0.0f, 0.015f, 1.1f, 0.015f, Color.white);
                    P(r, PrimitiveType.Cube, "PonteiraA", 0f, 0.92f, 0.08f, 0.05f, 0.1f, 0.12f, Dk(c, 0.8f));
                    P(r, PrimitiveType.Cube, "PonteiraB", 0f, -0.32f, 0.08f, 0.05f, 0.1f, 0.12f, Dk(c, 0.8f));
                    break;
                case "wand":
                    P(r, PrimitiveType.Cylinder, "Varinha", 0f, 0.2f, 0f, 0.05f, 0.3f, 0.05f, c);
                    P(r, PrimitiveType.Sphere, "Ponta", 0f, 0.52f, 0f, 0.12f, 0.12f, 0.12f, hasGlow ? glow : Dk(c, 1.2f));
                    break;
                case "staff":
                    P(r, PrimitiveType.Cylinder, "Cajado", 0f, 0.35f, 0f, 0.07f, 0.95f, 0.07f, c);
                    P(r, PrimitiveType.Sphere, "Orbe", 0f, 1.38f, 0f, 0.27f, 0.27f, 0.27f, hasGlow ? glow : Dk(c, 1.3f));
                    P(r, PrimitiveType.Cube, "Garra", 0f, 1.2f, 0f, 0.14f, 0.08f, 0.14f, Dk(c, 0.7f));
                    break;
                default:
                    P(r, PrimitiveType.Cylinder, "Graveto", 0f, 0.3f, 0f, 0.06f, 0.4f, 0.06f, c);
                    break;
            }
            return root;
        }

        // ------------------------------------------------------------------ armaduras
        private void Head(string shape, Color c, Color glow, bool hasGlow)
        {
            Transform head;
            if (!_parts.Sockets.TryGetValue("head", out head)) return;
            var root = new GameObject("Cabeca_" + shape);
            root.transform.SetParent(head, false);
            Transform r = root.transform;
            switch (shape)
            {
                case "helm":
                    P(r, PrimitiveType.Sphere, "Elmo", 0f, 0.08f, -0.01f, 0.5f, 0.42f, 0.5f, c);
                    P(r, PrimitiveType.Cube, "Viseira", 0f, 0.0f, 0.2f, 0.34f, 0.06f, 0.08f, Dk(c, 0.4f));
                    if (hasGlow) P(r, PrimitiveType.Cube, "Crista", 0f, 0.3f, -0.02f, 0.05f, 0.18f, 0.34f, glow);
                    break;
                case "cap":
                    P(r, PrimitiveType.Sphere, "Gorro", 0f, 0.12f, 0f, 0.46f, 0.26f, 0.46f, c);
                    break;
                default: // hood
                    P(r, PrimitiveType.Sphere, "Capuz", 0f, 0.08f, -0.05f, 0.52f, 0.48f, 0.52f, c);
                    P(r, PrimitiveType.Cube, "Ponta", 0f, 0.1f, -0.32f, 0.12f, 0.12f, 0.2f, Dk(c, 0.9f));
                    if (hasGlow) P(r, PrimitiveType.Sphere, "Joia", 0f, 0.22f, 0.2f, 0.08f, 0.08f, 0.06f, glow);
                    break;
            }
            _spawned.Add(root);
        }

        private void Chest(string shape, Color c, Color glow, bool hasGlow)
        {
            Transform chest;
            if (!_parts.Sockets.TryGetValue("chest", out chest)) return;
            var root = new GameObject("Peito_" + shape);
            root.transform.SetParent(chest, false);
            Transform r = root.transform;
            Vector3 t = _parts.Torso;
            P(r, PrimitiveType.Cube, "Armadura", 0f, 0f, 0f, t.x + 0.05f, t.y * 0.98f, t.z + 0.06f, c);
            if (shape == "plate")
            {
                P(r, PrimitiveType.Cube, "Peitoral", 0f, 0.05f, t.z * 0.5f + 0.05f, t.x * 0.85f, t.y * 0.6f, 0.07f, Dk(c, 1.12f));
                P(r, PrimitiveType.Sphere, "OmbroE", -t.x * 0.55f, 0.32f, 0f, 0.3f, 0.22f, 0.3f, Dk(c, 0.9f));
                P(r, PrimitiveType.Sphere, "OmbroD", t.x * 0.55f, 0.32f, 0f, 0.3f, 0.22f, 0.3f, Dk(c, 0.9f));
                if (hasGlow) P(r, PrimitiveType.Sphere, "Emblema", 0f, 0.1f, t.z * 0.5f + 0.1f, 0.12f, 0.12f, 0.05f, glow);
            }
            else
            {
                P(r, PrimitiveType.Cube, "Gola", 0f, t.y * 0.5f, 0f, t.x * 0.55f, 0.08f, t.z + 0.1f, Dk(c, 0.8f));
            }
            _spawned.Add(root);
        }

        private void Legs(Color c)
        {
            for (int i = 0; i < _parts.Legs.Length; i++)
            {
                if (_parts.Legs[i] == null) continue;
                GameObject o = P(_parts.Legs[i], PrimitiveType.Cylinder, "Calca", 0f, -0.4f, 0f, 0.3f, 0.4f, 0.3f, c);
                _spawned.Add(o);
            }
        }

        private void Feet(Color c)
        {
            for (int i = 0; i < _parts.Legs.Length; i++)
            {
                if (_parts.Legs[i] == null) continue;
                GameObject o = P(_parts.Legs[i], PrimitiveType.Cube, "BotaEquipada", 0f, -0.82f, 0.06f, 0.3f, 0.2f, 0.4f, c);
                _spawned.Add(o);
            }
        }
    }
}
