using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;   // System.Random と取り違えないように

namespace ShoryoumaTaxi
{
    /// <summary>
    /// 「○○タクシー地獄営業所」の本体。空のシーンの空オブジェクトにこれを1つ付ければ遊べる。
    ///
    /// 座標の決まり（Web版と同じ）：
    ///   y  … 道の高さ。0 が地獄、roadLength が現世。
    ///   x,z … 道の断面上の位置。プレイヤーはこの平面を動く。
    ///   カメラは常に +y（上）を見上げ、画面の上が +z になる。
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

        [Header("ルール")]
        public float roadLength = 1500f;
        public int goalCount = 5;
        [Tooltip("運賃の基本（文）")] public int fare = 6;
        [Tooltip("乗車距離10丈ごとに上がる運賃（文）")] public int farePer10Jo = 1;
        [Tooltip("乗せている客を飛ばしたときのお詫び料（文）")] public int swapFine = 5;
        [Tooltip("ノルマ達成ボーナスの基本（文）。残り時間10秒ごとに1文足される")] public int quotaBonus = 20;
        [Tooltip("営業時間（秒）")] public float timeLimit = 330f;
        [Tooltip("上り（きゅうり）の速さ")] public float speedUp = 16f;
        [Tooltip("下り（ナス）の速さ")] public float speedDown = 10f;
        [Tooltip("塩1発で減る水分")] public float saltCost = 0.7f;

        [Header("カメラ（上りも下りも同じ向き・画角）")]
        public float camBackUp = 7f;
        [Tooltip("下りのとき、ナスの牛がカメラからどれだけ奥にいるか")] public float downGap = 15f;
        public float fovUp = 66f;

        // 設定画面で切り替える（PlayerPrefs に保存）
        [System.NonSerialized] public bool mouseFollow = false;

        const float CAMF = 0.5f, CAM_UP = 1.6f, WARN_RANGE = 34f, WEB_R = 1.9f;

        // 登場するもの（Enemy, Soul, Shot, Salt, Flying, Spark, Goal, Chaser）は、それぞれ別のファイルにある
        struct Msg { public string text; public float dur, t0; }
        class Callout { public string who, text, arrow; public bool danger; public float t0; }

        static readonly string[][] STOPS = {
            new[] { "閻魔庁前", "血の池地獄", "針の山ふもと" },
            new[] { "三途の川 渡し場", "賽の河原", "奪衣婆の茶屋" },
            new[] { "雲の上の停留所", "雷さまの太鼓前", "天の浮橋" },
            new[] { "迎え火の家", "盆踊りのやぐら", "縁側の仏壇" },
        };

        // ===================== 状態 =====================
        enum Mode { Title, Play, Pause, Result, Shop, Book, Name }
        Mode mode = Mode.Title;

        float L;
        float t, y, yPrev, vel, bk, px, pz, water, silk, cool, inv, soulCD, hurtT, webGlow, shake, step, clearT, overT, shrink = 1f;
        float timeLeft, fineFlash, callCD, chaseCD;
        int dir = 1, want = 1, kills, fired, caught, delivered, mon, thrown, lastStep, bonus, rideHits;
        bool silkLock, guarding, over, carrying, timeUp;
        Goal goal;
        Luck luck;
        Callout call;
        readonly List<Enemy> enemies = new List<Enemy>();
        readonly List<Soul> souls = new List<Soul>();
        readonly List<Chaser> chasers = new List<Chaser>();
        readonly List<Shot> shots = new List<Shot>();
        readonly List<Salt> salts = new List<Salt>();
        readonly List<Flying> flying = new List<Flying>();
        readonly Queue<Msg> msgQ = new Queue<Msg>();
        Msg? msg;
        readonly HashSet<string> flags = new HashSet<string>();
        bool resultClear; string resultTitle, resultText;

        // ===================== シーン部品 =====================
        Camera cam, mirrorCam;
        RenderTexture mirrorRT;
        World world;
        Sfx sfx;
        ModelFactory.Steed steed;
        GameObject signLamp; TextMesh signText;
        GameObject gate; TextMesh gateLabel;
        GameObject web; MaterialPropertyBlock mpb;
        LineRenderer thread;
        Mesh airMesh; Vector3[] airV; Color[] airC; readonly List<Vector4> air = new List<Vector4>(); // x,y,z,vy
        readonly List<Spark> sparks = new List<Spark>();
        readonly List<GameObject> warnRings = new List<GameObject>();
        readonly List<GameObject> reticles = new List<GameObject>();
        readonly List<(GameObject go, float life)> hoof = new List<(GameObject, float)>();
        readonly List<(GameObject root, Renderer soft, Renderer sharp)> shadows = new List<(GameObject, Renderer, Renderer)>();
        readonly List<(GameObject ring, GameObject ripple)> soulRings = new List<(GameObject, GameObject)>();
        Font jpFont;
        float RX = 5, RZ = 3.3f; int lastW, lastH;
        float camFX, camFZ, frameDt = 1f / 60f;
        const int AIR_N = 110;

        // ============================================================
        void Start()
        {
            L = roadLength;
            jpFont = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic UI", "Yu Gothic", "Meiryo", "Hiragino Sans", "Hiragino Kaku Gothic ProN", "Noto Sans CJK JP", "Arial Unicode MS" }, 32);
            ModelFactory.Init();
            mpb = new MaterialPropertyBlock();
            LoadSettings();
            LoadProfile();

            cam = Camera.main;
            if (cam == null) { var cg = new GameObject("Main Camera"); cg.tag = "MainCamera"; cam = cg.AddComponent<Camera>(); cg.AddComponent<AudioListener>(); }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 320f;
            cam.fieldOfView = fovUp;

            // バックミラー用のカメラ：カメラの後ろ（下＝手前）を見る。映像は HUD で左右反転して貼る
            var mg = new GameObject("Mirror Camera");
            mirrorCam = mg.AddComponent<Camera>();
            mirrorCam.clearFlags = CameraClearFlags.SolidColor;
            mirrorCam.fieldOfView = 62f; mirrorCam.nearClipPlane = 0.5f; mirrorCam.farClipPlane = 90f;
            mirrorRT = new RenderTexture(760, 316, 16) { name = "MirrorRT" };
            mirrorCam.targetTexture = mirrorRT;
            mirrorCam.enabled = false;

            if (FindFirstObjectByType<Light>() == null)
            {
                var lg = new GameObject("Directional Light"); var l = lg.AddComponent<Light>();
                l.type = LightType.Directional; l.intensity = 0.8f; lg.transform.rotation = Quaternion.Euler(60f, 30f, 0);
            }
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;

            world = new World(L);
            sfx = new Sfx(gameObject);
            steed = ModelFactory.BuildSteed(cucumberPrefab, nasuPrefab, soulPrefab);
            signLamp = ModelFactory.SignLamp(jpFont, out signText);
            signLamp.transform.SetParent(steed.root.transform, false);
            signLamp.transform.localPosition = new Vector3(0, 1.25f, -0.75f);
            ApplyName();
            gate = ModelFactory.Gate(out gateLabel, jpFont); gate.SetActive(false); gateLabel.gameObject.SetActive(false);

            var vc = Mats.VertexColor();
            web = LineObj("web", WebMesh(), vc);
            web.SetActive(false);
            for (int i = 0; i < 16; i++) { var w = LineObj("warn", MeshKit.CircleLines(1f, 40, Color.white), vc); w.SetActive(false); warnRings.Add(w); }
            reticles.Add(LineObj("reticle", MeshKit.CircleLines(0.46f, 32, Color.white), vc));
            reticles.Add(LineObj("reticle", MeshKit.CircleLines(0.64f, 32, Color.white), vc));
            for (int i = 0; i < 10; i++) { var h = LineObj("hoof", MeshKit.CircleLines(0.56f, 32, new Color(0.72f, 1f, 0.56f)), vc); h.SetActive(false); hoof.Add((h, 0f)); }
            // 空車のとき、魂のまわりに出す光の輪（輪と、広がる波紋）
            for (int i = 0; i < 8; i++)
            {
                var a = LineObj("soulRing", MeshKit.CircleLines(0.96f, 40, Color.white), vc); a.SetActive(false);
                var b = LineObj("soulRipple", MeshKit.CircleLines(1.2f, 40, Color.white), vc); b.SetActive(false);
                soulRings.Add((a, b));
            }
            // 下りの影：地獄の火に照らされて、手前（下）にいるものの影が牛の手前に映る
            for (int i = 0; i < 18; i++)
            {
                var root = new GameObject("shadow");
                var soft = ModelFactory.Prim(PrimitiveType.Quad, root.transform, Vector3.zero, Vector3.one * 1.25f, ModelFactory.ShadowMat("soul", true));
                var sharp = ModelFactory.Prim(PrimitiveType.Quad, root.transform, new Vector3(0, 0, -0.01f), Vector3.one, ModelFactory.ShadowMat("soul", false));
                root.SetActive(false);
                shadows.Add((root, soft.GetComponent<Renderer>(), sharp.GetComponent<Renderer>()));
            }

            thread = new GameObject("thread").AddComponent<LineRenderer>();
            thread.material = vc; thread.widthMultiplier = 0.03f; thread.positionCount = 2;
            thread.startColor = thread.endColor = new Color(0.87f, 0.91f, 0.95f, 0.7f);
            thread.enabled = false;

            // 風の筋：1枚のメッシュを毎フレーム書き換える
            airV = new Vector3[AIR_N * 2]; airC = new Color[AIR_N * 2];
            airMesh = new Mesh(); airMesh.MarkDynamic();
            airMesh.vertices = airV; airMesh.colors = airC;
            var ai = new int[AIR_N * 2]; for (int i = 0; i < ai.Length; i++) ai[i] = i;
            airMesh.SetIndices(ai, MeshTopology.Lines, 0);
            var ago = new GameObject("air"); ago.AddComponent<MeshFilter>().sharedMesh = airMesh; ago.AddComponent<MeshRenderer>().sharedMaterial = vc;

            // 火花（小さな立方体のプール）
            var sparkMat = Mats.Unlit(Color.white);
            for (int i = 0; i < 320; i++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(s.GetComponent<Collider>());
                s.GetComponent<Renderer>().sharedMaterial = sparkMat; s.SetActive(false);
                sparks.Add(new Spark { tr = s.transform, rd = s.GetComponent<Renderer>() });
            }

            NewGame();
            FitField();
        }

        void OnDestroy()
        {
            if (mirrorRT != null) { mirrorRT.Release(); Destroy(mirrorRT); }
        }

        GameObject LineObj(string name, Mesh m, Material mat)
        {
            var g = new GameObject(name);
            g.AddComponent<MeshFilter>().sharedMesh = m;
            g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return g;
        }

        static Mesh WebMesh()
        {
            var v = new List<Vector3>(); var c = new List<Color>(); var idx = new List<int>();
            int spokes = 10;
            void Seg(Vector3 a, Vector3 b) { idx.Add(v.Count); v.Add(a); c.Add(Color.white); idx.Add(v.Count); v.Add(b); c.Add(Color.white); }
            for (int i = 0; i < spokes; i++) { float a = i * Mathf.PI * 2f / spokes; Seg(Vector3.zero, new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a))); }
            for (int ring = 1; ring <= 5; ring++)
            {
                float rr = ring / 5f;
                for (int i = 0; i < spokes; i++)
                {
                    float a0 = i * Mathf.PI * 2f / spokes, a1 = (i + 1) * Mathf.PI * 2f / spokes, am = (a0 + a1) / 2f, sag = rr * 0.9f;
                    var p0 = new Vector3(Mathf.Cos(a0) * rr, 0, Mathf.Sin(a0) * rr);
                    var pm = new Vector3(Mathf.Cos(am) * sag, 0, Mathf.Sin(am) * sag);
                    var p1 = new Vector3(Mathf.Cos(a1) * rr, 0, Mathf.Sin(a1) * rr);
                    Seg(p0, pm); Seg(pm, p1);
                }
            }
            return MeshKit.Lines(v, c, idx);
        }

        /// <summary>画面の縦横比から、馬が動ける楕円の大きさを決める。</summary>
        void FitField()
        {
            lastW = Screen.width; lastH = Screen.height;
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            float hh = Mathf.Tan(fovUp * Mathf.Deg2Rad / 2f) * camBackUp, hw = hh * aspect;
            RX = Mathf.Clamp((0.85f * hw - 0.6f) / (1 - CAMF), 2.6f, 7f);
            RZ = Mathf.Clamp((0.85f * hh - CAM_UP - 0.6f) / (1 - CAMF), 2.2f, 5.5f);
            world.FitShaft(RX, RZ);
        }

        /// <summary>社名を行灯などに反映する。</summary>
        void ApplyName()
        {
            if (signText == null) return;
            string n = CoName(); if (n.Length > 6) n = n.Substring(0, 6);
            signText.text = string.Join("\n", n.ToCharArray());
        }

        // ============================================================
        //  ゲームの開始・終了
        // ============================================================
        void NewGame()
        {
            foreach (var e in enemies) Destroy(e.go); enemies.Clear();
            foreach (var s in souls) Destroy(s.go); souls.Clear();
            ClearChasers();
            foreach (var s in shots) Destroy(s.go); shots.Clear();
            foreach (var s in salts) Destroy(s.go); salts.Clear();
            foreach (var f in flying) Destroy(f.go); flying.Clear();
            air.Clear(); msgQ.Clear(); msg = null; flags.Clear();

            t = 0; y = yPrev = 30; vel = 0; dir = want = 1; bk = 0; px = 0; pz = 0.6f;
            water = MaxWater; silk = 100; silkLock = guarding = over = carrying = timeUp = false; cool = inv = 0; soulCD = 1;
            kills = fired = caught = delivered = mon = thrown = bonus = rideHits = 0; hurtT = webGlow = shake = step = clearT = overT = 0; shrink = 1; lastStep = 0;
            timeLeft = timeLimit + 30 * Lv("incense"); fineFlash = 0; callCD = 0; chaseCD = 2; call = null;
            goal = null; luck = null;
            steed.rider.SetActive(false); gate.SetActive(false); gateLabel.gameObject.SetActive(false);
            MakeEnemies();
        }

        /// <summary>「営業を始める」：社名が決まっていなければ先に決めてもらう。</summary>
        void Begin()
        {
            if (string.IsNullOrEmpty(coNameRaw)) OpenName(Mode.Play);
            else StartGame();
        }

        void StartGame()
        {
            NewGame(); mode = Mode.Play;
            cool = 0.5f;   // 開始ボタンのクリックで塩が出ないように
            luck = DrawOmikuji();
            timeLeft += luck.time;
            Say($"{CoName()}タクシー地獄営業所、営業開始。", 2.6f);
            Say($"今夜のおみくじは「{luck.name}」。{luck.text}", 3f);
            Say("青く光る魂がお客さん。触れると乗せられる。", 3.4f);
            Say("左クリックで塩。撃つほど、馬は干からびる。", 3.4f);
        }

        void ToTitle() { NewGame(); mode = Mode.Title; }

        void Finish(bool clear)
        {
            mode = Mode.Result; resultClear = clear;
            if (!clear)
            {
                resultTitle = timeUp ? "時間切れ" : "干からびた";
                resultText = timeUp
                    ? $"時間切れ。送り届けた魂は {delivered} 人。\nノルマの {goalCount} 人まで、あと {goalCount - delivered} 人だった。"
                    : carrying && goal != null
                    ? $"お客さんを乗せたまま、干からびてしまった。\n{goal.goalName}まで、あと {Mathf.CeilToInt(Mathf.Abs(goal.y - y) / 10)} 丈だった。"
                    : $"精霊馬は干からびてしまった。\n送り届けた魂は {delivered} 人。";
            }
            else
            {
                resultTitle = $"{CoName()}タクシー地獄営業所　本日の営業終了";
                resultText = thrown == 0
                    ? $"ひとりも落とさずに、{goalCount} 人の魂を送り届けた。\n{mon} 文の稼ぎに、ノルマ達成ボーナス {bonus} 文。"
                    : $"{goalCount} 人の魂を送り届けた。ノルマ達成ボーナス {bonus} 文。\n途中で飛ばしてしまったお客さんが {thrown} 人。";
            }
        }

        void Say(string text, float dur = 3.2f) => msgQ.Enqueue(new Msg { text = text, dur = dur });
        float Rel(float yy) => (yy - y) * dir;
        static float R(float a, float b) => Random.Range(a, b);

        // ============================================================
        //  敵とお客さん
        // ============================================================
        string PickType(float f)
        {
            float wz = 5f, wh = f > 0.2f ? 2.2f : 1.2f, wt = f > 0.28f ? 0.7f : 0f;
            float r = R(0, wz + wh + wt);
            if (r < wz) return "zakuro";
            if (r < wz + wh) return "hozuki";
            return "tongue";
        }

        GameObject BuildEnemy(string type, out Renderer body, out GameObject arils, out int hp, out float r)
        {
            body = null; arils = null;
            if (type == "zakuro") { hp = 2; r = 1.0f; return ModelFactory.Zakuro(zakuroPrefab, out body, out arils); }
            if (type == "hozuki") { hp = 1; r = 0.9f; return ModelFactory.Hozuki(hozukiPrefab); }
            hp = 5; r = 1.5f; return ModelFactory.Tongue(tonguePrefab);
        }

        void MakeEnemies()
        {
            for (float h = 70; h < L - 50; h += R(14, 20))
            {
                int n = Random.value < 0.15f ? 2 : 1;
                for (int i = 0; i < n; i++)
                {
                    string type = PickType(h / L);
                    var e = new Enemy { type = type, y0 = h + i * 3 };
                    e.y = e.yPrev = e.y0;
                    float a = R(0, Mathf.PI * 2), r = Mathf.Sqrt(Random.value) * 0.85f;
                    e.nx = Mathf.Cos(a) * r; e.nz = Mathf.Sin(a) * r;
                    e.cd = R(0.4f, 2f); e.ph = R(0, 6);
                    e.go = BuildEnemy(type, out e.body, out e.arils, out e.hp, out e.r);
                    e.shooter = type != "zakuro" || Random.value < 0.5f;
                    e.max = e.hp;
                    e.go.SetActive(false);
                    enemies.Add(e);
                }
            }
        }

        // ---- 「追われる」モード：下りの間、置いてある敵は眠り、上（奥）から追っ手が迫ってくる ----
        void ClearChasers() { foreach (var c in chasers) Destroy(c.go); chasers.Clear(); }

        void SpawnChaser()
        {
            string type = PickType(Mathf.Clamp01(y / L));
            var c = new Chaser { type = type };
            c.go = BuildEnemy(type, out _, out _, out c.hp, out c.r);
            c.hp += 1; c.max = c.hp;
            // 赤い気配（霧の中でも見える）。敵はカメラに顔を向けるので、気配は体の奥（-Z）に置く
            c.aura = ModelFactory.Glow(c.go.transform, ModelFactory.AuraMat, 4.2f * c.r, new Vector3(0, 0, -0.6f));
            c.auraRd = c.aura.GetComponent<Renderer>();
            float a = R(0, Mathf.PI * 2), rr = Mathf.Sqrt(Random.value) * 0.85f;
            c.x = Mathf.Cos(a) * rr * RX; c.z = Mathf.Sin(a) * rr * RZ;
            c.y = c.yPrev = y + R(40, 54);
            c.close = R(7, 10); c.cd = R(0.8f, 1.6f); c.ph = R(0, 6);
            chasers.Add(c);
        }

        void Ripen(Enemy e)
        {
            e.ripe = true;
            if (e.type == "zakuro" && e.body != null && zakuroPrefab == null) e.body.sharedMaterial = ModelFactory.ZakuroRipeMat;
        }

        void Revive(Enemy e)
        {
            e.alive = true; e.hp = e.max; e.ripe = e.emerged = e.bumped = false; e.y = e.yPrev = e.y0;
            if (e.type == "zakuro" && zakuroPrefab == null) { e.body.sharedMaterial = ModelFactory.ZakuroMat; e.arils.SetActive(false); }
        }

        void Kill(Enemy e)
        {
            e.alive = false; e.go.SetActive(false); e.deadT = 0; kills++;
            Burst(new Vector3(e.x, e.y, e.z), e.type == "hozuki" ? new Color(1f, 0.55f, 0.2f) : new Color(1f, 0.2f, 0.35f), 22, 4f);
            Burst(new Vector3(e.x, e.y, e.z), Color.white, 10, 3f);
            sfx.Play(sfx.pop);
        }

        void SpawnSoul(float yy)
        {
            float a = R(0, Mathf.PI * 2), r = Mathf.Sqrt(Random.value) * 0.8f;
            var guest = PickGuest();
            var s = new Soul { go = ModelFactory.Soul(soulPrefab, guest.rare), guest = guest, y0 = yy, y = yy, yPrev = yy, nx = Mathf.Cos(a) * r, nz = Mathf.Sin(a) * r, ph = R(0, 6) };
            s.go.SetActive(false);
            souls.Add(s);
        }

        void LaunchFlying(Vector3 p, bool home)
        {
            var go = ModelFactory.Soul(soulPrefab);
            float a = R(0, Mathf.PI * 2);
            var f = new Flying { go = go, x = p.x, y = p.y, z = p.z, home = home };
            if (home) { f.vx = 0; f.vy = 3; f.vz = 2.5f; f.life = f.max = 2.2f; }
            else { f.vx = Mathf.Cos(a) * 9; f.vy = R(-2, 2); f.vz = Mathf.Abs(Mathf.Sin(a)) * 6 + 2; f.life = f.max = 1.4f; f.spin = 800; }
            flying.Add(f);
        }

        /// <summary>魂に触れた：乗車。乗せていた客がいれば飛ばされ、お詫び料を払う。向きはお客さんしだい。</summary>
        void Board(Soul s)
        {
            s.gone = true;
            Burst(new Vector3(s.x, s.y, s.z), new Color(0.62f, 0.9f, 1f), 20, 3.5f);
            if (carrying)
            {
                LaunchFlying(new Vector3(px, y + 1, pz), false);
                thrown++; sfx.Play(sfx.swap);
                int fine = Mathf.Min(swapFine, purse);
                purse -= fine; mon -= swapFine; SaveProfile();
                fineFlash = 1.2f;
                Say($"「ひゃあっ！」乗っていたお客さんが飛ばされた！　お詫び料 −{swapFine}文", 3f);
            }
            else sfx.Play(sfx.catchSilk);
            carrying = true; steed.rider.SetActive(true); rideHits = 0;
            var guest = s.guest ?? GUESTS[0];
            ModelFactory.SetSoulRare(steed.rider, guest.rare);
            steed.rider.transform.localScale = Vector3.one * (guest.id == "sumo" ? 0.7f : 0.5f);

            float roomUp = L - 45 - y, roomDown = y - 45;
            var can = new List<int>();
            if (roomUp > 220) can.Add(1);
            if (roomDown > 220) can.Add(-1);
            int nd = can.Count == 2 ? (Random.value < 0.5f ? want : -want) : can.Count == 1 ? can[0] : -want;

            float gy;
            if (nd < 0) { float hi = y - 200; if (hi < 50) hi = y - 40; gy = R(Mathf.Max(45, y - 600), Mathf.Max(46, hi)); }
            else { float lo = y + 200; if (lo > L - 50) lo = y + 40; gy = R(Mathf.Min(L - 46, lo), Mathf.Min(L - 45, y + 600)); }
            var zone = STOPS[Mathf.Clamp(gy / L < 0.2f ? 0 : gy / L < 0.45f ? 1 : gy / L < 0.7f ? 2 : 3, 0, 3)];
            int jo = Mathf.RoundToInt(Mathf.Abs(gy - y) / 10);              // 乗車距離（丈）
            int baseFare = fare + jo / 10 * farePer10Jo;
            int f = Mathf.RoundToInt(baseFare * guest.mul * (luck != null ? luck.fareMul : 1f)) + (Lv("neko") > 0 ? 2 : 0);
            goal = new Goal { y = gy, goalName = zone[Random.Range(0, zone.Length)], jo = jo, fare = f, guest = guest };
            gateLabel.text = "行き先\n" + goal.goalName;

            bool turned = nd != want;
            want = nd;
            string star = guest.rare == 1 ? "★珍しいお客さん★ " : guest.rare == 2 ? "★★とても珍しいお客さん★★ " : "";
            Say($"{star}{guest.name}「{guest.hello}」", 2.8f);
            Say($"「{goal.goalName}まで、お願いします」　{(turned ? "折り返して" : "このまま")}{(nd > 0 ? "上へ" : "下へ")}　{jo}丈・運賃 {(guest.tanuki ? "？？" : f.ToString())}文", 3.6f);
            if (flags.Add("boardTip"))
            {
                Say("そのまま進むか、折り返すかは、お客さんしだい。", 3f);
                Say("乗せている間は、ほかの魂に触れないように。", 3.6f);
            }
        }

        /// <summary>行き先に着いた：運賃（距離・お客さん・おみくじ）と心づけをもらって折り返す。</summary>
        void Deliver()
        {
            // 心づけ：乗車中に一度も当たらなければ（蓮の座布団があれば1回までは大丈夫）
            bool safe = rideHits <= (Lv("lotus") > 0 ? 1 : 0);
            int tip = safe ? Mathf.RoundToInt(3 * (luck != null ? luck.tipMul : 1f)) : 0;
            var guest = goal != null && goal.guest != null ? goal.guest : GUESTS[0];
            int f = goal != null ? goal.fare : fare, jo = goal != null ? goal.jo : 0;
            string tanukiNote = "";
            if (guest.tanuki)   // 化け狸：半分の確率で木の葉、半分で3倍
            {
                if (Random.value < 0.5f) { f = 0; tanukiNote = "……と思ったら木の葉だった！"; }
                else { f *= 3; tanukiNote = "本物の小判だ！ 3倍！"; }
            }
            delivered++; mon += f + tip; purse += f + tip;
            bool first = BookCount(guest.id) == 0;
            AddBook(guest.id); SaveProfile();
            water = guest.fill ? MaxWater : Mathf.Min(MaxWater, water + 15);
            LaunchFlying(new Vector3(px, y + 1, pz), true);
            Burst(new Vector3(px, y, pz), new Color(1f, 0.82f, 0.48f), 36, 5f);
            carrying = false; steed.rider.SetActive(false); goal = null;
            want = -want;
            sfx.Play(sfx.bell);
            Say($"{guest.name}「{guest.thanks}」{tanukiNote}", 3f);
            Say($"{jo}丈で {f}文{(tip > 0 ? $"、心づけ {tip}文" : "")}　+{f + tip}文　水分 {(guest.fill ? "満タン" : "+15")}", 3f);
            if (first) Say($"お客さん図鑑に「{guest.name}」が載った！（{BookSeen()} / {GUESTS.Length}）", 3f);
            if (flags.Add("backTip")) Say("折り返して、来た道の魂を拾いに行こう。", 3.2f);
            if (delivered >= goalCount)
            {
                // ノルマ達成ボーナス：基本＋残り時間10秒ごとに1文＋おみくじ
                bonus = quotaBonus + Mathf.FloorToInt(Mathf.Max(0, timeLeft) / 10) + (luck != null ? luck.bonus : 0);
                purse += bonus; SaveProfile();
                clearT = 0.001f;
                Say($"ノルマ達成！ ボーナス +{bonus}文（残り時間ぶん +{bonus - quotaBonus}文）", 3.2f);
            }
        }

        void TurnAround(bool top)
        {
            want = -want;
            if (top)
            {
                water = Mathf.Min(MaxWater, water + 20);
                Say("現世の井戸の口。お供えの水をもらって折り返す。　水分 +20", 3.2f); sfx.Play(sfx.bell);
            }
            else
            {
                Say($"{CoName()}タクシー地獄営業所に戻ってきた。折り返す。", 2.6f);
                if (clearT == 0) OpenShop(Mode.Play);   // 営業所の売店に寄れる
            }
        }

        /// <summary>実際に進む向きが変わった瞬間。飛んでいる弾を片づけ、敵の状態を戻す。</summary>
        void Flip(int nd)
        {
            dir = nd;
            ClearChasers(); chaseCD = 2;
            if (nd < 0 && chaseMode && flags.Add("chaseTip"))
            {
                Say("追われるモード：下りは、後ろ（奥）から敵が迫ってくる。", 3.4f);
                Say("左クリックで、後ろへ塩を撒ける。", 3.2f);
            }
            foreach (var s in shots) Destroy(s.go); shots.Clear();
            foreach (var s in salts) Destroy(s.go); salts.Clear();
            air.Clear();
            foreach (var e in enemies) { e.bumped = false; e.emerged = false; e.called = false; }
            foreach (var s in souls) { s.emerged = false; s.called = false; }
            if (nd < 0 && flags.Add("downTip"))
            {
                Say("下りは塩が撒けない。避けて進もう。糸で受けることはできる。", 3.8f);
                Say(hint == Hint.Mirror ? "手前から魂や敵が来る。上のバックミラーに映る。"
                  : hint == Hint.Both ? "手前から魂や敵が来る。地獄の火の影と、バックミラーで分かる。"
                  : hint == Hint.Ring ? "手前から魂や敵が来る。光る輪が、抜けてくる場所の合図。"
                  : "手前から魂や敵が来る。地獄の火に照らされた影が、通り道に映る。", 4f);
            }
        }

        void Hurt(float amount)
        {
            if (inv > 0 || over) return;
            if (charms > 0)   // 厄除けのお守りが1回だけ防ぐ
            {
                charms--; SaveProfile();
                inv = 1.0f; Burst(new Vector3(px, y, pz), new Color(1f, 0.88f, 0.54f), 22, 3f); sfx.Play(sfx.catchSilk);
                Say($"厄除けのお守りが守ってくれた。（のこり {charms}）", 2.4f);
                return;
            }
            rideHits++;
            water -= amount; inv = 1.3f; hurtT = 0.35f; shake = 0.3f;
            Burst(new Vector3(px, y, pz), new Color(0.42f, 0.77f, 0.91f), 14, 3f);
            sfx.Play(sfx.hurt);
        }

        void Burst(Vector3 p, Color c, int n, float sp)
        {
            int made = 0;
            foreach (var s in sparks)
            {
                if (made >= n) break;
                if (s.life > 0) continue;
                s.p = p; s.v = Random.onUnitSphere * R(0.4f, 1f) * sp; s.life = s.max = R(0.4f, 1f); s.c = c;
                s.tr.gameObject.SetActive(true); made++;
            }
        }

        /// <summary>火花を1つだけ出す（線香花火の跡など）。</summary>
        void Spark1(Vector3 p, Vector3 v, float life, Color c)
        {
            foreach (var s in sparks)
            {
                if (s.life > 0) continue;
                s.p = p; s.v = v; s.life = s.max = life; s.c = c;
                s.tr.gameObject.SetActive(true);
                return;
            }
        }
    }
}
