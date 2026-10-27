using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;   // System.Random と取り違えないように

namespace ShoryoumaTaxi
{
    /// <summary>
    /// v19〜v32 で増えた仮モデル（唐傘お化け・提灯お化け・立体のアイテム・親玉・鬼の手・車掌・行き先の門）。
    /// 寸法は Web 版（three.js）と同じ。Unity の球（半径0.5）・円柱（高さ2）に合わせて大きさを換算している。
    /// </summary>
    public static partial class ModelFactory
    {
        // three.js の半径1の球 → Unity の球（直径1）
        static GameObject Sph(Transform p, Vector3 pos, Vector3 radius, Material m, Vector3? euler = null) => Prim(PrimitiveType.Sphere, p, pos, radius * 2f, m, euler);
        // three.js の CylinderGeometry(r, r, h) → Unity の円柱（半径0.5・高さ2）
        static GameObject Cyl(Transform p, Vector3 pos, float r, float h, Material m, Vector3? euler = null) => Prim(PrimitiveType.Cylinder, p, pos, new Vector3(r * 2f, h / 2f, r * 2f), m, euler);
        static GameObject Box(Transform p, Vector3 pos, Vector3 size, Material m, Vector3? euler = null) => Prim(PrimitiveType.Cube, p, pos, size, m, euler);
        static GameObject ConeObj(Transform p, Vector3 pos, float r, float h, int seg, Material m, Vector3? euler = null) => MeshObj("cone", MeshKit.ConeC(r, h, seg), p, m, pos, Vector3.one, euler);
        static Transform Group(string name, Transform parent, Vector3 pos, Vector3? euler = null)
        {
            var g = new GameObject(name).transform; g.SetParent(parent, false); g.localPosition = pos;
            if (euler.HasValue) g.localEulerAngles = euler.Value;
            return g;
        }
        public static Color Hex(int h) => new Color(((h >> 16) & 255) / 255f, ((h >> 8) & 255) / 255f, (h & 255) / 255f);

        static readonly Dictionary<Color, Material> glowCache = new Dictionary<Color, Material>();
        /// <summary>同じ色のグローは同じマテリアルを使う（色・透明度を毎フレーム変えるものは MaterialPropertyBlock で）。</summary>
        public static Material GlowMatC(Color c)
        {
            if (!glowCache.TryGetValue(c, out var m)) { m = GlowMat(c); glowCache[c] = m; }
            return m;
        }
        /// <summary>three.js の glowSprite(色, 大きさ, 透明度) に当たる光る板。親の XY 平面に置く。</summary>
        public static GameObject GlowQ(Transform parent, int hex, float size, float opacity, Vector3 pos)
        {
            var c = Hex(hex); c.a = opacity;
            return Glow(parent, GlowMatC(c), size, pos);
        }
        /// <summary>XZ 平面（道の断面）に寝かせた光る板。カメラは道の上を見上げるので、いつもこちらを向く。</summary>
        public static GameObject GlowFlat(Transform parent, int hex, float size, float opacity, Vector3 pos)
        {
            var g = GlowQ(parent, hex, size, opacity, pos); g.transform.localEulerAngles = new Vector3(90, 0, 0);
            return g;
        }

        static Material litCache(Dictionary<string, Material> d, int hex, float sm = 0.4f, int emi = -1, float ek = 1f)
        {
            string key = hex + "_" + sm + "_" + emi + "_" + ek;
            if (!d.TryGetValue(key, out var m)) { m = emi >= 0 ? Mats.Lit(Hex(hex), sm, Hex(emi) * ek) : Mats.Lit(Hex(hex), sm); d[key] = m; }
            return m;
        }
        static readonly Dictionary<string, Material> lits = new Dictionary<string, Material>();
        public static Material Std(int hex, float smooth = 0.4f) => litCache(lits, hex, smooth);
        public static Material StdE(int hex, int emissive, float k = 1f, float smooth = 0.4f) => litCache(lits, hex, smooth, emissive, k);

        public static Material Gold => StdE(0xffd060, 0x5a3a00, 1f, 0.7f);

        // ---------- 唐傘お化け：一つ目・一本足。赤と生成りの縞の傘 ----------
        public static GameObject Kasa()
        {
            var g = new GameObject("唐傘お化け"); var t = g.transform;
            var red = Std(0xd02e21, 0.4f); var cream = Std(0xf2dc9e, 0.4f);
            ConeObj(t, new Vector3(0, 0.3f, 0), 1.15f, 1.1f, 16, red);
            // 縞：傘の骨にそって生成りの帯
            for (int i = 0; i < 4; i++)
            {
                var rib = Group("rib", t, new Vector3(0, 0.3f, 0), new Vector3(0, i * 90f + 45f, 0));
                Box(rib, new Vector3(0.6f, 0.02f, 0), new Vector3(1.6f, 0.06f, 0.3f), cream, new Vector3(0, 0, -43.7f));
            }
            Cyl(t, new Vector3(0, 1.0f, 0), 0.07f, 0.4f, Std(0x3a1a10));
            Sph(t, new Vector3(0, 0.22f, 0.66f), new Vector3(0.3f, 0.26f, 0.12f), eyeWhite);
            Sph(t, new Vector3(0, 0.2f, 0.76f), Vector3.one * 0.12f, eyeDark);
            Box(t, new Vector3(0, -0.42f, 0.62f), new Vector3(0.24f, 0.55f, 0.06f), StdE(0xff4060, 0x40000c), new Vector3(17f, 0, 0));
            Cyl(t, new Vector3(0, -0.8f, 0), 0.07f, 1.1f, Std(0x6a4a2a));
            Box(t, new Vector3(0, -1.38f, 0), new Vector3(0.42f, 0.12f, 0.7f), Std(0x8a6a3a));
            return g;
        }

        // ---------- 提灯お化け：口を開けた提灯 ----------
        static Mesh chochinMesh;
        public static GameObject Chochin()
        {
            if (chochinMesh == null)
                chochinMesh = MeshKit.Lathe(new[] { new Vector2(0, -0.95f), new Vector2(0.55f, -0.9f), new Vector2(0.85f, -0.55f), new Vector2(0.95f, 0f),
                    new Vector2(0.85f, 0.55f), new Vector2(0.55f, 0.9f), new Vector2(0, 0.95f) }, 20, (a, y) => 1 + 0.045f * Mathf.Cos(y * 18));
            var g = new GameObject("提灯お化け"); var t = g.transform;
            MeshObj("body", chochinMesh, t, StdE(0xfff0d0, 0xff9a40, 0.6f, 0.2f), Vector3.zero, Vector3.one);
            foreach (float yy in new[] { -0.98f, 0.98f }) Cyl(t, new Vector3(0, yy, 0), 0.5f, 0.18f, Std(0x2a1a14));
            Eyes(t, 0.32f, 0.86f, 0.3f, 0.17f, true);
            Sph(t, new Vector3(0, -0.3f, 0.86f), new Vector3(0.5f, 0.26f, 0.12f), eyeDark);
            Box(t, new Vector3(0, -0.75f, 0.92f), new Vector3(0.26f, 0.7f, 0.06f), StdE(0xff3050, 0x400010));
            GlowQ(t, 0xffa040, 4.6f, 0.6f, new Vector3(0, 0, -0.4f));
            return g;
        }

        // ---------- 道中のアイテム（立体の小物） ----------
        public class ItemModel { public GameObject root; public Transform model, spin; public Renderer smoke; }

        public static ItemModel Item(string kind)
        {
            var im = new ItemModel();
            var g = new GameObject("item_" + kind); im.root = g;
            var m = new GameObject("model").transform; m.SetParent(g.transform, false); m.localScale = Vector3.one * 1.15f; im.model = m;
            switch (kind)
            {
                case "water":
                {
                    // お供えの水：ひと目で水と分かる、大きな青いしずく
                    var dropMat = StdE(0x3aa8ff, 0x1670d0, 0.75f, 0.95f);
                    MeshObj("drop", MeshKit.Lathe(new[] { new Vector2(0, -0.62f), new Vector2(0.26f, -0.56f), new Vector2(0.44f, -0.42f), new Vector2(0.53f, -0.18f),
                        new Vector2(0.5f, 0.06f), new Vector2(0.4f, 0.27f), new Vector2(0.26f, 0.5f), new Vector2(0.12f, 0.7f), new Vector2(0, 0.85f) }, 28), m, dropMat, Vector3.zero, Vector3.one);
                    var white = Mats.Unlit(Color.white);
                    Sph(m, new Vector3(-0.22f, 0.1f, 0.43f), new Vector3(0.09f, 0.2f, 0.05f), white, new Vector3(0, 0, 28.6f));
                    Sph(m, new Vector3(-0.3f, -0.18f, 0.42f), Vector3.one * 0.05f, white);
                    GlowQ(m, 0x9adcff, 1.6f, 0.8f, new Vector3(0, -0.1f, 0));
                    var orbit = Group("orbit", m, Vector3.zero); im.spin = orbit;
                    for (int i = 0; i < 3; i++) { float a = i / 3f * Mathf.PI * 2; Sph(orbit, new Vector3(Mathf.Cos(a) * 0.75f, -0.2f, Mathf.Sin(a) * 0.75f), Vector3.one * 0.09f, dropMat); }
                    break;
                }
                case "wind":
                {
                    // 追い風：風車
                    Cyl(m, new Vector3(0, -0.8f, 0), 0.05f, 1.6f, Std(0x8a6a3a));
                    var wheel = Group("wheel", m, Vector3.zero); im.spin = wheel;
                    int[] cols = { 0x7ce06a, 0xffffff, 0x7ce06a, 0xffe060 };
                    var blade = MeshKit.Tri(Vector3.zero, new Vector3(0.95f, 0.15f, 0), new Vector3(0.2f, 0.75f, 0));
                    for (int i = 0; i < 4; i++) MeshObj("blade", blade, wheel, StdE(cols[i], cols[i], 0.15f, 0.5f), Vector3.zero, Vector3.one, new Vector3(0, 0, i * 90f));
                    Sph(wheel, new Vector3(0, 0, 0.05f), Vector3.one * 0.12f, Std(0xd83a2a));
                    break;
                }
                case "incense":
                {
                    // 線香：香炉に立てた線香と煙
                    MeshObj("bowl", MeshKit.Lathe(new[] { new Vector2(0, -0.5f), new Vector2(0.55f, -0.45f), new Vector2(0.7f, -0.1f), new Vector2(0.6f, 0.05f), new Vector2(0, 0.05f) }, 20),
                        m, Std(0x6a5a40, 0.65f), Vector3.zero, Vector3.one);
                    for (int i = 0; i < 3; i++)
                    {
                        Cyl(m, new Vector3((i - 1) * 0.16f, 0.65f, 0), 0.03f, 1.3f, Std(0x3a6a3a), new Vector3(0, 0, (i - 1) * 6.9f));
                        GlowQ(m, 0xff7020, 0.45f, 1f, new Vector3((i - 1) * 0.24f, 1.3f, 0));
                    }
                    var smoke = GlowQ(m, 0xe8e0d0, 1.6f, 0.5f, new Vector3(0, 2f, 0));
                    smoke.transform.localScale = new Vector3(1.4f, 1.8f, 1f);
                    im.smoke = smoke.GetComponent<Renderer>();
                    break;
                }
                case "nasu":
                {
                    // ナス：つやつやの紫に緑のへた
                    var body = Group("body", m, Vector3.zero, new Vector3(0, 0, 20f));
                    MeshObj("nasu", MeshKit.Lathe(new[] { new Vector2(0, -0.7f), new Vector2(0.3f, -0.62f), new Vector2(0.42f, -0.4f), new Vector2(0.44f, -0.1f),
                        new Vector2(0.38f, 0.2f), new Vector2(0.3f, 0.42f), new Vector2(0.22f, 0.55f), new Vector2(0, 0.6f) }, 24), body, StdE(0x4a1e7a, 0x2a0a4a, 0.6f, 0.85f), Vector3.zero, Vector3.one);
                    ConeObj(m, new Vector3(-0.2f, 0.62f, 0), 0.3f, 0.3f, 6, Std(0x3f7a2a), new Vector3(0, 0, 20f + 180f));
                    Cyl(m, new Vector3(-0.28f, 0.85f, 0), 0.06f, 0.35f, Std(0x4a6a2a), new Vector3(0, 0, 20f));
                    Sph(m, new Vector3(-0.12f, -0.05f, 0.4f), new Vector3(0.06f, 0.22f, 0.04f), Mats.Unlit(Color.white), new Vector3(0, 0, 20f));
                    foreach (int sd in new[] { -1, 1 }) Cyl(m, new Vector3(sd * 0.2f, -0.75f, 0), 0.03f, 0.5f, Std(0xd8c090));
                    break;
                }
                default:
                {
                    // 厄除けのお守り：赤い袋と金の紐
                    var red = Std(0xc8242a, 0.4f);
                    Box(m, Vector3.zero, new Vector3(1.0f, 1.35f, 0.28f), red);
                    Cyl(m, new Vector3(0, 0.67f, 0), 0.5f, 0.28f, red, new Vector3(90f, 0, 0));
                    MeshObj("knot", MeshKit.Torus(0.16f, 0.05f, 24, 6), m, Gold, new Vector3(0, 1.0f, 0), Vector3.one, new Vector3(90f, 0, 0));
                    Box(m, new Vector3(0, 0, 0.15f), new Vector3(0.55f, 0.9f, 0.02f), Std(0xff8a8a));
                    break;
                }
            }
            return im;
        }

        // ---------- 親玉（ステージ3） ----------
        public class BossModel
        {
            public GameObject root, aura, shield, core, warn; public Renderer body, auraRd, shieldRd; public GameObject arils;
            public Transform halo; public Transform[] flames;
        }

        public static BossModel Boss(GameObject zakuroPrefab, Font font)
        {
            var bm = new BossModel();
            var g = new GameObject("鬼ザクロの親玉"); var t = g.transform; bm.root = g;
            var z = Zakuro(zakuroPrefab, out bm.body, out bm.arils); z.transform.SetParent(t, false); z.transform.localScale = Vector3.one * 4.6f;
            // 金の冠と宝玉
            var gold = StdE(0xffd060, 0x7a5200, 0.6f, 0.75f);
            MeshObj("band", MeshKit.Torus(1.9f, 0.3f, 28, 8), t, gold, new Vector3(0, 5.4f, 0), Vector3.one);
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2;
                ConeObj(t, new Vector3(Mathf.Cos(a) * 1.9f, i % 2 == 1 ? 6.2f : 6.7f, Mathf.Sin(a) * 1.9f), 0.42f, i % 2 == 1 ? 1.5f : 2.5f, 5, gold);
                Sph(t, new Vector3(Mathf.Cos(a) * 2.2f, 5.4f, Mathf.Sin(a) * 2.2f), Vector3.one * 0.24f, Mats.Unlit(Hex(i % 3 != 0 ? 0xff3050 : 0x60e0ff)));
            }
            // 光る目
            var eye = Mats.Unlit(Hex(0xffe040));
            foreach (int sd in new[] { -1, 1 })
            {
                Sph(t, new Vector3(sd * 1.3f, 0.48f, 4.6f), new Vector3(0.34f, 0.22f, 0.1f), eye);
                GlowQ(t, 0xffd040, 2.6f, 0.9f, new Vector3(sd * 1.3f, 0.48f, 4.9f));
            }
            // 火焔光背：背中に燃える炎の輪
            var halo = Group("halo", t, new Vector3(0, 0.8f, -2.8f)); bm.halo = halo;
            MeshObj("ring", MeshKit.Torus(6.3f, 0.4f, 56, 8), halo, Mats.Unlit(Hex(0xff3a10)), Vector3.zero, Vector3.one, new Vector3(90f, 0, 0));
            MeshObj("ring2", MeshKit.Torus(5.6f, 0.14f, 56, 6), halo, Mats.Unlit(Hex(0xffd060)), Vector3.zero, Vector3.one, new Vector3(90f, 0, 0));
            var fm1 = Mats.Unlit(Hex(0xff6a18)); var fm2 = Mats.Unlit(Hex(0xffc040));
            bm.flames = new Transform[20];
            for (int i = 0; i < 20; i++)
            {
                float a = i / 20f * Mathf.PI * 2;
                var pv = Group("flame", halo, new Vector3(Mathf.Cos(a) * 7.4f, Mathf.Sin(a) * 7.4f, 0), new Vector3(0, 0, a * Mathf.Rad2Deg - 90f));
                ConeObj(pv, Vector3.zero, 0.75f, i % 2 == 1 ? 2.2f : 3.2f, 6, i % 2 == 1 ? fm2 : fm1);
                bm.flames[i] = pv;
            }
            GlowQ(halo, 0xff5a20, 24f, 0.5f, new Vector3(0, 0, -0.3f));
            bm.aura = GlowQ(t, 0xff3a2a, 26f, 0.6f, new Vector3(0, 0, -3.5f)); bm.auraRd = bm.aura.GetComponent<Renderer>();
            bm.shield = GlowQ(t, 0x80c8ff, 20f, 0.45f, new Vector3(0, 0, 5.2f)); bm.shieldRd = bm.shield.GetComponent<Renderer>(); bm.shield.SetActive(false);
            // 弱点の核：ひるむと割れ目から金色に光って出てくる
            var core = Group("core", t, new Vector3(0, -1.4f, 4.4f)); bm.core = core.gameObject;
            Sph(core, Vector3.zero, Vector3.one * 0.9f, Mats.Unlit(Hex(0xfff0a0)));
            GlowQ(core, 0xffd040, 6f, 1f, new Vector3(0, 0, 0.5f));
            bm.core.SetActive(false);
            // 突進の合図：頭の上の「！」
            var warn = Group("warn", t, new Vector3(0, 10.5f, 0)); bm.warn = warn.gameObject;
            MeshObj("disc", MeshKit.Disc(1.76f, 32), warn, Mats.Unlit(Hex(0xff2a2a)), Vector3.zero, Vector3.one);
            MeshObj("rim", MeshKit.Ring(1.76f, 2.0f, 32), warn, Mats.Unlit(Color.white), Vector3.zero, Vector3.one);
            var tx = new GameObject("mark"); tx.transform.SetParent(warn, false); tx.transform.localPosition = new Vector3(0, 0, 0.1f);
            var tm = tx.AddComponent<TextMesh>(); tm.font = font; tm.fontSize = 64; tm.characterSize = 0.4f; tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = Color.white; tm.text = "！";
            tx.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            bm.warn.SetActive(false);
            return bm;
        }

        // ---------- 鬼の手 ----------
        public static GameObject Hand(int sd, out Renderer glow)
        {
            var g = new GameObject("鬼の手"); var t = g.transform;
            var hand = StdE(0xb02828, 0x2a0000, 1f, 0.4f); var nail = Std(0xf1e2c0, 0.6f);
            Box(t, Vector3.zero, new Vector3(2.2f, 2.3f, 0.9f), hand);
            for (int i = 0; i < 4; i++)
            {
                var f = Group("finger", t, new Vector3(-0.8f + i * 0.53f, 1.1f, 0), new Vector3(17f, 0, 0));
                Cyl(f, new Vector3(0, 0.7f, 0), 0.22f, 1.4f, hand);
                ConeObj(f, new Vector3(0, 1.6f, 0), 0.17f, 0.65f, 6, nail);
            }
            var th = Group("thumb", t, new Vector3(-sd * 1.1f, -0.1f, 0), new Vector3(0, 0, sd * 57.3f));
            Cyl(th, new Vector3(0, 0.6f, 0), 0.25f, 1.2f, hand);
            ConeObj(th, new Vector3(0, 1.4f, 0), 0.2f, 0.6f, 6, nail);
            MeshObj("cuff", MeshKit.Torus(1.15f, 0.22f, 20, 6), t, Gold, new Vector3(0, -1.3f, 0), Vector3.one);
            var gl = GlowQ(t, 0xff3020, 6f, 0.45f, new Vector3(0, 0, -0.6f));
            glow = gl.GetComponent<Renderer>();
            return g;
        }

        // ---------- 車掌（Blender のモデルがないときの仮のモデル） ----------
        public class ConductorModel { public Transform body, head; public Transform[] arms; }

        public static ConductorModel Conductor(Transform parent)
        {
            var cm = new ConductorModel();
            var navy = Std(0x1c2a4a, 0.4f); var dark = Std(0x10141e, 0.5f); var skin = Std(0xf0e6d6, 0.3f); var white = Std(0xf8f4ea, 0.4f);
            var g = Group("車掌", parent, Vector3.zero); cm.body = g;
            Cyl(g, new Vector3(0, 0.3f, 0), 0.2f, 0.46f, navy);
            for (int i = 0; i < 3; i++) Sph(g, new Vector3(0, 0.42f - i * 0.1f, 0.19f), Vector3.one * 0.025f, Gold);
            Cyl(g, new Vector3(0, 0.55f, 0), 0.13f, 0.06f, white);
            foreach (int sd in new[] { -1, 1 })
            {
                Cyl(g, new Vector3(sd * 0.2f, 0f, 0.1f), 0.058f, 0.42f, navy, new Vector3(28.6f, 0, sd * 31.5f));
                Box(g, new Vector3(sd * 0.32f, -0.17f, 0.2f), new Vector3(0.1f, 0.08f, 0.17f), dark);
            }
            cm.arms = new Transform[2];
            for (int k = 0; k < 2; k++)
            {
                int sd = k == 0 ? -1 : 1;
                var arm = Group("arm", g, new Vector3(sd * 0.2f, 0.47f, 0.02f), new Vector3(-65.9f, 0, sd * 6.9f));
                Cyl(arm, new Vector3(0, -0.15f, 0), 0.048f, 0.34f, navy);
                Sph(arm, new Vector3(0, -0.34f, 0), Vector3.one * 0.055f, white);
                cm.arms[k] = arm;
            }
            var head = Group("head", g, new Vector3(0, 0.72f, 0)); cm.head = head;
            Sph(head, Vector3.zero, new Vector3(0.15f, 0.16f, 0.15f), skin);
            foreach (int sd in new[] { -1, 1 }) Sph(head, new Vector3(sd * 0.055f, 0f, 0.135f), new Vector3(0.022f, 0.03f, 0.02f), eyeDark);
            Cyl(head, new Vector3(0, 0.12f, 0), 0.16f, 0.1f, navy);
            Cyl(head, new Vector3(0, 0.08f, 0), 0.152f, 0.035f, dark);
            Box(head, new Vector3(0, 0.07f, 0.12f), new Vector3(0.26f, 0.015f, 0.14f), dark, new Vector3(8.6f, 0, 0));
            Sph(head, new Vector3(0, 0.12f, 0.17f), new Vector3(0.035f, 0.035f, 0.012f), Gold);
            return cm;
        }

        // ---------- 行き先の門（乗っているお客さん1人に1つ） ----------
        public static GameObject DestGate(out Renderer disc)
        {
            var g = new GameObject("行き先の門"); var t = g.transform;
            MeshObj("ring", MeshKit.Torus(7.4f, 0.34f, 64, 8), t, Mats.Unlit(Hex(0xd8342a)), Vector3.zero, Vector3.one);
            MeshObj("ring2", MeshKit.Torus(6.7f, 0.12f, 64, 6), t, Mats.Unlit(Hex(0xffd27a)), Vector3.zero, Vector3.one);
            var dm = Mats.VertexColor(); dm.SetColor("_Color", new Color(1f, 0.75f, 0.44f, 0.12f));
            var d = MeshObj("disc", MeshKit.Disc(7.3f, 48), t, dm, Vector3.zero, Vector3.one, new Vector3(90f, 0, 0));
            disc = d.GetComponent<Renderer>();
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2;
                GlowFlat(t, 0xffa040, 2.4f, 0.95f, new Vector3(Mathf.Cos(a) * 7.4f, 0, Mathf.Sin(a) * 7.4f));
            }
            return g;
        }

        // ---------- 文字の札（行き先・お客さんの札・アイテムの名前） ----------
        public class Plate
        {
            public GameObject root; public TextMesh top, main; public Material bg, edge; public string text;
            public Color topCol = Color.white, mainCol = Color.white, edgeCol = Color.white; public float bgA = 0.82f;
            public void SetAlpha(float a)
            {
                if (top != null) top.color = new Color(topCol.r, topCol.g, topCol.b, a);
                if (main != null) main.color = new Color(mainCol.r, mainCol.g, mainCol.b, a);
                bg.color = new Color(0.075f, 0.067f, 0.106f, bgA * a);
                edge.color = new Color(edgeCol.r, edgeCol.g, edgeCol.b, a);
            }
        }

        static Shader overlayShader;
        /// <summary>いつも手前に描く単色の板（three.js の depthTest:false の札に当たる）。</summary>
        public static Material OverlayMat(Color c, int queue)
        {
            if (overlayShader == null) overlayShader = Shader.Find("GUI/Text Shader");
            if (overlayShader == null) overlayShader = Shader.Find("Sprites/Default");
            var m = new Material(overlayShader) { mainTexture = Texture2D.whiteTexture, color = c, renderQueue = queue };
            return m;
        }

        static TextMesh Text(Transform p, Font font, Vector3 pos, float height, Color c)
        {
            var g = new GameObject("text"); g.transform.SetParent(p, false); g.transform.localPosition = pos;
            var tm = g.AddComponent<TextMesh>();
            tm.font = font; tm.fontSize = 64; tm.characterSize = height / 6.4f;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = c;
            var r = g.GetComponent<MeshRenderer>(); r.sharedMaterial = font.material;
            return tm;
        }

        /// <summary>札を作る。大きさは幅 w・高さ h（ワールド単位、拡大する前）。top が空なら1行だけ。</summary>
        public static Plate MakeLabel(Font font, float w, float h, string top, Color topCol, Color mainCol, Color edgeCol, float topH, float mainH)
        {
            if (font.material.renderQueue < 3102) font.material.renderQueue = 3102;
            var lb = new Plate { topCol = topCol, mainCol = mainCol, edgeCol = edgeCol };
            var g = new GameObject("label"); lb.root = g; var t = g.transform;
            lb.edge = OverlayMat(edgeCol, 3100); lb.bg = OverlayMat(new Color(0.075f, 0.067f, 0.106f, 0.82f), 3101);
            Prim(PrimitiveType.Quad, t, new Vector3(0, 0, 0.02f), new Vector3(w, h, 1), lb.edge);
            Prim(PrimitiveType.Quad, t, new Vector3(0, 0, 0.01f), new Vector3(w - h * 0.1f, h * 0.9f, 1), lb.bg);
            if (!string.IsNullOrEmpty(top))
            {
                lb.top = Text(t, font, new Vector3(0, h * 0.22f, 0), topH, topCol); lb.top.text = top;
                lb.main = Text(t, font, new Vector3(0, -h * 0.16f, 0), mainH, mainCol);
            }
            else lb.main = Text(t, font, Vector3.zero, mainH, mainCol);
            g.SetActive(false);
            return lb;
        }

        // ---------- 影の形：v32 の妖怪 ----------
        static bool InShapeExtra(string type, float x, float y, out bool hit)
        {
            bool Ell(float cx, float cy, float rx, float ry) { float a = (x - cx) / rx, b = (y - cy) / ry; return a * a + b * b <= 1; }
            hit = false;
            if (type == "kasa")
            {
                // 傘（三角）と一本足
                bool umb = y >= 28 && y <= 70 && Mathf.Abs(x - 64) <= (y - 28) / 42f * 46f;
                hit = umb || (x >= 60 && x <= 68 && y > 70 && y <= 112) || (y >= 108 && y <= 116 && Mathf.Abs(x - 64) <= 14);
                return true;
            }
            if (type == "chochin") { hit = Ell(64, 66, 34, 40) || (Mathf.Abs(x - 64) <= 18 && (Mathf.Abs(y - 24) <= 5 || Mathf.Abs(y - 108) <= 5)); return true; }
            return false;
        }
    }
}
