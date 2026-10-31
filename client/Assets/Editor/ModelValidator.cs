using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Mmorpg.EditorTools
{
    /// <summary>
    /// Menu "Mmorpg > Validar modelos (Resources/Models)". Confere cada prefab das pastas de modelos prontos contra
    /// o orçamento para Android e as regras de docs/MODELOS.md: altura, triângulos, materiais, tamanho de textura,
    /// LOD, encaixes (hand_r, hand_l, head, chest) e Animator. Só lê; não altera nenhum arquivo.
    /// ATENÇÃO: este arquivo nunca foi executado em um Editor Unity.
    /// </summary>
    public static class ModelValidator
    {
        private sealed class Budget
        {
            public string Folder;
            public int MaxTriangles;
            public float MinHeight, MaxHeight;
            public bool NeedsSockets, NeedsAnimator;
        }

        private static readonly Budget[] Budgets =
        {
            new Budget { Folder = "Classes", MaxTriangles = 15000, MinHeight = 1.5f, MaxHeight = 2.6f, NeedsSockets = true, NeedsAnimator = true },
            new Budget { Folder = "Monsters", MaxTriangles = 20000, MinHeight = 0.3f, MaxHeight = 12f },
            new Budget { Folder = "Npcs", MaxTriangles = 15000, MinHeight = 1.0f, MaxHeight = 3f },
            new Budget { Folder = "Props", MaxTriangles = 6000, MinHeight = 0.2f, MaxHeight = 30f },
            new Budget { Folder = "Buildings", MaxTriangles = 30000, MinHeight = 1f, MaxHeight = 60f },
            new Budget { Folder = "Items", MaxTriangles = 3000, MinHeight = 0.05f, MaxHeight = 3f }
        };

        private static readonly string[] Sockets = { "hand_r", "hand_l", "head", "chest" };

        [MenuItem("Mmorpg/Validar modelos (Resources/Models)")]
        public static void Validate()
        {
            var report = new StringBuilder();
            int checkedCount = 0, problems = 0;
            foreach (Budget b in Budgets)
            {
                string folder = "Assets/Resources/Models/" + b.Folder;
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (asset == null) continue;
                    checkedCount++;
                    List<string> issues = Check(asset, b);
                    if (issues.Count == 0) report.AppendLine("OK     " + path);
                    else
                    {
                        problems += issues.Count;
                        report.AppendLine("AVISO  " + path);
                        foreach (string s in issues) report.AppendLine("         - " + s);
                    }
                }
            }
            string head = "Validação de modelos: " + checkedCount + " prefab(s), " + problems + " aviso(s).";
            if (checkedCount == 0) head += " Nenhum prefab encontrado em Resources/Models (as pastas estão vazias; isso é esperado antes do primeiro modelo).";
            Debug.Log(head + "\n" + report);
        }

        private static List<string> Check(GameObject asset, Budget b)
        {
            var issues = new List<string>();
            GameObject inst = UnityEngine.Object.Instantiate(asset);
            try
            {
                Renderer[] renderers = inst.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) { issues.Add("sem nenhum Renderer (o modelo ficaria invisível)"); return issues; }

                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                float height = bounds.size.y;
                if (height < b.MinHeight || height > b.MaxHeight)
                    issues.Add("altura " + height.ToString("0.00") + " m fora de [" + b.MinHeight + ", " + b.MaxHeight + "] (1 unidade = 1 metro)");
                if (bounds.min.y < -0.2f || bounds.min.y > 0.5f)
                    issues.Add("os pés/base estão em y=" + bounds.min.y.ToString("0.00") + " (deveriam estar em y=0)");

                long triangles = 0;
                var materials = new HashSet<Material>();
                int maxTex = 0;
                foreach (Renderer r in renderers)
                {
                    Mesh mesh = null;
                    var mf = r.GetComponent<MeshFilter>();
                    var sk = r as SkinnedMeshRenderer;
                    if (sk != null) mesh = sk.sharedMesh; else if (mf != null) mesh = mf.sharedMesh;
                    if (mesh != null)
                        for (int s = 0; s < mesh.subMeshCount; s++) triangles += (long)mesh.GetIndexCount(s) / 3;
                    foreach (Material m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        materials.Add(m);
                        Texture tex = m.mainTexture;
                        if (tex != null) maxTex = Math.Max(maxTex, Math.Max(tex.width, tex.height));
                    }
                }
                if (triangles > b.MaxTriangles)
                    issues.Add(triangles + " triângulos acima do orçamento de " + b.MaxTriangles + " para Android (use LOD ou reduza a malha)");
                if (materials.Count > 4)
                    issues.Add(materials.Count + " materiais: cada um é uma chamada de desenho a mais no celular (ideal: 1 a 3 com atlas)");
                if (maxTex > 2048)
                    issues.Add("textura de " + maxTex + "px; para celular use até 2048 (1024 para props e itens)");

                bool needsLod = triangles > 5000 && inst.GetComponentInChildren<LODGroup>() == null;
                if (needsLod) issues.Add("malha pesada (> 5000 triângulos) sem LODGroup");

                if (b.NeedsSockets)
                {
                    foreach (string sock in Sockets)
                        if (FindDeep(inst.transform, sock) == null)
                            issues.Add("falta o encaixe '" + sock + "' (armas e armaduras equipadas não aparecem sem ele)");
                }
                if (b.NeedsAnimator)
                {
                    var anim = inst.GetComponentInChildren<Animator>();
                    if (anim == null) issues.Add("sem Animator (o jogo usará só o balanço por código, que não move um modelo rigado)");
                    else
                    {
                        if (anim.runtimeAnimatorController == null) issues.Add("Animator sem controlador");
                        else if (anim.avatar == null) issues.Add("Animator sem Avatar (rig humanoide não configurado)");
                    }
                }
            }
            catch (Exception e)
            {
                issues.Add("erro ao analisar: " + e.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inst);
            }
            return issues;
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t == null) return null;
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                Transform f = FindDeep(t.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }
    }
}
