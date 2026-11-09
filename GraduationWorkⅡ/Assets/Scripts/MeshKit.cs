using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShoryoumaTaxi
{
    /// <summary>
    /// マテリアルと手続き的メッシュを作る道具箱。
    /// 仮モデルを全部コードで作るので、アセットなしで遊べる。
    /// </summary>
    public static class Mats
    {
        static Shader lit, unlit, vcol;

        static Shader Find(params string[] names)
        {
            foreach (var n in names) { var s = Shader.Find(n); if (s != null) return s; }
            return null;
        }

        /// <summary>光の当たる不透明マテリアル（URP Lit／なければ Standard）</summary>
        public static Material Lit(Color c, float smoothness = 0.4f, Color? emission = null)
        {
            if (lit == null) lit = Find("Universal Render Pipeline/Lit", "Standard");
            var m = new Material(lit);
            m.SetColor("_BaseColor", c); m.SetColor("_Color", c);
            m.SetFloat("_Smoothness", smoothness); m.SetFloat("_Glossiness", smoothness);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return m;
        }

        /// <summary>光の影響を受けない単色マテリアル</summary>
        public static Material Unlit(Color c)
        {
            if (unlit == null) unlit = Find("Universal Render Pipeline/Unlit", "Unlit/Color");
            var m = new Material(unlit);
            m.SetColor("_BaseColor", c); m.SetColor("_Color", c);
            return m;
        }

        /// <summary>頂点カラーをそのまま描く線用マテリアル</summary>
        public static Material VertexColor()
        {
            if (vcol == null) vcol = Find("Sprites/Default", "Universal Render Pipeline/Particles/Unlit");
            var m = new Material(vcol);
            m.SetColor("_Color", Color.white); m.SetColor("_BaseColor", Color.white);
            return m;
        }

        public static void SetColor(Material m, Color c)
        {
            m.SetColor("_BaseColor", c); m.SetColor("_Color", c);
        }
    }

    public static class MeshKit
    {
        /// <summary>
        /// 回転体（ろくろ）メッシュ。profile は (半径, 高さ) の列。
        /// radial(角度, 高さ) で半径に係数を掛けると、ザクロの筋などが作れる。
        /// プロファイルは下（yが小さい側）から上へ並べること。
        /// </summary>
        public static Mesh Lathe(Vector2[] profile, int seg, System.Func<float, float, float> radial = null)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            int rows = profile.Length;
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                for (int j = 0; j < rows; j++)
                {
                    float r = profile[j].x * (radial != null ? radial(a, profile[j].y) : 1f);
                    v.Add(new Vector3(Mathf.Cos(a) * r, profile[j].y, Mathf.Sin(a) * r));
                }
            }
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < rows - 1; j++)
                {
                    int a = i * rows + j, b = (i + 1) * rows + j;
                    AddQuad(t, a, a + 1, b, b + 1);
                }
            return Build(v, t);
        }

        /// <summary>ドーナツ形（注連縄・ゲート用）</summary>
        public static Mesh Torus(float R, float r, int seg = 48, int tube = 8)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float u = i * Mathf.PI * 2f / seg;
                for (int j = 0; j <= tube; j++)
                {
                    float w = j * Mathf.PI * 2f / tube;
                    float rr = R + r * Mathf.Cos(w);
                    v.Add(new Vector3(Mathf.Cos(u) * rr, r * Mathf.Sin(w), Mathf.Sin(u) * rr));
                }
            }
            int rows = tube + 1;
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < tube; j++)
                {
                    int a = i * rows + j, b = (i + 1) * rows + j;
                    AddQuad(t, a, a + 1, b, b + 1);
                }
            return Build(v, t);
        }

        public static Mesh Cone(float radius, float height, int seg = 10)
        {
            return Lathe(new[] { new Vector2(0, 0), new Vector2(radius, 0), new Vector2(0, height) }, seg);
        }

        /// <summary>XZ平面の円（線）。リング表示用。</summary>
        public static Mesh CircleLines(float radius, int seg, Color c)
        {
            var v = new List<Vector3>(); var col = new List<Color>(); var idx = new List<int>();
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                idx.Add(v.Count); v.Add(new Vector3(Mathf.Cos(a0) * radius, 0, Mathf.Sin(a0) * radius)); col.Add(c);
                idx.Add(v.Count); v.Add(new Vector3(Mathf.Cos(a1) * radius, 0, Mathf.Sin(a1) * radius)); col.Add(c);
            }
            return Lines(v, col, idx);
        }

        public static Mesh Lines(List<Vector3> v, List<Color> col, List<int> idx)
        {
            var m = new Mesh();
            if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v); m.SetColors(col);
            m.SetIndices(idx.ToArray(), MeshTopology.Lines, 0);
            m.RecalculateBounds();
            return m;
        }

        // Unity は時計回りが表。プロファイルを下から上へ並べると外側が表になる。
        static void AddQuad(List<int> t, int a, int a1, int b, int b1)
        {
            t.Add(a); t.Add(a1); t.Add(b); t.Add(b); t.Add(a1); t.Add(b1);
        }

        static Mesh Build(List<Vector3> v, List<int> t)
        {
            var m = new Mesh();
            if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
    }
}
