using UnityEngine;

namespace ShoryoumaTaxi
{
    /// <summary>
    /// v19：ステージの配置表。お客さん・アイテム・敵の波は、あらかじめ決めておく（Web版 v32 と同じ表）。
    /// souls: (高さ, お客さん, 行き先の高さ)　items: (高さ, 種類)　waves: (高さ, 出方, 敵, 数)
    /// 置いてある敵（enemies）は、ステージごとの種（seed）から決まった並びで作る。
    /// </summary>
    public class StageDef
    {
        public int no, quota, seed; public string name, goal; public float time, tongue, yokai; public bool boss;
        public float[] gap;   // 置いてある敵の間隔（null なら置かない）
        public (float y, string guest, float dest)[] souls;
        public (float y, string kind)[] items;
        public (float y, string pattern, string type, int n)[] waves;
    }

    public partial class TaxiGame
    {
        static readonly StageDef[] STAGES = {
            new StageDef {
                no = 1, name = "送り", goal = "お客さんを5人送り届ける", time = 180, quota = 5, seed = 11, gap = new[] { 16f, 24f }, tongue = 0.3f, yokai = 0.8f,
                souls = new (float, string, float)[] {
                    (97, "jiisan", 317), (229, "okami", 389), (332, "kozo", 792), (461, "oyakata", 921), (586, "samurai", 1046),
                    (725, "jiisan", 885), (869, "neko", 1029), (982, "okami", 1442), (1123, "geisha", 1823), (1261, "kozo", 1481),
                    (1393, "oyakata", 1853), (1545, "tanuki", 2005), (1645, "jiisan", 1805), (1802, "okami", 1962), (1897, "tono", 2117),
                    (2004, "kozo", 2140) },
                items = new (float, string)[] {
                    (120, "water"), (198, "wind"), (276, "water"), (354, "nasu"), (432, "water"), (510, "charm"), (588, "incense"), (666, "water"), (744, "wind"),
                    (822, "water"), (900, "nasu"), (978, "water"), (1056, "water"), (1134, "wind"), (1212, "water"), (1290, "nasu"), (1368, "water"), (1446, "charm"),
                    (1524, "incense"), (1602, "water"), (1680, "wind"), (1758, "water"), (1836, "nasu"), (1914, "water"), (1992, "water"), (2070, "wind") },
                waves = new (float, string, string, int)[] {
                    (293, "cross", "zakuro", 4), (440, "escort", "zakuro", 3), (513, "cross", "zakuro", 4), (704, "escort", "zakuro", 4), (953, "cross", "kasa", 4),
                    (1687, "drop", "hozuki", 3), (821, "drop", "zakuro", 2), (1115, "side", "kasa", 3), (1291, "escort", "hozuki", 4), (1540, "escort", "kasa", 4),
                    (1760, "cross", "hozuki", 4), (1936, "escort", "hozuki", 4), (1600, "cross", "kasa", 5), (1850, "escort", "zakuro", 4), (2000, "drop", "chochin", 3),
                    (1400, "side", "zakuro", 3) },
            },
            new StageDef {
                no = 2, name = "帰り", goal = "お客さんを8人送り届ける", time = 240, quota = 8, seed = 23, gap = new[] { 11f, 16f }, tongue = 0.9f, yokai = 1.1f,
                souls = new (float, string, float)[] {
                    (102, "okami", 562), (183, "jiisan", 883), (297, "oyakata", 457), (385, "kozo", 945), (456, "sumo", 616),
                    (569, "geisha", 729), (664, "jiisan", 1124), (737, "samurai", 1297), (848, "okami", 1548), (930, "tanuki", 1390),
                    (1057, "kozo", 1357), (1115, "oyakata", 1815), (1202, "zashiki", 1902), (1299, "jiisan", 1759), (1395, "neko", 1775),
                    (1521, "okami", 2081), (1590, "gosenzo", 2050), (1714, "kozo", 1874), (1781, "oyakata", 2001), (1888, "geisha", 2108),
                    (1991, "jiisan", 2140), (2034, "tono", 2140) },
                items = new (float, string)[] {
                    (110, "water"), (182, "nasu"), (254, "water"), (326, "wind"), (398, "charm"), (470, "water"), (542, "nasu"), (614, "incense"), (686, "water"),
                    (758, "wind"), (830, "water"), (902, "nasu"), (974, "water"), (1046, "nasu"), (1118, "water"), (1190, "wind"), (1262, "charm"), (1334, "water"),
                    (1406, "nasu"), (1478, "incense"), (1550, "water"), (1622, "wind"), (1694, "water"), (1766, "nasu"), (1838, "water"), (1910, "nasu"), (1982, "water"),
                    (2054, "wind"), (2126, "charm") },
                waves = new (float, string, string, int)[] {
                    (264, "escort", "zakuro", 3), (367, "cross", "zakuro", 4), (484, "escort", "zakuro", 4), (616, "side", "kasa", 3), (763, "escort", "chochin", 3),
                    (880, "drop", "hozuki", 3), (1027, "escort", "zakuro", 4), (1203, "cross", "zakuro", 5), (1349, "escort", "kasa", 4), (1467, "side", "tongue", 1),
                    (1584, "escort", "zakuro", 4), (1731, "drop", "zakuro", 3), (1833, "escort", "chochin", 4), (1936, "cross", "kasa", 5), (557, "drop", "zakuro", 3),
                    (1115, "cross", "chochin", 4), (1672, "side", "kasa", 3), (2053, "escort", "zakuro", 5), (1700, "cross", "hozuki", 5), (1900, "escort", "kasa", 5),
                    (2050, "drop", "chochin", 3), (1450, "side", "tongue", 1) },
            },
            new StageDef {
                no = 3, name = "幻の三日目", goal = "大ボス「鬼ザクロの親玉」を祓う", time = 180, quota = 0, boss = true, seed = 37, gap = null, tongue = 0,
                souls = new (float, string, float)[0],
                items = new (float, string)[] {
                    (920, "water"), (992, "charm"), (1064, "water"), (1136, "nasu"), (1208, "water"), (1280, "incense"), (1352, "water"), (1424, "water"), (1496, "nasu"),
                    (1568, "charm"), (1640, "water"), (1712, "charm"), (1784, "water"), (1856, "nasu"), (1928, "water"), (2000, "incense"), (2072, "water"), (2144, "water") },
                waves = new (float, string, string, int)[0],
            },
        };

        // ---- 停留所の名前：高さ（見た目の層）で決まる ----
        static readonly (float a, float b, string[] names)[] STOPS = {
            (0.00f, 0.20f, new[] { "閻魔庁前", "血の池地獄", "針の山ふもと" }),
            (0.20f, 0.45f, new[] { "三途の川 渡し場", "賽の河原", "奪衣婆の茶屋" }),
            (0.45f, 0.70f, new[] { "雲の上の停留所", "雷さまの太鼓前", "天の浮橋" }),
            (0.70f, 1.01f, new[] { "迎え火の家", "盆踊りのやぐら", "縁側の仏壇" }),
        };

        static string StopName(float yy)
        {
            float f = Road.Zf(yy);
            var z = STOPS[3];
            foreach (var s in STOPS) if (f >= s.a && f < s.b) { z = s; break; }
            return z.names[Mathf.FloorToInt(yy / 37) % 3];
        }

        /// <summary>決まった並びの乱数（Web版の mulberry32 と同じ計算）。</summary>
        class Seeded
        {
            uint s;
            public Seeded(int seed) { s = (uint)seed; }
            public float Next()
            {
                unchecked
                {
                    s += 0x6D2B79F5u;
                    uint t = (s ^ (s >> 15)) * (1u | s);
                    t = (t + ((t ^ (t >> 7)) * (61u | t))) ^ t;
                    return Mathf.Min(0.9999999f, (float)((t ^ (t >> 14)) / 4294967296.0));
                }
            }
        }

        // ---- 敵の種類 ----
        static int TypeHp(string type) => type == "hozuki" ? 1 : type == "tongue" ? 5 : type == "chochin" ? 3 : 2;
        static float TypeR(string type) => type == "hozuki" ? 0.9f : type == "tongue" ? 1.5f : type == "chochin" ? 1.1f : 1.0f;
        static string EnemyName(string type) => type == "zakuro" ? "鬼ザクロ" : type == "hozuki" ? "鬼灯" : type == "tongue" ? "悪魔の舌" : type == "kasa" ? "唐傘お化け" : type == "chochin" ? "提灯お化け" : type == "boss" ? "親玉" : "敵";
        static Color KCol(string type) => Hex(type == "hozuki" ? 0xff8a30 : type == "tongue" ? 0xc060ff : type == "kasa" ? 0xff5a3a : type == "chochin" ? 0xffc060 : 0xff2d55);
        static Color WarnColor(string type) => Hex(type == "hozuki" ? 0xff9a30 : type == "tongue" ? 0xc060ff : type == "kasa" ? 0xff6040 : type == "chochin" ? 0xffc060 : 0xff3050);

        GameObject BuildEnemy(string type, out Renderer body, out GameObject arils)
        {
            body = null; arils = null;
            switch (type)
            {
                case "zakuro": return ModelFactory.Zakuro(zakuroPrefab, out body, out arils);
                case "hozuki": return ModelFactory.Hozuki(hozukiPrefab);
                case "tongue": return ModelFactory.Tongue(tonguePrefab);
                case "kasa": return ModelFactory.Kasa();
                default: return ModelFactory.Chochin();
            }
        }

        void MakeEnemies()
        {
            if (st.gap == null) return;
            var r = new Seeded(st.seed * 7 + 1);
            for (float h = 80; h < L - 50; h += st.gap[0] + r.Next() * (st.gap[1] - st.gap[0]))
            {
                int n = r.Next() < 0.15f ? 2 : 1;
                for (int i = 0; i < n; i++)
                {
                    float f = h / L;
                    string[] names = { "zakuro", "hozuki", "tongue", "kasa", "chochin" };
                    float[] w = { 5, f > 0.2f ? 2.2f : 1.2f, f > 0.28f ? st.tongue : 0, f > 0.12f ? 2 * st.yokai : 0, f > 0.3f ? 1.4f * st.yokai : 0 };
                    float sum = 0; foreach (var v in w) sum += v;
                    float q = r.Next() * sum; string type = "zakuro";
                    for (int k = 0; k < w.Length; k++) { q -= w[k]; if (q < 0) { type = names[k]; break; } }
                    float a = r.Next() * Mathf.PI * 2, rr = Mathf.Sqrt(r.Next()) * 0.85f;
                    var e = new Enemy { type = type, y0 = h + i * 3, nx = Mathf.Cos(a) * rr, nz = Mathf.Sin(a) * rr, hp = TypeHp(type), r = TypeR(type) };
                    e.y = e.yPrev = e.y0; e.max = e.hp;
                    e.shooter = type != "zakuro" || r.Next() < 0.5f;
                    e.cd = 0.4f + r.Next() * 1.6f; e.ph = r.Next() * 6;
                    e.go = BuildEnemy(type, out e.body, out e.arils);
                    e.go.SetActive(false);
                    enemies.Add(e);
                }
            }
        }

        void MakeSouls()
        {
            var r = new Seeded(st.seed * 13 + 5);
            foreach (var d in st.souls)
            {
                GuestDef guest = GUESTS[0];
                foreach (var g in GUESTS) if (g.id == d.guest) { guest = g; break; }
                float a = r.Next() * Mathf.PI * 2, rr = Mathf.Sqrt(r.Next()) * 0.75f;
                int jo = Mathf.RoundToInt(Mathf.Abs(d.dest - d.y) / 10);
                int fare = Mathf.RoundToInt((FARE + jo / 10 * FARE_PER_10JO) * guest.mul);
                var s = new Soul { guest = guest, y0 = d.y, y = d.y, yPrev = d.y, destY = d.dest, destName = StopName(d.dest), jo = jo, fare = fare,
                    nx = Mathf.Cos(a) * rr, nz = Mathf.Sin(a) * rr, ph = r.Next() * 6 };
                s.go = ModelFactory.Soul(soulPrefab, guest.rare); s.go.SetActive(false);
                // 頭の上の札：行き先・距離・運賃
                var rc = RARE_COL[guest.rare];
                s.tag = ModelFactory.MakeLabel(jpFont, 3.4f, 1.3f, s.destName, Color.white, Hex(0xffd27a), rc, 0.42f, 0.3f);
                s.tag.main.text = $"{(s.destY > s.y0 ? "▲" : "▼")} {jo}丈　{(guest.tanuki ? "？？" : fare.ToString())}文";
                souls.Add(s);
            }
        }

        static int ItemHex(string kind) => kind == "water" ? 0x6cc4e8 : kind == "wind" ? 0x9be38a : kind == "incense" ? 0xe8b07a : kind == "charm" ? 0xff8a8a : 0xb48cff;
        static string ItemName(string kind) => kind == "water" ? "お供えの水" : kind == "wind" ? "追い風" : kind == "incense" ? "線香" : kind == "charm" ? "厄除けのお守り" : "ナス";
        static string ItemText(string kind) => kind == "water" ? "水分 +30" : kind == "wind" ? "5秒間、ぐんと速くなる" : kind == "incense" ? "時間 +10秒" : kind == "charm" ? "次にぶつかっても平気" : "向きが反転する";

        void MakeItems()
        {
            var r = new Seeded(st.seed * 17 + 3);
            foreach (var d in st.items)
            {
                float a = r.Next() * Mathf.PI * 2, rr = Mathf.Sqrt(r.Next()) * 0.7f;
                var it = new Pickup { kind = d.kind, y = d.y, nx = Mathf.Cos(a) * rr, nz = Mathf.Sin(a) * rr, ph = r.Next() * 6 };
                it.im = ModelFactory.Item(d.kind);
                var root = it.im.root.transform;
                int hex = ItemHex(d.kind);
                ModelFactory.GlowQ(root, hex, 4.2f, 0.55f, new Vector3(0, 0, -0.8f));   // 奥行きどおり（馬の前には出ない）
                var hm = Mats.VertexColor(); hm.SetColor("_Color", Hex(hex));
                var halo = ModelFactory.MeshObj("halo", MeshKit.Ring(1.45f, 1.55f, 40), root, hm, Vector3.zero, Vector3.one);
                it.halo = halo.transform; it.haloRd = halo.GetComponent<Renderer>();
                it.tag = ModelFactory.MakeLabel(jpFont, 3.2f, 0.8f, null, Color.white, Color.white, Hex(hex), 0f, 0.42f);
                it.tag.main.text = ItemName(d.kind);
                it.im.root.SetActive(false);
                items.Add(it);
            }
        }
    }
}
