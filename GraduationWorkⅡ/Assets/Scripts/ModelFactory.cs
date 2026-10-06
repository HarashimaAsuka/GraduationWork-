using UnityEngine;

namespace ShoryoumaTaxi
{
    /// <summary>
    /// 仮モデルをプリミティブと手続きメッシュで組み立てる。
    /// 自作モデルに差し替えるときは TaxiGame のインスペクターにプレハブを入れる
    /// （向きは +Z が正面、+Y が上。大きさは2ユニット前後）。
    /// </summary>
    public static class ModelFactory
    {
        // ---------- 共通部品 ----------
        static Material eyeWhite, eyeDark, stickMat;
        static Mesh zakuroMesh, hozukiMesh, soulMesh, smallCone, hornCone, spatheCone;

        public static Material ZakuroMat, ZakuroRipeMat, ArilMat, SepalMat, HornMat;
        public static Material HozukiMat, BerryMat, TongueMat, SpatheMat;
        public static Material CucumberMat, NasuMat, SoulMat, SeedMat, SaltMat, OnibiMat;

        public static void Init()
        {
            eyeWhite = Mats.Lit(new Color(1f, 0.96f, 0.85f), 0.7f);
            eyeDark = Mats.Lit(new Color(0.1f, 0.02f, 0.03f), 0.6f);
            stickMat = Mats.Lit(new Color(0.85f, 0.75f, 0.56f), 0.2f);
            ZakuroMat = Mats.Lit(new Color(0.7f, 0.08f, 0.12f), 0.55f);
            ZakuroRipeMat = Mats.Lit(new Color(0.45f, 0.04f, 0.12f), 0.7f, new Color(0.15f, 0f, 0.03f));
            ArilMat = Mats.Lit(new Color(1f, 0.12f, 0.29f), 0.85f, new Color(0.4f, 0f, 0.08f));
            SepalMat = Mats.Lit(new Color(0.35f, 0.05f, 0.1f), 0.3f);
            HornMat = Mats.Lit(new Color(0.95f, 0.89f, 0.75f), 0.5f);
            HozukiMat = Mats.Lit(new Color(1f, 0.48f, 0.1f), 0.3f, new Color(0.9f, 0.3f, 0f));
            BerryMat = Mats.Unlit(new Color(1f, 0.82f, 0.38f));
            TongueMat = Mats.Lit(new Color(0.42f, 0.27f, 0.3f), 0.2f);
            SpatheMat = Mats.Lit(new Color(0.29f, 0.04f, 0.15f), 0.6f, new Color(0.15f, 0f, 0.07f));
            CucumberMat = Mats.Lit(new Color(0.37f, 0.61f, 0.25f), 0.45f);
            NasuMat = Mats.Lit(new Color(0.23f, 0.12f, 0.37f), 0.85f);
            SoulMat = Mats.Lit(new Color(0.9f, 0.96f, 1f), 0.6f, new Color(0.35f, 0.75f, 1f));
            SeedMat = Mats.Lit(new Color(1f, 0.18f, 0.33f), 0.85f, new Color(0.6f, 0f, 0.15f));
            SaltMat = Mats.Unlit(Color.white);
            OnibiMat = Mats.Unlit(new Color(0.55f, 0.82f, 1f));

            // ザクロ：6本の筋が入った回転体
            zakuroMesh = MeshKit.Lathe(new[] {
                new Vector2(0,-1f), new Vector2(0.42f,-0.95f), new Vector2(0.74f,-0.76f), new Vector2(0.94f,-0.42f),
                new Vector2(1f,-0.02f), new Vector2(0.97f,0.34f), new Vector2(0.84f,0.62f), new Vector2(0.62f,0.82f),
                new Vector2(0.38f,0.93f), new Vector2(0.25f,0.99f), new Vector2(0.2f,1.1f), new Vector2(0.24f,1.22f) },
                30, (a, y) => y < 1f ? 1f + 0.035f * Mathf.Cos(a * 6f) : 1f);
            // ホオズキ：5本の稜がある提灯形
            hozukiMesh = MeshKit.Lathe(new[] {
                new Vector2(0,-1.05f), new Vector2(0.2f,-0.9f), new Vector2(0.55f,-0.5f), new Vector2(0.78f,-0.05f),
                new Vector2(0.72f,0.4f), new Vector2(0.45f,0.8f), new Vector2(0.14f,1f), new Vector2(0.06f,1.12f) },
                30, (a, y) => 1f - 0.16f * Mathf.Pow(0.5f + 0.5f * Mathf.Cos(a * 5f), 2f));
            // 人魂：しずく形（下から上へ）
            soulMesh = MeshKit.Lathe(new[] {
                new Vector2(0,-1.42f), new Vector2(0.05f,-1.25f), new Vector2(0.15f,-0.95f), new Vector2(0.32f,-0.62f),
                new Vector2(0.5f,-0.32f), new Vector2(0.58f,0f), new Vector2(0.52f,0.34f), new Vector2(0.3f,0.56f), new Vector2(0,0.62f) }, 24);
            smallCone = MeshKit.Cone(0.09f, 0.36f, 4);
            hornCone = MeshKit.Cone(0.11f, 0.5f, 8);
            spatheCone = MeshKit.Cone(0.62f, 2.7f, 14);
        }

        // ---------- 組み立てヘルパー ----------
        public static GameObject Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material m, Vector3? euler = null)
        {
            var g = GameObject.CreatePrimitive(t);
            Object.Destroy(g.GetComponent<Collider>());     // 当たり判定は自前の距離計算で行う
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = scale;
            if (euler.HasValue) g.transform.localEulerAngles = euler.Value;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        public static GameObject MeshObj(string name, Mesh mesh, Transform parent, Material m, Vector3 pos, Vector3 scale, Vector3? euler = null)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = scale;
            if (euler.HasValue) g.transform.localEulerAngles = euler.Value;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = m;
            return g;
        }

        static GameObject Root(string name, GameObject prefab)
        {
            if (prefab != null) return Object.Instantiate(prefab);
            return new GameObject(name);
        }

        static void Eyes(Transform p, float y, float z, float spread, float size, bool angry)
        {
            foreach (int sd in new[] { -1, 1 })
            {
                Prim(PrimitiveType.Sphere, p, new Vector3(sd * spread, y, z), new Vector3(size * 2, size * 1.5f, size), eyeWhite);
                Prim(PrimitiveType.Sphere, p, new Vector3(sd * spread * 0.92f, y - size * 0.1f, z + size * 0.35f), Vector3.one * size * 0.9f, eyeDark);
                if (angry)
                    Prim(PrimitiveType.Cube, p, new Vector3(sd * spread, y + size * 1.2f, z + 0.02f), new Vector3(size * 2.4f, size * 0.45f, size * 0.4f), eyeDark, new Vector3(0, 0, -sd * 28f));
            }
        }

        // ---------- 敵 ----------
        /// <summary>鬼ザクロ。戻り値の body / arils は被弾・熟れの表示切り替えに使う。</summary>
        public static GameObject Zakuro(GameObject prefab, out Renderer body, out GameObject arils)
        {
            var g = Root("鬼ザクロ", prefab);
            body = null; arils = null;
            if (prefab != null) { body = g.GetComponentInChildren<Renderer>(); return g; }
            var t = g.transform;
            body = MeshObj("body", zakuroMesh, t, ZakuroMat, Vector3.zero, Vector3.one).GetComponent<Renderer>();
            for (int i = 0; i < 6; i++)
            {
                var pv = new GameObject("sepal").transform; pv.SetParent(t, false); pv.localEulerAngles = new Vector3(0, i * 60f, 0);
                MeshObj("s", smallCone, pv, SepalMat, new Vector3(0.19f, 1.2f, 0), Vector3.one, new Vector3(0, 0, -32f));
            }
            foreach (int sd in new[] { -1, 1 })
                MeshObj("horn", hornCone, t, HornMat, new Vector3(sd * 0.5f, 0.75f, 0.3f), Vector3.one, new Vector3(0, 0, -sd * 34f));
            Eyes(t, 0.12f, 0.9f, 0.3f, 0.16f, true);
            Prim(PrimitiveType.Cube, t, new Vector3(0, -0.32f, 0.94f), new Vector3(0.42f, 0.06f, 0.08f), eyeDark);
            arils = new GameObject("arils");
            arils.transform.SetParent(t, false);
            Prim(PrimitiveType.Sphere, arils.transform, new Vector3(0.32f, -0.5f, 0.78f), new Vector3(0.84f, 0.6f, 0.24f), eyeDark, new Vector3(0, 0, 28f));
            for (int i = 0; i < 8; i++)
                Prim(PrimitiveType.Sphere, arils.transform, new Vector3(0.32f + Random.Range(-0.28f, 0.28f), -0.5f + Random.Range(-0.16f, 0.16f), 0.88f), Vector3.one * Random.Range(0.15f, 0.21f), ArilMat);
            arils.SetActive(false);
            return g;
        }

        public static GameObject Hozuki(GameObject prefab)
        {
            var g = Root("鬼灯", prefab);
            if (prefab != null) return g;
            var t = g.transform;
            MeshObj("husk", hozukiMesh, t, HozukiMat, Vector3.zero, Vector3.one);
            Prim(PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * 0.6f, BerryMat);
            Prim(PrimitiveType.Cylinder, t, new Vector3(0, 1.25f, 0), new Vector3(0.1f, 0.18f, 0.1f), stickMat);
            foreach (int sd in new[] { -1, 1 })
                Prim(PrimitiveType.Cube, t, new Vector3(sd * 0.22f, 0.1f, 0.72f), new Vector3(0.26f, 0.06f, 0.06f), eyeDark, new Vector3(0, 0, -sd * 20f));
            return g;
        }

        public static GameObject Tongue(GameObject prefab)
        {
            var g = Root("悪魔の舌", prefab);
            if (prefab != null) return g;
            var t = g.transform;
            Prim(PrimitiveType.Sphere, t, Vector3.zero, new Vector3(2.7f, 1.7f, 2.7f), TongueMat);
            MeshObj("spathe", spatheCone, t, SpatheMat, new Vector3(0, 0.55f, -0.1f), Vector3.one, new Vector3(-10f, 0, 0));
            Prim(PrimitiveType.Cylinder, t, new Vector3(0, 2.2f, 0.05f), new Vector3(0.2f, 1.7f, 0.2f), eyeDark);
            foreach (int sd in new[] { -1, 1 })
                Prim(PrimitiveType.Sphere, t, new Vector3(sd * 0.42f, 0.22f, 1.22f), Vector3.one * 0.28f, Mats.Unlit(new Color(1f, 0.88f, 0.25f)));
            return g;
        }

        // ---------- お客さん ----------
        public static GameObject Soul(GameObject prefab)
        {
            var g = Root("魂", prefab);
            if (prefab != null) return g;
            var t = g.transform;
            MeshObj("body", soulMesh, t, SoulMat, Vector3.zero, Vector3.one);
            var face = Mats.Unlit(new Color(0.1f, 0.16f, 0.25f));
            foreach (int sd in new[] { -1, 1 })
                Prim(PrimitiveType.Sphere, t, new Vector3(sd * 0.17f, 0.06f, 0.53f), new Vector3(0.12f, 0.18f, 0.08f), face);
            Prim(PrimitiveType.Cube, t, new Vector3(0, -0.12f, 0.55f), new Vector3(0.16f, 0.035f, 0.04f), face);
            // 天冠（額の白い三角）
            MeshObj("tenkan", MeshKit.Cone(0.17f, 0.22f, 3), t, Mats.Unlit(Color.white), new Vector3(0, 0.3f, 0.5f), new Vector3(1f, 1f, 0.15f), new Vector3(0, 30f, 0));
            return g;
        }

        // ---------- 精霊馬 ----------
        public class Steed
        {
            public GameObject root, cucumber, nasu, spider, rider;
            public Transform[] cucLegs, nasuLegs;
        }

        static Transform[] Legs(Transform parent, Vector3[] pts, float len)
        {
            var list = new Transform[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                var pv = new GameObject("leg").transform; pv.SetParent(parent, false); pv.localPosition = pts[i];
                Prim(PrimitiveType.Cylinder, pv, new Vector3(0, -len / 2f, 0), new Vector3(0.07f, len / 2f, 0.07f), stickMat);
                list[i] = pv;
            }
            return list;
        }

        public static Steed BuildSteed(GameObject cucumberPrefab, GameObject nasuPrefab, GameObject soulPrefab)
        {
            var s = new Steed();
            s.root = new GameObject("精霊馬");
            var t = s.root.transform;

            // きゅうりの馬
            s.cucumber = cucumberPrefab != null ? Object.Instantiate(cucumberPrefab, t) : new GameObject("きゅうりの馬");
            s.cucumber.transform.SetParent(t, false);
            if (cucumberPrefab == null)
            {
                var c = s.cucumber.transform;
                Prim(PrimitiveType.Capsule, c, Vector3.zero, new Vector3(0.72f, 1.35f, 0.72f), CucumberMat, new Vector3(90f, 0, 0));
                var bump = Mats.Lit(new Color(0.18f, 0.35f, 0.12f), 0.3f);
                for (int i = 0; i < 18; i++)
                {
                    float a = Random.Range(0f, Mathf.PI * 2f), z = Random.Range(-1.0f, 1.0f);
                    Prim(PrimitiveType.Sphere, c, new Vector3(Mathf.Cos(a) * 0.35f, Mathf.Sin(a) * 0.35f, z), Vector3.one * 0.09f, bump);
                }
                Prim(PrimitiveType.Sphere, c, new Vector3(0, 0.32f, 1.32f), new Vector3(0.48f, 0.48f, 1f), CucumberMat, new Vector3(-45f, 0, 0));
                Prim(PrimitiveType.Sphere, c, new Vector3(0, 0.66f, 1.62f), Vector3.one * 0.22f, Mats.Lit(new Color(0.94f, 0.78f, 0.25f)));
                s.cucLegs = Legs(c, new[] { new Vector3(-0.2f, -0.2f, 0.8f), new Vector3(0.2f, -0.2f, 0.8f), new Vector3(-0.2f, -0.2f, -0.8f), new Vector3(0.2f, -0.2f, -0.8f) }, 1.0f);
            }
            else s.cucLegs = new Transform[0];

            // ナスの牛
            s.nasu = nasuPrefab != null ? Object.Instantiate(nasuPrefab, t) : new GameObject("ナスの牛");
            s.nasu.transform.SetParent(t, false);
            if (nasuPrefab == null)
            {
                var n = s.nasu.transform;
                Prim(PrimitiveType.Sphere, n, Vector3.zero, new Vector3(1.24f, 1.12f, 1.96f), NasuMat);
                MeshObj("calyx", MeshKit.Cone(0.45f, 0.35f, 5), n, Mats.Lit(new Color(0.18f, 0.29f, 0.16f), 0.3f), new Vector3(0, 0, 0.9f), Vector3.one, new Vector3(90f, 0, 0));
                Prim(PrimitiveType.Cylinder, n, new Vector3(0, 0.18f, 1.3f), new Vector3(0.15f, 0.25f, 0.15f), Mats.Lit(new Color(0.25f, 0.35f, 0.16f)), new Vector3(55f, 0, 0));
                s.nasuLegs = Legs(n, new[] { new Vector3(-0.3f, -0.35f, 0.5f), new Vector3(0.3f, -0.35f, 0.5f), new Vector3(-0.3f, -0.35f, -0.5f), new Vector3(0.3f, -0.35f, -0.5f) }, 0.8f);
            }
            else s.nasuLegs = new Transform[0];

            // 相棒のクモ
            s.spider = new GameObject("クモ");
            s.spider.transform.SetParent(t, false);
            var black = Mats.Lit(new Color(0.08f, 0.07f, 0.1f), 0.6f);
            var sp = s.spider.transform;
            Prim(PrimitiveType.Sphere, sp, Vector3.zero, Vector3.one * 0.26f, black);
            Prim(PrimitiveType.Sphere, sp, new Vector3(0, 0, -0.26f), Vector3.one * 0.38f, black);
            for (int i = 0; i < 4; i++)
                foreach (int sd in new[] { -1, 1 })
                    Prim(PrimitiveType.Cylinder, sp, new Vector3(sd * 0.2f, -0.02f, 0.1f - i * 0.1f), new Vector3(0.03f, 0.21f, 0.03f), black, new Vector3(0, (i - 1.5f) * 20f * sd, sd * 68f));
            foreach (int sd in new[] { -1, 1 })
                Prim(PrimitiveType.Sphere, sp, new Vector3(sd * 0.05f, 0.05f, 0.11f), Vector3.one * 0.08f, Mats.Unlit(Color.white));

            // 乗っているお客さん（背中の上）
            s.rider = Soul(soulPrefab);
            s.rider.transform.SetParent(t, false);
            s.rider.transform.localPosition = new Vector3(0, 1.0f, 0.3f);
            s.rider.transform.localScale = Vector3.one * 0.5f;
            s.rider.SetActive(false);
            return s;
        }

        // ---------- 行き先のゲート ----------
        public static GameObject Gate(out TextMesh label, Font font)
        {
            var g = new GameObject("行き先ゲート");
            var t = g.transform;
            MeshObj("ring", MeshKit.Torus(7.4f, 0.34f), t, Mats.Unlit(new Color(0.85f, 0.2f, 0.16f)), Vector3.zero, Vector3.one);
            MeshObj("ring2", MeshKit.Torus(6.7f, 0.12f), t, Mats.Unlit(new Color(1f, 0.82f, 0.48f)), Vector3.zero, Vector3.one);
            var lamp = Mats.Lit(new Color(1f, 0.75f, 0.45f), 0.3f, new Color(1f, 0.55f, 0.15f) * 2f);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                Prim(PrimitiveType.Cylinder, t, new Vector3(Mathf.Cos(a) * 7.4f, -0.5f, Mathf.Sin(a) * 7.4f), new Vector3(0.6f, 0.4f, 0.6f), lamp);
            }
            var lg = new GameObject("label");
            label = lg.AddComponent<TextMesh>();
            label.font = font; label.fontSize = 64; label.characterSize = 0.08f;
            label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
            label.color = new Color(1f, 0.92f, 0.8f);
            lg.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return g;
        }
    }
}
