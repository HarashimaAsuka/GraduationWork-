using System.Collections.Generic;
using UnityEngine;

namespace ShoryoumaTaxi
{
    /// <summary>
    /// 画面表示（IMGUI）。試作用なので OnGUI で描いている。
    /// 本番では uGUI / UI Toolkit + TextMeshPro（日本語フォント）に置き換えるとよい。
    /// 1280x720 を基準に、画面の高さに合わせて拡大縮小する。
    /// </summary>
    public partial class TaxiGame
    {
        GUIStyle sBody, sSmall, sTiny, sBig, sTitle, sSign, sMsg, sMsgDown, sBtn, sBtnSmall, sToggle, sCenter, sField, sCall;
        static readonly Color cPaper = new Color(0.94f, 0.91f, 0.85f), cDim = new Color(0.65f, 0.62f, 0.71f);
        static readonly Color cWater = new Color(0.42f, 0.77f, 0.91f), cHell = new Color(0.88f, 0.21f, 0.29f);
        static readonly Color cLantern = new Color(0.96f, 0.64f, 0.29f), cFree = new Color(0.56f, 0.89f, 0.6f), cHire = new Color(1f, 0.6f, 0.48f);
        static readonly Color cCuc = new Color(0.49f, 0.76f, 0.33f), cNasu = new Color(0.63f, 0.48f, 0.88f), cNight = new Color(0.075f, 0.067f, 0.106f);
        static readonly Color cCard = new Color(0.12f, 0.105f, 0.17f), cSoulDot = new Color(0.62f, 0.91f, 1f);

        // 画面の行き来
        Mode shopFrom = Mode.Title, bookFrom = Mode.Title, nameNext = Mode.Title;
        int titleTab;                     // 0=あそびかた 1=設定
        string nameEdit = "";
        Vector2 shopScroll, bookScroll;
        readonly List<Goal> hudRiders = new List<Goal>();
        readonly List<Soul> hudSouls = new List<Soul>();
        readonly List<Pickup> hudItems = new List<Pickup>();

        void InitStyles()
        {
            if (sBody != null) return;
            GUIStyle Make(int size, FontStyle fs = FontStyle.Normal, TextAnchor a = TextAnchor.UpperLeft)
            {
                var st = new GUIStyle(GUI.skin.label) { font = jpFont, fontSize = size, fontStyle = fs, alignment = a, wordWrap = true, richText = true };
                st.normal.textColor = cPaper;
                return st;
            }
            sBody = Make(18); sSmall = Make(14); sTiny = Make(12); sBig = Make(30, FontStyle.Bold);
            sTitle = Make(72, FontStyle.Bold); sSign = Make(18, FontStyle.Bold, TextAnchor.MiddleCenter);
            sMsg = Make(24, FontStyle.Normal, TextAnchor.MiddleCenter); sMsgDown = Make(16, FontStyle.Normal, TextAnchor.MiddleCenter);
            sCenter = Make(18, FontStyle.Normal, TextAnchor.MiddleCenter);
            sCall = Make(18, FontStyle.Bold, TextAnchor.MiddleCenter); sCall.wordWrap = false;
            sBtn = new GUIStyle(GUI.skin.button) { font = jpFont, fontSize = 20, fontStyle = FontStyle.Bold };
            sBtnSmall = new GUIStyle(GUI.skin.button) { font = jpFont, fontSize = 15, fontStyle = FontStyle.Bold };
            sToggle = new GUIStyle(GUI.skin.toggle) { font = jpFont, fontSize = 17 };
            sToggle.normal.textColor = sToggle.onNormal.textColor = sToggle.hover.textColor = sToggle.onHover.textColor = cPaper;
            sField = new GUIStyle(GUI.skin.textField) { font = jpFont, fontSize = 30, alignment = TextAnchor.MiddleLeft };
        }

        static void Fill(Rect r, Color c) { var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = o; }
        static void Frame(Rect r, Color c, float w = 2)
        {
            Fill(new Rect(r.x, r.y, r.width, w), c); Fill(new Rect(r.x, r.yMax - w, r.width, w), c);
            Fill(new Rect(r.x, r.y, w, r.height), c); Fill(new Rect(r.xMax - w, r.y, w, r.height), c);
        }
        static void Label(Rect r, string s, GUIStyle st, Color c) { var o = st.normal.textColor; st.normal.textColor = c; GUI.Label(r, s, st); st.normal.textColor = o; }
        static string HexS(Color c) => ColorUtility.ToHtmlStringRGB(c);
        bool Btn(Rect r, string s, bool enabled = true, bool small = false)
        {
            bool o = GUI.enabled; GUI.enabled = o && enabled;
            bool hit = GUI.Button(r, s, small ? sBtnSmall : sBtn);
            GUI.enabled = o;
            return hit;
        }

        float guiScale = 1;

        void OnGUI()
        {
            if (cam == null) return;
            InitStyles();
            guiScale = Screen.height / 720f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(guiScale, guiScale, 1));
            float W = Screen.width / guiScale, H = 720f;

            // 地獄の赤いふち（地獄の近く）と、親玉の攻撃の合図（ふちが赤く点滅）
            if (mode == Mode.Play || mode == Mode.Pause)
            {
                bool bossWarn = false;
                if (boss != null && !boss.dead) { bossWarn = boss.mode == "windup"; foreach (var h in boss.hands) if (h.mode == "aim" || h.mode == "aimS") bossWarn = true; }
                float rim = Mathf.Max(Mathf.Clamp01(1 - Road.Zf(y) / 0.28f) * 0.45f, bossWarn ? 0.45f + 0.35f * Mathf.Sin(t * 18) : 0);
                if (rim > 0.01f) Rim(W, H, new Color(0.88f, 0.12f, 0.1f, rim));
            }
            // 被弾の赤・クリア時の暗転
            if (hurtT > 0) Fill(new Rect(0, 0, W, H), new Color(0.88f, 0.21f, 0.29f, hurtT / 0.35f * 0.35f));
            if (clearT > 0) Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, Mathf.Clamp(Mathf.Clamp01((clearT - 1) / 2), 0, 0.85f)));

            if (mode == Mode.Play || mode == Mode.Pause) DrawHUD(W, H);
            switch (mode)
            {
                case Mode.Title: DrawTitle(W, H); break;
                case Mode.Pause: DrawPause(W, H); break;
                case Mode.Result: DrawResult(W, H); break;
                case Mode.Shop: DrawShop(W, H); break;
                case Mode.Book: DrawBook(W, H); break;
                case Mode.Name: DrawName(W, H); break;
            }
            // v30：BGM の切り替え（左下）
            if (mode != Mode.Name)
            {
                var br = new Rect(16, H - 16 - 30, 170, 30);
                if (GUI.Button(br, bgm.Muted ? "♪ BGM オフ（M）" : "♪ BGM オン（M）", sBtnSmall)) bgm.Toggle();
            }
        }

        /// <summary>画面のふちを、内側へ向かって薄くなる色で塗る。</summary>
        static void Rim(float W, float H, Color c)
        {
            const int N = 10; const float w = 9;
            for (int i = 0; i < N; i++)
            {
                var k = new Color(c.r, c.g, c.b, c.a * (1 - i / (float)N) * 0.35f);
                Fill(new Rect(i * w, 0, w, H), k); Fill(new Rect(W - (i + 1) * w, 0, w, H), k);
                Fill(new Rect(0, i * w, W, w), k); Fill(new Rect(0, H - (i + 1) * w, W, w), k);
            }
        }

        // ============================================================
        //  営業中の表示
        // ============================================================
        void DrawHUD(float W, float H)
        {
            // --- 左上：社名・ステージ・残り時間・水分・ノルマ・運賃・乗っているお客さん ---
            float x = 16, yy = 12, gw = 230;
            var right = new GUIStyle(sBody) { alignment = TextAnchor.UpperRight };
            Label(new Rect(x, yy, gw, 24), $"<b>{CoName()}</b>タクシー", sBody, cLantern); yy += 26;
            var chip = new Rect(x, yy, gw, 22); Fill(chip, new Color(cNight.r, cNight.g, cNight.b, 0.6f));
            Label(new Rect(x + 8, yy + 2, gw - 16, 20), $"ステージ{st.no}　{st.name}", sSmall, cPaper); yy += 28;
            int tl = Mathf.Max(0, Mathf.CeilToInt(timeLeft));
            Label(new Rect(x, yy + 2, gw, 22), "残り時間", sSmall, cDim);
            Label(new Rect(x, yy, gw, 24), $"<b>{tl / 60}:{tl % 60:00}</b>", right, tl <= 20 ? cHell : cPaper); yy += 26;
            Label(new Rect(x, yy + 2, gw, 22), "水分", sSmall, cDim);
            Label(new Rect(x, yy, gw, 24), $"<b>{Mathf.CeilToInt(Mathf.Max(0, water))}</b>", right, cPaper); yy += 24;
            Fill(new Rect(x, yy, gw, 9), new Color(1, 1, 1, 0.12f));
            Fill(new Rect(x, yy, gw * Mathf.Clamp01(water / 100f), 9), water < 25 ? cHell : cWater); yy += 16;
            if (!st.boss) { Label(new Rect(x, yy + 2, gw, 22), "送り届けた魂", sSmall, cDim); Label(new Rect(x, yy, gw, 24), $"<b>{delivered} / {st.quota}</b>", right, cPaper); yy += 24; }
            Label(new Rect(x, yy + 2, gw, 22), "今日の運賃", sSmall, cDim); Label(new Rect(x, yy, gw, 24), $"<b>{mon} 文</b>", right, fineFlash > 0 ? cHell : cPaper); yy += 24;
            string eff = (wind > 0 ? $"追い風 {wind:0.0}秒　" : "") + (charmOn ? "お守り" : "");
            if (eff.Length > 0) { Label(new Rect(x, yy, gw, 22), $"<b>{eff}</b>", sSmall, new Color(0.61f, 0.89f, 0.54f)); yy += 22; }
            // 乗っているお客さん：近い行き先から順に
            hudRiders.Clear(); hudRiders.AddRange(riders);
            hudRiders.Sort((a, b) => Mathf.Abs(a.y - y).CompareTo(Mathf.Abs(b.y - y)));
            foreach (var r in hudRiders)
            {
                var rr = new Rect(x, yy + 2, gw, 22);
                Fill(rr, new Color(cNight.r, cNight.g, cNight.b, 0.62f));
                Fill(new Rect(rr.x, rr.y, 3, rr.height), RARE_COL[r.guest.rare]);
                Label(new Rect(rr.x + 8, rr.y + 2, gw - 100, 20), r.goalName, sSmall, cPaper);
                Label(new Rect(rr.x, rr.y + 2, gw - 46, 20), $"<b>{(r.y > y ? "▲" : "▼")}{Mathf.CeilToInt(Mathf.Abs(r.y - y) / 10)}丈</b>", new GUIStyle(sSmall) { alignment = TextAnchor.UpperRight }, cPaper);
                Label(new Rect(rr.x, rr.y + 2, gw - 6, 20), r.guest.tanuki ? "？？" : $"{r.fare}文", new GUIStyle(sSmall) { alignment = TextAnchor.UpperRight }, cLantern);
                yy += 25;
            }

            // --- 上中央：行灯（空車／乗車中）と、進む向き ---
            bool up = (want > 0) != Road.Flip;
            int n = riders.Count;
            string sign = (st.boss ? "<b>親玉</b>　塩で祓う" : n > 0 ? $"<b>乗車 {n}/{MAX_RIDERS}</b>　遅さ −{Mathf.RoundToInt(n * SLOW_PER * 100)}%" : "<b>空車</b>　お客さんを探す")
                + $"　　{(up ? "▲" : "▼")} {(want > 0 ? "前へ" : "後ろへ")}";
            var sc = st.boss || n > 0 ? cHire : cFree;
            float sw = Mathf.Min(460, W - 2 * (gw + 40));
            var sr = new Rect((W - sw) / 2, 12, sw, 38);
            Fill(sr, new Color(cNight.r, cNight.g, cNight.b, 0.75f)); Frame(sr, sc);
            Label(sr, sign, sSign, sc);

            // --- 親玉の体力 ---
            if (boss != null)
            {
                float bw = Mathf.Min(420, W * 0.56f);
                var lr = new Rect((W - bw) / 2, 58, bw, 20);
                string[] PH = { "", "一の段", "二の段（熟れ）", "三の段（割れ）" };
                string lbl = $"鬼ザクロの親玉　{PH[Mathf.Clamp(boss.phase, 1, 3)]}{(boss.stun > 0 ? "　★弱点が出ている！" : boss.shield ? "　守りあり" : "")}";
                Label(lr, $"<b>{lbl}</b>", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter }, new Color(1f, 0.7f, 0.6f));
                Fill(new Rect(lr.x, lr.yMax + 2, bw, 10), new Color(1, 1, 1, 0.12f));
                Fill(new Rect(lr.x, lr.yMax + 2, bw * Mathf.Clamp01(Mathf.Max(0, boss.hp) / (float)boss.max), 10), cHell);
            }

            // --- バックミラー（後ろ向き）：映像は左右反転して、本物のミラーと同じ向きにする ---
            if (mirrorCam.enabled && mirrorRT != null)
            {
                float mwid = Mathf.Min(380, W * 0.52f), mh = mwid / 2.4f;
                var mr = new Rect((W - mwid) / 2, boss != null ? 100 : 88, mwid, mh);
                Fill(new Rect(mr.x - 7, mr.y - 7, mr.width + 14, mr.height + 14), new Color(0.17f, 0.11f, 0.09f));
                GUI.DrawTextureWithTexCoords(mr, mirrorRT, new Rect(1, 0, -1, 1));
                Label(new Rect(mr.x, mr.yMax + 8, mr.width, 18), "バックミラー（手前から来るもの）", new GUIStyle(sTiny) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold }, cDim);
            }

            // --- 右：高度メーター（上が現世、下が地獄） ---
            float mx = W - 30, top = 70, bot = H - 40, mH = bot - top;
            for (int i = 0; i < 60; i++)
            {
                float f0 = 1 - i / 60f;
                Fill(new Rect(mx, top + mH * i / 60f, 16, mH / 60f + 1), ZoneLook(f0, z => z.bg) * 1.8f + new Color(0.05f, 0.05f, 0.05f));
            }
            Frame(new Rect(mx, top, 16, mH), new Color(1, 1, 1, 0.25f), 1);
            void Zone(float f, string name, Color c) => Label(new Rect(mx - 96, top + mH * (1 - f) - 10, 88, 20), name, new GUIStyle(sSmall) { alignment = TextAnchor.MiddleRight }, c);
            Zone(1f, "現世", cLantern); Zone(0.83f, "夜空", cDim); Zone(0.58f, "雲海", cDim); Zone(0.33f, "三途の霧", cDim); Zone(0f, "地獄", cHell);
            float MY(float yy2) => top + mH * (1 - Road.Zf(yy2));
            // 流れる縞：進む向きへ流れる
            float flow = Mathf.Repeat(t * (up ? 30f : -15f), 16f);
            for (float s = -flow; s < mH; s += 16) if (s >= 0) Fill(new Rect(mx + 2, top + s, 12, 2), new Color(1, 1, 1, 0.35f));
            // 待っている魂（近い4人）
            if (n < MAX_RIDERS)
            {
                hudSouls.Clear(); foreach (var s in souls) if (!s.gone) hudSouls.Add(s);
                hudSouls.Sort((a, b) => Mathf.Abs(a.y - y).CompareTo(Mathf.Abs(b.y - y)));
                for (int i = 0; i < hudSouls.Count && i < 4; i++)
                {
                    float sy = MY(hudSouls[i].y); var dc = RARE_COL[hudSouls[i].guest.rare];
                    Fill(new Rect(mx + 1, sy - 7, 14, 14), new Color(dc.r, dc.g, dc.b, 0.35f));
                    Fill(new Rect(mx + 3, sy - 5, 10, 10), dc);
                }
            }
            // アイテム（近い4つ。ひし形の代わりに小さな四角）
            hudItems.Clear(); foreach (var it in items) if (!it.taken) hudItems.Add(it);
            hudItems.Sort((a, b) => Mathf.Abs(a.y - y).CompareTo(Mathf.Abs(b.y - y)));
            for (int i = 0; i < hudItems.Count && i < 4; i++)
            {
                float iy = MY(hudItems[i].y); var ic = Hex(ItemHex(hudItems[i].kind));
                Fill(new Rect(mx + 14, iy - 6, 12, 12), cNight); Fill(new Rect(mx + 16, iy - 4, 8, 8), ic);
            }
            // 行き先（乗っているお客さん全員）
            foreach (var r in riders)
            {
                float gy = MY(r.y);
                Fill(new Rect(mx - 6, gy - 3, 28, 6), new Color(1f, 0.42f, 0.29f));
            }
            float my = MY(y);
            var dotC = want > 0 ? cCuc : cNasu;
            Fill(new Rect(mx - 6, my - 14, 28, 28), dotC);
            Label(new Rect(mx - 6, my - 14, 28, 28), up ? "▲" : "▼", sCenter, cNight);
            var nearest = hudRiders.Count > 0 ? hudRiders[0] : null;
            string tag = nearest != null ? $"{nearest.goalName}まで {Mathf.CeilToInt(Mathf.Abs(nearest.y - y) / 10)}丈"
                : st.boss ? (want > 0 ? $"現世の出口まで {Mathf.Max(0, Mathf.CeilToInt((L - 30 - y) / 10))}丈" : $"地獄の底まで {Mathf.Max(0, Mathf.CeilToInt((y - 30) / 10))}丈")
                : World.ZoneName(Road.Zf(y));
            var tr = new Rect(mx - 196, my - 13, 184, 26);
            Fill(tr, new Color(cNight.r, cNight.g, cNight.b, 0.85f));
            Label(tr, tag, new GUIStyle(sCenter) { fontSize = 15 }, cPaper);

            bool down = dir < 0;
            // --- メッセージ：後ろ向きのときは画面のいちばん下に小さく（手前から来るものを隠さない） ---
            if (msg != null)
            {
                var m = msg.Value; float age = t - m.t0;
                float a = Mathf.Clamp01(Mathf.Min(age / 0.12f, (m.dur - age) / 0.4f));
                if (a > 0)
                {
                    Rect r = down ? new Rect((W - Mathf.Min(620, W - 32)) / 2, H - 12 - 34, Mathf.Min(620, W - 32), 34)
                                  : new Rect(W * 0.12f, H * 0.8f, W * 0.76f, 48);
                    Fill(r, new Color(cNight.r, cNight.g, cNight.b, (down ? 0.5f : 0.6f) * a));
                    Label(r, m.text, down ? sMsgDown : sMsg, new Color(cPaper.r, cPaper.g, cPaper.b, a));
                }
            }
        }

        void Panel(float W, float H, float w, float h, out Rect r)
        {
            Fill(new Rect(0, 0, W, H), new Color(cNight.r, cNight.g, cNight.b, 0.8f));
            r = new Rect((W - w) / 2, (H - h) / 2, w, h);
        }

        // ============================================================
        //  タイトル（左：社名とボタン・ステージを選ぶ　右：あそびかた／設定）
        // ============================================================
        void DrawTitle(float W, float H)
        {
            float tw = Mathf.Min(1120, W - 48);
            bool twoCol = tw >= 900;
            Panel(W, H, tw, 620, out var r);
            float lw = twoCol ? tw * 0.5f : tw, yy = r.y;
            Label(new Rect(r.x, yy, lw, 22), "3D 縦穴シューティング ／ 3ステージ", sSmall, cDim); yy += 28;
            string n = CoName();
            var ts = new GUIStyle(sTitle) { fontSize = n.Length <= 4 ? 72 : n.Length <= 6 ? 58 : 46, wordWrap = false };
            Label(new Rect(r.x, yy, lw, 84), n, ts, cPaper); yy += 84;
            Label(new Rect(r.x, yy, lw, 40), "タクシー地獄営業所", sBig, cLantern); yy += 52;
            Label(new Rect(r.x, yy, lw - 16, 130),
                "お盆の夜、地獄と現世をつなぐ縦の道で、魂のお客さんを送り届ける。魂の上の札で行き先を見て、誰を乗せるか選ぶ。5人まで乗れるが、乗せるほど遅くなる。敵にぶつかると向きが反転して、後ろへ進んでしまう。",
                sBody, cPaper); yy += 132;
            if (Btn(new Rect(r.x, yy, 240, 52), "営業を始める")) Begin(1);
            if (Btn(new Rect(r.x + 252, yy, 170, 52), "社名を変える", true, true)) OpenName(Mode.Title);
            yy += 64;
            Label(new Rect(r.x, yy + 10, 110, 22), "ステージを選ぶ", sSmall, cDim);
            if (Btn(new Rect(r.x + 112, yy, 96, 40), "1 送り", true, true)) Begin(1);
            if (Btn(new Rect(r.x + 214, yy, 96, 40), "2 帰り", true, true)) Begin(2);
            if (Btn(new Rect(r.x + 316, yy, 140, 40), "3 幻の三日目", true, true)) Begin(3);

            Rect pr = twoCol ? new Rect(r.x + lw + 16, r.y + 28, tw - lw - 16, 560) : new Rect(r.x, yy + 54, tw, r.yMax - yy - 54);
            titleTab = GUI.Toolbar(new Rect(pr.x, pr.y, 260, 34), titleTab, new[] { "あそびかた", "設定" }, sBtnSmall);
            var box = new Rect(pr.x, pr.y + 42, pr.width, pr.height - 42);
            Fill(box, cCard);
            var inner = new Rect(box.x + 16, box.y + 14, box.width - 32, box.height - 28);
            if (titleTab == 0)
                Label(inner,
                    "・魂の上の札に、行き先・距離・運賃が出る。触れると乗車（5人まで。1人ごとに10%遅くなる）。満席で触れると、いちばん前のお客さんが押し出される。\n" +
                    "・行き先の門をくぐると降りて運賃。\n" +
                    "・敵にぶつかると向きが反転し、お客さんを1人落とす。もう一度ぶつかると元に戻る。後ろ向きのときは、弾に当たっても前に戻る。道の端でも折り返す。\n" +
                    "・道中のアイテム：水（水分）・風車（ぐんと加速）・線香（時間）・お守り（1回守る）・ナス（向きが反転）\n" +
                    "・ステージ1「送り」・2「帰り」はノルマの人数を送り届けるとクリア。3「幻の三日目」は親玉を祓う。道の終わりに着くと負け。\n\n" +
                    "<color=#A69EB5>左クリック / Z / Space：塩（撃つほど干からびる）　WASD・矢印：移動　P / Esc：一時停止　M：BGM</color>",
                    sSmall, cPaper);
            else DrawSettings(inner);
        }

        /// <summary>設定（タイトルと一時停止で共通）。変えるとすぐ保存する。</summary>
        void DrawSettings(Rect r)
        {
            float yy = r.y;
            bool mf = GUI.Toggle(new Rect(r.x, yy, r.width, 28), mouseFollow, " マウスの位置に馬がついていく", sToggle); yy += 40;
            Label(new Rect(r.x, yy, r.width, 22), "後ろ向きのとき、手前から来るものの知らせ方", sSmall, cDim); yy += 24;
            var h = (Hint)GUI.Toolbar(new Rect(r.x, yy, Mathf.Min(r.width, 440), 32), (int)hint, HINT_LABEL, sBtnSmall); yy += 44;
            if (mf != mouseFollow || h != hint)
            {
                mouseFollow = mf; hint = h;
                SaveSettings();
            }
        }

        void DrawPause(float W, float H)
        {
            Panel(W, H, Mathf.Min(560, W - 32), 400, out var r);
            Label(new Rect(r.x, r.y, r.width, 40), "一時停止", sBig, cPaper);
            DrawSettings(new Rect(r.x, r.y + 54, r.width, 200));
            float by = r.y + 330;
            if (Btn(new Rect(r.x, by, 170, 48), "続ける")) Resume();
            if (Btn(new Rect(r.x + 182, by, 170, 48), "社名を変える", true, true)) OpenName(Mode.Pause);
            if (Btn(new Rect(r.x + 364, by, 170, 48), "タイトルへ")) ToTitle();
        }

        void DrawResult(float W, float H)
        {
            Panel(W, H, Mathf.Min(640, W - 32), 470, out var r);
            Label(new Rect(r.x, r.y, r.width, 24), resultTitle, sSmall, cDim);
            Label(new Rect(r.x, r.y + 30, r.width, 120), resultText, new GUIStyle(sBig) { fontSize = 24 }, cPaper);
            string[] k = { "送り届けた魂", "運賃", "落としたお客さん", "祓った敵" };
            string[] v = { $"{delivered} 人", $"{mon} 文", $"{thrown} 人", $"{kills}" };
            for (int i = 0; i < 4; i++)
            {
                var cell = new Rect(r.x + (i % 2) * (r.width / 2 + 5), r.y + 160 + (i / 2) * 76, r.width / 2 - 5, 68);
                Fill(cell, cCard);
                Label(new Rect(cell.x + 12, cell.y + 6, cell.width, 20), k[i], sSmall, cDim);
                Label(new Rect(cell.x + 12, cell.y + 26, cell.width, 36), $"<b>{v[i]}</b>", sBig, cPaper);
            }
            float by = r.y + 330;
            float bx = r.x;
            if (resultNext) { if (Btn(new Rect(bx, by, 220, 50), "次のステージへ")) Begin(Mathf.Min(3, stage + 1)); bx += 232; }
            if (Btn(new Rect(bx, by, 240, 50), resultClear ? "もう一度、このステージ" : "もう一度挑戦する", true, true)) Begin(stage);
            bx += 252;
            if (Btn(new Rect(bx, by, 140, 50), "タイトルへ", true, true)) ToTitle();
        }

        // ============================================================
        //  売店
        // ============================================================
        void OpenShop(Mode from) { shopFrom = from; mode = Mode.Shop; shopScroll = Vector2.zero; }

        void CloseShop()
        {
            if (shopFrom == Mode.Play) Resume();
            else mode = shopFrom;
        }

        void DrawShop(float W, float H)
        {
            float pw = Mathf.Min(1060, W - 32);
            Panel(W, H, pw, 660, out var r);
            Label(new Rect(r.x, r.y, pw, 22), shopFrom == Mode.Play ? "地獄営業所に戻ってきた" : "売店", sSmall, cDim);
            Label(new Rect(r.x, r.y + 22, pw, 40), $"{CoName()}タクシー地獄営業所の売店", sBig, cPaper);
            Label(new Rect(r.x, r.y + 22, pw, 40), $"所持金 <b>{purse}</b> 文", new GUIStyle(sBody) { alignment = TextAnchor.MiddleRight }, cLantern);

            int cols = pw >= 900 ? 3 : 2;
            float gap = 12, cw = (pw - 20 - gap * (cols - 1)) / cols, ch = 168;
            int rows = (ITEMS.Length + cols - 1) / cols;
            var view = new Rect(r.x, r.y + 72, pw, 520);
            var content = new Rect(0, 0, pw - 20, rows * (ch + gap));
            shopScroll = GUI.BeginScrollView(view, shopScroll, content);
            for (int i = 0; i < ITEMS.Length; i++)
            {
                var it = ITEMS[i];
                var c = new Rect((i % cols) * (cw + gap), (i / cols) * (ch + gap), cw, ch);
                Fill(c, cCard);
                int n = LvRaw(it.id); bool maxed = n >= it.max; int price = maxed ? 0 : it.price[n];
                Label(new Rect(c.x + 12, c.y + 8, cw - 24, 24), $"<b>{it.name}</b>", sBody, cPaper);
                Label(new Rect(c.x + 12, c.y + 10, cw - 24, 20), it.kind, new GUIStyle(sTiny) { alignment = TextAnchor.UpperRight }, cLantern);
                Label(new Rect(c.x + 12, c.y + 36, cw - 24, 64), it.desc, sTiny, cDim);
                for (int p = 0; p < it.max; p++)
                    Fill(new Rect(c.x + 12 + p * 18, c.y + 104, 12, 12), p < n ? cLantern : new Color(1, 1, 1, 0.15f));
                string label = maxed ? (it.id == "charm" ? "これ以上持てない" : "買い済み") : purse < price ? $"{price}文　お金が足りない" : $"{price}文で買う";
                if (Btn(new Rect(c.x + 12, c.y + ch - 44, cw - 24, 34), label, !maxed && purse >= price, true)) Buy(it);
            }
            GUI.EndScrollView();
            if (Btn(new Rect(r.x, r.yMax - 52, 220, 48), shopFrom == Mode.Play ? "営業に戻る" : "閉じる")) CloseShop();
        }

        void Buy(ItemDef it)
        {
            int n = LvRaw(it.id);
            if (n >= it.max || purse < it.price[n]) return;
            purse -= it.price[n];
            SetLv(it.id, n + 1);   // お守りは所持数、ほかは段階が上がる（中で保存する）
            sfx.Play(sfx.catchSilk);
            if (it.id == "jar" && shopFrom == Mode.Play) water = Mathf.Min(MaxWater, water + 20);
        }

        // ============================================================
        //  お客さん図鑑
        // ============================================================
        void OpenBook(Mode from) { bookFrom = from; mode = Mode.Book; bookScroll = Vector2.zero; }

        void DrawBook(float W, float H)
        {
            float pw = Mathf.Min(1060, W - 32);
            Panel(W, H, pw, 660, out var r);
            Label(new Rect(r.x, r.y, pw, 22), "お客さん図鑑", sSmall, cDim);
            Label(new Rect(r.x, r.y + 22, pw, 40), $"見つけたお客さん <b>{BookSeen()}</b> / {GUESTS.Length}", sBig, cPaper);
            int cols = pw >= 900 ? 3 : 2;
            float gap = 12, cw = (pw - 20 - gap * (cols - 1)) / cols, ch = 124;
            int rows = (GUESTS.Length + cols - 1) / cols;
            var view = new Rect(r.x, r.y + 72, pw, 520);
            var content = new Rect(0, 0, pw - 20, rows * (ch + gap));
            bookScroll = GUI.BeginScrollView(view, bookScroll, content);
            string[] STARS = { "★", "★★", "★★★" };
            for (int i = 0; i < GUESTS.Length; i++)
            {
                var g = GUESTS[i];
                var c = new Rect((i % cols) * (cw + gap), (i / cols) * (ch + gap), cw, ch);
                int cnt = BookCount(g.id);
                var rc = RARE_COL[g.rare];
                Fill(c, cCard);
                Fill(new Rect(c.x, c.y, 4, ch), cnt > 0 ? rc : new Color(rc.r, rc.g, rc.b, 0.3f));
                Label(new Rect(c.x + 14, c.y + 8, cw - 28, 24), cnt > 0 ? $"<b>{g.name}</b>" : "<b>？？？</b>", sBody, cnt > 0 ? cPaper : cDim);
                Label(new Rect(c.x + 14, c.y + 10, cw - 28, 20), STARS[g.rare], new GUIStyle(sSmall) { alignment = TextAnchor.UpperRight }, rc);
                string body = cnt > 0
                    ? $"「{g.hello}」\n運賃 ×{g.mul}{(g.tanuki ? "（木の葉か、3倍か）" : "")}{(g.fill ? "・水分が満タンに" : "")}\n送り届けた回数 {cnt}"
                    : "まだ乗せたことがない。" + (g.rare == 2 ? "金色に光る魂らしい。" : g.rare == 1 ? "紫に光る魂らしい。" : "");
                Label(new Rect(c.x + 14, c.y + 38, cw - 28, ch - 44), body, sTiny, cnt > 0 ? cPaper : cDim);
            }
            GUI.EndScrollView();
            if (Btn(new Rect(r.x, r.yMax - 52, 220, 48), "閉じる")) mode = bookFrom;
        }

        // ============================================================
        //  社名を決める（初めての営業の前と、タイトル・一時停止から）
        // ============================================================
        void OpenName(Mode next) { nameNext = next; nameEdit = coNameRaw; mode = Mode.Name; }

        void CloseName(bool ok)
        {
            if (ok)
            {
                string n = (nameEdit ?? "").Trim();
                if (n.Length > 8) n = n.Substring(0, 8);
                coNameRaw = n.Length > 0 ? n : DEFAULT_NAME;
                SaveProfile(); ApplyName();
            }
            if (nameNext == Mode.Play) { if (ok) StartStage(nameStage); else mode = Mode.Title; }
            else mode = nameNext;
        }

        void DrawName(float W, float H)
        {
            Panel(W, H, Mathf.Min(620, W - 32), 330, out var r);
            var ev = Event.current;
            if (ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter)) { ev.Use(); CloseName(true); return; }
            Label(new Rect(r.x, r.y, r.width, 22), "営業所の名前", sSmall, cDim);
            Label(new Rect(r.x, r.y + 24, r.width, 40), "○○タクシー地獄営業所の「○○」を決める", sBody, cPaper);
            GUI.SetNextControlName("coName");
            nameEdit = GUI.TextField(new Rect(r.x, r.y + 76, r.width, 52), nameEdit ?? "", 8, sField);
            if (GUI.GetNameOfFocusedControl() != "coName") GUI.FocusControl("coName");
            string pv = string.IsNullOrEmpty((nameEdit ?? "").Trim()) ? DEFAULT_NAME : nameEdit.Trim();
            Label(new Rect(r.x, r.y + 140, r.width, 40), $"<b>{pv}</b><color=#{HexS(cLantern)}>タクシー地獄営業所</color>", new GUIStyle(sBig) { fontSize = 26 }, cPaper);
            Label(new Rect(r.x, r.y + 186, r.width, 22), "8文字まで。あとからタイトル・一時停止でいつでも変えられる。", sTiny, cDim);
            if (Btn(new Rect(r.x, r.y + 240, 200, 50), "決める")) CloseName(true);
            if (Btn(new Rect(r.x + 212, r.y + 240, 220, 50), nameNext == Mode.Pause ? "一時停止に戻る" : "戻る", true, true)) CloseName(false);
        }
    }
}
