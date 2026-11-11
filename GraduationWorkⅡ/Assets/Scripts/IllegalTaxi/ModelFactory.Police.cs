using UnityEngine;

namespace ShoryoumaTaxi
{
    /// <summary>
    /// v42「違法タクシー」で増えた仮モデル：鬼の巡査・パト提灯・大警部・白手袋・手錠・小銭。
    /// 寸法は Web 版（three.js）と同じ。正面は +Z（カメラの方を向けて使う）。
    /// </summary>
    public static partial class ModelFactory
    {
        public class PoliceModel { public GameObject root; public GameObject[] siren = new GameObject[0]; public Transform face, mouth; public GameObject core; }

        static Material PCap => StdE(0x22336a, 0x0a1030, 1f, 0.5f);
        static Material PVisor => Std(0x0c0f1c, 0.7f);
        static Material PBadge => StdE(0xffd060, 0x8a6a10, 1f, 0.75f);
        static Material Oni => StdE(0xd8302e, 0x5a0c0a, 0.6f, 0.5f);
        static Material OniHorn => StdE(0xf1e2c0, 0x302820, 1f, 0.5f);

        /// <summary>制帽（size = 大きさ）。y は帽子の中心の高さ。</summary>
        static void PoliceCap(Transform t, float y, float s)
        {
            Prim(PrimitiveType.Cylinder, t, new Vector3(0, y, 0), new Vector3(1.15f * s, 0.21f * s, 1.15f * s), PCap);
            Cyl(t, new Vector3(0, y - 0.2f * s, 0), 0.52f * s, 0.14f * s, PVisor);
            Box(t, new Vector3(0, y - 0.26f * s, 0.45f * s), new Vector3(0.9f * s, 0.05f * s, 0.45f * s), PVisor);
            Box(t, new Vector3(0, y + 0.02f * s, 0.6f * s), new Vector3(0.28f * s, 0.28f * s, 0.04f * s), PBadge, new Vector3(0, 0, 45f));
        }

        /// <summary>赤と青のパトランプ。戻り値の2つを交互に点ける。</summary>
        static GameObject[] SirenLamp(Transform t, float y, float s)
        {
            var red = GlowQ(t, 0xff2030, 2.2f * s, 0.95f, new Vector3(-0.3f * s, y, 0.1f));
            var blue = GlowQ(t, 0x2a7aff, 2.2f * s, 0.95f, new Vector3(0.3f * s, y, 0.1f));
            Sph(t, new Vector3(-0.3f * s, y, 0), Vector3.one * 0.18f * s, Mats.Unlit(Hex(0xff3040)));
            Sph(t, new Vector3(0.3f * s, y, 0), Vector3.one * 0.18f * s, Mats.Unlit(Hex(0x3a8aff)));
            return new[] { red, blue };
        }

        /// <summary>鬼の巡査：赤鬼の顔に制帽。赤い弾を撃つ。</summary>
        public static PoliceModel Junsa()
        {
            var pm = new PoliceModel();
            var g = new GameObject("鬼の巡査"); var t = g.transform; pm.root = g;
            Sph(t, Vector3.zero, new Vector3(0.9f, 0.85f, 0.85f), Oni);
            foreach (int sd in new[] { -1, 1 }) ConeObj(t, new Vector3(sd * 0.42f, 0.72f, 0.1f), 0.11f, 0.5f, 8, OniHorn, new Vector3(0, 0, sd * 23f));
            Eyes(t, 0.12f, 0.72f, 0.28f, 0.17f, true);
            Box(t, new Vector3(0, -0.32f, 0.8f), new Vector3(0.42f, 0.08f, 0.06f), eyeDark);
            Cyl(t, new Vector3(0.18f, -0.34f, 0.86f), 0.06f, 0.3f, PBadge, new Vector3(0, 0, 90f));   // 呼子笛
            PoliceCap(t, 0.82f, 1f);
            GlowQ(t, 0xff5040, 3.4f, 0.35f, new Vector3(0, 0, -0.6f));
            return pm;
        }

        /// <summary>パト提灯：提灯お化けにパトランプ。3方向に弾を撒く。</summary>
        public static PoliceModel Patochin()
        {
            var pm = new PoliceModel();
            var g = Chochin(); g.name = "パト提灯"; pm.root = g;
            pm.siren = SirenLamp(g.transform, 1.25f, 1f);
            return pm;
        }

        /// <summary>大警部：大きな赤鬼の顔・制帽・口ひげ・パトランプ。ひるむと帽章（core）が金色に光る。</summary>
        public static PoliceModel DaiKeibu()
        {
            var pm = new PoliceModel();
            var g = new GameObject("大警部"); var t = g.transform; pm.root = g;
            var face = Group("face", t, Vector3.zero); pm.face = face;
            Sph(face, Vector3.zero, new Vector3(1f, 0.92f, 0.85f), StdE(0xc02026, 0x500608, 0.7f, 0.55f));
            foreach (int sd in new[] { -1, 1 }) ConeObj(face, new Vector3(sd * 0.5f, 0.86f, 0.05f), 0.16f, 0.7f, 8, OniHorn, new Vector3(0, 0, sd * 26f));
            var eyeGlow = Mats.Unlit(Hex(0xfff2a0));
            foreach (int sd in new[] { -1, 1 })
            {
                Sph(face, new Vector3(sd * 0.32f, 0.12f, 0.8f), new Vector3(0.2f, 0.12f, 0.08f), eyeGlow);
                Box(face, new Vector3(sd * 0.32f, 0.3f, 0.82f), new Vector3(0.42f, 0.09f, 0.08f), eyeDark, new Vector3(0, 0, sd * 26f));
                Box(face, new Vector3(sd * 0.2f, -0.16f, 0.86f), new Vector3(0.42f, 0.08f, 0.06f), Std(0x1a1010), new Vector3(0, 0, -sd * 14f));   // 口ひげ
                ConeObj(face, new Vector3(sd * 0.2f, -0.27f, 0.86f), 0.06f, 0.18f, 4, OniHorn, new Vector3(180f, 0, 0));   // 牙
            }
            pm.mouth = Sph(face, new Vector3(0, -0.35f, 0.78f), new Vector3(0.42f, 0.18f, 0.1f), eyeDark).transform;
            PoliceCap(face, 0.95f, 1.25f);
            pm.siren = SirenLamp(face, 1.45f, 1.8f);
            GlowQ(t, 0xff2a20, 9f, 0.5f, new Vector3(0, 0, -0.6f));
            pm.core = GlowQ(face, 0xffd040, 3.2f, 0.9f, new Vector3(0, 0.95f, 0.95f));
            pm.core.SetActive(false);
            return pm;
        }

        /// <summary>大警部の白手袋（ステージ4のしかけ）。</summary>
        public static GameObject Glove()
        {
            var g = new GameObject("白手袋"); var t = g.transform;
            var w = StdE(0xf4f2ec, 0x3a3836, 1f, 0.4f);
            Sph(t, Vector3.zero, new Vector3(1.1f, 1.25f, 0.55f), w);
            for (int i = 0; i < 4; i++) Cyl(t, new Vector3(-0.6f + i * 0.4f, 1.35f, 0), 0.17f, 0.9f, w);
            Cyl(t, new Vector3(1.05f, 0.25f, 0), 0.17f, 0.7f, w, new Vector3(0, 0, -52f));
            Cyl(t, new Vector3(0, -1.25f, 0), 0.85f, 0.35f, PCap);
            g.transform.localScale = Vector3.one * 1.3f;
            return g;
        }

        /// <summary>手錠（大警部が投げる）。</summary>
        public static GameObject Cuffs()
        {
            var g = new GameObject("手錠"); var t = g.transform;
            var metal = StdE(0xd0d4dc, 0x303440, 1f, 0.8f);
            foreach (int sd in new[] { -1, 1 }) MeshObj("ring", MeshKit.Torus(0.42f, 0.1f, 20, 8), t, metal, new Vector3(sd * 0.55f, 0, 0), Vector3.one, new Vector3(90f, 0, 0));
            Cyl(t, Vector3.zero, 0.05f, 0.5f, metal, new Vector3(0, 0, 90f));
            GlowQ(t, 0xff4a3a, 3.6f, 0.8f, new Vector3(0, 0, -0.2f));
            return g;
        }

        /// <summary>敵の弾：大きく、白い芯と黒い縁で、どの背景でも見える。</summary>
        public static GameObject Pellet()
        {
            var g = new GameObject("弾"); var t = g.transform;
            GlowQ(t, 0xff3040, 2.8f, 0.7f, new Vector3(0, 0, -0.05f));
            MeshObj("edge", MeshKit.Disc(0.58f, 24), t, Mats.Unlit(new Color(0, 0, 0)), new Vector3(0, 0, -0.01f), Vector3.one);
            MeshObj("red", MeshKit.Disc(0.5f, 24), t, Mats.Unlit(Hex(0xff3040)), Vector3.zero, Vector3.one);
            MeshObj("core", MeshKit.Disc(0.24f, 16), t, Mats.Unlit(Color.white), new Vector3(0, 0, 0.01f), Vector3.one);
            return g;
        }

        /// <summary>落とし物の小銭（帰りの救済、1枚5文）。</summary>
        public static GameObject Coin()
        {
            var g = new GameObject("小銭"); var t = g.transform;
            Cyl(t, Vector3.zero, 0.55f, 0.1f, StdE(0xffc840, 0x7a5000, 0.7f, 0.75f), new Vector3(90f, 0, 0));
            Box(t, new Vector3(0, 0, 0.02f), new Vector3(0.22f, 0.22f, 0.14f), Std(0x2a1a08));
            GlowQ(t, 0xffd060, 2.6f, 0.7f, new Vector3(0, 0, -0.1f));
            return g;
        }
    }
}
