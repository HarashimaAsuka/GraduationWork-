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

        PlayerController playerController;
        ProjectileManager projectileManager;

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
        List<Shot> shots => projectileManager.Shots;
        List<Salt> salts => projectileManager.Salts;
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
            projectileManager = new ProjectileManager();
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
            playerController = new PlayerController(steed);
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
            
            projectileManager.ClearAll();

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

        /// <summary>実際に進む向きが変わった瞬間。飛んでいる弾を片づけ、敵の状態を戻す。</summary>
        void Flip(int nd)
        {
            dir = nd;
            projectileManager.ClearAll();

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
