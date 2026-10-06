using System.Collections.Generic;
using UnityEngine;

namespace ShoryoumaTaxi
{
    /// <summary>
    /// 精霊馬タクシーの本体。空のシーンの空オブジェクトにこれを1つ付ければ遊べる。
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
        public int fare = 6;
        [Tooltip("上り（きゅうり）の速さ")] public float speedUp = 16f;
        [Tooltip("下り（ナス）の速さ")] public float speedDown = 10f;
        [Tooltip("塩1発で減る水分")] public float saltCost = 0.7f;

        [Header("カメラ")]
        public float camBackUp = 7f;
        public float camBackDown = 34f;
        public float fovUp = 66f;
        public float fovDown = 44f;

        [Header("操作")]
        [Tooltip("オンにするとマウスの位置に馬がついていく")] public bool mouseFollow = false;

        const float CAMF = 0.5f, CAM_UP = 1.6f, WARN_RANGE = 34f, WEB_R = 1.9f;

        struct Msg { public string text; public float dur, t0; }

        static readonly string[][] STOPS = {
            new[] { "閻魔庁前", "血の池地獄", "針の山ふもと" },
            new[] { "三途の川 渡し場", "賽の河原", "奪衣婆の茶屋" },
            new[] { "雲の上の停留所", "雷さまの太鼓前", "天の浮橋" },
            new[] { "迎え火の家", "盆踊りのやぐら", "縁側の仏壇" },
        };

        // ===================== 状態 =====================
        enum Mode { Title, Play, Pause, Result }
        Mode mode = Mode.Title;

        float L;
        float t, y, yPrev, vel, bk, px, pz, water, silk, cool, inv, soulCD, hurtT, webGlow, shake, step, clearT, overT, shrink = 1f;
        int dir = 1, want = 1, kills, fired, caught, delivered, mon, thrown, lastStep;
        bool silkLock, guarding, over, carrying;
        Goal goal;
        readonly List<Enemy> enemies = new List<Enemy>();
        readonly List<Soul> souls = new List<Soul>();
        readonly List<Shot> shots = new List<Shot>();
        readonly List<Salt> salts = new List<Salt>();
        readonly List<Flying> flying = new List<Flying>();
        readonly Queue<Msg> msgQ = new Queue<Msg>();
        Msg? msg;
        readonly HashSet<string> flags = new HashSet<string>();
        bool resultClear; string resultTitle, resultText;

        // ===================== シーン部品 =====================
        Camera cam;
        World world;
        Sfx sfx;
        ModelFactory.Steed steed;
        GameObject gate; TextMesh gateLabel;
        GameObject web; MaterialPropertyBlock mpb;
        LineRenderer thread;
        Mesh airMesh; Vector3[] airV; Color[] airC; readonly List<Vector4> air = new List<Vector4>(); // x,y,z,vy
        readonly List<Spark> sparks = new List<Spark>();
        readonly List<GameObject> warnRings = new List<GameObject>();
        readonly List<GameObject> reticles = new List<GameObject>();
        readonly List<(GameObject go, float life)> hoof = new List<(GameObject, float)>();
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

            cam = Camera.main;
            if (cam == null) { var cg = new GameObject("Main Camera"); cg.tag = "MainCamera"; cam = cg.AddComponent<Camera>(); cg.AddComponent<AudioListener>(); }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 320f;
            cam.fieldOfView = fovUp;

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
            gate = ModelFactory.Gate(out gateLabel, jpFont); gate.SetActive(false); gateLabel.gameObject.SetActive(false);

            var vc = Mats.VertexColor();
            web = LineObj("web", WebMesh(), vc);
            web.SetActive(false);
            for (int i = 0; i < 16; i++) { var w = LineObj("warn", MeshKit.CircleLines(1f, 40, Color.white), vc); w.SetActive(false); warnRings.Add(w); }
            reticles.Add(LineObj("reticle", MeshKit.CircleLines(0.46f, 32, Color.white), vc));
            reticles.Add(LineObj("reticle", MeshKit.CircleLines(0.64f, 32, Color.white), vc));
            for (int i = 0; i < 10; i++) { var h = LineObj("hoof", MeshKit.CircleLines(0.56f, 32, new Color(0.72f, 1f, 0.56f)), vc); h.SetActive(false); hoof.Add((h, 0f)); }

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
            for (int i = 0; i < 260; i++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(s.GetComponent<Collider>());
                s.GetComponent<Renderer>().sharedMaterial = sparkMat; s.SetActive(false);
                sparks.Add(new Spark { tr = s.transform, rd = s.GetComponent<Renderer>() });
            }

            NewGame();
            FitField();
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

        // ============================================================
        //  ゲームの開始・終了
        // ============================================================
        void NewGame()
        {
            foreach (var e in enemies) Destroy(e.go); enemies.Clear();
            foreach (var s in souls) Destroy(s.go); souls.Clear();
            foreach (var s in shots) Destroy(s.go); shots.Clear();
            foreach (var s in salts) Destroy(s.go); salts.Clear();
            foreach (var f in flying) Destroy(f.go); flying.Clear();
            air.Clear(); msgQ.Clear(); msg = null; flags.Clear();

            t = 0; y = yPrev = 30; vel = 0; dir = want = 1; bk = 0; px = 0; pz = 0.6f;
            water = 100; silk = 100; silkLock = guarding = over = carrying = false; cool = inv = 0; soulCD = 1;
            kills = fired = caught = delivered = mon = thrown = 0; hurtT = webGlow = shake = step = clearT = overT = 0; shrink = 1; lastStep = 0;
            goal = null;
            steed.rider.SetActive(false); gate.SetActive(false); gateLabel.gameObject.SetActive(false);
            MakeEnemies();
        }

        void StartGame()
        {
            NewGame(); mode = Mode.Play;
            cool = 0.5f;   // 開始ボタンのクリックで塩が出ないように
            Say("精霊馬タクシー、営業開始。", 2.6f);
            Say("青く光る魂がお客さん。触れると乗せられる。", 3.4f);
            Say("左クリックで塩。撃つほど、馬は干からびる。", 3.4f);
        }

        void Finish(bool clear)
        {
            mode = Mode.Result; resultClear = clear;
            if (!clear)
            {
                resultTitle = "干からびた";
                resultText = carrying && goal != null
                    ? $"お客さんを乗せたまま、干からびてしまった。\n{goal.goalName}まで、あと {Mathf.CeilToInt(Mathf.Abs(goal.y - y) / 10)} 丈だった。"
                    : $"精霊馬は干からびてしまった。\n送り届けた魂は {delivered} 人。";
            }
            else
            {
                resultTitle = "本日の営業終了";
                resultText = thrown == 0
                    ? $"ひとりも落とさずに、{goalCount} 人の魂を送り届けた。\n{mon} 文の稼ぎ。また来年のお盆に。"
                    : $"{goalCount} 人の魂を送り届けた。\n途中で飛ばしてしまったお客さんが {thrown} 人。\nまた来年のお盆に。";
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
                    if (type == "zakuro") { e.go = ModelFactory.Zakuro(zakuroPrefab, out e.body, out e.arils); e.hp = 2; e.r = 1.0f; e.shooter = Random.value < 0.5f; }
                    else if (type == "hozuki") { e.go = ModelFactory.Hozuki(hozukiPrefab); e.hp = 1; e.r = 0.9f; e.shooter = true; }
                    else { e.go = ModelFactory.Tongue(tonguePrefab); e.hp = 5; e.r = 1.5f; e.shooter = true; }
                    e.max = e.hp;
                    e.go.SetActive(false);
                    enemies.Add(e);
                }
            }
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
            var s = new Soul { go = ModelFactory.Soul(soulPrefab), y0 = yy, y = yy, yPrev = yy, nx = Mathf.Cos(a) * r, nz = Mathf.Sin(a) * r, ph = R(0, 6) };
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

        /// <summary>魂に触れた：乗車。乗せていた客がいれば飛ばされる。向きはお客さんしだい。</summary>
        void Board(Soul s)
        {
            s.gone = true;
            Burst(new Vector3(s.x, s.y, s.z), new Color(0.62f, 0.9f, 1f), 20, 3.5f);
            if (carrying)
            {
                LaunchFlying(new Vector3(px, y + 1, pz), false);
                thrown++; sfx.Play(sfx.swap);
                Say("「ひゃあっ！」乗っていたお客さんが飛ばされた！", 2.8f);
            }
            else sfx.Play(sfx.catchSilk);
            carrying = true; steed.rider.SetActive(true);

            float roomUp = L - 45 - y, roomDown = y - 45;
            var can = new List<int>();
            if (roomUp > 220) can.Add(1);
            if (roomDown > 220) can.Add(-1);
            int nd = can.Count == 2 ? (Random.value < 0.5f ? want : -want) : can.Count == 1 ? can[0] : -want;

            float gy;
            if (nd < 0) { float hi = y - 200; if (hi < 50) hi = y - 40; gy = R(Mathf.Max(45, y - 600), Mathf.Max(46, hi)); }
            else { float lo = y + 200; if (lo > L - 50) lo = y + 40; gy = R(Mathf.Min(L - 46, lo), Mathf.Min(L - 45, y + 600)); }
            var zone = STOPS[Mathf.Clamp(gy / L < 0.2f ? 0 : gy / L < 0.45f ? 1 : gy / L < 0.7f ? 2 : 3, 0, 3)];
            goal = new Goal { y = gy, name = zone[Random.Range(0, zone.Length)] };
            gateLabel.text = "行き先\n" + goal.goalName;

            bool turned = nd != want;
            want = nd;
            Say($"「{goal.goalName}まで、お願いします」 {(turned ? "折り返して" : "このまま")}{(nd > 0 ? "上へ" : "下へ")}", 3.4f);
            if (flags.Add("boardTip"))
            {
                Say("そのまま進むか、折り返すかは、お客さんしだい。", 3f);
                Say("乗せている間は、ほかの魂に触れないように。", 3.6f);
            }
        }

        /// <summary>行き先に着いた：運賃をもらって折り返す。</summary>
        void Deliver()
        {
            delivered++; mon += fare;
            water = Mathf.Min(100, water + 15);
            LaunchFlying(new Vector3(px, y + 1, pz), true);
            Burst(new Vector3(px, y, pz), new Color(1f, 0.82f, 0.48f), 36, 5f);
            carrying = false; steed.rider.SetActive(false); goal = null;
            want = -want;
            sfx.Play(sfx.bell);
            Say("「ありがとう。六文、置いていくよ」 水分 +15", 3f);
            if (flags.Add("backTip")) Say("折り返して、来た道の魂を拾いに行こう。", 3.2f);
            if (delivered >= goalCount) { clearT = 0.001f; Say("今夜の営業、おしまい。", 3f); }
        }

        void TurnAround(bool top)
        {
            want = -want;
            if (top) { water = Mathf.Min(100, water + 20); Say("現世の井戸の口。お供えの水をもらって折り返す。 水分 +20", 3.2f); sfx.Play(sfx.bell); }
            else Say("地獄の門だ。折り返す。", 2.6f);
        }

        /// <summary>実際に進む向きが変わった瞬間。飛んでいる弾を片づけ、敵の状態を戻す。</summary>
        void Flip(int nd)
        {
            dir = nd;
            foreach (var s in shots) Destroy(s.go); shots.Clear();
            foreach (var s in salts) Destroy(s.go); salts.Clear();
            air.Clear();
            foreach (var e in enemies) { e.bumped = false; e.emerged = false; }
            foreach (var s in souls) s.emerged = false;
            if (nd < 0 && flags.Add("downTip"))
            {
                Say("下りは塩が撒けない。避けて進もう。糸で受けることはできる。", 3.8f);
                Say("手前から魂や敵が来る。光る輪が、抜けてくる場所の合図。", 4f);
            }
        }

        void Hurt(float amount)
        {
            if (inv > 0 || over) return;
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
    }
}
