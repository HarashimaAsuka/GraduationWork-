using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;   // System.Random と取り違えないように

namespace ShoryoumaTaxi
{
    /// <summary>
    /// 地獄(y=0)から現世(y=L)までの縦の道の景色（v32）。
    /// 道は曲がっている（Road.CxB / CzB）。景色はすべて root の下に置き、ステージ2（帰り）では root を上下反転する。
    /// 稲妻・花火・星・赤い月・花びら・出口の光は遊びの側の座標で動かすので、反転しない（top の下）。
    /// </summary>
    public class World
    {
        public struct Zone { public float f; public Color bg, fog, sky, ground, rock, air; }

        static Color C(int hex) => ModelFactory.Hex(hex);

        public static readonly Zone[] Zones = {
            new Zone { f = 0.00f, bg = C(0x2c0508), fog = C(0x3a070c), sky = C(0xff7040), ground = C(0x200000), rock = C(0x5a1c1a), air = C(0xff7a30) },
            new Zone { f = 0.18f, bg = C(0x3a0d16), fog = C(0x4a1420), sky = C(0xe0708a), ground = C(0x200010), rock = C(0x4e2430), air = C(0xff9a60) },
            new Zone { f = 0.30f, bg = C(0x2a1f40), fog = C(0x3a2c56), sky = C(0xc0a8ea), ground = C(0x1a1030), rock = C(0x4a4260), air = C(0xd8c8ff) },
            new Zone { f = 0.50f, bg = C(0x22386a), fog = C(0x3a5488), sky = C(0xdfeaff), ground = C(0x1a2448), rock = C(0x3a4a70), air = C(0xffffff) },
            new Zone { f = 0.72f, bg = C(0x0b1230), fog = C(0x111c44), sky = C(0xa8b8ff), ground = C(0x080818), rock = C(0x2a3050), air = C(0xbcd0ff) },
            new Zone { f = 1.00f, bg = C(0x160f28), fog = C(0x221638), sky = C(0xffc58a), ground = C(0x100808), rock = C(0x2a2030), air = C(0xffd8a0) },
        };

        /// <summary>v23：ステージ3「幻の三日目」の色。彼岸花の赤と、夜明け前の紫。どの高さでも現実ばなれした空。</summary>
        public static readonly Zone[] ZonesM = {
            new Zone { f = 0.00f, bg = C(0x1a0410), fog = C(0x2a0818), sky = C(0xff3a50), ground = C(0x100004), rock = C(0x5a1c1a), air = C(0xff5060) },
            new Zone { f = 0.35f, bg = C(0x220a2c), fog = C(0x341040), sky = C(0xff6a90), ground = C(0x14041a), rock = C(0x4a4260), air = C(0xff7090) },
            new Zone { f = 0.70f, bg = C(0x160828), fog = C(0x2a1040), sky = C(0xffb070), ground = C(0x0c0418), rock = C(0x2a3050), air = C(0xffc080) },
            new Zone { f = 1.00f, bg = C(0x1c0a20), fog = C(0x301034), sky = C(0xffd090), ground = C(0x100810), rock = C(0x2a2030), air = C(0xffd0a0) },
        };

        /// <summary>高さの割合 f(0..1) における層の色を補間して返す。</summary>
        public static Color ZoneColor(float f, System.Func<Zone, Color> pick) => ZoneColorOf(Zones, f, pick);

        public static Color ZoneColorOf(Zone[] zs, float f, System.Func<Zone, Color> pick)
        {
            f = Mathf.Clamp01(f);
            int i = 0; while (i < zs.Length - 2 && f > zs[i + 1].f) i++;
            var a = zs[i]; var b = zs[i + 1];
            return Color.Lerp(pick(a), pick(b), Mathf.InverseLerp(a.f, b.f, f));
        }

        public static string ZoneName(float f) => f < 0.2f ? "地獄" : f < 0.45f ? "三途の霧" : f < 0.7f ? "雲海" : f < 0.97f ? "夜空" : "現世";

        readonly float L;
        public readonly Transform root, top;
        readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        static float R(float a, float b) => Random.Range(a, b);

        // 浮かんでゆれる景色
        class Floater { public Transform tr; public float y, bx, bz, ph, amp, spin; }
        readonly List<Floater> floaters = new List<Floater>();
        readonly List<(Transform tr, float y)> props = new List<(Transform, float)>();
        class Cloud { public Transform tr; public Renderer rd; public float y, bx, bz, ph, op; public Color col; }
        readonly List<Cloud> clouds = new List<Cloud>();
        class Lantern { public Transform tr; public float y, bx, bz, ph; }
        readonly List<Lantern> lanterns = new List<Lantern>();
        readonly List<(GameObject go, float y)> chunks = new List<(GameObject, float)>();
        readonly Transform[] motes = new Transform[2];
        Renderer hellGlow;
        Material cloudMatA, cloudMatB, cloudMatC;

        // 反転しないもの
        LineRenderer bolt;
        class Firework { public int n; public Vector3[] p, v; public float life, max; public Color col; }
        readonly List<Firework> fireworks = new List<Firework>();
        MeshKit.QuadCloud fwCloud, petalCloud, starCloud;
        Transform starTr, bloodMoon, goalBeacon; Renderer starRd, coronaRd, beaconRd;
        Vector3[] petalP, petalV; bool petalInit; GameObject petalGo;
        float boltT, fwCD = 1;
        public float Flash { get; private set; }
        public bool Dream { get; private set; }

        public static Texture2D PuffTex, LavaTex, PetalTex, MoonTex;

        public World(float length)
        {
            L = length;
            root = new GameObject("World").transform;
            top = new GameObject("World (反転しない)").transform;
            MakeTextures();
            BuildRocks();
            BuildHellFloor();
            BuildClouds();
            BuildLanterns();
            BuildShimenawa();
            BuildProps();
            BuildGrand();
            BuildEnds();
            BuildSky();
        }

        // ============================================================
        //  テクスチャ（Web版の canvas に当たる）
        // ============================================================
        static Texture2D NewTex(int n) => new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };

        static void MakeTextures()
        {
            if (PuffTex != null) return;
            // 雲：ふんわりした丸を9つ重ねる
            const int N = 128;
            var px = new Color[N * N];
            var blobs = new Vector3[9];
            for (int i = 0; i < 9; i++) blobs[i] = new Vector3(N * R(0.3f, 0.7f), N * R(0.35f, 0.65f), N * R(0.16f, 0.32f));
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float a = 0;
                    foreach (var b in blobs) { float d = Mathf.Sqrt((x - b.x) * (x - b.x) + (y - b.y) * (y - b.y)) / b.z; if (d < 1) a = a + (1 - d) * 0.55f * (1 - a); }
                    px[y * N + x] = new Color(1, 1, 1, a);
                }
            PuffTex = NewTex(N); PuffTex.SetPixels(px); PuffTex.Apply();
            // 溶岩
            const int M = 256;
            var lp = new Color[M * M];
            var bg = C(0x3a0505);
            for (int i = 0; i < lp.Length; i++) lp[i] = bg;
            for (int k = 0; k < 70; k++)
            {
                float cx = R(0, M), cy = R(0, M), rr = R(6, 36);
                Color hot = k % 3 != 0 ? new Color(1f, 0.47f, 0.12f) : new Color(1f, 0.86f, 0.35f);
                for (int y = Mathf.Max(0, (int)(cy - rr)); y < Mathf.Min(M, (int)(cy + rr)); y++)
                    for (int x = Mathf.Max(0, (int)(cx - rr)); x < Mathf.Min(M, (int)(cx + rr)); x++)
                    {
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / rr; if (d >= 1) continue;
                        lp[y * M + x] = Color.Lerp(lp[y * M + x], hot, (1 - d) * 0.9f);
                    }
            }
            LavaTex = new Texture2D(M, M, TextureFormat.RGBA32, false); LavaTex.SetPixels(lp); LavaTex.Apply();
            // 彼岸花の花びら
            const int P = 64;
            var pp = new Color[P * P];
            float ca = Mathf.Cos(0.6f), sa = Mathf.Sin(0.6f);
            for (int y = 0; y < P; y++)
                for (int x = 0; x < P; x++)
                {
                    float dx = (x - P / 2f) / P, dy = (y - P / 2f) / P;
                    float u = dx * ca + dy * sa, v = -dx * sa + dy * ca;
                    Color c = new Color(0, 0, 0, 0);
                    if ((u * u) / (0.18f * 0.18f) + (v * v) / (0.42f * 0.42f) <= 1) c = C(0xff2a3a);
                    if ((u * u) / (0.06f * 0.06f) + (v * v) / (0.3f * 0.3f) <= 1) c = C(0xffb0a0);
                    pp[y * P + x] = c;
                }
            PetalTex = NewTex(P); PetalTex.SetPixels(pp); PetalTex.Apply();
            // 赤い月（皆既の月）
            const int Q = 128;
            var mp = new Color[Q * Q];
            var spots = new Vector3[9];
            for (int i = 0; i < 9; i++) spots[i] = new Vector3(Q * R(0.3f, 0.7f), Q * R(0.3f, 0.7f), Q * R(0.03f, 0.09f));
            for (int y = 0; y < Q; y++)
                for (int x = 0; x < Q; x++)
                {
                    float d = Mathf.Sqrt((x - Q / 2f) * (x - Q / 2f) + (y - Q / 2f) * (y - Q / 2f)) / (Q * 0.48f);
                    if (d > 1) { mp[y * Q + x] = new Color(0, 0, 0, 0); continue; }
                    Color c = d < 0.6f ? Color.Lerp(C(0xff8a5a), C(0xc0182a), d / 0.6f) : Color.Lerp(C(0xc0182a), C(0x5a0614), (d - 0.6f) / 0.4f);
                    foreach (var s in spots) if (Mathf.Sqrt((x - s.x) * (x - s.x) + (y - s.y) * (y - s.y)) < s.z) c = Color.Lerp(c, C(0x28000a), 0.35f);
                    c.a = d > 0.95f ? (1 - d) / 0.05f : 1;
                    mp[y * Q + x] = c;
                }
            MoonTex = NewTex(Q); MoonTex.SetPixels(mp); MoonTex.Apply();
        }

        static Material TexMat(Texture2D tex, Color c)
        {
            var m = Mats.VertexColor(); m.mainTexture = tex; m.SetColor("_Color", c); m.SetColor("_BaseColor", c);
            return m;
        }

        /// <summary>XZ 平面に寝かせた板（カメラはいつも道の上を見上げるので、こちらを向く）。</summary>
        static GameObject FlatQuad(Transform parent, Material m, Vector3 pos, float w, float h)
        {
            var g = ModelFactory.Prim(PrimitiveType.Quad, parent, pos, new Vector3(w, h, 1), m, new Vector3(90, 0, 0));
            return g;
        }

        // ============================================================
        //  岩：地獄と三途の霧は岩の洞窟。雲海から上は空がひらける
        // ============================================================
        void BuildRocks()
        {
            var parent = new GameObject("rocks").transform; parent.SetParent(root, false);
            var ico = MeshKit.Ico(1f);
            var mats = new Material[10];
            for (int b = 0; b < 10; b++) mats[b] = Mats.Lit(ZoneColor(b / 10f + 0.05f, z => z.rock), 0.05f);
            const float CH = 100f;
            var lists = new Dictionary<int, List<CombineInstance>>();
            for (float y = -30; y < L * 0.5f; y += 3.2f)
            {
                float f = y / L;
                int n = f < 0.38f ? 3 : f < 0.46f ? 2 : 1;
                for (int i = 0; i < n; i++)
                {
                    float yy = y + R(-1, 1), a = R(0, Mathf.PI * 2), r = R(16, 24) + (f > 0.38f ? R(3, 10) : 0);
                    float big = R(1.4f, 4.2f);
                    var p = new Vector3(Road.CxB(yy) + Mathf.Cos(a) * r, yy, Road.CzB(yy) + Mathf.Sin(a) * r);
                    var s = new Vector3(big * R(0.7f, 1.3f), big * R(0.8f, 2.2f), big * R(0.7f, 1.3f));
                    int band = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0, yy) / L * 10), 0, 9);
                    int chunk = Mathf.FloorToInt((yy + 40) / CH);
                    int key = chunk * 16 + band;
                    if (!lists.TryGetValue(key, out var l)) { l = new List<CombineInstance>(); lists[key] = l; }
                    l.Add(new CombineInstance { mesh = ico, transform = Matrix4x4.TRS(p, Quaternion.Euler(R(0, 360), R(0, 360), R(0, 360)), s) });
                }
            }
            foreach (var kv in lists)
            {
                var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                m.CombineMeshes(kv.Value.ToArray(), true, true);
                var g = new GameObject("rockChunk"); g.transform.SetParent(parent, false);
                g.AddComponent<MeshFilter>().sharedMesh = m;
                g.AddComponent<MeshRenderer>().sharedMaterial = mats[kv.Key % 16];
                chunks.Add((g, (kv.Key / 16) * CH - 40 + CH / 2));
            }
        }

        void BuildHellFloor()
        {
            var lava = Mats.Unlit(Color.white); lava.mainTexture = LavaTex;
            ModelFactory.MeshObj("hellFloor", MeshKit.Disc(90, 40), root, lava, new Vector3(Road.CxB(-26), -26, Road.CzB(-26)), Vector3.one, new Vector3(90, 0, 0));
            var hg = ModelFactory.GlowFlat(root, 0xff4020, 70, 0.7f, new Vector3(Road.CxB(-20), -20, Road.CzB(-20)));
            hellGlow = hg.GetComponent<Renderer>();
        }

        // 雲と霧の層：まっすぐ突き抜けていく
        void BuildClouds()
        {
            var parent = new GameObject("clouds").transform; parent.SetParent(root, false);
            cloudMatA = TexMat(PuffTex, C(0xc8b4f0)); cloudMatB = TexMat(PuffTex, C(0xf2f6ff)); cloudMatC = TexMat(PuffTex, C(0xa8b8e8));
            void Add(float y, int n, float spread, Material m, Color col, float op, float size)
            {
                for (int i = 0; i < n; i++)
                {
                    float a = R(0, Mathf.PI * 2), r = Mathf.Sqrt(Random.value) * spread, sz = size * R(0.7f, 1.4f), yy = y + R(-1.8f, 1.8f);
                    var c = new Cloud { y = yy, bx = Road.CxB(y) + Mathf.Cos(a) * r, bz = Road.CzB(y) + Mathf.Sin(a) * r, ph = R(0, 6), op = op * R(0.6f, 1f), col = col };
                    var g = FlatQuad(parent, m, new Vector3(c.bx, yy, c.bz), sz, sz * 0.6f);
                    c.tr = g.transform; c.rd = g.GetComponent<Renderer>();
                    clouds.Add(c);
                }
            }
            for (float y = L * 0.2f; y < L * 0.46f; y += 16) Add(y, 9, 24, cloudMatA, C(0xc8b4f0), 0.42f, 16);
            for (float y = L * 0.46f; y < L * 0.72f; y += 14) Add(y, 12, 26, cloudMatB, C(0xf2f6ff), 0.62f, 15);
            for (float y = L * 0.72f; y < L * 0.98f; y += 38) Add(y, 6, 26, cloudMatC, C(0xa8b8e8), 0.28f, 18);
        }

        // 現世の夜空に浮かぶ提灯：いつも下にぶら下がる
        void BuildLanterns()
        {
            var parent = new GameObject("lanterns").transform; parent.SetParent(root, false);
            var body = ModelFactory.StdE(0xffc07a, 0xff8a2a, 1.1f, 0.2f);
            var cap = ModelFactory.Std(0x2a1a14); var tassel = ModelFactory.Std(0xc0392b);
            for (int i = 0; i < 50; i++)
            {
                var g = new GameObject("lantern").transform; g.SetParent(parent, false);
                float a = R(0, Mathf.PI * 2), r = R(10, 24), y = R(L * 0.64f, L + 20);
                var lt = new Lantern { tr = g, y = y, bx = Mathf.Cos(a) * r, bz = Mathf.Sin(a) * r, ph = R(0, 6) };
                g.localPosition = new Vector3(Road.CxB(y) + lt.bx, y, Road.CzB(y) + lt.bz);
                ModelFactory.Prim(PrimitiveType.Cylinder, g, Vector3.zero, new Vector3(0.76f, 0.35f, 0.76f), body);
                ModelFactory.Prim(PrimitiveType.Cylinder, g, new Vector3(0, 0.4f, 0), new Vector3(0.56f, 0.05f, 0.56f), cap);
                ModelFactory.Prim(PrimitiveType.Cylinder, g, new Vector3(0, -0.4f, 0), new Vector3(0.56f, 0.05f, 0.56f), cap);
                ModelFactory.Prim(PrimitiveType.Cylinder, g, new Vector3(0, -0.7f, 0), new Vector3(0.11f, 0.25f, 0.11f), tassel);
                ModelFactory.GlowFlat(g, 0xffa040, 3.2f, 0.55f, Vector3.zero);
                lanterns.Add(lt);
            }
        }

        // 150 ごとの注連縄。紙垂は必ず下に垂れるので、上下の手がかりになる
        void BuildShimenawa()
        {
            var rope = Mats.Lit(C(0xc9a45c), 0.1f);
            var paper = Mats.VertexColor(); paper.SetColor("_Color", new Color(0.95f, 0.95f, 0.95f, 1f));
            var torus = MeshKit.Torus(8.2f, 0.32f, 64, 8); var torus2 = MeshKit.Torus(8.2f, 0.16f, 64, 6);
            for (float y = 150; y < L; y += 150)
            {
                var g = new GameObject("shimenawa").transform; g.SetParent(root, false); g.localPosition = new Vector3(Road.CxB(y), y, Road.CzB(y));
                ModelFactory.MeshObj("rope", torus, g, rope, Vector3.zero, Vector3.one);
                ModelFactory.MeshObj("rope2", torus2, g, rope, new Vector3(0, -0.35f, 0), Vector3.one);
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI * 2f / 12;
                    var s = new GameObject("shide").transform; s.SetParent(g, false);
                    s.localPosition = new Vector3(Mathf.Cos(a) * 8.2f, -0.4f, Mathf.Sin(a) * 8.2f); s.localEulerAngles = new Vector3(0, -a * Mathf.Rad2Deg + 90f, 0);
                    for (int j = 0; j < 4; j++)
                        ModelFactory.Prim(PrimitiveType.Quad, s, new Vector3(j % 2 == 1 ? 0.1f : -0.1f, -0.2f - j * 0.3f, 0), new Vector3(0.28f, 0.32f, 1), paper);
                }
                props.Add((g, y));
            }
        }

        // ============================================================
        //  v19・v20：道ぞいの景色（標識・浮き島・漂う粒）。全部ゆっくり漂って、浮いている感じを出す
        // ============================================================
        static GameObject Torii(Transform parent, float s, Material m)
        {
            var g = new GameObject("torii"); g.transform.SetParent(parent, false);
            var t = g.transform;
            foreach (int sd in new[] { -1, 1 }) ModelFactory.Prim(PrimitiveType.Cylinder, t, new Vector3(sd * 1.4f, 1.6f, 0), new Vector3(0.38f, 1.6f, 0.38f), m);
            ModelFactory.Prim(PrimitiveType.Cube, t, new Vector3(0, 3.3f, 0), new Vector3(4.2f, 0.32f, 0.4f), m);
            ModelFactory.Prim(PrimitiveType.Cube, t, new Vector3(0, 2.6f, 0), new Vector3(3.4f, 0.22f, 0.3f), m);
            t.localScale = Vector3.one * s;
            return g;
        }

        void BuildProps()
        {
            var parent = new GameObject("props").transform; parent.SetParent(root, false);
            var stoneMat = ModelFactory.Std(0x5a4a4e, 0.05f); var capMat = ModelFactory.Std(0x3a3036, 0.05f);
            var lampMat = ModelFactory.StdE(0xffc07a, 0xff8a2a, 1.1f, 0.2f); var poleMat = ModelFactory.Std(0x3a2a20, 0.1f);
            var pillarMat = ModelFactory.StdE(0x4a0c0c, 0xff2a08, 0.35f, 0.1f); var toriiMat = ModelFactory.StdE(0xc8361e, 0x5a0a00, 0.4f, 0.3f);
            var capCone = MeshKit.ConeC(0.85f, 0.55f, 4);
            var puff = TexMat(PuffTex, new Color(0.95f, 0.96f, 1f, 0.55f));
            var ico = MeshKit.Ico(1f);

            // 1) 道のすぐ横の標識：11 丈ごとに左右交互。通り過ぎる速さが分かる
            int k = 0;
            for (float y = 12; y < L; y += 11, k++)
            {
                float f = y / L; int sd = k % 2 == 1 ? -1 : 1;
                var g = new GameObject("marker").transform; g.SetParent(parent, false);
                float lx = sd * R(8.5f, 11.5f), lz = R(-3.5f, 3.5f);
                if (f < 0.45f)
                {
                    if (k % 4 == 3) Torii(g, 0.7f, toriiMat);
                    else
                    {
                        // 石灯籠：地獄は赤い火、三途の霧は青白い火
                        ModelFactory.Prim(PrimitiveType.Cube, g, Vector3.zero, new Vector3(0.8f, 1f, 0.8f), stoneMat);
                        ModelFactory.MeshObj("cap", capCone, g, capMat, new Vector3(0, 0.75f, 0), Vector3.one, new Vector3(0, 45, 0));
                        ModelFactory.GlowFlat(g, f < 0.2f ? 0xff6a2a : 0xa8c8ff, 3.6f, 0.85f, Vector3.zero);
                    }
                }
                else if (f < 0.72f) FlatQuad(g, puff, Vector3.zero, R(7, 10), R(4, 6));   // 雲の柱
                else
                {
                    // 提灯の竿
                    ModelFactory.Prim(PrimitiveType.Cylinder, g, Vector3.zero, new Vector3(0.14f, 1.3f, 0.14f), poleMat);
                    ModelFactory.Prim(PrimitiveType.Cylinder, g, new Vector3(0, 1.1f, 0), new Vector3(0.72f, 0.35f, 0.72f), lampMat);
                    ModelFactory.GlowFlat(g, 0xffa040, 3.2f, 0.6f, new Vector3(0, 1.1f, 0));
                }
                g.localPosition = new Vector3(Road.CxB(y) + lx, y, Road.CzB(y) + lz);
                floaters.Add(new Floater { tr = g, y = y, bx = lx, bz = lz, ph = R(0, 6), amp = f < 0.45f ? 0.35f : 0.9f });
                props.Add((g, y));
            }
            // 地獄の溶岩の柱：道に沿って立つ、熱く光る柱
            for (float y = -10; y < L * 0.22f; y += 36)
                foreach (int sd in new[] { -1, 1 })
                {
                    var g = new GameObject("lavaPillar").transform; g.SetParent(parent, false);
                    ModelFactory.Prim(PrimitiveType.Cylinder, g, Vector3.zero, new Vector3(2.2f, 7f, 2.2f), pillarMat);
                    ModelFactory.GlowFlat(g, 0xff3010, 9, 0.35f, Vector3.zero);
                    g.localPosition = new Vector3(Road.CxB(y) + sd * R(14, 17), y, Road.CzB(y) + R(-5, 5));
                    props.Add((g, y));
                }
            // 2) 遠くの浮き島：岩の上に鳥居・灯籠・松。ゆっくり回る
            for (float y = L * 0.36f; y < L; y += R(14, 22))
            {
                float f = y / L, a = R(0, Mathf.PI * 2), rad = R(22, 40);
                var g = new GameObject("island").transform; g.SetParent(parent, false);
                float sz = R(2.2f, 4.5f);
                ModelFactory.MeshObj("rock", ico, g, Mats.Lit(ZoneColor(f, z => z.rock) * 1.2f, 0.05f), Vector3.zero, new Vector3(sz * 1.3f, sz * 0.8f, sz));
                float pick = Random.value;
                if (pick < 0.4f) { var t = Torii(g, sz * 0.32f, toriiMat); t.transform.localPosition = new Vector3(0, sz * 0.6f, 0); t.transform.localEulerAngles = new Vector3(0, R(0, 170), 0); }
                else if (pick < 0.75f)
                {
                    ModelFactory.Prim(PrimitiveType.Cylinder, g, new Vector3(0, sz * 0.9f, 0), new Vector3(0.72f, 0.35f, 0.72f) * sz * 0.45f, lampMat);
                    ModelFactory.GlowFlat(g, f < 0.3f ? 0xff5020 : 0xffb050, sz * 2.4f, 0.6f, new Vector3(0, sz * 0.9f, 0));
                }
                else ModelFactory.MeshObj("pine", MeshKit.ConeC(sz * 0.35f, sz * 1.1f, 6), g, ModelFactory.Std(0x1e3a2a, 0.1f), new Vector3(0, sz * 1.1f, 0), Vector3.one);
                float bx = Mathf.Cos(a) * rad, bz = Mathf.Sin(a) * rad * 0.7f;
                g.localPosition = new Vector3(Road.CxB(y) + bx, y, Road.CzB(y) + bz);
                floaters.Add(new Floater { tr = g, y = y, bx = bx, bz = bz, ph = R(0, 6), amp = R(1.2f, 2.4f), spin = R(-0.25f, 0.25f) });
                props.Add((g, y));
            }
            // 3) 漂う粒：地獄の火の粉、霧の花びら、雲のちり、夜空の蛍。2組を逆向きに漂わせる
            var glowMat = TexMat(ModelFactory.GlowTex, Color.white);
            for (int s = 0; s < 2; s++)
            {
                const int N = 1400;
                var q = new MeshKit.QuadCloud(N);
                for (int i = 0; i < N; i++)
                {
                    float y = R(-20, L + 20), a = R(0, Mathf.PI * 2), rad = 3 + Mathf.Pow(Random.value, 0.7f) * 26;
                    var c = ZoneColor(y / L, z => z.air) * R(0.5f, 1f); c.a = 0.8f;
                    q.Set(i, new Vector3(Road.CxB(y) + Mathf.Cos(a) * rad, y, Road.CzB(y) + Mathf.Sin(a) * rad * 0.8f), s == 1 ? 0.3f : 0.48f, c);
                }
                q.Apply(new Bounds(new Vector3(0, L / 2, 0), new Vector3(200, L + 100, 200)));
                var g = new GameObject("motes"); g.transform.SetParent(root, false);
                g.AddComponent<MeshFilter>().sharedMesh = q.mesh; g.AddComponent<MeshRenderer>().sharedMaterial = glowMat;
                motes[s] = g.transform;
            }
        }

        // ============================================================
        //  v22：もっと豪華に —— 道をまたぐ大門、針の山、雷、花火
        // ============================================================
        void BuildGrand()
        {
            var parent = new GameObject("zoneGates").transform; parent.SetParent(root, false);
            var hellPost = ModelFactory.StdE(0x1a0c0c, 0x300400, 1f, 0.6f); var hellBeam = ModelFactory.Std(0x2a1010, 0.6f);
            var mistPost = ModelFactory.Std(0x6a6a72, 0.05f); var mistBeam = ModelFactory.Std(0x55555e, 0.05f);
            var cloudMat = ModelFactory.StdE(0xd8341e, 0x5a0a00, 0.5f, 0.4f);
            var nightPost = ModelFactory.Std(0x3a2a20, 0.1f);
            var blackBeam = ModelFactory.Std(0x18100c, 0.3f);
            var lampMat = ModelFactory.StdE(0xffc07a, 0xff8a2a, 1.2f, 0.2f);
            var ropeMat = ModelFactory.Std(0x2a1a10);
            const float W = 14;
            for (float y = 70; y < L - 20; y += 92)
            {
                float f = y / L;
                string kind = f < 0.2f ? "hell" : f < 0.45f ? "mist" : f < 0.72f ? "cloud" : "night";
                Material post = kind == "hell" ? hellPost : kind == "mist" ? mistPost : kind == "cloud" ? cloudMat : nightPost;
                Material beam = kind == "hell" ? hellBeam : kind == "mist" ? mistBeam : kind == "cloud" ? cloudMat : nightPost;
                int lamp = kind == "hell" ? 0xff4a1a : kind == "mist" ? 0x9ad0ff : kind == "cloud" ? 0xffd080 : 0xffa040;
                var g = new GameObject("zoneGate_" + kind).transform; g.SetParent(parent, false);
                // 柱は画面の上下（z）にのびる
                foreach (int sd in new[] { -1, 1 }) ModelFactory.Prim(PrimitiveType.Cylinder, g, new Vector3(sd * W, 0, 0), new Vector3(2f, 12f, 2f), post, new Vector3(90, 0, 0));
                if (kind == "night")
                {
                    // 盆踊りの提灯の綱：2本の竿のあいだに、提灯がずらりと下がる
                    for (int row = 0; row < 2; row++)
                    {
                        var lr = new GameObject("rope").AddComponent<LineRenderer>(); lr.transform.SetParent(g, false);
                        lr.useWorldSpace = false; lr.positionCount = 21; lr.widthMultiplier = 0.16f; lr.sharedMaterial = ropeMat;
                        for (int q = 0; q <= 20; q++) { float u = q / 20f * 2 - 1; lr.SetPosition(q, new Vector3(u * W, 0, 10 - row * 4 - (1 - u * u) * 3)); }
                        for (int q = 1; q < 9; q++)
                        {
                            float u = q / 9f * 2 - 1;
                            var lp = new Vector3(u * W, 0, 10 - row * 4 - (1 - u * u) * 3 - 1.0f);
                            ModelFactory.Prim(PrimitiveType.Cylinder, g, lp, new Vector3(1.4f, 0.65f, 1.4f), lampMat, new Vector3(90, 0, 0));
                            ModelFactory.GlowFlat(g, 0xffa040, 3.2f, 0.6f, lp);
                        }
                    }
                }
                else
                {
                    foreach (int sd in new[] { -1, 1 }) ModelFactory.GlowFlat(g, lamp, 6, 0.8f, new Vector3(sd * W, -1.5f, 2));
                    ModelFactory.Prim(PrimitiveType.Cube, g, new Vector3(0, 0, 12), new Vector3(W * 2 + 7, 1.6f, 2.0f), kind == "cloud" ? blackBeam : beam);
                    ModelFactory.Prim(PrimitiveType.Cube, g, new Vector3(0, 0, 8.5f), new Vector3(W * 2 + 2, 1.0f, 1.2f), beam);
                    if (kind == "cloud") ModelFactory.Prim(PrimitiveType.Cube, g, new Vector3(0, 0, 10.8f), new Vector3(W * 2 + 5, 0.9f, 1.6f), beam);
                    ModelFactory.Prim(PrimitiveType.Cube, g, new Vector3(0, 0, 10.3f), new Vector3(3.2f, 0.5f, 2.2f), kind == "hell" ? ModelFactory.StdE(0x400808, 0xff2a08, 0.8f) : ModelFactory.Std(0x2a1a10));
                    if (kind == "hell")
                    {
                        // 鬼の顔の飾りと、ぶら下がる鎖の火
                        ModelFactory.GlowFlat(g, 0xff2a08, 9, 0.6f, new Vector3(0, 0, 12));
                        foreach (float sd in new[] { -0.5f, 0.5f }) ModelFactory.GlowFlat(g, 0xff7a20, 3, 0.9f, new Vector3(sd * W, 0, 6));
                    }
                }
                g.localPosition = new Vector3(Road.CxB(y), y, Road.CzB(y));
                props.Add((g, y));
            }
            // 針の山：地獄の壁から道に向かって突き出す、鋼の針
            {
                const int N = 420;
                var cone = MeshKit.ConeC(1, 1, 5);
                var list = new CombineInstance[N];
                for (int i = 0; i < N; i++)
                {
                    float y = R(-20, L * 0.24f), a = R(0, Mathf.PI * 2), rad = R(24, 40);
                    var pos = new Vector3(Road.CxB(y) + Mathf.Cos(a) * rad, y, Road.CzB(y) + Mathf.Sin(a) * rad * 0.85f);
                    var dir = new Vector3(-Mathf.Cos(a), R(-0.2f, 0.5f), -Mathf.Sin(a)).normalized;   // 道の中心へ向けて、少し先を向く
                    float h = R(5, 14);
                    pos += dir * (h * 0.5f);
                    list[i] = new CombineInstance { mesh = cone, transform = Matrix4x4.TRS(pos, Quaternion.FromToRotation(Vector3.up, dir), new Vector3(R(0.6f, 1.3f), h, R(0.6f, 1.3f))) };
                }
                var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                m.CombineMeshes(list, true, true);
                var g = new GameObject("needles"); g.transform.SetParent(root, false);
                g.AddComponent<MeshFilter>().sharedMesh = m;
                g.AddComponent<MeshRenderer>().sharedMaterial = ModelFactory.StdE(0x8a8890, 0x2a0000, 1f, 0.7f);
            }
            // 雷：雲海で、ときどき遠くに稲妻が走る（反転しない）
            bolt = new GameObject("bolt").AddComponent<LineRenderer>(); bolt.transform.SetParent(top, false);
            bolt.useWorldSpace = true; bolt.positionCount = 14; bolt.widthMultiplier = 0.5f;
            bolt.sharedMaterial = Mats.VertexColor(); bolt.startColor = bolt.endColor = new Color(0.91f, 0.94f, 1f, 0);
            // 花火：夜空で、遠くに上がる（反転しない）
            fwCloud = new MeshKit.QuadCloud(6 * 130);
            for (int k = 0; k < 6; k++) fireworks.Add(new Firework { n = 130, p = new Vector3[130], v = new Vector3[130] });
            for (int i = 0; i < fwCloud.n; i++) fwCloud.Hide(i);
            var fg = new GameObject("fireworks"); fg.transform.SetParent(top, false);
            fg.AddComponent<MeshFilter>().sharedMesh = fwCloud.mesh; fg.AddComponent<MeshRenderer>().sharedMaterial = TexMat(ModelFactory.GlowTex, Color.white);
        }

        void BuildEnds()
        {
            // 現世の端：石の井戸の口、迎え火、月
            ModelFactory.MeshObj("wellRim", MeshKit.Torus(8.6f, 0.9f, 48, 8), root, ModelFactory.Std(0x6a6470, 0.05f), new Vector3(Road.CxB(L + 5), L + 5, Road.CzB(L + 5)), Vector3.one);
            ModelFactory.GlowFlat(root, 0xff9a30, 14, 0.9f, new Vector3(Road.CxB(L + 12), L + 12, Road.CzB(L + 12) - 6));
            var mp = new Vector3(Road.CxB(L) + 40, L + 230, Road.CzB(L) + 70);
            ModelFactory.GlowFlat(root, 0xfff2d0, 60, 0.9f, mp);
            ModelFactory.Prim(PrimitiveType.Sphere, root, mp, Vector3.one * 18f, Mats.Unlit(C(0xfff6e0))).name = "moon";
        }

        void BuildSky()
        {
            // 星：カメラについてくる
            starCloud = new MeshKit.QuadCloud(700);
            for (int i = 0; i < 700; i++)
            {
                float u = Random.value * 2 - 1, a = Random.value * Mathf.PI * 2, r = 180;
                var p = new Vector3(Mathf.Sqrt(1 - u * u) * Mathf.Cos(a) * r, u * r, Mathf.Sqrt(1 - u * u) * Mathf.Sin(a) * r);
                starCloud.SetFacing(i, p, p.normalized, R(0.5f, 1.1f), Color.white);
            }
            starCloud.Apply(new Bounds(Vector3.zero, Vector3.one * 400));
            var sg = new GameObject("stars"); sg.transform.SetParent(top, false); starTr = sg.transform;
            sg.AddComponent<MeshFilter>().sharedMesh = starCloud.mesh;
            starRd = sg.AddComponent<MeshRenderer>(); starRd.sharedMaterial = TexMat(ModelFactory.GlowTex, Color.white);
            // 出口の光（現世の井戸、帰りは地獄の火）
            var gb = ModelFactory.GlowQ(top, 0xffb060, 1, 0.95f, Vector3.zero); goalBeacon = gb.transform; beaconRd = gb.GetComponent<Renderer>();
            // 幻の三日目だけ：大きな赤い月と、舞い散る彼岸花の花びら
            bloodMoon = new GameObject("bloodMoon").transform; bloodMoon.SetParent(top, false);
            ModelFactory.GlowQ(bloodMoon, 0xff2030, 150, 0.75f, new Vector3(0, 0, 1));
            ModelFactory.Prim(PrimitiveType.Quad, bloodMoon, Vector3.zero, new Vector3(70, 70, 1), TexMat(MoonTex, Color.white));
            var cor = ModelFactory.MeshObj("corona", MeshKit.Ring(36, 39, 64), bloodMoon, Mats.VertexColor(), new Vector3(0, 0, -0.5f), Vector3.one);
            coronaRd = cor.GetComponent<Renderer>();
            bloodMoon.gameObject.SetActive(false);
            petalCloud = new MeshKit.QuadCloud(700); petalP = new Vector3[700]; petalV = new Vector3[700];
            for (int i = 0; i < 700; i++) petalCloud.Hide(i);
            petalGo = new GameObject("petals"); petalGo.transform.SetParent(top, false);
            petalGo.AddComponent<MeshFilter>().sharedMesh = petalCloud.mesh; petalGo.AddComponent<MeshRenderer>().sharedMaterial = TexMat(PetalTex, Color.white);
            petalGo.SetActive(false);
        }

        // ============================================================
        //  ステージの見た目
        // ============================================================
        /// <summary>ステージ2（帰り）は景色を上下反転、ステージ3（幻の三日目）は赤い月と花びら・桃色の雲。</summary>
        public void SetStage(int no)
        {
            bool flip = no == 2;
            root.localScale = new Vector3(1, flip ? -1 : 1, 1);
            root.localPosition = new Vector3(0, flip ? L : 0, 0);
            Dream = no == 3;
            bloodMoon.gameObject.SetActive(Dream); petalGo.SetActive(Dream); petalInit = false;
            var dream = C(0xd070a0);
            Mats.SetColor(cloudMatA, Dream ? dream : C(0xc8b4f0));
            Mats.SetColor(cloudMatB, Dream ? dream : C(0xf2f6ff));
            Mats.SetColor(cloudMatC, Dream ? dream : C(0xa8b8e8));
        }

        /// <summary>
        /// 毎フレーム：遠い景色を隠し、近い景色をゆらす。playY = 馬の高さ、camPos = カメラの位置（遊びの座標）。
        /// fSee = 見た目の高さ（0 = 地獄、1 = 現世）。
        /// </summary>
        public void Tick(float t, float dt, float playY, Vector3 camPos, Quaternion camRot, float fSee, int stage, bool playing, System.Action<AudioClip> play, Sfx sfx)
        {
            float yl = Road.ToLocal(playY), camL = Road.ToLocal(camPos.y);
            foreach (var c in chunks) { bool on = Mathf.Abs(c.y - yl) < 200; if (c.go.activeSelf != on) c.go.SetActive(on); }
            foreach (var p in props) { bool on = Mathf.Abs(p.y - yl) < 180; if (p.tr.gameObject.activeSelf != on) p.tr.gameObject.SetActive(on); }
            foreach (var fl in floaters)
            {
                if (!fl.tr.gameObject.activeSelf) continue;
                fl.tr.localPosition = new Vector3(Road.CxB(fl.y) + fl.bx + Mathf.Sin(t * 0.4f + fl.ph) * fl.amp * 0.6f, fl.y + Mathf.Sin(t * 0.7f + fl.ph) * fl.amp, Road.CzB(fl.y) + fl.bz + Mathf.Cos(t * 0.5f + fl.ph) * fl.amp * 0.4f);
                if (fl.spin != 0) fl.tr.localEulerAngles = new Vector3(0, (t * fl.spin + fl.ph) * Mathf.Rad2Deg, 0);
            }
            for (int i = 0; i < 2; i++)
            {
                float sgn = i == 1 ? -1 : 1;
                motes[i].localPosition = new Vector3(Mathf.Sin(t * 0.23f + i) * 1.4f * sgn, Mathf.Sin(t * 0.31f + i * 1.7f) * 3 * sgn, Mathf.Cos(t * 0.19f + i * 2) * 1.0f);
            }
            foreach (var c in clouds)
            {
                float dy = Mathf.Abs(c.y - camL);
                bool on = dy < 110;
                if (c.tr.gameObject.activeSelf != on) c.tr.gameObject.SetActive(on);
                if (!on) continue;
                c.tr.localPosition = new Vector3(c.bx + Mathf.Sin(t * 0.15f + c.ph) * 4, c.y, c.bz + Mathf.Cos(t * 0.12f + c.ph) * 2);
                var col = Dream ? C(0xd070a0) : c.col; col.a = c.op * Mathf.Clamp01((dy - 1.5f) / 7f);
                mpb.Clear(); mpb.SetColor("_Color", col); mpb.SetColor("_RendererColor", Color.white); c.rd.SetPropertyBlock(mpb);
            }
            foreach (var lt in lanterns)
            {
                bool on = Mathf.Abs(lt.y - yl) < 160;
                if (lt.tr.gameObject.activeSelf != on) lt.tr.gameObject.SetActive(on);
                if (on) lt.tr.localPosition = new Vector3(Road.CxB(lt.y) + lt.bx + Mathf.Sin(t * 0.3f + lt.ph) * 0.8f, lt.y, Road.CzB(lt.y) + lt.bz + Mathf.Cos(t * 0.25f + lt.ph) * 0.8f);
            }
            // 地獄の火の照り返し・星
            var hc = ModelFactory.Hex(0xff4020); hc.a = 0.7f * Mathf.Clamp01(1 - fSee * 4);
            mpb.Clear(); mpb.SetColor("_Color", hc); mpb.SetColor("_RendererColor", Color.white); hellGlow.SetPropertyBlock(mpb);
            starTr.position = camPos;
            float so = stage == 3 ? 0.9f : Mathf.Clamp01((fSee - 0.45f) / 0.3f) * 0.95f;
            mpb.Clear(); mpb.SetColor("_Color", new Color(1, 1, 1, so)); mpb.SetColor("_RendererColor", Color.white); starRd.SetPropertyBlock(mpb);
            if (starRd.enabled != so > 0.01f) starRd.enabled = so > 0.01f;
            // 出口の光
            float gd = L + 40 - playY;
            goalBeacon.position = new Vector3(Road.Cx(L), playY + Mathf.Min(gd, 330), Road.Cz(L));
            goalBeacon.rotation = camRot;
            goalBeacon.localScale = Vector3.one * Mathf.Lerp(22, 90, Mathf.Clamp01(1 - gd / 900));
            var bc = ModelFactory.Hex(Road.Flip ? 0xff3018 : stage == 3 ? 0xff5a60 : 0xffb060); bc.a = 0.95f;
            mpb.Clear(); mpb.SetColor("_Color", bc); mpb.SetColor("_RendererColor", Color.white); beaconRd.SetPropertyBlock(mpb);
            if (Dream)
            {
                bloodMoon.position = camPos + new Vector3(30, 300, 70); bloodMoon.rotation = camRot;
                mpb.Clear(); mpb.SetColor("_Color", new Color(1f, 0.82f, 0.63f, 0.4f + 0.15f * Mathf.Sin(t * 0.8f))); mpb.SetColor("_RendererColor", Color.white); coronaRd.SetPropertyBlock(mpb);
                UpdatePetals(t, dt, camPos);
            }

            // 雷
            Flash = Mathf.Max(0, Flash - dt); boltT -= dt;
            if (fSee > 0.45f && fSee < 0.74f && boltT <= 0)
            {
                boltT = 2 + Random.value * 4; Flash = 0.25f;
                float a = Random.value * Mathf.PI * 2, rad = 45 + Random.value * 25, by = playY + 50 + Random.value * 80;
                float x = Road.Cx(by) + Mathf.Cos(a) * rad, z = Road.Cz(by) + 40, yy = by;
                for (int i = 0; i < 14; i++) { bolt.SetPosition(i, new Vector3(x, yy, z)); x += (Random.value - 0.5f) * 6; z -= 5 + Random.value * 3; yy += (Random.value - 0.5f) * 4; }
                if (playing && play != null) play(sfx.thunder);
            }
            float bo = Flash > 0 ? Mathf.Min(1, Flash * 6) : 0;
            bolt.startColor = bolt.endColor = new Color(0.91f, 0.94f, 1f, bo);
            bolt.enabled = bo > 0;
            // 花火
            fwCD -= dt;
            if (fSee > 0.7f && fwCD <= 0)
            {
                fwCD = 0.7f + Random.value * 1.6f;
                var fw = fireworks.Find(w => w.life <= 0);
                if (fw != null)
                {
                    float a = Random.value * Mathf.PI * 2, rad = 24 + Random.value * 22, fy = playY + 40 + Random.value * 60;
                    var o = new Vector3(Road.Cx(fy) + Mathf.Cos(a) * rad, fy, Road.Cz(fy) + 15 + Mathf.Abs(Mathf.Sin(a)) * 15);
                    fw.life = fw.max = 1.8f + Random.value * 0.6f;
                    fw.col = Color.HSVToRGB(Random.value, 0.75f, 1f);
                    float sp = 11 + Random.value * 6;
                    for (int i = 0; i < fw.n; i++)
                    {
                        float u = Random.value * 2 - 1, th = Random.value * Mathf.PI * 2, rr = Mathf.Sqrt(1 - u * u);
                        fw.v[i] = new Vector3(rr * Mathf.Cos(th) * sp, u * sp, rr * Mathf.Sin(th) * sp);
                        fw.p[i] = o;
                    }
                    if (playing && play != null) play(sfx.firework);
                }
            }
            for (int k = 0; k < fireworks.Count; k++)
            {
                var fw = fireworks[k];
                if (fw.life <= 0) { for (int i = 0; i < fw.n; i++) fwCloud.Hide(k * 130 + i); continue; }
                fw.life -= dt;
                float a = Mathf.Clamp01(fw.life / fw.max);
                var c = new Color(fw.col.r, fw.col.g, fw.col.b, a);
                float damp = Mathf.Pow(0.97f, dt * 60f);
                for (int i = 0; i < fw.n; i++)
                {
                    fw.v[i] = new Vector3(fw.v[i].x * damp, fw.v[i].y * damp, fw.v[i].z * damp - 6 * dt);
                    fw.p[i] += fw.v[i] * dt;
                    fwCloud.Set(k * 130 + i, fw.p[i], 2.4f, c);
                }
            }
            fwCloud.Apply(new Bounds(camPos, Vector3.one * 600));
        }

        void UpdatePetals(float t, float dt, Vector3 cp)
        {
            for (int i = 0; i < petalP.Length; i++)
            {
                var p = petalP[i];
                if (!petalInit || p.y < cp.y - 4 || p.y > cp.y + 90 || Mathf.Abs(p.x - cp.x) > 30 || Mathf.Abs(p.z - cp.z) > 22)
                {
                    p = new Vector3(cp.x + R(-28, 28), cp.y + (petalInit ? R(60, 90) : R(0, 90)), cp.z + R(-20, 20));
                    petalV[i] = new Vector3(R(-1.5f, 1.5f), -R(2, 6), -R(0.5f, 2));
                }
                p += new Vector3(petalV[i].x + Mathf.Sin(t * 1.3f + i) * 1.2f, petalV[i].y, petalV[i].z) * dt;
                petalP[i] = p;
                petalCloud.Set(i, p, 0.7f, Color.white);
            }
            petalInit = true;
            petalCloud.Apply(new Bounds(cp, Vector3.one * 300));
        }
    }
}
