using System;
using Unity.Burst.CompilerServices;
using UnityEngine;
using static UnityEditor.Progress;

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
            sTitle = Make(72, FontStyle.Bold); sSign = Make(20, FontStyle.Bold, TextAnchor.MiddleCenter);
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
        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
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

            // 被弾の赤・クリア時の暗転
            if (hurtT > 0) Fill(new Rect(0, 0, W, H), new Color(0.88f, 0.21f, 0.29f, hurtT / 0.35f * 0.35f));
            if (clearT > 0) Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, Mathf.Clamp01((clearT - 1) / 2) * 0.85f));

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
        }

        // ============================================================
        //  営業中の表示
        // ============================================================
        void DrawHUD(float W, float H)
        {
            // --- 左上：ゲージ ---
            float x = 16, yy = 14, gw = 210;
            var right = new GUIStyle(sBody) { alignment = TextAnchor.UpperRight };
            float mw = MaxWater;
            Label(new Rect(x, yy, gw, 24), "水分", sSmall, cDim);
            Label(new Rect(x, yy - 2, gw, 26), $"<b>{Mathf.CeilToInt(Mathf.Max(0, water))}</b>", right, cPaper);
            Fill(new Rect(x, yy + 24, gw, 9), new Color(1, 1, 1, 0.12f));
            Fill(new Rect(x, yy + 24, gw * Mathf.Clamp01(water / mw), 9), water < 25 ? cHell : cWater);
            Label(new Rect(x, yy + 38, gw, 22), "クモの糸", sSmall, cDim);
            Fill(new Rect(x, yy + 60, gw, 4), new Color(1, 1, 1, 0.12f));
            Fill(new Rect(x, yy + 60, gw * silk / 100f, 4), new Color(0.87f, 0.91f, 0.95f, silkLock ? 0.35f : 1f));
            float row = yy + 70;
            void Row(string k, string v, Color c) { Label(new Rect(x, row + 2, gw, 22), k, sSmall, cDim); Label(new Rect(x, row, gw, 24), $"<b>{v}</b>", right, c); row += 24; }
            Row("送り届けた魂", $"{delivered} / {goalCount}", cPaper);
            Row("運賃", $"{mon} 文", fineFlash > 0 ? cHell : cPaper);
            Row("所持金", $"{purse} 文", cPaper);
            if (charms > 0) Row("お守り", $"×{charms}", new Color(1f, 0.88f, 0.54f));

            // --- 上中央：行灯（空車／賃走）、残り時間、おみくじ ---
            bool up = want > 0;
            string sign = carrying && goal != null ? $"賃走　{goal.goalName}まで　{(up ? "▲上り" : "▼下り")}" : $"空車　お客さんを探す　{(up ? "▲上り" : "▼下り")}";
            var sc = carrying ? cHire : cFree;
            float sw = Mathf.Min(520, W - 2 * (gw + 40));
            var sr = new Rect((W - sw) / 2, 12, sw, 40);
            Fill(sr, new Color(cNight.r, cNight.g, cNight.b, 0.75f)); Frame(sr, sc);
            Label(sr, sign, sSign, sc);
            int tl = Mathf.Max(0, Mathf.CeilToInt(timeLeft));
            string luckText = luck != null ? $"　　おみくじ：{luck.name}　{luck.text}" : "";
            Label(new Rect((W - sw) / 2, 56, sw, 24), $"残り時間 <b>{tl / 60}:{tl % 60:00}</b>{luckText}", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter }, tl <= 30 ? cHell : cPaper);

            // --- バックミラー（下り）：映像は左右反転して、本物のミラーと同じ向きにする ---
            if (mirrorCam.enabled && mirrorRT != null)
            {
                float mwid = Mathf.Min(380, W * 0.52f), mh = mwid / 2.4f;
                var mr = new Rect((W - mwid) / 2, 88, mwid, mh);
                Fill(new Rect(mr.x - 7, mr.y - 7, mr.width + 14, mr.height + 14), new Color(0.17f, 0.11f, 0.09f));
                GUI.DrawTextureWithTexCoords(mr, mirrorRT, new Rect(1, 0, -1, 1));
                Label(new Rect(mr.x, mr.yMax + 8, mr.width, 18), "バックミラー（手前から来るもの）", new GUIStyle(sTiny) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold }, cDim);
            }

            // --- 右：高度メーター ---
            float mx = W - 30, top = 70, bot = H - 40, mH = bot - top;
            for (int i = 0; i < 60; i++)
            {
                float f0 = 1 - i / 60f;
                Fill(new Rect(mx, top + mH * i / 60f, 16, mH / 60f + 1), World.ZoneColor(f0, z => z.bg) * 1.8f + new Color(0.05f, 0.05f, 0.05f));
            }
            Frame(new Rect(mx, top, 16, mH), new Color(1, 1, 1, 0.25f), 1);
            void Zone(float f, string name, Color c) => Label(new Rect(mx - 96, top + mH * (1 - f) - 10, 88, 20), name, new GUIStyle(sSmall) { alignment = TextAnchor.MiddleRight }, c);
            Zone(1f, "現世", cLantern); Zone(0.83f, "夜空", cDim); Zone(0.58f, "雲海", cDim); Zone(0.33f, "三途の霧", cDim); Zone(0f, "地獄", cHell);
            // 流れる縞：進む向きへ流れる
            float flow = Mathf.Repeat(t * (up ? 30f : -15f), 16f);
            for (float s = -flow; s < mH; s += 16) if (s >= 0) Fill(new Rect(mx + 2, top + s, 12, 2), new Color(1, 1, 1, 0.35f));
            // 空車のとき、前方の近い魂を水色の点で出す
            if (!carrying)
            {
                int shown = 0; float lastD = 0;
                for (int n = 0; n < 3; n++)
                {
                    Soul nearest = null; float nd = float.MaxValue;
                    foreach (var s in souls) { float d = Rel(s.y); if (d > lastD + 0.001f && d < nd) { nd = d; nearest = s; } }
                    if (nearest == null) break;
                    lastD = nd; shown++;
                    float sy = top + mH * (1 - Mathf.Clamp01(nearest.y / L));
                    var dc = RARE_COL[nearest.guest != null ? nearest.guest.rare : 0];
                    Fill(new Rect(mx + 1, sy - 7, 14, 14), new Color(dc.r, dc.g, dc.b, 0.35f));
                    Fill(new Rect(mx + 3, sy - 5, 10, 10), dc);
                }
            }
            if (goal != null)
            {
                float gy = top + mH * (1 - goal.y / L);
                Fill(new Rect(mx - 6, gy - 3, 28, 6), new Color(1f, 0.42f, 0.29f));
                Label(new Rect(mx - 70, gy - 10, 60, 20), "行き先", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold }, new Color(1f, 0.7f, 0.6f));
            }
            float my = top + mH * (1 - Mathf.Clamp01(y / L));
            var dotC = up ? cCuc : cNasu;
            Fill(new Rect(mx - 6, my - 14, 28, 28), dotC);
            Label(new Rect(mx - 6, my - 14, 28, 28), up ? "▲" : "▼", sCenter, cNight);
            string tag = goal != null ? $"行き先まで {Mathf.CeilToInt(Mathf.Abs(goal.y - y) / 10)}丈" : World.ZoneName(y / L);
            var tr = new Rect(mx - 168, my - 13, 156, 26);
            Fill(tr, new Color(cNight.r, cNight.g, cNight.b, 0.85f));
            Label(tr, tag, sCenter, cPaper);

            bool down = dir < 0;

            // --- 声の知らせ：上りは馬の上に吹き出し、下りは画面の下にまとめる（前を隠さない） ---
            if (mode == Mode.Play && call != null && t - call.t0 < 1.6f)
            {
                float age = t - call.t0, a = age > 1.3f ? (1.6f - age) / 0.3f : 1f;
                var edge = call.danger ? new Color(1f, 0.48f, 0.35f) : new Color(0.56f, 0.86f, 1f);
                string text = $"<size={(down ? 13 : 15)}><color=#{Hex(edge)}>{call.who}</color></size>  <color=#FFD27A>{call.arrow}</color>  {call.text}";
                var st = new GUIStyle(sCall) { fontSize = down ? 15 : 18 };
                var size = st.CalcSize(new GUIContent(text));
                float bw = size.x + 26, bh = size.y + 10, cx, by;
                if (down) { cx = W / 2; by = H - 58 - bh; }
                else
                {
                    var p = cam.WorldToScreenPoint(steed.root.transform.position);
                    cx = p.z > 0 ? p.x / guiScale : W / 2;
                    float gyy = p.z > 0 ? (Screen.height - p.y) / guiScale : H / 2;
                    by = Mathf.Max(60, gyy - 54) - bh;
                }
                var br = new Rect(cx - bw / 2, by, bw, bh);
                Fill(br, new Color(cNight.r, cNight.g, cNight.b, (down ? 0.62f : 0.9f) * a));
                Frame(br, new Color(edge.r, edge.g, edge.b, a));
                if (!down) Fill(new Rect(cx - 6, br.yMax, 12, 8), new Color(edge.r, edge.g, edge.b, a));
                Label(br, text, st, new Color(cPaper.r, cPaper.g, cPaper.b, a));
            }

            // --- メッセージ：下りは画面のいちばん下に小さく（手前から来るものを隠さない） ---
            if (msg != null)
            {
                var m = msg.Value; float age = t - m.t0;
                float a = Mathf.Clamp01(Mathf.Min(age / 0.35f, (m.dur - age) / 0.4f));
                if (a > 0)
                {
                    Rect r = down ? new Rect((W - Mathf.Min(560, W - 32)) / 2, H - 12 - 34, Mathf.Min(560, W - 32), 34)
                                  : new Rect(W * 0.12f, H * 0.74f, W * 0.76f, 52);
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
        //  タイトル（左：社名とボタン　右：あそびかた／設定）
        // ============================================================
        void DrawTitle(float W, float H)
        {
            float tw = Mathf.Min(1120, W - 48);
            bool twoCol = tw >= 900;
            Panel(W, H, tw, 620, out var r);
            float lw = twoCol ? tw * 0.5f : tw, yy = r.y;
            Label(new Rect(r.x, yy, lw, 22), "3D 縦穴タクシー ／ 約5分", sSmall, cDim); yy += 28;
            string n = CoName();
            var ts = new GUIStyle(sTitle) { fontSize = n.Length <= 4 ? 72 : n.Length <= 6 ? 58 : 46, wordWrap = false };
            Label(new Rect(r.x, yy, lw, 84), n, ts, cPaper); yy += 84;
            Label(new Rect(r.x, yy, lw, 40), "タクシー地獄営業所", sBig, cLantern); yy += 52;
            Label(new Rect(r.x, yy, lw - 16, 120),
                "お盆の夜、地獄と現世をつなぐ縦の道で、魂のお客さんを送り届ける。上りはきゅうりの馬、下りはナスの牛。遠くまで運ぶほど運賃が上がる。稼いだお金は売店で使える。",
                sBody, cPaper); yy += 118;
            Label(new Rect(r.x, yy, lw, 24), $"所持金 <b>{purse} 文</b>　　お守り {charms}　　図鑑 {BookSeen()} / {GUESTS.Length}", sBody, cPaper); yy += 40;
            if (Btn(new Rect(r.x, yy, 240, 52), "営業を始める")) Begin();
            if (Btn(new Rect(r.x + 252, yy, 150, 52), "売店")) OpenShop(Mode.Title);
            yy += 62;
            if (Btn(new Rect(r.x, yy, 240, 44), "お客さん図鑑", true, true)) OpenBook(Mode.Title);
            if (Btn(new Rect(r.x + 252, yy, 150, 44), "社名を変える", true, true)) OpenName(Mode.Title);
            yy += 54;

            Rect pr = twoCol ? new Rect(r.x + lw + 16, r.y + 28, tw - lw - 16, 560) : new Rect(r.x, yy, tw, r.yMax - yy);
            titleTab = GUI.Toolbar(new Rect(pr.x, pr.y, 260, 34), titleTab, new[] { "あそびかた", "設定" }, sBtnSmall);
            var box = new Rect(pr.x, pr.y + 42, pr.width, pr.height - 42);
            Fill(box, cCard);
            var inner = new Rect(box.x + 16, box.y + 14, box.width - 32, box.height - 28);
            if (titleTab == 0)
                Label(inner,
                    "・青く光る魂がお客さん。触れると乗車。紫・金に光る魂は珍しいお客さん。\n" +
                    "・行き先の高さを通り過ぎると到着。乗車距離が長いほど運賃が上がる。\n" +
                    "・乗せている間にほかの魂に触れると、乗っていたお客さんが飛ばされて、お詫び料を払う。\n" +
                    "・乗車中に一度も当たらなければ、心づけがもらえる。\n" +
                    "・上りは塩で鬼ザクロを祓える。撃つほど馬は干からびる。下りは避けて進む（設定で「追われる」にもできる）。\n" +
                    $"・{goalCount} 人送り届ければノルマ達成。残り時間が多いほどボーナスが増える。\n" +
                    "・道のいちばん下（地獄営業所）に戻ると、売店に寄れる。\n\n" +
                    "<color=#A69EB5>左クリック：塩　右クリック：クモの糸　WASD：移動　P / Esc：一時停止</color>",
                    sSmall, cPaper);
            else DrawSettings(inner);
        }

        /// <summary>設定（タイトルと一時停止で共通）。変えるとすぐ保存する。</summary>
        void DrawSettings(Rect r)
        {
            float yy = r.y;
            bool mf = GUI.Toggle(new Rect(r.x, yy, r.width, 28), mouseFollow, " マウスの位置に馬がついていく", sToggle); yy += 40;
            Label(new Rect(r.x, yy, r.width, 22), "下りで、手前から来るものの知らせ方", sSmall, cDim); yy += 24;
            var h = (Hint)GUI.Toolbar(new Rect(r.x, yy, Mathf.Min(r.width, 440), 32), (int)hint, HINT_LABEL, sBtnSmall); yy += 44;
            bool co = GUI.Toggle(new Rect(r.x, yy, r.width, 28), callout, " 声で知らせる（お客さん・クモが方向を叫ぶ）", sToggle); yy += 40;
            Label(new Rect(r.x, yy, r.width, 22), "下りの遊び方", sSmall, cDim); yy += 24;
            bool ch = GUI.Toolbar(new Rect(r.x, yy, Mathf.Min(r.width, 300), 32), chaseMode ? 1 : 0, new[] { "よける", "追われる" }, sBtnSmall) == 1; yy += 40;
            Label(new Rect(r.x, yy, r.width, 60), ch ? "下りは、後ろ（奥）から敵が迫ってくる。左クリックで後ろへ塩を撒ける。" : "下りは塩が撒けない。置いてある敵を避けて進む。", sTiny, cDim);
            if (mf != mouseFollow || h != hint || co != callout || ch != chaseMode)
            {
                if (ch != chaseMode && dir < 0) ClearChasers();   // 下りの最中に切り替えたら、追っ手を片づける
                mouseFollow = mf; hint = h; callout = co; chaseMode = ch;
                SaveSettings();
            }
        }

        void DrawPause(float W, float H)
        {
            Panel(W, H, Mathf.Min(560, W - 32), 470, out var r);
            Label(new Rect(r.x, r.y, r.width, 40), "一時停止", sBig, cPaper);
            DrawSettings(new Rect(r.x, r.y + 54, r.width, 300));
            float by = r.y + 400;
            if (Btn(new Rect(r.x, by, 170, 48), "続ける")) Resume();
            if (Btn(new Rect(r.x + 182, by, 170, 48), "社名を変える", true, true)) OpenName(Mode.Pause);
            if (Btn(new Rect(r.x + 364, by, 170, 48), "タイトルへ")) ToTitle();
        }

        void DrawResult(float W, float H)
        {
            Panel(W, H, Mathf.Min(620, W - 32), 500, out var r);
            Label(new Rect(r.x, r.y, r.width, 24), resultTitle, sSmall, cDim);
            Label(new Rect(r.x, r.y + 30, r.width, 120), resultText, new GUIStyle(sBig) { fontSize = 24 }, cPaper);
            string[] k = { "送り届けた魂", "運賃", "飛ばしてしまったお客さん", "祓った敵" };
            string[] v = { $"{delivered} 人", $"{mon} 文", $"{thrown} 人", $"{kills}" };
            for (int i = 0; i < 4; i++)
            {
                var cell = new Rect(r.x + (i % 2) * (r.width / 2 + 5), r.y + 160 + (i / 2) * 76, r.width / 2 - 5, 68);
                Fill(cell, cCard);
                Label(new Rect(cell.x + 12, cell.y + 6, cell.width, 20), k[i], sSmall, cDim);
                Label(new Rect(cell.x + 12, cell.y + 26, cell.width, 36), $"<b>{v[i]}</b>", sBig, cPaper);
            }
            Label(new Rect(r.x, r.y + 320, r.width, 26), (bonus > 0 ? $"ボーナス +{bonus} 文　" : "") + $"所持金 {purse} 文", sBody, cLantern);
            float by = r.y + 366;
            if (Btn(new Rect(r.x, by, 300, 50), "もう一度、営業する")) Begin();
            if (Btn(new Rect(r.x + 312, by, 140, 50), "売店")) OpenShop(Mode.Result);
            by += 60;
            if (Btn(new Rect(r.x, by, 300, 44), "お客さん図鑑", true, true)) OpenBook(Mode.Result);
            if (Btn(new Rect(r.x + 312, by, 140, 44), "タイトルへ", true, true)) ToTitle();
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
                int n = Lv(it.id); bool maxed = n >= it.max; int price = maxed ? 0 : it.price[n];
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
            int n = Lv(it.id);
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
            if (nameNext == Mode.Play) { if (ok) StartGame(); else mode = Mode.Title; }
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
            Label(new Rect(r.x, r.y + 140, r.width, 40), $"<b>{pv}</b><color=#{Hex(cLantern)}>タクシー地獄営業所</color>", new GUIStyle(sBig) { fontSize = 26 }, cPaper);
            Label(new Rect(r.x, r.y + 186, r.width, 22), "8文字まで。あとからタイトル・一時停止でいつでも変えられる。", sTiny, cDim);
            if (Btn(new Rect(r.x, r.y + 240, 200, 50), "決める")) CloseName(true);
            if (Btn(new Rect(r.x + 212, r.y + 240, 220, 50), nameNext == Mode.Pause ? "一時停止に戻る" : "戻る", true, true)) CloseName(false);
        }
    }
}
