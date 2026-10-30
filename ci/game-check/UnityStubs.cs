// Stubs MÍNIMOS da UnityEngine, usados SOMENTE pelo CI para verificar que a camada client/Assets/Scripts/Game
// compila (sintaxe, tipos e uso correto da API do núcleo). Não executam nada e NÃO provam que o jogo roda na Unity.
// As assinaturas foram escritas a partir da API da Unity; se a Unity real divergir de algum stub, o erro só
// aparecerá ao abrir o projeto no Editor.
#pragma warning disable CS0649, CS0414, CS0169, IDE0051

using System;

namespace UnityEngine
{
    public class Object
    {
        public string name;
        public static void Destroy(Object o) { }
        public static void DontDestroyOnLoad(Object o) { }
        public static T Instantiate<T>(T original) where T : Object { return original; }
    }

    public class Component : Object
    {
        public Transform transform;
        public GameObject gameObject;
        public T GetComponent<T>() where T : Component { return null; }
        public T GetComponentInParent<T>() where T : Component { return null; }
        public T[] GetComponentsInChildren<T>() where T : Component { return null; }
    }

    public class Behaviour : Component { }
    public class MonoBehaviour : Behaviour { }

    public class Transform : Component
    {
        public Vector3 position, localPosition, localScale;
        public Transform parent;
        public Quaternion rotation, localRotation;
        public void SetParent(Transform parent, bool worldPositionStays) { }
        public Transform GetChild(int i) { return null; }
        public int childCount;
    }

    public class GameObject : Object
    {
        public Transform transform;
        public string tag;
        public GameObject() { }
        public GameObject(string name) { }
        public void SetActive(bool value) { }
        public T AddComponent<T>() where T : Component { return null; }
        public T GetComponent<T>() where T : Component { return null; }
        public T[] GetComponentsInChildren<T>() where T : Component { return null; }
        public static GameObject CreatePrimitive(PrimitiveType type) { return null; }
    }

    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float magnitude { get { return 0f; } }
        public static Vector2 operator -(Vector2 a, Vector2 b) { return a; }
        public static float Distance(Vector2 a, Vector2 b) { return 0f; }
        public static implicit operator Vector3(Vector2 v) { return new Vector3(v.x, v.y, 0f); }
        public static implicit operator Vector2(Vector3 v) { return new Vector2(v.x, v.y); }
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero { get { return default(Vector3); } }
        public static Vector3 one { get { return new Vector3(1, 1, 1); } }
        public static Vector3 up { get { return new Vector3(0, 1, 0); } }
        public static Vector3 forward { get { return new Vector3(0, 0, 1); } }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return a; }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return a; }
        public static Vector3 operator *(Vector3 a, float s) { return a; }
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { return a; }
        public static float Distance(Vector3 a, Vector3 b) { return 0f; }
    }

    public struct Quaternion
    {
        public static Quaternion identity { get { return default(Quaternion); } }
        public static Quaternion Euler(float x, float y, float z) { return default(Quaternion); }
        public static Quaternion LookRotation(Vector3 forward) { return default(Quaternion); }
        public static Quaternion LookRotation(Vector3 forward, Vector3 up) { return default(Quaternion); }
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) { return a; }
        public static Vector3 operator *(Quaternion q, Vector3 v) { return v; }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; a = 1f; }
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white { get { return new Color(1, 1, 1); } }
        public static Color gray { get { return new Color(.5f, .5f, .5f); } }
        public static Color red { get { return new Color(1, 0, 0); } }
        public static Color operator *(Color c, float f) { return c; }
    }

    public static class ColorUtility
    {
        public static bool TryParseHtmlString(string html, out Color color) { color = default(Color); return false; }
    }

    public static class Mathf
    {
        public static float Clamp(float v, float min, float max) { return v; }
        public static int Clamp(int v, int min, int max) { return v; }
        public static float Clamp01(float v) { return v; }
        public static float Max(float a, float b) { return a; }
        public static int Max(int a, int b) { return a; }
        public static float Min(float a, float b) { return a; }
        public static int Min(int a, int b) { return a; }
        public static float Exp(float p) { return 0f; }
        public static int RoundToInt(float f) { return 0; }
        public const float PI = 3.14159274f;
        public const float Rad2Deg = 57.29578f;
        public static float Sin(float f) { return 0f; }
        public static float Cos(float f) { return 0f; }
        public static float Abs(float f) { return 0f; }
        public static float Sqrt(float f) { return 0f; }
        public static float Atan2(float y, float x) { return 0f; }
        public static float Lerp(float a, float b, float t) { return a; }
    }

    public struct Ray { }
    public struct RaycastHit { public Collider collider; }
    public class Collider : Component { }
    public static class Physics
    {
        public static bool Raycast(Ray ray, out RaycastHit hit, float maxDistance) { hit = default(RaycastHit); return false; }
    }

    public class Material : Object { public Color color; public Material() { } public Material(Material source) { } }
    public class Renderer : Component { public Material material, sharedMaterial; public bool enabled; }

    public enum CameraClearFlags { Skybox = 1, SolidColor = 2 }
    public class Camera : Behaviour
    {
        public static Camera main;
        public CameraClearFlags clearFlags;
        public Color backgroundColor;
        public float nearClipPlane, farClipPlane;
        public Ray ScreenPointToRay(Vector3 position) { return default(Ray); }
        public Vector3 WorldToScreenPoint(Vector3 position) { return default(Vector3); }
    }
    public class AudioListener : Behaviour { }

    public enum FogMode { Linear = 1, Exponential = 2, ExponentialSquared = 3 }
    public enum LightType { Spot, Directional, Point }
    public enum LightShadows { None, Hard, Soft }
    public class Light : Behaviour { public LightType type; public float intensity; public LightShadows shadows; public Color color; }

    public static class RenderSettings
    {
        public static Rendering.AmbientMode ambientMode;
        public static Color ambientLight;
        public static Light sun;
        public static bool fog;
        public static Color fogColor;
        public static FogMode fogMode;
        public static float fogStartDistance, fogEndDistance;
    }

    public static class Time { public static float deltaTime; public static float unscaledTime; public static float time; }
    public static class Screen { public static int width; public static int height; }
    public static class Application
    {
        public static bool isMobilePlatform;
        public static int targetFrameRate;
        public static string persistentDataPath;
        public static void Quit() { }
    }
    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogWarning(object message) { }
        public static void LogException(Exception e) { }
    }
    public static class PlayerPrefs
    {
        public static string GetString(string key, string defaultValue) { return defaultValue; }
        public static void SetString(string key, string value) { }
        public static void Save() { }
    }

    public class TextAsset : Object { public string text; }
    public static class Resources { public static T Load<T>(string path) where T : Object { return null; } }

    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad, AfterAssembliesLoaded, BeforeSplashScreen, SubsystemRegistration }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute() { }
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType loadType) { }
    }

    // ------------------------------------------------------------------ entrada
    public enum KeyCode { None, Tab, Escape, F2, UpArrow, DownArrow, RightArrow, LeftArrow, Alpha1, Alpha2, Alpha3, Alpha4, Alpha5, Alpha6, Alpha7, Alpha8, Alpha9, A, B, D, I, S, W }
    public enum TouchPhase { Began, Moved, Stationary, Ended, Canceled }
    public struct Touch { public int fingerId; public Vector2 position; public TouchPhase phase; }
    public static class Input
    {
        public static int touchCount;
        public static Vector3 mousePosition;
        public static Vector2 mouseScrollDelta;
        public static Touch GetTouch(int i) { return default(Touch); }
        public static bool GetKey(KeyCode k) { return false; }
        public static bool GetKeyDown(KeyCode k) { return false; }
        public static bool GetMouseButton(int b) { return false; }
        public static bool GetMouseButtonDown(int b) { return false; }
        public static bool GetMouseButtonUp(int b) { return false; }
    }

    // ------------------------------------------------------------------ IMGUI
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
        public float xMax { get { return x + width; } }
        public float yMax { get { return y + height; } }
        public bool Contains(Vector2 p) { return p.x >= x && p.x < x + width && p.y >= y && p.y < y + height; }
    }

    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight }
    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }
    public enum EventType { MouseDown, MouseUp, Repaint, Layout }
    public class Event { public static Event current; public EventType type; }
    public enum TextureFormat { RGBA32 }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public class Texture : Object { public FilterMode filterMode; }
    public class Texture2D : Texture
    {
        public static Texture2D whiteTexture;
        public Texture2D(int width, int height, TextureFormat format, bool mipChain) { }
        public void SetPixel(int x, int y, Color c) { }
        public void Apply() { }
    }

    public class GUIStyleState { public Color textColor; }
    public class GUIStyle
    {
        public static GUIStyle none;
        public bool wordWrap, richText;
        public TextAnchor alignment;
        public int fontSize;
        public FontStyle fontStyle;
        public GUIStyleState normal = new GUIStyleState();
        public GUIStyle() { }
        public GUIStyle(GUIStyle other) { }
    }
    public class GUIContent { public static GUIContent none; }
    public class GUISkin { public GUIStyle label, button; }

    public static class GUI
    {
        public static GUISkin skin;
        public static Color color;
        public static void Label(Rect r, string text, GUIStyle style) { }
        public static bool Button(Rect r, string text, GUIStyle style) { return false; }
        public static bool Button(Rect r, GUIContent content, GUIStyle style) { return false; }
        public static void Box(Rect r, GUIContent content) { }
        public static void DrawTexture(Rect r, Texture t) { }
        public static float HorizontalSlider(Rect r, float value, float left, float right) { return value; }
        public static bool Toggle(Rect r, bool value, string text, GUIStyle style) { return value; }
        public static Vector2 BeginScrollView(Rect position, Vector2 scrollPosition, Rect viewRect) { return scrollPosition; }
        public static void EndScrollView() { }
        public static bool enabled;
        public static string TextField(Rect r, string text, int maxLength) { return text; }
    }
}

namespace UnityEngine.Rendering
{
    public enum AmbientMode { Skybox, Trilight, Flat, Custom }
}
