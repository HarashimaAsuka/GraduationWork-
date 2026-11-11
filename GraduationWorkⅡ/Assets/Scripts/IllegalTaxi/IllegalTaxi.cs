using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ShoryoumaTaxi
{
    /// <summary>
    /// 「精霊馬タクシー 地獄営業所」v42「違法タクシー」版（Web版 v42 に合わせたもの）。
    /// 新しいシーンを作り、空のオブジェクトにこれを1つだけ付ければ遊べる（TaxiGame と同じシーンに置かないこと）。
    ///
    /// ルール：
    ///   行き（きゅうりの馬）…罪人の魂を乗せて、奥の現世の門へ。警察にぶつかると魂を1人捕まえられる。
    ///   現世の門…運賃をもらい、水分も回復。そのあと通報され、ナスの牛に化ける。
    ///   帰り（ナスの牛）…大警部から手前の地獄の門まで逃げきる。牛は警察を吹っ飛ばせるが、運賃を落として少し止まる。
    ///   地獄の門で売上が目標以上ならクリア（黒字）。足りなければ赤字。
    ///   ステージ4…大警部との決戦。地獄の門に着くまでに塩で大警部を祓う。
    ///
    /// ファイル：
    ///   IllegalTaxi.cs      … 部品の用意・ステージ・毎フレームの進行
    ///   IllegalTaxi.Boss.cs … 大警部（追いかけ）と、ステージ4の決戦のしかけ
    ///   IllegalTaxi.View.cs … 毎フレームの見た目
    ///   IllegalTaxi.HUD.cs  … 画面表示（IMGUI）
    ///   ModelFactory.Police.cs … 警察・大警部などの仮モデル
    ///   TaxiAudio.cs        … BGM（Resources/BGM/sneak・chase・boss）と効果音
    /// </summary>
    public partial class IllegalTaxi : MonoBehaviour
    {
        [Header("差し替え用プレハブ（空なら仮モデル）")]
        public GameObject cucumberPrefab;
        public GameObject nasuPrefab;
        [Header("魂の色違い（班のモデル）")]
        [Tooltip("Element 0=白 1=黄 2=紫")]
        public GameObject[] soulColorModels = new GameObject[3];
        public float soulModelScale = 1.15f;
        [Header("車掌（Blender のモデル）")]
        public GameObject conductorPrefab;
        public float conductorScale = 0.7f;
        public Vector3 conductorOffset = new Vector3(0, 0.45f, 0);
        public Vector3 conductorEuler = Vector3.zero;

        // ---- 決まりごと（Web版 v42 と同じ） ----
        const float L = 700f, START = 30f, END = L - 40f;
        const float V_OUT = 16f, V_HOME = 12f, CAMF = 0.5f, CAM_BACK = 7f, CAM_UP = 1.6f, FOV_UP = 66f, DOWN_GAP = 15f;
        const int DROP_FARE = 10, AIR_N = 170, PART_N = 500, RIDER_SHOW = 12;
        static readonly int[] FARE_BY_RARE = { 10, 20, 30 };
        static readonly string[] RARE_NAME = { "罪人", "大悪党", "大罪人" };
        static readonly Color[] RARE_COL = { new Color(0.9f, 0.96f, 1f), new Color(1f, 0.82f, 0.38f), new Color(0.78f, 0.61f, 1f) };

        enum Mode { Title, Play, Pause, Result }
        Mode mode = Mode.Title;

        // ---- ステージ ----
        class StageDef
        {
            public int no, goal, bossHp; public string name; public bool tutorial, boss, lunge;
            public float vOut = V_OUT, vHome = V_HOME, bossV, bossGap, waveOut, waveBack, bossAtk, firstWave = 4; public int bossVolley = 1;
            public List<(float y, int rare)> souls = new List<(float, int)>();
            public List<(float y, string type, float x, float z, bool fixedPos)> cops = new List<(float, string, float, float, bool)>();
            public List<(float y, string kind)> items = new List<(float, string)>();
        }
        StageDef[] STAGES;
        StageDef st; int stage = 1;

        // ---- 状態 ----
        class Cop { public GameObject go; public GameObject[] siren; public string type, mode = "static", after = "static"; public float y, x, z, r, cd, ph, flash, prevRel, lead, holdT, enterT, enterDur, ex, ey, ez; public int hp; public bool dead; }
        class SoulW { public GameObject go; public float y, x, z, ph; public int rare; public bool gone; }
        class ItemW { public ModelFactory.ItemModel im; public ModelFactory.Plate tag; public string kind; public float y, x, z; public bool taken; }
        class CoinW { public GameObject go; public float y, x, z; public bool taken; }
        class ShotW { public GameObject go; public Vector3 p, v; public bool cuff; public float life; public bool dead; }
        class SaltW { public GameObject go; public float y, y0, dx, dz, vy, life; public Vector3 w0; public bool dead; }
        class Fly { public GameObject go; public float x, y, z, vx, vy, vz, life, max, spin; public bool dead; }
        class Part { public Vector3 p, v; public float life, max; public Color c; }
        struct AirP { public float x, y, z, vy; public Color c; }
        class Pop { public string text; public Vector3 w; public Color c; public float t, size; }
        class LaterJob { public float t; public System.Action fn; }
        class Tip { public string html, step; public float dur = 4, min = 2.6f, wait = 0.7f, t, expire; public bool warn, slow = true; public System.Func<bool> until; }

        float t, y, yPrev, vel, bk, px, pz = 0.4f, water, cool, inv, hurtT, shake, stepT, stun, stunK = 0.35f, wind, sirenT, phaseT, alarmT, slowT, ts = 1, moved;
        int dir = 1, fare, lost, delivered, kills, rams; bool charm, over; string phase = "out", endReason = "";
        float bossY, bossX, bossZ, bossV, bossAtkT = 3, roarT = 4, lungeT = 7, lunging, bossFlash, waveT; int waveN;
        int bossHp, bossHpMax = 1;
        readonly List<int> riders = new List<int>();
        readonly List<Cop> cops = new List<Cop>();
        readonly List<SoulW> souls = new List<SoulW>();
        readonly List<ItemW> items = new List<ItemW>();
        readonly List<CoinW> coins = new List<CoinW>();
        readonly List<ShotW> shots = new List<ShotW>();
        readonly List<SaltW> salts = new List<SaltW>();
        readonly List<Fly> flying = new List<Fly>();
        readonly List<Part> parts = new List<Part>();
        readonly List<AirP> air = new List<AirP>();
        readonly List<Pop> pops = new List<Pop>();
        readonly List<LaterJob> laters = new List<LaterJob>();
        readonly List<Tip> tipQ = new List<Tip>();
        readonly HashSet<string> taught = new HashSet<string>();
        readonly HashSet<string> flags = new HashSet<string>();
        Tip tipCur; int tut = -1; bool tutShown, tutEntered;
        string bannerMain = "", bannerSub = ""; Color bannerCol = Color.white; float bannerT;

        // ---- シーン部品 ----
        Camera cam; World world; TaxiSe se; TaxiBgm bgm; Sfx worldSfx;
        ModelFactory.Steed steed; ModelFactory.PoliceModel bossM;
        readonly GameObject[] riderGo = new GameObject[RIDER_SHOW]; readonly int[] riderRare = new int[RIDER_SHOW];
        Transform conductorRoot, conductorModel; ModelFactory.ConductorModel conductorFallback;
        GameObject gateHell, gateWorld; ModelFactory.Plate labelHell, labelWorld;
        Light horseLight, keyLight, strobeR, strobeB; GameObject aura;
        GameObject[] reticles = new GameObject[2];
        Mesh airMesh; Vector3[] airV; Color[] airC; MeshKit.QuadCloud partCloud;
        Font jpFont; Transform fx;
        float RX = 5, RZ = 3.3f; int lastW, lastH;
        float camFX, camFZ, frameDt = 1f / 60f, camRoll, lookX, lookZ, camGap = CAM_BACK;
        Vector3 camPos;

        // ============================================================
        void Start()
        {
            Road.L = L; Road.Flip = false;
            jpFont = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic UI", "Yu Gothic", "Meiryo", "Hiragino Sans", "Hiragino Kaku Gothic ProN", "Noto Sans CJK JP", "Arial Unicode MS" }, 32);
            ModelFactory.Init();
            ModelFactory.SoulModels = soulColorModels; ModelFactory.SoulModelScale = soulModelScale;
            fx = new GameObject("効果").transform;

            cam = Camera.main;
            if (cam == null) { var cg = new GameObject("Main Camera"); cg.tag = "MainCamera"; cam = cg.AddComponent<Camera>(); cg.AddComponent<AudioListener>(); }
            cam.clearFlags = CameraClearFlags.SolidColor; cam.nearClipPlane = 0.1f; cam.farClipPlane = 420f; cam.fieldOfView = FOV_UP;

            if (FindFirstObjectByType<Light>() == null)
            {
                var l = new GameObject("Directional Light").AddComponent<Light>();
                l.type = LightType.Directional; l.intensity = 0.7f; l.transform.rotation = Quaternion.Euler(60f, 30f, 0);
            }
            horseLight = MakeLight("Horse Light", LightType.Point, ModelFactory.Hex(0xffd8a0), 1.6f, 24f);
            // 見る側から当てる光：敵の顔が暗くならないように
            keyLight = MakeLight("Key Light", LightType.Directional, ModelFactory.Hex(0xfff2e6), 0.9f, 0);
            strobeR = MakeLight("Siren Red", LightType.Point, ModelFactory.Hex(0xff2030), 0, 70f);
            strobeB = MakeLight("Siren Blue", LightType.Point, ModelFactory.Hex(0x2a6aff), 0, 70f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;

            world = new World(L);
            se = new TaxiSe(gameObject); bgm = new TaxiBgm(gameObject); worldSfx = new Sfx(gameObject);   // 雷・花火の音（World が使う）
            steed = ModelFactory.BuildSteed(cucumberPrefab, nasuPrefab, null);
            steed.rider.SetActive(false); steed.spider.SetActive(false);
            aura = ModelFactory.GlowQ(fx, 0x9dffb0, 6.5f, 0.55f, Vector3.zero);
            for (int i = 0; i < RIDER_SHOW; i++)
            {
                var m = ModelFactory.Soul(null);
                m.transform.SetParent(steed.root.transform, false);
                m.transform.localPosition = new Vector3(i % 2 == 1 ? -0.2f : 0.2f, 1.0f, 0.35f - (i / 2) * 0.36f);
                m.transform.localScale = Vector3.one * 0.42f; m.SetActive(false); riderGo[i] = m;
            }
            BuildConductor();
            Renderer disc;
            gateHell = ModelFactory.DestGate(out disc); gateWorld = ModelFactory.DestGate(out disc);
            foreach (var (g, yy) in new[] { (gateHell, START), (gateWorld, END) }) { g.transform.position = Road.WP(0, yy, 0); g.transform.localScale = Vector3.one * 1.25f; }
            labelHell = ModelFactory.MakeLabel(jpFont, 7f, 1.75f, "行き先", ModelFactory.Hex(0xffd9a0), Color.white, ModelFactory.Hex(0xff7a5a), 0.42f, 0.75f); labelHell.main.text = "地獄の門";
            labelWorld = ModelFactory.MakeLabel(jpFont, 7f, 1.75f, "行き先", ModelFactory.Hex(0xffd9a0), Color.white, ModelFactory.Hex(0xff7a5a), 0.42f, 0.75f); labelWorld.main.text = "現世の門";
            bossM = ModelFactory.DaiKeibu(); bossM.root.SetActive(false);
            BuildBossFightParts();
            var vc = Mats.VertexColor();
            for (int i = 0; i < 2; i++)
            {
                var g = new GameObject("reticle"); g.transform.SetParent(fx, false);
                g.AddComponent<MeshFilter>().sharedMesh = MeshKit.CircleLines(i == 0 ? 0.46f : 0.64f, 32, Color.white);
                g.AddComponent<MeshRenderer>().sharedMaterial = vc; reticles[i] = g;
            }
            airV = new Vector3[AIR_N * 2]; airC = new Color[AIR_N * 2];
            airMesh = new Mesh(); airMesh.MarkDynamic(); airMesh.vertices = airV; airMesh.colors = airC;
            var ai = new int[AIR_N * 2]; for (int i = 0; i < ai.Length; i++) ai[i] = i;
            airMesh.SetIndices(ai, MeshTopology.Lines, 0);
            var ago = new GameObject("air"); ago.AddComponent<MeshFilter>().sharedMesh = airMesh; ago.AddComponent<MeshRenderer>().sharedMaterial = vc;
            partCloud = new MeshKit.QuadCloud(PART_N); for (int i = 0; i < PART_N; i++) partCloud.Hide(i);
            var pm = Mats.VertexColor(); pm.mainTexture = ModelFactory.GlowTex;
            var pgo = new GameObject("sparks"); pgo.AddComponent<MeshFilter>().sharedMesh = partCloud.mesh; pgo.AddComponent<MeshRenderer>().sharedMaterial = pm;

            STAGES = MakeStages();
            NewGame(1); FitField();
        }

        Light MakeLight(string name, LightType type, Color c, float intensity, float range)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.type = type; l.color = c; l.intensity = intensity; if (range > 0) l.range = range;
            return l;
        }

        void BuildConductor()
        {
            conductorRoot = new GameObject("車掌").transform;
            conductorRoot.SetParent(steed.root.transform, false);
            conductorRoot.localPosition = new Vector3(0, 0.3f, 0.72f);
            if (conductorPrefab != null)
            {
                var wrap = new GameObject("車掌のモデル").transform; wrap.SetParent(conductorRoot, false);
                var m = Instantiate(conductorPrefab, wrap);
                m.transform.localPosition = Vector3.zero; m.transform.localRotation = Quaternion.Euler(conductorEuler);
                m.transform.localScale = Vector3.one * conductorScale;
                wrap.localPosition = conductorOffset;
                foreach (var c in m.GetComponentsInChildren<Collider>()) Destroy(c);
                ModelFactory.GlowQ(wrap, 0xdff4ff, 2.2f, 0.35f, new Vector3(0, 0, -0.3f));
                conductorModel = wrap;
            }
            else conductorFallback = ModelFactory.Conductor(conductorRoot);
        }

        void FitField()
        {
            lastW = Screen.width; lastH = Screen.height;
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            float hh = Mathf.Tan(FOV_UP * Mathf.Deg2Rad / 2f) * CAM_BACK, hw = hh * aspect;
            RX = Mathf.Clamp((0.85f * hw - 0.6f) / (1 - CAMF), 2.6f, 7f);
            RZ = Mathf.Clamp((0.85f * hh - CAM_UP - 0.6f) / (1 - CAMF), 2.2f, 5.5f);
        }

        // ============================================================
        //  ステージの配置
        // ============================================================
        StageDef[] MakeStages()
        {
            var s1 = new StageDef { no = 1, name = "研修", goal = 20, tutorial = true, vOut = 13, vHome = 12, bossV = 9.5f, bossGap = 55, waveBack = 7, firstWave = 6 };
            foreach (var (yy, r) in new[] { (80f, 0), (105f, 0), (135f, 0), (170f, 1), (205f, 0), (250f, 0), (290f, 0) }) s1.souls.Add((yy, r));
            s1.cops.Add((420, "junsa", -2.2f, 0, true)); s1.cops.Add((470, "patochin", 0, 0, false)); s1.cops.Add((540, "junsa", 2, 0, true));
            s1.cops.Add((600, "junsa", 1.5f, 0, true)); s1.cops.Add((630, "patochin", 0, 0, false));
            s1.items.Add((330, "water")); s1.items.Add((360, "wind")); s1.items.Add((390, "charm"));

            var s2 = Gen(2, 13, 26, new[] { (150f, "water"), (230f, "charm"), (300f, "wind"), (380f, "water"), (470f, "charm"), (560f, "wind"), (610f, "water") }, 0.55f, 0.3f);
            s2.name = "一日目"; s2.goal = 60; s2.bossV = 12.6f; s2.bossGap = 68; s2.waveOut = 6.5f; s2.waveBack = 4f; s2.bossAtk = 5.5f; s2.bossVolley = 1; s2.firstWave = 3;
            var s3 = Gen(3, 15, 36, new[] { (130f, "water"), (210f, "charm"), (260f, "wind"), (340f, "water"), (420f, "charm"), (480f, "wind"), (550f, "water"), (620f, "wind") }, 0.4f, 0.35f);
            s3.name = "二日目"; s3.goal = 90; s3.bossV = 13f; s3.bossGap = 80; s3.lunge = true; s3.waveOut = 4.2f; s3.waveBack = 2.5f; s3.bossAtk = 3.6f; s3.bossVolley = 3; s3.firstWave = 2;
            var s4 = new StageDef { no = 4, name = "大警部との決戦", boss = true, bossHp = 300, vHome = 7 };
            foreach (var (yy, k) in new[] { (590f, "water"), (520f, "charm"), (440f, "water"), (370f, "water"), (300f, "charm"), (230f, "water"), (160f, "water"), (100f, "water") }) s4.items.Add((yy, k));
            return new[] { s1, s2, s3, s4 };
        }
        StageDef Gen(int no, int nSouls, int nCops, (float, string)[] its, float w0, float w1)
        {
            var s = new StageDef { no = no };
            var rng = new System.Random(no * 977 + 13);
            float Rn() => (float)rng.NextDouble();
            const float from = 70;
            for (int i = 0; i < nSouls; i++)
            {
                float yy = from + (END - 70 - from) * (i + 0.5f) / nSouls + (Rn() - 0.5f) * 14, r = Rn();
                s.souls.Add((Mathf.Round(yy), r < w0 ? 0 : r < w0 + w1 ? 1 : 2));
            }
            string[] types = { "junsa", "junsa", "junsa", "patochin", "patochin" };
            float yc = from + 30;
            for (int i = 0; i < nCops; i++) { yc += (END - 40 - from - 30) / nCops; s.cops.Add((Mathf.Round(yc + (Rn() - 0.5f) * 6), types[rng.Next(types.Length)], 0, 0, false)); }
            foreach (var it in its) s.items.Add(it);
            return s;
        }

        // ============================================================
        //  ゲームの開始・終わり
        // ============================================================
        void ClearGame()
        {
            foreach (var c in cops) Destroy(c.go); cops.Clear();
            foreach (var s in souls) Destroy(s.go); souls.Clear();
            foreach (var it in items) { Destroy(it.im.root); Destroy(it.tag.root); } items.Clear();
            foreach (var c in coins) Destroy(c.go); coins.Clear();
            foreach (var s in shots) Destroy(s.go); shots.Clear();
            foreach (var s in salts) Destroy(s.go); salts.Clear();
            foreach (var f in flying) Destroy(f.go); flying.Clear();
            ClearBossFight();
            riders.Clear(); parts.Clear(); air.Clear(); pops.Clear(); laters.Clear(); tipQ.Clear(); taught.Clear(); flags.Clear(); tipCur = null;
        }

        Cop MakeCop(float yy, string type, float x, float z)
        {
            var pm = type == "junsa" ? ModelFactory.Junsa() : ModelFactory.Patochin();
            pm.root.transform.SetParent(fx, false);
            return new Cop { go = pm.root, siren = pm.siren, type = type, y = yy, x = x, z = z, hp = type == "junsa" ? 2 : 3, r = type == "junsa" ? 1f : 1.1f,
                cd = R(1.2f, 2.8f), ph = R(0, 6), prevRel = yy - y };
        }

        void NewGame(int no)
        {
            ClearGame();
            stage = Mathf.Clamp(no, 1, 4); st = STAGES[stage - 1];
            Road.Flip = false;
            world.SetStage(st.boss ? 3 : 1);   // 決戦は赤い月の夜
            var rng = new System.Random(no * 31 + 7);
            float RXp() => ((float)rng.NextDouble() * 2 - 1) * 3.2f;
            float RZp() => ((float)rng.NextDouble() * 2 - 1) * 2.4f;
            bool bs = st.boss;
            t = 0; y = yPrev = bs ? END - 4 : START + 6; vel = 0; dir = bs ? -1 : 1; phase = bs ? "back" : "out"; phaseT = 0; bk = bs ? 1 : 0; px = 0; pz = 0.4f;
            water = 100; fare = lost = delivered = kills = rams = 0; cool = inv = hurtT = shake = stun = wind = sirenT = alarmT = slowT = moved = 0; ts = 1; charm = false; over = false; endReason = "";
            bossHp = st.bossHp; bossHpMax = Mathf.Max(1, st.bossHp); bossAtkT = 3; roarT = 4; lungeT = 7; lunging = 0; bossFlash = 0; waveT = st.firstWave; waveN = 0;
            tut = st.tutorial ? 0 : -1; tutShown = tutEntered = false;
            foreach (var (yy, rare) in st.souls)
            {
                var g = ModelFactory.Soul(null, rare); g.transform.SetParent(fx, false);
                souls.Add(new SoulW { go = g, y = yy, x = RXp(), z = RZp(), rare = rare, ph = (float)rng.NextDouble() * 6 });
            }
            foreach (var c in st.cops) cops.Add(MakeCop(c.y, c.type, c.fixedPos ? c.x : RXp(), c.fixedPos ? c.z : RZp()));
            foreach (var (yy, kind) in st.items)
            {
                var im = ModelFactory.Item(kind); im.root.transform.SetParent(fx, false);
                var tag = ModelFactory.MakeLabel(jpFont, 4.2f, 1.45f, ItemName(kind), ItemCol(kind), Color.white, ItemCol(kind), 0.5f, 0.42f);
                tag.main.text = ItemEffect(kind);
                items.Add(new ItemW { im = im, tag = tag, kind = kind, y = yy, x = RXp(), z = RZp() });
            }
            bossM.root.SetActive(false);
            if (bs) { NewBossFight(); bossY = y + B.relY; bossX = 0; bossZ = 3.2f; bossM.root.SetActive(true); }
            for (int i = 0; i < RIDER_SHOW; i++) riderGo[i].SetActive(false);
            bannerT = 0;
        }

        static string ItemName(string k) => k == "water" ? "お供えの水" : k == "wind" ? "追い風" : "厄除けのお守り";
        static string ItemEffect(string k) => k == "water" ? "水分 +30" : k == "wind" ? "4秒間、速くなる" : "次の1回、ぶつかっても平気";
        static Color ItemCol(string k) => ModelFactory.Hex(k == "water" ? 0x6cc4e8 : k == "wind" ? 0x9be38a : 0xff8a8a);

        void StartStage(int no)
        {
            NewGame(no); mode = Mode.Play; cool = 0.4f;
            bgm.Play(st.boss ? "boss" : "sneak");
            if (st.boss) { Banner("大警部を祓え", "ステージ4　地獄の門に着くまでに倒す", ModelFactory.Hex(0xff8a7a), 3.2f); sirenT = 2; se.Play(se.siren, 0.7f); Later(1.2f, () => se.Play(se.roar)); }
            else Banner($"目標 {st.goal}文", $"ステージ{st.no}　{st.name}", ModelFactory.Hex(0xffd060), 3.2f);
        }
        void ToTitle() { bgm.Stop(); NewGame(1); mode = Mode.Title; }
        void Pause() { mode = Mode.Pause; bgm.Pause(true); }
        void Resume() { mode = Mode.Play; cool = Mathf.Max(cool, 0.35f); bgm.Pause(false); }

        string resultTitle, resultText; bool resultOk;
        void Finish(string reason)
        {
            if (mode != Mode.Play) return;
            mode = Mode.Result; bgm.Stop(); tipCur = null;
            resultOk = reason == "clear" || reason == "bossWin";
            switch (reason)
            {
                case "clear": resultTitle = "黒字！"; resultText = $"地獄の門に逃げ帰った。今夜の売上は {fare}文。大警部は門の前で地団駄を踏んでいる。"; break;
                case "red": resultTitle = "赤字……"; resultText = $"逃げきったが、売上は {fare}文。目標の {st.goal}文に届かなかった。"; break;
                case "caught": resultTitle = "御用だ！"; resultText = "大警部に追いつかれ、精霊馬タクシーは差し押さえられた。"; break;
                case "dry": resultTitle = "干からびた"; resultText = "塩を撒きすぎて、精霊馬が干からびてしまった。"; break;
                case "gate": resultTitle = "門が開かない！"; resultText = "大警部を祓えないまま、地獄の門に着いてしまった。"; break;
                default: resultTitle = "大警部を祓った！"; resultText = "お盆の夜の取り締まりは終わった。精霊馬タクシーは、今日も無許可で営業中。"; break;
            }
            se.Play(resultOk ? se.win : se.bell);
        }

        // ============================================================
        //  毎フレーム
        // ============================================================
        void Update()
        {
            frameDt = Mathf.Min(Time.deltaTime, 0.033f);
            if (Screen.width != lastW || Screen.height != lastH) FitField();
            if (GameInput.MutePressed()) bgm.Toggle();
            if (GameInput.PausePressed()) { if (mode == Mode.Play) Pause(); else if (mode == Mode.Pause) Resume(); }
            if (mode == Mode.Play) Step(frameDt);
            else if (mode == Mode.Title) Idle(frameDt);
            bgm.Tick(Time.unscaledDeltaTime);
        }

        void Idle(float dt)
        {
            t += dt; yPrev = y; vel = 8; y += 8 * dt; if (y > END - 20) y = START + 20;
            px = Mathf.Sin(t * 0.4f) * 1.5f; pz = 0.5f + Mathf.Sin(t * 0.6f) * 0.6f;
            UpdateAir(dt); UpdateParts(dt);
        }

        void Step(float rdt)
        {
            slowT = Mathf.Max(0, slowT - rdt);
            ts += ((slowT > 0 ? 0.55f : 1f) - ts) * (1 - Mathf.Exp(-8 * rdt));
            float dt = rdt * ts;
            t += dt; phaseT += dt;
            inv = Mathf.Max(0, inv - dt); hurtT = Mathf.Max(0, hurtT - dt); shake = Mathf.Max(0, shake - dt);
            cool -= dt; wind = Mathf.Max(0, wind - dt); stun = Mathf.Max(0, stun - dt); sirenT = Mathf.Max(0, sirenT - dt); bossFlash = Mathf.Max(0, bossFlash - dt);
            if (bannerT > 0) bannerT -= rdt;
            TickTips(rdt);
            UpdateParts(dt); UpdateFlying(dt);
            foreach (var p in pops) p.t += rdt; pops.RemoveAll(p => p.t > 1.1f);
            for (int i = 0; i < laters.Count; i++) { var lt = laters[i]; lt.t -= dt; if (lt.t <= 0) { laters.RemoveAt(i--); lt.fn(); if (mode != Mode.Play) return; } }
            if (over) { UpdateAir(dt); return; }

            // 研修の手順
            if (tut >= 0 && tut < 5 && phase == "out") TutorialStep();

            // 進む速さ
            if (phase == "alarm")
            {
                alarmT += dt; vel *= Mathf.Exp(-6 * dt);
                bk = Mathf.Clamp01((alarmT - 1.6f) / 0.8f);
                if (alarmT > 1.7f && flags.Add("puffed")) { se.Play(se.puff); Burst(px, y, pz, 0xffffff, 70, 4); }
                if (alarmT > 2.8f) StartEscape();
            }
            else
            {
                bool outP = phase == "out";
                float load = outP ? Mathf.Max(0.45f, 1 - 0.06f * riders.Count) : 1, boost = wind > 0 ? 2.6f : 1, stn = stun > 0 ? stunK : 1;
                float target = (outP ? st.vOut : -st.vHome) * load * boost * stn;
                vel += Mathf.Clamp(target - vel, -40 * dt, 40 * dt);
            }
            yPrev = y; y = Mathf.Clamp(y + vel * dt, START - 2, END + 2);

            // 横の動き（WASD）：画面の右・上に合わせる
            Vector2 mv = MoveWASD();
            float sp = phase == "out" ? 6.5f : 6f;
            Vector3 cr = cam.transform.right, cu = cam.transform.up;
            var rgt = new Vector2(cr.x, cr.z).normalized; var upv = new Vector2(cu.x, cu.z).normalized;
            px += (rgt.x * mv.x + upv.x * mv.y) * sp * dt; pz += (rgt.y * mv.x + upv.y * mv.y) * sp * dt;
            if (mv != Vector2.zero) moved += dt;
            float dd = Mathf.Sqrt((px / RX) * (px / RX) + (pz / RZ) * (pz / RZ)); if (dd > 1) { px /= dd; pz /= dd; }

            // 塩：道の向きにまっすぐ飛ぶ
            if (GameInput.Fire() && cool <= 0 && phase != "alarm")
            {
                AimLine(out var w0, out var ddx, out var ddz);
                var g = ModelFactory.Prim(PrimitiveType.Cube, fx, Vector3.zero, Vector3.one * 0.28f, ModelFactory.SaltMat);
                ModelFactory.GlowQ(g.transform, 0xbfe8ff, 3f, 0.8f, Vector3.zero);
                salts.Add(new SaltW { go = g, y0 = y + 0.8f, y = y + 0.8f, w0 = w0 + new Vector3(ddx * 0.8f, 0, ddz * 0.8f), dx = ddx, dz = ddz, vy = 48 + Mathf.Max(0, vel) });
                water = Mathf.Max(0, water - (st.boss ? 0.12f : 0.25f)); cool = 0.14f; se.Play(se.salt, 0.7f);
            }
            foreach (var s in salts)
            {
                s.y += s.vy * dt; s.life += dt;
                foreach (var c in cops)
                {
                    if (c.dead || c.mode == "enter" || !SaltHits(s, c.y, c.x, c.z, c.r)) continue;
                    s.dead = true; c.hp--; c.flash = 0.12f; se.Play(se.hit, 0.8f); Burst(c.x, c.y - 0.5f, c.z, 0xffffff, 6, 3);
                    if (c.hp <= 0) KillCop(c, true);
                    break;
                }
                if (!s.dead && st.boss) SaltBossFight(s);
                else if (!s.dead && bossM.root.activeSelf && phase == "back" && SaltHits(s, bossY, bossX, bossZ, 4))
                {
                    s.dead = true; bossFlash = 0.08f; Burst(bossX, bossY - 2, bossZ, 0xffffff, 6, 4); se.Play(se.bossHit);
                    bossY = Mathf.Min(bossY + 0.45f, y + st.bossGap + 10);
                }
                if (s.life > 1.6f) s.dead = true;
                if (s.dead) Destroy(s.go);
            }
            salts.RemoveAll(s => s.dead);

            // 魂を乗せる（行きだけ）
            if (phase == "out")
                foreach (var s in souls)
                {
                    if (s.gone) continue;
                    bool crossed = (s.y - yPrev) * (s.y - y) <= 0 || Mathf.Abs(s.y - y) < 1.2f;
                    if (crossed && Hyp(s.x - px, s.z - pz) < 1.7f) Board(s);
                }

            // 警察
            Waves(dt);
            float atkK = st.no == 1 ? 1.5f : st.no == 2 ? 1f : 0.75f;
            foreach (var c in cops)
            {
                if (c.dead) continue;
                c.flash = Mathf.Max(0, c.flash - dt);
                if (c.mode == "enter")
                {
                    c.enterT += dt;
                    if (c.after == "cut") c.y = y - c.lead;
                    if (c.enterT >= c.enterDur) { c.mode = c.after; c.prevRel = c.y - y; }
                    if (Random.value < 0.6f) { float k = 1 - EaseOut(Mathf.Clamp01(c.enterT / c.enterDur)); Burst(c.x + c.ex * k, c.y + c.ey * k, c.z + c.ez * k, 0xff6a50, 1, 1); }
                    continue;
                }
                if (c.mode == "cut") { c.y = y - c.lead; c.holdT -= dt; if (c.holdT <= 0) c.mode = "static"; }
                if (c.mode == "chase") c.y += (vel - 9) * dt;   // 後ろからまっすぐ迫る
                float rel = c.y - y;
                float camY = cam.transform.position.y;
                if (phase != "alarm" && c.y > camY + 4 && Mathf.Abs(rel) > 5 && Mathf.Abs(rel) < 52 && (phase == "back" || rel > 0))
                {
                    c.cd -= dt;
                    if (c.cd <= 0)
                    {
                        if (c.type == "junsa") { c.cd = 2.6f * atkK; FireAt(c.x, c.y - 0.4f * Mathf.Sign(rel), c.z, false, 0, 0, 20); }
                        else { c.cd = 3.4f * atkK; foreach (float o in new[] { -1.8f, 0f, 1.8f }) FireAt(c.x, c.y - 0.4f * Mathf.Sign(rel), c.z, false, o, 0, 17); }
                        c.flash = 0.15f; se.Play(se.shoot, 0.6f);
                    }
                }
                bool crossed = c.prevRel * rel <= 0 || Mathf.Abs(rel) < 0.5f;
                c.prevRel = rel;
                if (crossed && inv <= 0 && Hyp(c.x - px, c.z - pz) < c.r + 0.6f) { if (phase == "out") Caught(c); else if (phase == "back") Ram(c); }
                if (c.y < cam.transform.position.y - 6 && c.mode != "static") { c.dead = true; Destroy(c.go); }
            }
            cops.RemoveAll(c => c.dead);

            // 敵の弾：撃った瞬間にねらいを決めて、あとはまっすぐ
            var pw = Road.WP(px, y, pz);
            foreach (var b in shots)
            {
                b.life += dt; b.p += b.v * dt;
                float r = b.cuff ? 1.2f : 0.95f;
                if (inv <= 0 && Mathf.Abs(b.p.y - y) < 0.9f && Hyp(b.p.x - pw.x, b.p.z - pw.z) < r)
                {
                    b.dead = true;
                    if (!UseCharm()) { if (b.cuff) { Hurt(8); stun = 0.5f; se.Play(se.cuffs); } else Hurt(6); }
                }
                if (b.life > 4 || b.p.y < cam.transform.position.y - 2) b.dead = true;
                if (b.dead) Destroy(b.go);
            }
            shots.RemoveAll(b => b.dead);

            // アイテム・小銭
            foreach (var it in items)
            {
                if (it.taken) continue;
                if ((it.y - yPrev) * (it.y - y) <= 0 && Hyp(it.x - px, it.z - pz) < 1.8f) TakeItem(it);
            }
            foreach (var cn in coins)
            {
                if (cn.taken) continue;
                if ((cn.y - yPrev) * (cn.y - y) <= 0 && Hyp(cn.x - px, cn.z - pz) < 1.8f)
                { cn.taken = true; Destroy(cn.go); fare += 5; se.Play(se.coins); Burst(cn.x, cn.y, cn.z, 0xffd060, 18, 5); PopAt(cn.x, cn.y, cn.z, "小銭 +5文", ModelFactory.Hex(0xffd060), 26); }
            }

            if (phase == "out" && y >= END) Arrive();
            if (phase == "back") { if (st.boss) BossFightUpdate(dt); else BossChase(dt); }
            if (over) return;
            if (water <= 0) { over = true; endReason = "dry"; Banner("干からびた……", "", ModelFactory.Hex(0xff8a7a), 2); Later(1.4f, () => Finish("dry")); return; }
            UpdateAir(dt);
        }

        static Vector2 MoveWASD()
        {
            // 矢印キーは使わない（WASD だけ）
            Vector2 v = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            var k = UnityEngine.InputSystem.Keyboard.current;
            if (k != null) { if (k.aKey.isPressed) v.x -= 1; if (k.dKey.isPressed) v.x += 1; if (k.wKey.isPressed) v.y += 1; if (k.sKey.isPressed) v.y -= 1; }
#else
            if (Input.GetKey(KeyCode.A)) v.x -= 1; if (Input.GetKey(KeyCode.D)) v.x += 1; if (Input.GetKey(KeyCode.W)) v.y += 1; if (Input.GetKey(KeyCode.S)) v.y -= 1;
#endif
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }

        // ============================================================
        //  できごと
        // ============================================================
        void Hurt(int w) { water = Mathf.Max(0, water - w); hurtT = 0.5f; shake = 0.35f; inv = 1.1f; se.Play(se.hurt); PopAt(px, y, pz, $"水分 −{w}", ModelFactory.Hex(0x7ad0ff), 24); }
        bool UseCharm()
        {
            if (!charm) return false;
            charm = false; inv = 1.2f; Burst(px, y, pz, 0xff8a8a, 30, 7); se.Play(se.charm);
            PopAt(px, y, pz, "お守りが守ってくれた！", ModelFactory.Hex(0xff9a9a), 24); return true;
        }
        void Board(SoulW s)
        {
            s.gone = true; s.go.SetActive(false); riders.Add(s.rare); se.Play(se.board);
            Burst(s.x, s.y, s.z, RARE_COL[s.rare], 18, 5);
            PopAt(s.x, s.y, s.z, $"{RARE_NAME[s.rare]} +{FARE_BY_RARE[s.rare]}文", RARE_COL[s.rare], 24);
        }
        void Caught(Cop c)
        {
            if (UseCharm()) { KillCop(c, false); return; }
            Hurt(8); se.Play(se.whistle); Later(0.15f, () => se.Play(se.cuffs));
            if (riders.Count > 0)
            {
                int r = riders[riders.Count - 1]; riders.RemoveAt(riders.Count - 1);
                var g = ModelFactory.Soul(null, r); g.transform.localScale = Vector3.one * 0.8f;
                AddFly(g, px, y + 0.5f, pz, R(-4, 4), 6, R(2, 4), 1.4f, 0);
                c.dead = true; AddFly(c.go, c.x, c.y, c.z, R(-4, 4), 6, R(2, 4), 1.4f, 0);
                PopAt(px, y + 1, pz, $"{RARE_NAME[r]}を捕まえられた！", ModelFactory.Hex(0xff8a7a), 24);
            }
            else KillCop(c, false);
        }
        void Ram(Cop c)
        {
            rams++;
            float away = c.x >= px ? 1 : -1;
            c.dead = true;
            AddFly(c.go, c.x, c.y, c.z, away * R(14, 20), R(4, 8), R(6, 10), 1.2f, R(8, 14));
            Burst(c.x, c.y, c.z, 0xffffff, 26, 9); shake = 0.5f; se.Play(se.ram);
            PopAt(c.x, c.y, c.z, "吹っ飛ばした！", Color.white, 30);
            if (UseCharm()) return;
            stun = 0.45f; inv = 0.6f;
            int d = Mathf.Min(fare, DROP_FARE);
            if (d > 0)
            {
                fare -= d; lost += d; se.Play(se.coins);
                Burst(px, y, pz, 0xffd060, 20, 8);
                Later(0.25f, () => PopAt(px, y, pz, $"運賃 −{d}文", ModelFactory.Hex(0xffb070), 30));
            }
        }
        void KillCop(Cop c, bool bySalt)
        {
            c.dead = true; Destroy(c.go);
            Burst(c.x, c.y, c.z, 0xfff2c0, 30, 7); Burst(c.x, c.y, c.z, 0xff5040, 12, 4);
            if (bySalt) { kills++; se.Play(se.kill); }
        }
        void TakeItem(ItemW it)
        {
            it.taken = true; it.im.root.SetActive(false); it.tag.root.SetActive(false);
            Burst(it.x, it.y, it.z, ItemCol(it.kind), 26, 6);
            if (it.kind == "water") { water = Mathf.Min(100, water + 30); se.Play(se.water); }
            else if (it.kind == "wind") { wind = 4; se.Play(se.wind); }
            else { charm = true; se.Play(se.charm); }
            PopAt(it.x, it.y, it.z, ItemName(it.kind) + "：" + ItemEffect(it.kind), ItemCol(it.kind), 26);
        }
        // 現世の門：魂を降ろして運賃をもらい、水分も回復 → 通報 → ナスの牛に化けて逃げる
        void Arrive()
        {
            phase = "alarm"; alarmT = 0;
            int sum = 0;
            for (int i = 0; i < riders.Count; i++)
            {
                sum += FARE_BY_RARE[riders[i]];
                var g = ModelFactory.Soul(null, riders[i]); g.transform.localScale = Vector3.one * 0.7f;
                AddFly(g, px + R(-1, 1), y + 1, pz, R(-2, 2), 9 + i * 0.6f, R(-1, 1), 1.6f, 0);
            }
            delivered = riders.Count; fare += sum; riders.Clear();
            bool healed = water < 99.5f; water = 100;
            se.Play(se.orin); se.Play(se.coins); se.Play(se.water);
            Banner($"運賃 ＋{sum}文", $"罪人の魂を {delivered}人、現世へ送り届けた{(healed ? "　水分も回復" : "")}", ModelFactory.Hex(0xffd060), 1.6f);
            foreach (var s in souls) if (!s.gone) { s.gone = true; s.go.SetActive(false); }
            tipCur = null; tipQ.Clear();
            Later(1.5f, () =>
            {
                if (phase != "alarm") return;
                se.Play(se.siren, 0.7f); se.Play(se.whistle); sirenT = 3;
                Banner("通報された！", "大警部が来る。地獄の門まで逃げろ！", ModelFactory.Hex(0xff6a5a), 2.6f);
                bossY = y + st.bossGap; bossX = px; bossZ = pz; bossM.root.SetActive(true);
                bgm.Play("chase");
            });
        }
        void StartEscape()
        {
            phase = "back"; dir = -1; phaseT = 0;
            se.Play(se.puff); Burst(px, y, pz, 0xffffff, 60, 5);
            Later(0.3f, () => se.Play(se.roar));
            Say("通報された！ 大警部は倒せない。<b>地獄の門まで逃げきれ</b>。塩を当てると少しひるむ", "帰り", 4.2f, 0.2f);
            // 救済：売上が目標に足りないときは、帰り道に落とし物の小銭が出る（1枚5文）
            int shortF = st.goal - fare;
            if (shortF > 0)
            {
                int n = Mathf.Clamp(Mathf.CeilToInt(shortF / 5f) + 2, 4, 14);
                for (int i = 0; i < n; i++)
                {
                    float yy = Mathf.Lerp(y - 60, START + 30, (i + 0.5f) / n) + R(-6, 6);
                    var g = ModelFactory.Coin(); g.transform.SetParent(fx, false);
                    coins.Add(new CoinW { go = g, y = yy, x = R(-3, 3), z = R(-2.2f, 2.2f) });
                }
            }
        }

        // ---- 警察の湧き方 ----
        void EnterFrom(Cop c, int side)
        {
            c.mode = "enter"; c.enterT = 0; c.enterDur = 1.1f;
            c.ex = side * R(22, 28); c.ez = R(-4, 10); c.ey = R(6, 12);
            se.Play(se.swoosh); Later(0.2f, () => se.Play(se.whistle, 0.6f));
        }
        void SpawnSide()   // 行き：前方の道へ、脇の景色から飛び込む
        {
            var c = MakeCop(y + R(42, 58), Random.value < 0.55f ? "junsa" : "patochin", R(-2.6f, 2.6f), R(-2, 2)); c.after = "static";
            EnterFrom(c, Random.value < 0.5f ? -1 : 1); cops.Add(c);
        }
        void SpawnCut()    // 帰り：前（手前）に回り込み、しばらく並走しながら撃つ
        {
            var c = MakeCop(y - 10, Random.value < 0.6f ? "junsa" : "patochin", R(-2.6f, 2.6f), R(-2, 2));
            c.after = "cut"; c.lead = 10; c.holdT = st.no >= 3 ? 1.2f : 1.6f;
            EnterFrom(c, Random.value < 0.5f ? -1 : 1); cops.Add(c);
            Later(1.0f, () => Teach("cut", "前の警察は<b>牛の体当たりで吹っ飛ばせる</b>。ただし<b>運賃を落として</b>、少し足が止まる", "帰り", 4, 0));
        }
        void SpawnChase()  // 帰り：後ろ（奥）から、まっすぐ迫ってくる（脇から飛び込まない）
        {
            float yy = Mathf.Min(y + 60, bossY - 4);
            var c = MakeCop(yy, Random.value < 0.5f ? "junsa" : "patochin", Mathf.Clamp(px + R(-1.5f, 1.5f), -3, 3), Mathf.Clamp(pz + R(-1.2f, 1.2f), -2.4f, 2.4f));
            c.mode = "chase"; cops.Add(c);
        }
        void Waves(float dt)
        {
            float every = phase == "out" ? st.waveOut : st.waveBack;
            if (every <= 0 || over) return;
            if (phase == "out" && y > END - 70) return;
            if (phase == "back" && (y < START + 50 || phaseT < 1.5f)) return;
            waveT -= dt; if (waveT > 0) return;
            waveT = every * R(0.8f, 1.2f);
            if (phase == "out") { SpawnSide(); if (st.no >= 3 && Random.value < 0.35f) SpawnSide(); return; }
            if (phase != "back") return;
            waveN++;
            if (st.tutorial) { if (waveN % 2 == 1) SpawnCut(); else SpawnChase(); return; }
            if (Random.value < 0.45f) SpawnCut(); else SpawnChase();
            if (st.no >= 3 && Random.value < 0.3f) { if (Random.value < 0.5f) SpawnCut(); else SpawnChase(); }
        }

        void FireAt(float x, float yy, float z, bool cuff, float offX, float offZ, float speed)
        {
            float rel = yy - y, tt = Mathf.Max(0.6f, Mathf.Abs(rel) / speed);
            float ty = y + vel * tt;
            var sP = Road.WP(x, yy, z); var tP = Road.WP(px + offX, ty, pz + offZ);
            var go = cuff ? ModelFactory.Cuffs() : ModelFactory.Pellet(); go.transform.SetParent(fx, false);
            shots.Add(new ShotW { go = go, p = sP, v = (tP - sP) / tt, cuff = cuff });
        }

        // ---- 塩のねらい（道の向きにまっすぐ） ----
        void AimLine(out Vector3 w0, out float ddx, out float ddz)
        {
            w0 = Road.WP(px, y, pz);
            ddx = (Road.Cx(y + 1) - Road.Cx(y - 1)) / 2f; ddz = (Road.Cz(y + 1) - Road.Cz(y - 1)) / 2f;
        }
        Vector3 SaltPos(SaltW s) => new Vector3(s.w0.x + s.dx * (s.y - s.y0), s.y, s.w0.z + s.dz * (s.y - s.y0));
        bool SaltHits(SaltW s, float ty, float tx, float tz, float r)
        {
            if (Mathf.Abs(ty - s.y) > 1.6f) return false;
            var p = SaltPos(s); var q = Road.WP(tx, ty, tz);
            return Hyp(p.x - q.x, p.z - q.z) < r + 0.35f;
        }
        bool InLine(float ty, float tx, float tz, float r)
        {
            float up = ty - y; if (up < 1 || up > 55) return false;
            AimLine(out var w0, out var ddx, out var ddz); var q = Road.WP(tx, ty, tz);
            return Hyp(w0.x + ddx * up - q.x, w0.z + ddz * up - q.z) < r + 0.3f;
        }

        // ---- 研修 ----
        bool ItemSeen() { foreach (var it in items) if (!it.taken && it.y - y > 8 && it.y - y < 40) return true; return false; }
        bool CopSeen() { foreach (var c in cops) if (!c.dead && c.mode != "enter" && c.y - y > 10 && c.y - y < 45) return true; return false; }
        System.Func<bool>[] tutUntil;   // 同じ条件を使い回す（説明の重なりを見分けるため）
        void TutorialStep()
        {
            if (tutUntil == null) tutUntil = new System.Func<bool>[] {
                () => riders.Count >= 1, () => riders.Count >= 3 || y > 300, () => !ItemSeen(), () => kills >= 1 || !CopSeen(), () => phase != "out" };
            var until = tutUntil;
            string[] steps = {
                "<b>W A S D</b> で動く。光る玉が<b>罪人の魂</b>だ。<b>触れて乗せろ</b>",
                "魂は何人でも乗る。運賃は白10・黄20・紫30文。<b>乗せるほど遅くなる</b>",
                "アイテムだ。<b>水</b>＝水分回復　<b>追い風</b>＝加速　<b>お守り</b>＝1回守る",
                "巡査だ！ <b>左クリック</b>か <b>Z</b> で塩。<b>白い輪</b>が塩の飛ぶ先だ。ぶつかると<b>魂を1人</b>、弾に当たると<b>水分</b>を取られる",
                "奥の<b>現世の門</b>まで運べば運賃がもらえる。着けば水分も回復する" };
            if (!tutShown)
            {
                if (tut == 2 && y > 400) { tut++; return; }
                if (tut == 3 && !tutEntered) { tutEntered = true; if (!CopSeen()) { var c = MakeCop(y + 34, "junsa", px, pz); c.after = "static"; EnterFrom(c, 1); cops.Add(c); } }
                bool when = tut == 2 ? ItemSeen() : tut == 3 ? CopSeen() : true;
                if (when) { tutShown = true; tipQ.Add(new Tip { html = steps[tut], step = $"研修 {tut + 1}/5", until = until[tut], min = 2.8f, expire = 1e9f }); }
            }
            else if (until[tut]() && (tipCur == null || tipCur.until != until[tut]))
            {
                var u = until[tut]; tipQ.RemoveAll(q => q.until == u);
                tutShown = false; tutEntered = false; tut++;
            }
        }
        // 下の説明は研修（ステージ1）だけ。同じ説明は二度出さない
        void Say(string html, string step = "", float dur = 4f, float wait = 0.7f)
        {
            if (!st.tutorial) return;
            tipQ.Add(new Tip { html = html, step = step, dur = dur, wait = wait, expire = t + 8 });
        }
        void Teach(string key, string html, string step = "", float dur = 4f, float wait = 0.7f) { if (!taught.Add(key)) return; Say(html, step, dur, wait); }
        void TickTips(float rdt)
        {
            if (tipCur != null)
            {
                tipCur.t += rdt;
                bool done = tipCur.until != null ? (tipCur.t > tipCur.min && tipCur.until()) : tipCur.t > tipCur.dur;
                if (done) tipCur = null;
            }
            if (tipCur == null && tipQ.Count > 0)
            {
                var q = tipQ[0];
                if (q.expire < t) { tipQ.RemoveAt(0); return; }
                q.wait -= rdt; if (q.wait > 0) return;
                tipQ.RemoveAt(0); tipCur = q; tipCur.t = 0;
                if (tipCur.slow) slowT = 1.0f;
            }
        }

        // ============================================================
        //  道具
        // ============================================================
        void Banner(string main, string sub, Color c, float dur) { bannerMain = main; bannerSub = sub; bannerCol = c; bannerT = dur; }
        void Later(float sec, System.Action fn) => laters.Add(new LaterJob { t = sec, fn = fn });
        void PopAt(float x, float yy, float z, string text, Color c, float size) => pops.Add(new Pop { text = text, w = Road.WP(x, yy, z), c = c, size = size });
        void AddFly(GameObject g, float x, float yy, float z, float vx, float vy, float vz, float life, float spin)
        { if (g.transform.parent == null) g.transform.SetParent(fx, false); flying.Add(new Fly { go = g, x = x, y = yy, z = z, vx = vx, vy = vy, vz = vz, life = life, max = life, spin = spin }); }
        void UpdateFlying(float dt)
        {
            foreach (var f in flying)
            {
                f.x += f.vx * dt; f.y += f.vy * dt; f.z += f.vz * dt; f.vz -= 6 * dt; f.life -= dt;
                if (f.life <= 0) { Destroy(f.go); f.dead = true; }
            }
            flying.RemoveAll(f => f.dead);
        }
        void UpdateParts(float dt)
        {
            foreach (var p in parts) { p.p += p.v * dt; p.v *= 0.96f; p.life -= dt; }
            parts.RemoveAll(p => p.life <= 0);
        }
        void UpdateAir(float dt)
        {
            bool back = dir < 0, rain = stage == 3; float camY = cam.transform.position.y, f = Road.Zf(y); bool first = air.Count == 0;
            int want = rain ? 150 : back ? 130 : 70;
            while (air.Count < Mathf.Min(want, AIR_N))
            {
                float a = R(0, Mathf.PI * 2), r = R(4, 15);
                float yy = back ? camY - 4 + (first ? R(0, 70) : R(-2, 2)) : y + R(first ? -5 : 40, 75);
                var c = rain ? ModelFactory.Hex(0x9ec8ff) : World.ZoneColor(f, z => z.air);
                air.Add(new AirP { x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r, y = yy, vy = back ? R(4, 9) : -R(6, 12), c = c });
            }
            for (int i = 0; i < air.Count; i++) { var p = air[i]; p.y += p.vy * dt; air[i] = p; }
            air.RemoveAll(p => back ? (p.y > y + 70 || p.y < camY - 8) : (p.y - y) < -8);
        }
        void Burst(float x, float yy, float z, int hex, int n, float sp) => Burst(x, yy, z, ModelFactory.Hex(hex), n, sp);
        void Burst(float x, float yy, float z, Color c, int n, float sp)
        {
            var p0 = Road.WP(x, yy, z);
            for (int i = 0; i < n; i++)
            {
                if (parts.Count >= PART_N) parts.RemoveAt(0);
                float life = R(0.4f, 0.9f);
                parts.Add(new Part { p = p0, v = Random.onUnitSphere * R(0.3f, 1f) * sp, life = life, max = 0.9f, c = c });
            }
        }
        static float R(float a, float b) => Random.Range(a, b);
        static float Hyp(float a, float b) => Mathf.Sqrt(a * a + b * b);
        static float Smooth(float k) { k = Mathf.Clamp01(k); return k * k * (3 - 2 * k); }
        static float EaseOut(float x) => 1 - (1 - x) * (1 - x);
    }
}
