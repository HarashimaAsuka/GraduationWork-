using UnityEngine;
using Random = UnityEngine.Random;   // System.Random と取り違えないように

namespace ShoryoumaTaxi
{
    /// <summary>
    /// 売店の品物・お客さんの種類・おみくじ・設定・営業所の記録（社名、所持金、買ったもの、図鑑）。
    /// 記録と設定は PlayerPrefs に保存する（Web版の localStorage に当たる）。
    /// </summary>
    /// <summary>お客さんの種類。rare 0=ふつう 1=珍しい（紫） 2=とても珍しい（金色に光る）。
    /// Soul.cs・Goal.cs からも使うので、TaxiGame の外に置いている。</summary>
    public class GuestDef
    {
        public string id, name, hello, thanks; public int rare; public float w, mul; public bool tanuki, fill;
    }

    public partial class TaxiGame
    {
        // ===================== 売店の品物 =====================
        class ItemDef
        {
            public string id, name, kind, desc; public int max; public int[] price;
            public ItemDef(string id, string name, string kind, int max, int[] price, string desc)
            { this.id = id; this.name = name; this.kind = kind; this.max = max; this.price = price; this.desc = desc; }
        }

        static readonly ItemDef[] ITEMS = {
            new ItemDef("hoof",    "鉄の蹄",         "走り",   3, new[] { 10, 20, 35 }, "脚にはかせる小さな蹄鉄。左右に動く速さが、1段ごとに15%上がる。"),
            new ItemDef("jar",     "水がめ",         "走り",   2, new[] { 15, 30 },     "背中にくくりつける水がめ。水分の上限が、1段ごとに20増える。"),
            new ItemDef("salt",    "上等な塩",       "走り",   2, new[] { 12, 25 },     "清めの力が強い塩。塩1発で減る水分が、1段ごとに25%少なくなる。"),
            new ItemDef("silk",    "クモの糸巻き",   "走り",   2, new[] { 10, 22 },     "相棒の糸を増やす。巣が1段ごとに15%大きくなり、糸も長持ちする。"),
            new ItemDef("lamp",    "迎え火の提灯",   "見通し", 1, new[] { 18 },         "下りで手前から来るものに早く気づける。影・ミラー・声の知らせが遠くから届く。"),
            new ItemDef("bell",    "呼び鈴",         "営業",   1, new[] { 20 },         "空車のとき、近くの魂が鈴の音に寄ってくる。触れられる範囲も広がる。"),
            new ItemDef("charm",   "厄除けのお守り", "消耗品", 3, new[] { 8, 8, 8 },    "攻撃や体当たりを1回だけ防ぐ。3つまで持てる。"),
            new ItemDef("sign",    "社名入りの行灯", "飾り",   1, new[] { 15 },         "精霊馬の背中に、営業所の名前を書いた行灯を立てる。"),
            new ItemDef("gold",    "金のきゅうり",   "走り",   2, new[] { 20, 35 },     "上りの速さが、1段ごとに8%上がる。"),
            new ItemDef("incense", "長い線香",       "時間",   2, new[] { 18, 30 },     "線香が燃えつきるまでが営業時間。1段ごとに30秒のびる。"),
            new ItemDef("juzu",    "数珠",           "塩",     1, new[] { 35 },         "塩が数珠つなぎに3方向へ散って飛ぶ。水分の消費は1発ぶんのまま。"),
            new ItemDef("lotus",   "蓮の座布団",     "営業",   1, new[] { 25 },         "お客さんの座り心地がよくなる。乗車中に1回当たっても、心づけがもらえる。"),
            new ItemDef("neko",    "招き猫",         "営業",   1, new[] { 40 },         "運賃が1人あたり2文上がる。珍しいお客さんも少し来やすくなる。"),
            new ItemDef("hanabi",  "線香花火",       "飾り",   1, new[] { 12 },         "走った跡に、ぱちぱちと火花が散る。"),
        };

        // ===================== お客さん =====================
        static readonly GuestDef[] GUESTS = {
            new GuestDef { id = "jiisan",  name = "村のおじいさん",   rare = 0, w = 30,   mul = 1.0f, hello = "のんびり頼むよ",             thanks = "いい道中だった。ありがとうよ" },
            new GuestDef { id = "kozo",    name = "わんぱく小僧",     rare = 0, w = 22,   mul = 0.8f, hello = "もっと速く！ もっと！",        thanks = "また乗せてね！" },
            new GuestDef { id = "okami",   name = "商家のおかみさん", rare = 0, w = 20,   mul = 1.2f, hello = "急いでおくれ、商いがあるの",   thanks = "お釣りはいらないよ" },
            new GuestDef { id = "oyakata", name = "漁師の親方",       rare = 0, w = 16,   mul = 1.0f, hello = "波よりは揺れねえな",           thanks = "達者でな！" },
            new GuestDef { id = "samurai", name = "お侍さま",         rare = 1, w = 6,    mul = 1.5f, hello = "くるしゅうない、参れ",         thanks = "見事な手綱さばきであった" },
            new GuestDef { id = "sumo",    name = "お相撲さん",       rare = 1, w = 5,    mul = 1.5f, hello = "どすこい。重くてすまんな",     thanks = "ごっつぁんです！" },
            new GuestDef { id = "neko",    name = "猫の魂",           rare = 1, w = 5,    mul = 1.3f, hello = "にゃあ（あっちへ）",           thanks = "ごろごろ……" },
            new GuestDef { id = "geisha",  name = "芸者さん",         rare = 1, w = 4,    mul = 1.6f, hello = "ゆるりと参りましょ",           thanks = "粋な運転、惚れ惚れしたわ" },
            new GuestDef { id = "tanuki",  name = "化け狸",           rare = 1, w = 4,    mul = 1.0f, hello = "ポンポコ。お代は弾むよ",       thanks = "さあ、受け取っておくれ", tanuki = true },
            new GuestDef { id = "zashiki", name = "座敷わらし",       rare = 2, w = 1.5f, mul = 2.0f, hello = "ふふ、いいことあるよ",         thanks = "営業所に幸せを置いていくね（水分が満タンに）", fill = true },
            new GuestDef { id = "gosenzo", name = "ご先祖さま",       rare = 2, w = 1.5f, mul = 2.0f, hello = "今年も帰ってこられたよ",       thanks = "来年もよろしく頼むよ" },
            new GuestDef { id = "tono",    name = "お殿様",           rare = 2, w = 1,    mul = 3.0f, hello = "余を運ぶ栄誉を与えよう",       thanks = "大儀であった。褒美をとらす！" },
        };

        static readonly Color[] RARE_COL = { new Color(0.62f, 0.91f, 1f), new Color(0.82f, 0.66f, 1f), new Color(1f, 0.82f, 0.38f) };

        GuestDef PickGuest()
        {
            float boost = (Lv("neko") > 0 ? 1.5f : 1f) * (luck != null && luck.rare ? 3f : 1f);
            float sum = 0;
            foreach (var g in GUESTS) sum += g.w * (g.rare > 0 ? boost : 1f);
            float r = Random.value * sum;
            foreach (var g in GUESTS) { r -= g.w * (g.rare > 0 ? boost : 1f); if (r < 0) return g; }
            return GUESTS[0];
        }

        // ===================== おみくじ（営業のはじめに1回引く） =====================
        class Luck
        {
            public string name, text; public float w, fareMul = 1, tipMul = 1, time; public bool rare, fierce; public int bonus;
        }

        static readonly Luck[] OMIKUJI = {
            new Luck { name = "大吉", w = 10, text = "運賃 1.5倍", fareMul = 1.5f },
            new Luck { name = "中吉", w = 20, text = "珍しいお客さんが来やすい", rare = true },
            new Luck { name = "小吉", w = 25, text = "営業時間 +20秒", time = 20 },
            new Luck { name = "吉",   w = 30, text = "心づけ 2倍", tipMul = 2 },
            new Luck { name = "凶",   w = 15, text = "敵が荒ぶる（ノルマボーナス +15文）", fierce = true, bonus = 15 },
        };

        Luck luck = null;   // v19：おみくじはいったん外している（引かない）

        static Luck DrawOmikuji()
        {
            float sum = 0; foreach (var o in OMIKUJI) sum += o.w;
            float r = Random.value * sum;
            foreach (var o in OMIKUJI) { r -= o.w; if (r < 0) return o; }
            return OMIKUJI[3];
        }

        // ===================== 設定 =====================
        enum Hint { Shadow, Mirror, Both, Ring }
        static readonly string[] HINT_LABEL = { "影", "ミラー", "影＋ミラー", "光る輪" };
        Hint hint = Hint.Shadow;
        bool callout = true;
        /// <summary>下りの遊び方。false=よける（置いてある敵を避ける） true=追われる（後ろから敵が迫る）</summary>
        bool chaseMode = false;

        const string PP = "shoryouma.";

        void LoadSettings()
        {
            mouseFollow = PlayerPrefs.GetInt(PP + "mouse", 0) == 1;
            hint = (Hint)Mathf.Clamp(PlayerPrefs.GetInt(PP + "hint", 0), 0, 3);
            callout = PlayerPrefs.GetInt(PP + "callout", 1) == 1;
            chaseMode = PlayerPrefs.GetInt(PP + "down", 0) == 1;
        }

        void SaveSettings()
        {
            PlayerPrefs.SetInt(PP + "mouse", mouseFollow ? 1 : 0);
            PlayerPrefs.SetInt(PP + "hint", (int)hint);
            PlayerPrefs.SetInt(PP + "callout", callout ? 1 : 0);
            PlayerPrefs.SetInt(PP + "down", chaseMode ? 1 : 0);
            PlayerPrefs.Save();
        }

        // ===================== 営業所の記録 =====================
        const string DEFAULT_NAME = "精霊馬";
        string coNameRaw = "";
        int purse, charms;

        string CoName() => string.IsNullOrEmpty(coNameRaw) ? DEFAULT_NAME : coNameRaw;
        readonly System.Collections.Generic.Dictionary<string, int> lvCache = new System.Collections.Generic.Dictionary<string, int>();
        /// <summary>v19：売店・おみくじ・図鑑はいったん外している。SHOP_ON = true で品物の効き目が戻る。</summary>
        const bool SHOP_ON = false;
        int LvRaw(string id) => id == "charm" ? charms : (lvCache.TryGetValue(id, out var n) ? n : 0);
        int Lv(string id) => SHOP_ON ? LvRaw(id) : 0;
        int BookCount(string id) => PlayerPrefs.GetInt(PP + "book." + id, 0);
        int BookSeen() { int n = 0; foreach (var g in GUESTS) if (BookCount(g.id) > 0) n++; return n; }

        void LoadProfile()
        {
            coNameRaw = PlayerPrefs.GetString(PP + "name", "");
            if (coNameRaw.Length > 8) coNameRaw = coNameRaw.Substring(0, 8);
            purse = Mathf.Max(0, PlayerPrefs.GetInt(PP + "purse", 0));
            charms = Mathf.Clamp(PlayerPrefs.GetInt(PP + "charms", 0), 0, 3);
            lvCache.Clear();
            foreach (var it in ITEMS) if (it.id != "charm") lvCache[it.id] = Mathf.Clamp(PlayerPrefs.GetInt(PP + "lv." + it.id, 0), 0, it.max);
        }

        void SaveProfile()
        {
            PlayerPrefs.SetString(PP + "name", coNameRaw);
            PlayerPrefs.SetInt(PP + "purse", purse);
            PlayerPrefs.SetInt(PP + "charms", charms);
            PlayerPrefs.Save();
        }

        void SetLv(string id, int n)
        {
            if (id == "charm") charms = n; else { lvCache[id] = n; PlayerPrefs.SetInt(PP + "lv." + id, n); }
            SaveProfile();
        }

        void AddBook(string id) { PlayerPrefs.SetInt(PP + "book." + id, BookCount(id) + 1); PlayerPrefs.Save(); }

        // ===================== 品物の効き目 =====================
        float MoveMul => 1 + 0.15f * Lv("hoof");
        float MaxWater => 100 + 20 * Lv("jar");
        float SaltCostNow => saltCost * (1 - 0.25f * Lv("salt"));
        float SilkDrain => 20 * (1 - 0.25f * Lv("silk"));
        float WebR => WEB_R * (1 + 0.15f * Lv("silk"));
        float WarnK => Lv("lamp") > 0 ? 1.5f : 1f;
        bool ChaseOn => chaseMode && dir < 0;
    }
}
