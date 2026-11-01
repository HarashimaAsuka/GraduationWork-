using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;   // System.Random と取り違えないように

namespace ShoryoumaTaxi
{
    /// <summary>
    /// 「○○タクシー地獄営業所」の本体（Web版 v32 に合わせたもの）。空のシーンの空オブジェクトにこれを1つ付ければ遊べる。
    ///
    /// 座標の決まり（Web版と同じ）：
    ///   y  … 道の高さ。0 が地獄、L(2200) が現世。
    ///   x,z … 道の中心からのずれ（道は曲がっている。中心は Road.Cx / Cz）。プレイヤーはこの平面を動く。
    ///   カメラは道の先（+y）を見上げ、画面の上が +z になる（Unity では画面の右が -x）。
    ///
    /// ステージ：1「送り」・2「帰り」（世界を上下反転して、現世から地獄へ帰る）・3「幻の三日目」（大ボス）。
    /// ファイルの分け方：
    ///   TaxiGame.cs        … 部品の用意・ステージの開始と終わり・共通の道具
    ///   TaxiGame.Stages.cs … ステージの配置表（お客さん・アイテム・敵の波）
    ///   TaxiGame.Play.cs   … 乗せる・降ろす・ぶつかる・アイテム
    ///   TaxiGame.Enemies.cs… 敵の撃ち方・敵の波・奥から飛んでくる敵
    ///   TaxiGame.Boss.cs   … 親玉
    ///   TaxiGame.Loop.cs   … 毎フレームの進行（Web版の update）
    ///   TaxiGame.Render.cs … 毎フレームの見た目（Web版の render）
    ///   TaxiGame.HUD.cs    … 画面表示（IMGUI）
    /// </summary>
    public partial class TaxiGame : MonoBehaviour
    {
        [Header("差し替え用プレハブ（空なら仮モデルを自動生成）")]
        public GameObject cucumberPrefab;
        public GameObject nasuPrefab;
        public GameObject zakuroPrefab;
        public GameObject hozukiPrefab;
        public GameObject tonguePrefab;
        public GameObject soulPrefab;

        [Header("車掌（Blender のモデル。FBX にして入れる。空なら仮のモデル）")]
        public GameObject conductorPrefab;
        [Tooltip("車掌の大きさ")] public float conductorScale = 0.7f;
        [Tooltip("馬の背からの位置（原点は頭の中心）")] public Vector3 conductorOffset = new Vector3(0, 0.45f, 0);
        [Tooltip("向きの調整（FBX の向きが合わないとき）")] public Vector3 conductorEuler = Vector3.zero;

        // 設定画面で切り替える（PlayerPrefs に保存）
        [System.NonSerialized] public bool mouseFollow = false;

        // ---- 決まりごと（Web版 v32 と同じ） ----
        const float L = 2200f, V_GO = 16f, V_BACK = 10f, CAMF = 0.5f, CAM_BACK = 7f, CAM_UP = 1.6f, WARN_RANGE = 34f, FOV_UP = 66f, DOWN_GAP = 15f;
        const float WEB_R = 1.9f, saltCost = 0.5f, SLOW_PER = 0.10f;
        const int MAX_RIDERS = 5, FARE = 6, FARE_PER_10JO = 1, AIR_N = 170, PART_N = 500;

        struct Msg { public string text; public float dur, at, t0; }

        // ===================== 状態 =====================
        enum Mode { Title, Play, Pause, Result, Shop, Book, Name }
        Mode mode = Mode.Title;

        int stage = 1; StageDef st;
        float t, y, yPrev, vel, bk, px, pz, water, cool, inv, hurtT, shake, step, clearT, overT, overDelay = 1.5f, shrink = 1f;
        float timeLeft, fineFlash, chaseCD, escCD, wind, backT;
        int dir = 1, want = 1, kills, fired, delivered, mon, thrown, flips;
        bool over, timeUp, charmOn; string endOut;
        readonly List<Goal> riders = new List<Goal>();
        readonly List<Enemy> enemies = new List<Enemy>();
        readonly List<Soul> souls = new List<Soul>();
        readonly List<Pickup> items = new List<Pickup>();
        readonly List<Mover> movers = new List<Mover>();
        readonly List<Chaser> chasers = new List<Chaser>();
        readonly List<Shot> shots = new List<Shot>();
        readonly List<Salt> salts = new List<Salt>();
        readonly List<Flying> flying = new List<Flying>();
        bool[] waveDone = new bool[0];
        readonly Dictionary<string, int> waveLeft = new Dictionary<string, int>();
        Boss boss;
        readonly List<Msg> msgQ = new List<Msg>();
        Msg? msg;
        readonly HashSet<string> flags = new HashSet<string>();
        bool resultClear, resultNext; string resultTitle, resultText;

        // ===================== シーン部品 =====================
        Camera cam, mirrorCam;
        RenderTexture mirrorRT;
        World world;
        Sfx sfx; Bgm bgm;
        ModelFactory.Steed steed;
        readonly GameObject[] riderGo = new GameObject[MAX_RIDERS];
        readonly int[] riderRare = new int[MAX_RIDERS];
        Transform conductorRoot, conductorModel; ModelFactory.ConductorModel conductorFallback;
        readonly GameObject[] gates = new GameObject[MAX_RIDERS];
        readonly Renderer[] gateDisc = new Renderer[MAX_RIDERS];
        readonly ModelFactory.Plate[] gateLabels = new ModelFactory.Plate[MAX_RIDERS];
        LineRenderer thread;
        Light horseLight;
        MaterialPropertyBlock mpb;
        Transform fx;   // 効果の入れ物

        struct AirP { public float x, y, z, vy; public Color c; }
        Mesh airMesh; Vector3[] airV; Color[] airC; readonly List<AirP> air = new List<AirP>();
        class Part { public Vector3 p, v; public float life, max; public Color c; }
        readonly List<Part> parts = new List<Part>();
        MeshKit.QuadCloud partCloud;
        readonly List<GameObject> warnRings = new List<GameObject>();
        readonly List<GameObject> reticles = new List<GameObject>();
        readonly List<(GameObject root, Renderer soft, Renderer sharp)> shadows = new List<(GameObject, Renderer, Renderer)>();
        readonly List<(GameObject ring, GameObject ripple)> soulRings = new List<(GameObject, GameObject)>();
        readonly List<(GameObject go, Renderer rd)> swoopGlows = new List<(GameObject, Renderer)>();
        class TRing { public GameObject g; public Material[] mats; public Transform o2; }
        readonly List<TRing> tRings = new List<TRing>();
        readonly List<(GameObject go, Material mat)> tBands = new List<(GameObject, Material)>();
        Font jpFont;
        float RX = 5, RZ = 3.3f; int lastW, lastH;
        float camFX, camFZ, frameDt = 1f / 60f, camRoll, lookX, lookZ;
        Vector3 camPos;

        // ============================================================
        void Start()
        {
            Road.L = L;
            jpFont = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic UI", "Yu Gothic", "Meiryo", "Hiragino Sans", "Hiragino Kaku Gothic ProN", "Noto Sans CJK JP", "Arial Unicode MS" }, 32);
            ModelFactory.Init();
            mpb = new MaterialPropertyBlock();
            LoadSettings();
            LoadProfile();
            fx = new GameObject("FX").transform;

            cam = Camera.main;
            if (cam == null) { var cg = new GameObject("Main Camera"); cg.tag = "MainCamera"; cam = cg.AddComponent<Camera>(); cg.AddComponent<AudioListener>(); }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 420f;
            cam.fieldOfView = FOV_UP;

            // バックミラー用のカメラ：カメラの後ろ（下＝手前）を見る。映像は HUD で左右反転して貼る
            var mg = new GameObject("Mirror Camera");
            mirrorCam = mg.AddComponent<Camera>();
            mirrorCam.clearFlags = CameraClearFlags.SolidColor;
            mirrorCam.fieldOfView = 62f; mirrorCam.nearClipPlane = 0.5f; mirrorCam.farClipPlane = 90f;
            mirrorRT = new RenderTexture(760, 316, 16) { name = "MirrorRT" };
            mirrorCam.targetTexture = mirrorRT;
            mirrorCam.enabled = false;
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += OnEndCamera;

            if (FindFirstObjectByType<Light>() == null)
            {
                var lg = new GameObject("Directional Light"); var l = lg.AddComponent<Light>();
                l.type = LightType.Directional; l.intensity = 0.7f; lg.transform.rotation = Quaternion.Euler(60f, 30f, 0);
            }
            horseLight = new GameObject("Horse Light").AddComponent<Light>();
            horseLight.type = LightType.Point; horseLight.color = ModelFactory.Hex(0xffd8a0); horseLight.intensity = 1.2f; horseLight.range = 18f;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;

            world = new World(L);
            sfx = new Sfx(gameObject);
            bgm = new Bgm(gameObject);
            steed = ModelFactory.BuildSteed(cucumberPrefab, nasuPrefab, soulPrefab);
            steed.rider.SetActive(false);
            // 乗っているお客さん：馬の背に、頭からしっぽへ5人まで並ぶ
            for (int i = 0; i < MAX_RIDERS; i++)
            {
                var m = ModelFactory.Soul(soulPrefab);
                m.transform.SetParent(steed.root.transform, false);
                m.transform.localPosition = new Vector3(0, 1.0f, 0.3f - i * 0.42f);
                m.transform.localScale = Vector3.one * 0.42f;
                m.SetActive(false); riderGo[i] = m; riderRare[i] = 0;
            }
            BuildConductor();
            for (int i = 0; i < MAX_RIDERS; i++)
            {
                gates[i] = ModelFactory.DestGate(out gateDisc[i]); gates[i].SetActive(false);
                gateLabels[i] = ModelFactory.MakeLabel(jpFont, 7f, 1.75f, "行き先", ModelFactory.Hex(0xffd9a0), Color.white, ModelFactory.Hex(0xff7a5a), 0.42f, 0.75f);
            }

            var vc = Mats.VertexColor();
            for (int i = 0; i < 16; i++) { var w = LineObj("warn", MeshKit.CircleLines(1f, 40, Color.white), vc); w.SetActive(false); warnRings.Add(w); }
            reticles.Add(LineObj("reticle", MeshKit.CircleLines(0.46f, 32, Color.white), vc));
            reticles.Add(LineObj("reticle", MeshKit.CircleLines(0.64f, 32, Color.white), vc));
            // お客さんのまわりに出す光の輪（輪と、広がる波紋）
            for (int i = 0; i < 8; i++)
            {
                var a = LineObj("soulRing", MeshKit.CircleLines(0.96f, 40, Color.white), vc); a.SetActive(false);
                var b = LineObj("soulRipple", MeshKit.CircleLines(1.2f, 40, Color.white), vc); b.SetActive(false);
                soulRings.Add((a, b));
            }
            // 後ろ向きの影：地獄の火に照らされて、手前（下）にいるものの影が馬の手前に映る
            for (int i = 0; i < 18; i++)
            {
                var root = new GameObject("shadow");
                var soft = ModelFactory.Prim(PrimitiveType.Quad, root.transform, Vector3.zero, Vector3.one * 1.25f, ModelFactory.ShadowMat("soul", true));
                var sharp = ModelFactory.Prim(PrimitiveType.Quad, root.transform, new Vector3(0, 0, -0.01f), Vector3.one, ModelFactory.ShadowMat("soul", false));
                root.SetActive(false);
                shadows.Add((root, soft.GetComponent<Renderer>(), sharp.GetComponent<Renderer>()));
            }
            // v28：後ろ向きのとき、横の景色の奥から飛んでくるものの光
            for (int i = 0; i < 14; i++)
            {
                var g = ModelFactory.Glow(fx, ModelFactory.GlowMat(new Color(1, 0.19f, 0.31f, 0.7f)), 5f, Vector3.zero);
                g.SetActive(false); swoopGlows.Add((g, g.GetComponent<Renderer>()));
            }
            // 親玉の合図：突進・叩きつけの赤い輪と、薙ぎ払いの赤い帯（いつも手前に描く）
            for (int i = 0; i < 3; i++)
            {
                var tr = new TRing { g = new GameObject("tRing"), mats = new Material[4] };
                tr.g.transform.SetParent(fx, false);
                Mesh[] ms = { MeshKit.Ring(2.75f, 3.05f, 48), MeshKit.Disc(2.75f, 48), MeshKit.Disc(0.35f, 16), MeshKit.Ring(3.3f, 3.42f, 48) };
                for (int k = 0; k < 4; k++)
                {
                    tr.mats[k] = ModelFactory.OverlayMat(new Color(1, 0.19f, 0.19f, 0.5f), 3050 + k);
                    var o = ModelFactory.MeshObj("r", ms[k], tr.g.transform, tr.mats[k], Vector3.zero, Vector3.one, new Vector3(90, 0, 0));
                    if (k == 3) tr.o2 = o.transform;
                }
                tr.g.SetActive(false); tRings.Add(tr);
            }
            for (int i = 0; i < 2; i++)
            {
                var m = ModelFactory.OverlayMat(new Color(1, 0.19f, 0.19f, 0.3f), 3049);
                var g = ModelFactory.Prim(PrimitiveType.Quad, fx, Vector3.zero, Vector3.one, m, new Vector3(90, 0, 0));
                g.SetActive(false); tBands.Add((g, m));
            }

            thread = new GameObject("thread").AddComponent<LineRenderer>();
            thread.material = vc; thread.widthMultiplier = 0.03f; thread.positionCount = 2;
            thread.startColor = thread.endColor = new Color(0.87f, 0.91f, 0.95f, 0.6f);
            thread.enabled = false;

            // 風の筋：1枚のメッシュを毎フレーム書き換える
            airV = new Vector3[AIR_N * 2]; airC = new Color[AIR_N * 2];
            airMesh = new Mesh(); airMesh.MarkDynamic();
            airMesh.vertices = airV; airMesh.colors = airC;
            var ai = new int[AIR_N * 2]; for (int i = 0; i < ai.Length; i++) ai[i] = i;
            airMesh.SetIndices(ai, MeshTopology.Lines, 0);
            var ago = new GameObject("air"); ago.AddComponent<MeshFilter>().sharedMesh = airMesh; ago.AddComponent<MeshRenderer>().sharedMaterial = vc;

            // 火花（光る粒を1枚のメッシュに並べる）
            partCloud = new MeshKit.QuadCloud(PART_N);
            for (int i = 0; i < PART_N; i++) partCloud.Hide(i);
            var pm = Mats.VertexColor(); pm.mainTexture = ModelFactory.GlowTex;
            var pgo = new GameObject("sparks"); pgo.AddComponent<MeshFilter>().sharedMesh = partCloud.mesh; pgo.AddComponent<MeshRenderer>().sharedMaterial = pm;

            NewGame(1);
            FitField();
        }

        void OnDestroy()
        {
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= OnEndCamera;
            if (mirrorRT != null) { mirrorRT.Release(); Destroy(mirrorRT); }
        }

        GameObject LineObj(string name, Mesh m, Material mat)
        {
            var g = new GameObject(name);
            g.transform.SetParent(fx, false);
            g.AddComponent<MeshFilter>().sharedMesh = m;
            g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return g;
        }

        /// <summary>v24：車掌 —— 馬の首のつけ根にまたがって手綱を握る。Blender のモデル（conductorPrefab）があればそれを使う。</summary>
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
                // おばけなので、ほんのり自分で光らせて、地獄の赤い光の中でも白く見えるようにする
                foreach (var rd in m.GetComponentsInChildren<Renderer>())
                    foreach (var mt in rd.materials)
                    {
                        if (!mt.HasProperty("_EmissionColor")) continue;
                        mt.EnableKeyword("_EMISSION");
                        var baseCol = mt.HasProperty("_BaseColor") ? mt.GetColor("_BaseColor") : Color.white;
                        if (mt.HasProperty("_BaseMap") && mt.GetTexture("_BaseMap") != null) { mt.SetTexture("_EmissionMap", mt.GetTexture("_BaseMap")); mt.SetColor("_EmissionColor", new Color(0.55f, 0.55f, 0.55f)); }
                        else mt.SetColor("_EmissionColor", baseCol * 0.35f);
                    }
                ModelFactory.GlowQ(wrap, 0xdff4ff, 2.2f, 0.35f, new Vector3(0, 0, -0.3f));
                conductorModel = wrap;
            }
            else conductorFallback = ModelFactory.Conductor(conductorRoot);
        }

        /// <summary>画面の縦横比から、馬が動ける楕円の大きさを決める。</summary>
        void FitField()
        {
            lastW = Screen.width; lastH = Screen.height;
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            float hh = Mathf.Tan(FOV_UP * Mathf.Deg2Rad / 2f) * CAM_BACK, hw = hh * aspect;
            RX = Mathf.Clamp((0.85f * hw - 0.6f) / (1 - CAMF), 2.6f, 7f);
            RZ = Mathf.Clamp((0.85f * hh - CAM_UP - 0.6f) / (1 - CAMF), 2.2f, 5.5f);
        }

        /// <summary>社名の表示を更新する（v32 は背中の行灯を出さないので、画面の文字だけ）。</summary>
        void ApplyName() { }

        // ============================================================
        //  ゲームの開始・終了
        // ============================================================
        void ClearGame()
        {
            foreach (var e in enemies) Destroy(e.go); enemies.Clear();
            foreach (var s in souls) { Destroy(s.go); if (s.tag != null) Destroy(s.tag.root); } souls.Clear();
            foreach (var it in items) { Destroy(it.im.root); Destroy(it.tag.root); } items.Clear();
            foreach (var m in movers) Destroy(m.go); movers.Clear();
            ClearChasers();
            foreach (var s in shots) Destroy(s.go); shots.Clear();
            foreach (var s in salts) Destroy(s.go); salts.Clear();
            foreach (var f in flying) Destroy(f.go); flying.Clear();
            if (boss != null)
            {
                Destroy(boss.m.root);
                foreach (var h in boss.hands) Destroy(h.go);
                if (boss.guards != null) foreach (var gd in boss.guards) Destroy(gd.go);
                boss = null;
            }
            riders.Clear(); air.Clear(); parts.Clear(); msgQ.Clear(); msg = null; flags.Clear(); waveLeft.Clear();
        }

        void NewGame(int no)
        {
            ClearGame();
            stage = Mathf.Clamp(no, 1, 3);
            st = STAGES[stage - 1];
            // v23：ステージ2は世界を上下反転（前が地獄・後ろが現世）、ステージ3は夢の色
            Road.Flip = stage == 2;
            world.SetStage(stage);
            float y0 = st.boss ? L - 1320 : 30;   // 親玉戦は出口まで1320丈
            t = 0; y = yPrev = y0; vel = 0; dir = want = 1; bk = 0; px = 0; pz = 0.6f;
            water = 100; cool = inv = 0; hurtT = shake = step = clearT = overT = 0; overDelay = 1.5f; shrink = 1;
            kills = fired = delivered = mon = thrown = flips = 0;
            over = timeUp = charmOn = false; endOut = null;
            timeLeft = st.time; fineFlash = 0; chaseCD = 2; escCD = 4; wind = 0; backT = 0;
            waveDone = new bool[st.waves.Length];
            MakeEnemies(); MakeSouls(); MakeItems();
            if (st.boss) MakeBoss();
            for (int i = 0; i < MAX_RIDERS; i++) { riderGo[i].SetActive(false); gates[i].SetActive(false); gateLabels[i].root.SetActive(false); }
        }

        /// <summary>「営業を始める」：社名が決まっていなければ先に決めてもらう。</summary>
        int nameStage = 1;
        void Begin(int no)
        {
            if (string.IsNullOrEmpty(coNameRaw)) { nameStage = no; OpenName(Mode.Play); }
            else StartStage(no);
        }

        void StartStage(int no)
        {
            NewGame(no); mode = Mode.Play;
            bgm.Play(stage);
            cool = 0.4f;   // 開始ボタンのクリックで塩が出ないように
            Say($"{CoName()}タクシー地獄営業所　ステージ{st.no}「{st.name}」", 2.6f);
            Say($"{st.goal}。制限時間 {Mathf.FloorToInt(st.time / 60)}分{(st.time % 60 != 0 ? (st.time % 60) + "秒" : "")}", 3f);
            if (st.no == 1)
            {
                Say("魂の上の札が行き先。選んで乗せよう。5人まで乗れるが、乗せるほど遅くなる。", 4f);
                Say("左クリックで塩。撃つほど、馬は干からびる。", 3.4f);
                Say("敵にぶつかると向きが反転し、お客さんを1人落とす。もう一度ぶつかると元に戻る。", 4.2f);
            }
            else if (st.boss) Say("親玉は塩で祓う。体当たりと種の雨に気をつけて。", 3.4f);
        }

        void ToTitle() { bgm.Stop(); NewGame(1); mode = Mode.Title; }

        void Pause() { mode = Mode.Pause; bgm.Pause(true); }

        void Finish(bool clear)
        {
            bgm.Stop();
            mode = Mode.Result; resultClear = clear; resultNext = clear && st.no < 3;
            if (!clear)
            {
                resultTitle = $"ステージ{st.no}「{st.name}」　" + (endOut != null ? "道の終わりに着いた" : timeUp ? "時間切れ" : "干からびた");
                int left = boss != null ? Mathf.Max(0, boss.hp) : 0;
                resultText = st.boss
                    ? (endOut == "top" ? $"現世の出口に着いてしまった。\n親玉は、あと {left} 発ぶん残したまま、現世へ抜け出した。"
                      : endOut == "bottom" ? $"地獄の底まで落ちてしまった。\n親玉は、あと {left} 発ぶん残っていた。"
                      : timeUp ? $"時間切れ。親玉は、あと {left} 発ぶん残っていた。" : "精霊馬は干からびてしまった。親玉はまだ笑っている。")
                    : timeUp
                    ? $"時間切れ。送り届けた魂は {delivered} 人。\nノルマの {st.quota} 人まで、あと {Mathf.Max(0, st.quota - delivered)} 人だった。"
                    : $"精霊馬は干からびてしまった。\n送り届けた魂は {delivered} 人。";
            }
            else
            {
                resultTitle = $"{CoName()}タクシー地獄営業所　ステージ{st.no}「{st.name}」クリア";
                resultText = st.boss
                    ? "鬼ザクロの親玉を祓った。\nお盆の三日目の夜が、静かに明けていく。"
                    : thrown == 0
                    ? $"ひとりも落とさずに、{delivered} 人の魂を送り届けた。\n残り時間 {Mathf.CeilToInt(timeLeft)} 秒。"
                    : $"{delivered} 人の魂を送り届けた。\n途中で落としてしまったお客さんが {thrown} 人。";
            }
        }

        /// <summary>v23：下の文字はすぐ出す。あとから来たものがあれば今のものは早めに切り上げ、古くなったものは捨てる。</summary>
        void Say(string text, float dur = 3.2f)
        {
            msgQ.Add(new Msg { text = text, dur = Mathf.Min(dur, 2.6f), at = t });
            if (msgQ.Count > 2) msgQ.RemoveRange(0, msgQ.Count - 2);
        }

        void UpdateMsg()
        {
            while (msgQ.Count > 0 && t - msgQ[0].at > 2.5f) msgQ.RemoveAt(0);
            bool expired = msg != null && t - msg.Value.t0 > msg.Value.dur;
            if (msg == null || expired || (msgQ.Count > 0 && t - msg.Value.t0 > 1.1f))
            {
                if (msgQ.Count > 0) { var m = msgQ[0]; msgQ.RemoveAt(0); m.t0 = t; msg = m; }
                else if (expired) msg = null;
            }
        }

        float Rel(float yy) => (yy - y) * dir;
        static float R(float a, float b) => Random.Range(a, b);
        static float Smooth(float k) { k = Mathf.Clamp01(k); return k * k * (3 - 2 * k); }
        static Vector3 WP(float x, float yy, float z) => Road.WP(x, yy, z);
        static Color Hex(int h) => ModelFactory.Hex(h);

        /// <summary>火花：x,z は道の中心からのずれ（遊びの座標）。</summary>
        void Burst(float x, float yy, float z, Color c, int n, float sp) => BurstW(WP(x, yy, z), c, n, sp);
        void Burst(float x, float yy, float z, int hex, int n, float sp) => BurstW(WP(x, yy, z), Hex(hex), n, sp);

        void BurstW(Vector3 p, Color c, int n, float sp)
        {
            for (int i = 0; i < n; i++)
            {
                if (parts.Count >= PART_N) parts.RemoveAt(0);
                float life = R(0.4f, 1f);
                parts.Add(new Part { p = p, v = Random.onUnitSphere * R(0.4f, 1f) * sp, life = life, max = 1f, c = c });
            }
        }

        void AddPart(Vector3 p, Vector3 v, float life, Color c)
        {
            if (parts.Count >= PART_N) parts.RemoveAt(0);
            parts.Add(new Part { p = p, v = v, life = life, max = life, c = c });
        }
    }
}
