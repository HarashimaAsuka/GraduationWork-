using System.Collections.Generic;
using UnityEngine;

namespace ShoryoumaTaxi
{
    /// <summary>
    /// 地獄(y=0)から現世(y=L)までの縦の道を作る。
    /// 層ごとの空の色・霧・岩の色もここで決める。
    /// </summary>
    public class World
    {
        public struct Zone { public float f; public Color bg, fog, sky, ground, rock, air; }

        static Color C(int hex) => new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);

        public static readonly Zone[] Zones = {
            new Zone { f = 0.00f, bg = C(0x2c0508), fog = C(0x3a070c), sky = C(0xff7040), ground = C(0x200000), rock = C(0x5a1c1a), air = C(0xff7a30) },
            new Zone { f = 0.18f, bg = C(0x3a0d16), fog = C(0x4a1420), sky = C(0xe0708a), ground = C(0x200010), rock = C(0x4e2430), air = C(0xff9a60) },
            new Zone { f = 0.30f, bg = C(0x2a1f40), fog = C(0x3a2c56), sky = C(0xc0a8ea), ground = C(0x1a1030), rock = C(0x4a4260), air = C(0xd8c8ff) },
            new Zone { f = 0.50f, bg = C(0x22386a), fog = C(0x3a5488), sky = C(0xdfeaff), ground = C(0x1a2448), rock = C(0x3a4a70), air = C(0xffffff) },
            new Zone { f = 0.72f, bg = C(0x0b1230), fog = C(0x111c44), sky = C(0xa8b8ff), ground = C(0x080818), rock = C(0x2a3050), air = C(0xbcd0ff) },
            new Zone { f = 1.00f, bg = C(0x160f28), fog = C(0x221638), sky = C(0xffc58a), ground = C(0x100808), rock = C(0x2a2030), air = C(0xffd8a0) },
        };

        /// <summary>高さの割合 f(0..1) における層の色を補間して返す。</summary>
        public static Color ZoneColor(float f, System.Func<Zone, Color> pick)
        {
            f = Mathf.Clamp01(f);
            int i = 0; while (i < Zones.Length - 2 && f > Zones[i + 1].f) i++;
            var a = Zones[i]; var b = Zones[i + 1];
            return Color.Lerp(pick(a), pick(b), Mathf.InverseLerp(a.f, b.f, f));
        }

        public static string ZoneName(float f) => f < 0.2f ? "地獄" : f < 0.45f ? "三途の霧" : f < 0.7f ? "雲海" : f < 0.97f ? "夜空" : "現世";

        readonly float L;
        readonly Transform root;
        readonly List<GameObject> shaftChunks = new List<GameObject>();
        readonly List<float> chunkY = new List<float>();
        readonly List<Transform> lanterns = new List<Transform>();
        const float CHUNK = 60f;

        public World(float length)
        {
            L = length;
            root = new GameObject("World").transform;
            BuildShaft();
            BuildRocks();
            BuildGates();
            BuildLanterns();
            BuildEnds();
        }

        /// <summary>道に沿ったリングとレール。全部が画面中央の消失点に集まり、奥行きと速さが分かる。
        /// 距離で表示を切り替えられるよう 60 ユニットごとに分けている。</summary>
        void BuildShaft()
        {
            var mat = Mats.VertexColor();
            for (float y0 = -24; y0 < L + 6; y0 += CHUNK)
            {
                var v = new List<Vector3>(); var col = new List<Color>(); var idx = new List<int>();
                for (float y = y0; y < y0 + CHUNK && y <= L + 6; y += 6)
                {
                    float f = y / L;
                    Color c = Color.Lerp(ZoneColor(f, z => z.rock), ZoneColor(f, z => z.air), 0.45f);
                    float k = (Mathf.RoundToInt(y / 6f) % 5 == 0) ? 1.25f : 0.75f;
                    c *= k; c.a = 1;
                    const int SEG = 48;
                    for (int i = 0; i < SEG; i++)
                    {
                        float a0 = i * Mathf.PI * 2f / SEG, a1 = (i + 1) * Mathf.PI * 2f / SEG;
                        idx.Add(v.Count); v.Add(new Vector3(Mathf.Cos(a0), y, Mathf.Sin(a0))); col.Add(c);
                        idx.Add(v.Count); v.Add(new Vector3(Mathf.Cos(a1), y, Mathf.Sin(a1))); col.Add(c);
                    }
                }
                Color rc = ZoneColor((y0 + CHUNK / 2) / L, z => z.rock) * 1.4f; rc.a = 1;
                for (int i = 0; i < 16; i++)
                {
                    float a = i * Mathf.PI * 2f / 16;
                    idx.Add(v.Count); v.Add(new Vector3(Mathf.Cos(a), y0, Mathf.Sin(a))); col.Add(rc);
                    idx.Add(v.Count); v.Add(new Vector3(Mathf.Cos(a), y0 + CHUNK, Mathf.Sin(a))); col.Add(rc);
                }
                var go = new GameObject("shaft");
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = MeshKit.Lines(v, col, idx);
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                shaftChunks.Add(go); chunkY.Add(y0 + CHUNK / 2);
            }
        }

        /// <summary>プレイ範囲(RX,RZ)に合わせて道の太さを変える。</summary>
        public void FitShaft(float rx, float rz)
        {
            foreach (var g in shaftChunks) g.transform.localScale = new Vector3(rx * 1.15f + 1.6f, 1f, rz * 1.15f + 1.6f);
        }
        public Vector2 ShaftRadius => shaftChunks.Count > 0 ? new Vector2(shaftChunks[0].transform.localScale.x, shaftChunks[0].transform.localScale.z) : new Vector2(9, 6);

        /// <summary>地獄と三途の霧は岩の洞窟。雲海から上は空がひらける。</summary>
        void BuildRocks()
        {
            var parent = new GameObject("rocks").transform; parent.SetParent(root, false);
            var mats = new Dictionary<int, Material>();
            for (float y = -30; y < L * 0.5f; y += 4f)
            {
                float f = y / L;
                int n = f < 0.38f ? 3 : f < 0.46f ? 2 : 1;
                int band = Mathf.Clamp(Mathf.FloorToInt(f * 10), 0, 9);
                if (!mats.TryGetValue(band, out var m)) { m = Mats.Lit(ZoneColor(band / 10f, z => z.rock), 0.1f); mats[band] = m; }
                for (int i = 0; i < n; i++)
                {
                    float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(10f, 16f) + (f > 0.38f ? Random.Range(2f, 8f) : 0);
                    float big = Random.Range(1.4f, 4.2f);
                    ModelFactory.Prim(i % 2 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere, parent,
                        new Vector3(Mathf.Cos(a) * r, y + Random.Range(-1f, 1f), Mathf.Sin(a) * r),
                        new Vector3(big * Random.Range(0.7f, 1.3f), big * Random.Range(0.8f, 2.2f), big * Random.Range(0.7f, 1.3f)),
                        m, new Vector3(Random.Range(0, 360), Random.Range(0, 360), Random.Range(0, 360)));
                }
            }
        }

        /// <summary>150ごとの注連縄。紙垂は必ず下に垂れるので、上下の手がかりになる。</summary>
        void BuildGates()
        {
            var rope = Mats.Lit(new Color(0.79f, 0.64f, 0.36f), 0.1f);
            var paper = Mats.Lit(Color.white, 0.2f, new Color(0.2f, 0.2f, 0.2f));
            var torus = MeshKit.Torus(8.2f, 0.32f, 64, 8);
            for (float y = 150; y < L; y += 150)
            {
                var g = new GameObject("shimenawa").transform; g.SetParent(root, false); g.localPosition = new Vector3(0, y, 0);
                ModelFactory.MeshObj("rope", torus, g, rope, Vector3.zero, Vector3.one);
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI * 2f / 12;
                    for (int j = 0; j < 4; j++)
                        ModelFactory.Prim(PrimitiveType.Cube, g, new Vector3(Mathf.Cos(a) * 8.2f + (j % 2 == 0 ? -0.1f : 0.1f), -0.4f - j * 0.3f, Mathf.Sin(a) * 8.2f),
                            new Vector3(0.28f, 0.3f, 0.02f), paper, new Vector3(0, -a * Mathf.Rad2Deg + 90f, 0));
                }
            }
        }

        /// <summary>現世の夜空に浮かぶ提灯。</summary>
        void BuildLanterns()
        {
            var parent = new GameObject("lanterns").transform; parent.SetParent(root, false);
            var body = Mats.Lit(new Color(1f, 0.75f, 0.48f), 0.2f, new Color(1f, 0.54f, 0.16f) * 1.6f);
            var cap = Mats.Lit(new Color(0.16f, 0.1f, 0.08f));
            for (int i = 0; i < 40; i++)
            {
                var g = new GameObject("lantern").transform; g.SetParent(parent, false);
                float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(10f, 24f);
                g.localPosition = new Vector3(Mathf.Cos(a) * r, Random.Range(L * 0.64f, L + 20f), Mathf.Sin(a) * r);
                ModelFactory.Prim(PrimitiveType.Cylinder, g, Vector3.zero, new Vector3(0.76f, 0.35f, 0.76f), body);
                ModelFactory.Prim(PrimitiveType.Cylinder, g, new Vector3(0, 0.4f, 0), new Vector3(0.56f, 0.05f, 0.56f), cap);
                ModelFactory.Prim(PrimitiveType.Cylinder, g, new Vector3(0, -0.4f, 0), new Vector3(0.56f, 0.05f, 0.56f), cap);
                lanterns.Add(g);
            }
        }

        void BuildEnds()
        {
            // 地獄の底：溶岩の床
            ModelFactory.Prim(PrimitiveType.Cylinder, root, new Vector3(0, -26f, 0), new Vector3(180f, 0.1f, 180f),
                Mats.Lit(new Color(0.35f, 0.03f, 0.02f), 0.2f, new Color(1f, 0.25f, 0.05f) * 1.5f));
            // 現世：井戸の口と迎え火
            ModelFactory.MeshObj("wellRim", MeshKit.Torus(8.6f, 0.9f, 48, 8), root, Mats.Lit(new Color(0.42f, 0.39f, 0.44f), 0.05f), new Vector3(0, L + 5f, 0), Vector3.one);
            ModelFactory.Prim(PrimitiveType.Sphere, root, new Vector3(0, L + 12f, -6f), Vector3.one * 2f, Mats.Unlit(new Color(1f, 0.6f, 0.2f)));
            var moon = ModelFactory.Prim(PrimitiveType.Sphere, root, new Vector3(40f, L + 230f, 70f), Vector3.one * 18f, Mats.Unlit(new Color(1f, 0.96f, 0.88f)));
            moon.name = "moon";
        }

        /// <summary>毎フレーム：遠い道や提灯を隠して軽くする。</summary>
        public void Tick(float playerY, float t)
        {
            for (int i = 0; i < shaftChunks.Count; i++)
            {
                bool on = Mathf.Abs(chunkY[i] - playerY) < 180f;
                if (shaftChunks[i].activeSelf != on) shaftChunks[i].SetActive(on);
            }
            for (int i = 0; i < lanterns.Count; i++)
            {
                var lt = lanterns[i];
                bool on = Mathf.Abs(lt.position.y - playerY) < 140f;
                if (lt.gameObject.activeSelf != on) lt.gameObject.SetActive(on);
            }
        }
    }
}
