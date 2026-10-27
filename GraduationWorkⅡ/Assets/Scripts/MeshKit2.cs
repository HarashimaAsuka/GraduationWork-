using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShoryoumaTaxi
{
    /// <summary>v32 で増えたメッシュ（Web版の three.js のジオメトリに合わせたもの）。</summary>
    public static partial class MeshKit
    {
        /// <summary>中心が原点の円すい（three.js の ConeGeometry と同じ。+Y が先）</summary>
        public static Mesh ConeC(float radius, float height, int seg = 10)
        {
            return Lathe(new[] { new Vector2(0, -height / 2), new Vector2(radius, -height / 2), new Vector2(0, height / 2) }, seg);
        }

        /// <summary>XY 平面の輪（裏表どちらからも見える）。three.js の RingGeometry。</summary>
        public static Mesh Ring(float r0, float r1, int seg)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                v.Add(new Vector3(Mathf.Cos(a) * r0, Mathf.Sin(a) * r0, 0));
                v.Add(new Vector3(Mathf.Cos(a) * r1, Mathf.Sin(a) * r1, 0));
            }
            for (int i = 0; i < seg; i++)
            {
                int a = i * 2, b = a + 2;
                Both(t, a, a + 1, b); Both(t, b, a + 1, b + 1);
            }
            return Flat(v, t);
        }

        /// <summary>XY 平面の円板（裏表どちらからも見える）。</summary>
        public static Mesh Disc(float r, int seg)
        {
            var v = new List<Vector3> { Vector3.zero }; var t = new List<int>();
            for (int i = 0; i <= seg; i++) { float a = i * Mathf.PI * 2f / seg; v.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0)); }
            for (int i = 1; i <= seg; i++) Both(t, 0, i, i + 1);
            return Flat(v, t);
        }

        /// <summary>三角形1枚（裏表どちらからも見える。風車の羽根用）。</summary>
        public static Mesh Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            var v = new List<Vector3> { a, b, c, a, c, b };
            var t = new List<int> { 0, 1, 2, 3, 4, 5 };
            var m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>角ばった岩（正二十面体。面ごとに頂点を分けて、平らな陰影にする）。</summary>
        public static Mesh Ico(float r = 1f)
        {
            float p = (1f + Mathf.Sqrt(5f)) / 2f;
            var P = new[] {
                new Vector3(-1, p, 0), new Vector3(1, p, 0), new Vector3(-1, -p, 0), new Vector3(1, -p, 0),
                new Vector3(0, -1, p), new Vector3(0, 1, p), new Vector3(0, -1, -p), new Vector3(0, 1, -p),
                new Vector3(p, 0, -1), new Vector3(p, 0, 1), new Vector3(-p, 0, -1), new Vector3(-p, 0, 1) };
            for (int i = 0; i < P.Length; i++) P[i] = P[i].normalized * r;
            int[] F = { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                        3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i < F.Length; i += 3)
            {
                Vector3 a = P[F[i]], b = P[F[i + 1]], c = P[F[i + 2]];
                // 外向きが表になるように並べる（Unity は Cross(b-a, c-a) が表の向き）
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) < 0) { var tmp = b; b = c; c = tmp; }
                t.Add(v.Count); v.Add(a); t.Add(v.Count); v.Add(b); t.Add(v.Count); v.Add(c);
            }
            var m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>XZ 平面に寝かせた四角形をたくさん並べるメッシュ（粒・花びら・星屑用）。
        /// カメラはいつも道の上（+Y）を見上げているので、XZ 平面の板はいつもこちらを向く。</summary>
        public class QuadCloud
        {
            public readonly Mesh mesh; public readonly int n;
            public readonly Vector3[] v; public readonly Color[] c;
            static readonly Vector2[] UVS = { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };

            public QuadCloud(int count)
            {
                n = count; v = new Vector3[n * 4]; c = new Color[n * 4];
                var uv = new Vector2[n * 4]; var idx = new int[n * 12];
                for (int i = 0; i < n; i++)
                {
                    for (int k = 0; k < 4; k++) uv[i * 4 + k] = UVS[k];
                    int b = i * 4, o = i * 12;
                    // 裏表どちらからも見えるように両面
                    idx[o] = b; idx[o + 1] = b + 1; idx[o + 2] = b + 2; idx[o + 3] = b; idx[o + 4] = b + 2; idx[o + 5] = b + 3;
                    idx[o + 6] = b; idx[o + 7] = b + 2; idx[o + 8] = b + 1; idx[o + 9] = b; idx[o + 10] = b + 3; idx[o + 11] = b + 2;
                }
                mesh = new Mesh(); mesh.MarkDynamic();
                if (n * 4 > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.vertices = v; mesh.uv = uv; mesh.colors = c; mesh.triangles = idx;
            }

            /// <summary>i 番目の板を、中心 p・大きさ s・色 col にする（s = 0 で隠す）。</summary>
            public void Set(int i, Vector3 p, float s, Color col)
            {
                float h = s * 0.5f; int b = i * 4;
                v[b] = new Vector3(p.x - h, p.y, p.z - h); v[b + 1] = new Vector3(p.x + h, p.y, p.z - h);
                v[b + 2] = new Vector3(p.x + h, p.y, p.z + h); v[b + 3] = new Vector3(p.x - h, p.y, p.z + h);
                c[b] = c[b + 1] = c[b + 2] = c[b + 3] = col;
            }

            /// <summary>中心 p から見て、向き nrm に垂直な板にする（星：まわりを囲む球の上に並べる）。</summary>
            public void SetFacing(int i, Vector3 p, Vector3 nrm, float s, Color col)
            {
                var a = Vector3.Cross(nrm, Mathf.Abs(nrm.y) < 0.9f ? Vector3.up : Vector3.right).normalized * (s * 0.5f);
                var b2 = Vector3.Cross(nrm, a).normalized * (s * 0.5f);
                int b = i * 4;
                v[b] = p - a - b2; v[b + 1] = p + a - b2; v[b + 2] = p + a + b2; v[b + 3] = p - a + b2;
                c[b] = c[b + 1] = c[b + 2] = c[b + 3] = col;
            }

            public void Hide(int i) { int b = i * 4; v[b] = v[b + 1] = v[b + 2] = v[b + 3] = new Vector3(0, -99999, 0); }

            public void Apply(Bounds bounds) { mesh.vertices = v; mesh.colors = c; mesh.bounds = bounds; }
        }

        static void Both(List<int> t, int a, int b, int c) { t.Add(a); t.Add(b); t.Add(c); t.Add(a); t.Add(c); t.Add(b); }

        static Mesh Flat(List<Vector3> v, List<int> t)
        {
            var m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0);
            var n = new Vector3[v.Count]; var uv = new Vector2[v.Count]; float ext = 0.0001f;
            foreach (var p in v) ext = Mathf.Max(ext, Mathf.Abs(p.x), Mathf.Abs(p.y));
            for (int i = 0; i < n.Length; i++) { n[i] = Vector3.back; uv[i] = new Vector2(v[i].x / ext * 0.5f + 0.5f, v[i].y / ext * 0.5f + 0.5f); }
            m.normals = n; m.uv = uv; m.RecalculateBounds();
            return m;
        }
    }
}
