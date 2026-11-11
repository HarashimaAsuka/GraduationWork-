using System.Collections.Generic;
using UnityEngine;

namespace ShoryoumaTaxi
{
    /// <summary>
    /// 画面表示（IMGUI。1280x720 を基準に拡大縮小）。出すのは4つだけ：
    ///   左上：売上／目標と水分　上：今やること（と乗せた魂）　右：道のり（自分と大警部）　下：研修の説明（ステージ1だけ）
    /// まんなかに大きく：ステージの目標・通報など。帰りは、手前から来るものの通る場所に「！」と縮む輪を出す。
    /// </summary>
    public partial class IllegalTaxi
    {
        GUIStyle sBody, sSmall, sBig, sHuge, sTip, sTipStep, sCenter, sBtn, sPop;
        Texture2D ringTex, triTex, dotTex;
        float gs = 1;
        static readonly Color cPaper = new Color(0.94f, 0.91f, 0.85f), cDim = new Color(0.65f, 0.62f, 0.71f), cGold = new Color(1f, 0.82f, 0.38f);
        static readonly Color cWater = new Color(0.42f, 0.77f, 0.91f), cHell = new Color(0.88f, 0.21f, 0.29f), cLantern = new Color(0.96f, 0.64f, 0.29f), cCard = new Color(0.075f, 0.067f, 0.106f, 0.85f);

        void InitStyles()
        {
            if (sBody != null) return;
            GUIStyle Make(int size, FontStyle fs = FontStyle.Normal, TextAnchor a = TextAnchor.UpperLeft)
            {
                var s = new GUIStyle(GUI.skin.label) { font = jpFont, fontSize = size, fontStyle = fs, alignment = a, wordWrap = true, richText = true };
                s.normal.textColor = cPaper; return s;
            }
            sBody = Make(18); sSmall = Make(14); sBig = Make(30, FontStyle.Bold); sHuge = Make(64, FontStyle.Bold, TextAnchor.MiddleCenter);
            sTip = Make(24, FontStyle.Bold, TextAnchor.MiddleCenter); sTipStep = Make(14, FontStyle.Bold, TextAnchor.UpperCenter); sTipStep.normal.textColor = cLantern;
            sCenter = Make(20, FontStyle.Normal, TextAnchor.MiddleCenter); sPop = Make(26, FontStyle.Bold, TextAnchor.MiddleCenter); sPop.wordWrap = false;
            sBtn = new GUIStyle(GUI.skin.button) { font = jpFont, fontSize = 22, fontStyle = FontStyle.Bold, richText = true };
            ringTex = MakeTex(128, (x, yy) => { float d = Mathf.Sqrt(x * x + yy * yy); return d > 0.82f && d < 0.98f ? 1 : 0; });
            triTex = MakeTex(64, (x, yy) => (yy > -0.85f && yy < 0.9f && Mathf.Abs(x) < (0.9f - yy) * 0.55f) ? 1 : 0);
            dotTex = MakeTex(32, (x, yy) => x * x + yy * yy < 0.9f ? 1 : 0);
        }
        static Texture2D MakeTex(int n, System.Func<float, float, float> a)
        {
            var tx = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int j = 0; j < n; j++) for (int i = 0; i < n; i++) px[j * n + i] = new Color(1, 1, 1, a((i + 0.5f) / n * 2 - 1, (j + 0.5f) / n * 2 - 1));
            tx.SetPixels(px); tx.Apply(); return tx;
        }
        static void Fill(Rect r, Color c) { var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = o; }
        void Img(Rect r, Texture2D tx, Color c) { var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, tx); GUI.color = o; }
        static void Label(Rect r, string s, GUIStyle st, Color c) { var o = st.normal.textColor; st.normal.textColor = c; GUI.Label(r, s, st); st.normal.textColor = o; }
        static void Box(Rect r, Color edge) { Fill(r, edge); Fill(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), cCard); }
        Vector2 ToGui(Vector3 w, out bool front)
        {
            var sp = cam.WorldToScreenPoint(w); front = sp.z > 0;
            return new Vector2(sp.x / gs, (Screen.height - sp.y) / gs);
        }

        void OnGUI()
        {
            if (cam == null) return;
            InitStyles();
            gs = Screen.height / 720f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(gs, gs, 1));
            float W = Screen.width / gs, H = 720f;
            bool play = mode == Mode.Play || mode == Mode.Pause;

            // 画面のふち：被弾の赤、パトランプ（大警部が近いほど強く）
            if (play && hurtT > 0) Fill(new Rect(0, 0, W, H), new Color(0.88f, 0.21f, 0.29f, hurtT / 0.5f * 0.3f));
            bool chase = play && phase == "back" && bossM.root.activeSelf;
            if (play && (sirenT > 0 || lunging > 0 || chase))
            {
                bool on = Mathf.FloorToInt(t * 4) % 2 == 0;
                float k = sirenT > 0 || lunging > 0 ? 0.55f : !st.boss ? 0.15f + 0.4f * Mathf.Clamp01(1 - (bossY - y) / 30f) : 0.15f;
                var c = on ? new Color(1, 0.16f, 0.2f, k) : new Color(0.16f, 0.47f, 1, k);
                for (int i = 0; i < 6; i++) Fill(new Rect(on ? 0 : W - 120 + i * 20, 0, 20 + (on ? i * 20 : 0), H), new Color(c.r, c.g, c.b, c.a * (1 - i / 6f)));
            }
            if (play) { DrawPops(); DrawIncoming(W, H); DrawHUD(W, H); }
            if (bannerT > 0 && play)
            {
                float a = Mathf.Clamp01(bannerT * 3);
                Label(new Rect(0, H * 0.3f, W, 90), bannerMain, sHuge, new Color(bannerCol.r, bannerCol.g, bannerCol.b, a));
                if (bannerSub != "") Label(new Rect(0, H * 0.3f + 86, W, 40), bannerSub, sCenter, new Color(1, 1, 1, a));
            }
            if (mode == Mode.Title) DrawTitle(W, H);
            else if (mode == Mode.Pause) DrawPause(W, H);
            else if (mode == Mode.Result) DrawResult(W, H);
        }

        void DrawHUD(float W, float H)
        {
            // 左上：売上と水分
            float mw = Mathf.Min(300, W * 0.38f);
            Box(new Rect(18, 16, mw, st.boss ? 62 : 112), new Color(1, 1, 1, 0.15f));
            float yy = 26;
            if (!st.boss)
            {
                Label(new Rect(32, yy, mw, 24), "売上", sSmall, cDim);
                Label(new Rect(32, yy - 8, mw - 30, 40), $"<b>{fare}</b><size=15> / {st.goal}文</size>", new GUIStyle(sBig) { alignment = TextAnchor.UpperRight }, fare >= st.goal ? cGold : cPaper);
                float fmax = Mathf.Max(st.goal * 1.5f, fare, 1);
                Fill(new Rect(32, yy + 34, mw - 28, 11), new Color(1, 1, 1, 0.12f));
                Fill(new Rect(32, yy + 34, (mw - 28) * fare / fmax, 11), cGold);
                Fill(new Rect(32 + (mw - 28) * st.goal / fmax - 1, yy + 30, 3, 19), Color.white);
                yy += 52;
            }
            Label(new Rect(32, yy, mw, 20), "水分（塩を撒くと減る）", sSmall, cDim);
            Fill(new Rect(32, yy + 22, mw - 28, 11), new Color(1, 1, 1, 0.12f));
            Fill(new Rect(32, yy + 22, (mw - 28) * water / 100f, 11), water < 25 && Mathf.FloorToInt(t * 4) % 2 == 0 ? cHell : cWater);

            // 上：今やること
            bool outP = phase == "out";
            string ph = st.boss ? "決戦　ナスの牛" : outP ? "行き　きゅうりの馬" : phase == "alarm" ? "現世の門" : "帰り　ナスの牛";
            int toEnd = Mathf.Max(0, Mathf.RoundToInt((END - y) / 10)), toHell = Mathf.Max(0, Mathf.RoundToInt((y - START) / 10));
            string obj = st.boss ? $"大警部を塩で祓え！　地獄の門まで あと{toHell}丈"
                : outP ? (riders.Count > 0 ? $"現世の門へ　あと{toEnd}丈" : "罪人の魂を乗せて、現世の門へ")
                : phase == "alarm" ? "運賃をもらった！" : $"大警部から逃げろ！　地獄の門まで あと{toHell}丈";
            float ow = Mathf.Min(560, W - 2 * mw - 80);
            var orc = new Rect((W - ow) / 2, 16, ow, outP ? 92 : 64);
            Box(orc, outP ? new Color(0.49f, 0.76f, 0.33f, 0.7f) : new Color(0.88f, 0.21f, 0.29f, 0.8f));
            Label(new Rect(orc.x, 22, ow, 20), ph, new GUIStyle(sSmall) { alignment = TextAnchor.UpperCenter }, cDim);
            Label(new Rect(orc.x, 40, ow, 32), obj, new GUIStyle(sBig) { fontSize = 24, alignment = TextAnchor.UpperCenter }, cPaper);
            if (outP)
            {
                int[] cnt = new int[3]; foreach (int r in riders) cnt[r]++;
                int est = cnt[0] * 10 + cnt[1] * 20 + cnt[2] * 30;
                string seat = riders.Count == 0 ? "まだ誰も乗っていない" : $"白×{cnt[0]}　黄×{cnt[1]}　紫×{cnt[2]}　<color=#ffd060>運賃 {est}文の見込み</color>";
                Label(new Rect(orc.x, 74, ow, 24), seat, new GUIStyle(sBody) { fontSize = 16, alignment = TextAnchor.UpperCenter }, cPaper);
            }
            // ステージ4：大警部の体力
            if (st.boss && B != null)
            {
                float bw = Mathf.Min(520, W * 0.7f); var br = new Rect((W - bw) / 2, 118, bw, 44);
                Box(br, new Color(1, 0.35f, 0.3f, 0.6f));
                Label(new Rect(br.x + 14, br.y + 4, 200, 20), "大警部" + (B.shield ? "（護衛が塩をはじく）" : B.stunT > 0 ? "（ひるんでいる！）" : ""), sSmall, new Color(1, 0.6f, 0.55f));
                float r = bossHp / (float)bossHpMax;
                Fill(new Rect(br.x + 14, br.y + 24, bw - 28, 12), new Color(1, 1, 1, 0.12f));
                Fill(new Rect(br.x + 14, br.y + 24, (bw - 28) * r, 12), B.stunT > 0 ? cGold : new Color(1, 0.19f, 0.25f));
            }
            // 右：道のり
            float rx = W - 60, top = 110, bot = H - 110;
            Fill(new Rect(rx, top, 6, bot - top), new Color(1, 1, 1, 0.18f));
            Label(new Rect(rx - 90, top - 26, 120, 22), "現世の門", new GUIStyle(sSmall) { alignment = TextAnchor.UpperRight }, cDim);
            Label(new Rect(rx - 90, bot + 6, 120, 22), "地獄の門", new GUIStyle(sSmall) { alignment = TextAnchor.UpperRight }, cDim);
            float Pos(float yy2) => top + (1 - (yy2 - START) / (END - START)) * (bot - top);
            Img(new Rect(rx - 8, Pos(y) - 11, 22, 22), dotTex, outP ? new Color(0.49f, 0.76f, 0.33f) : new Color(0.63f, 0.48f, 0.88f));
            if (phase == "back" && bossM.root.activeSelf) Img(new Rect(rx - 11, Pos(Mathf.Min(END, bossY)) - 14, 28, 28), dotTex, cHell);
            if (phase == "back" && !st.boss)
            {
                float gap = Mathf.Max(0, (bossY - y) / 10);
                Label(new Rect(0, H * 0.74f, W, 30), $"大警部まで {gap:0.0}丈" + (charm ? "　お守りあり" : ""), new GUIStyle(sCenter) { fontStyle = FontStyle.Bold }, gap < 2 ? cHell : new Color(1, 0.7f, 0.66f));
            }
            // 下：研修の説明（ステージ1だけ）
            if (tipCur != null && st.tutorial)
            {
                float tw = Mathf.Min(760, W - 40), th = 100; var tr = new Rect((W - tw) / 2, H - th - 22, tw, th);
                Box(tr, cLantern);
                Label(new Rect(tr.x, tr.y + 8, tw, 20), "運転手" + (string.IsNullOrEmpty(tipCur.step) ? "" : "　" + tipCur.step), sTipStep, cLantern);
                Label(new Rect(tr.x + 20, tr.y + 26, tw - 40, th - 32), tipCur.html.Replace("<b>", "<color=#ffd060><b>").Replace("</b>", "</b></color>"), sTip, cPaper);
            }
        }

        void DrawPops()
        {
            foreach (var p in pops)
            {
                var g = ToGui(p.w, out bool front); if (!front) continue;
                float a = 1 - p.t / 1.1f;
                var s = new GUIStyle(sPop) { fontSize = Mathf.RoundToInt(p.size) };
                Label(new Rect(g.x - 200, g.y - 30 - p.t * 60, 400, 40), p.text, s, new Color(0, 0, 0, a * 0.8f));
                Label(new Rect(g.x - 202, g.y - 32 - p.t * 60, 400, 40), p.text, s, new Color(p.c.r, p.c.g, p.c.b, a));
            }
        }

        // 帰り：カメラの後ろ（手前）から来るものは見えないので、通る場所に「！」と、近づくほど縮む輪を出す
        void DrawIncoming(float W, float H)
        {
            if (phase != "back" || over) return;
            var list = new List<(float d, float x, float z, Color c, string label)>();
            float camY = camPos.y;
            foreach (var c in cops) { if (c.mode == "enter") continue; float d = y - c.y; if (d > 0 && d < 40 && c.y < camY + 3.5f) list.Add((d, c.x, c.z, new Color(1, 0.29f, 0.23f), "手前から " + (c.type == "junsa" ? "巡査" : "パト提灯"))); }
            foreach (var it in items) { if (it.taken) continue; float d = y - it.y; if (d > 0 && d < 40 && it.y < camY + 3.5f) list.Add((d, it.x, it.z, ItemCol(it.kind), ItemName(it.kind))); }
            foreach (var cn in coins) { if (cn.taken) continue; float d = y - cn.y; if (d > 0 && d < 40 && cn.y < camY + 3.5f) list.Add((d, cn.x, cn.z, cGold, "小銭 +5文")); }
            list.Sort((a, b) => a.d.CompareTo(b.d));
            for (int i = 0; i < Mathf.Min(8, list.Count); i++)
            {
                var m = list[i];
                var g = ToGui(Road.WP(m.x, y - 0.6f, m.z), out bool front); if (!front) continue;
                float k = Mathf.Clamp01(m.d / 40f), sz = Mathf.Lerp(60, 150, k), a = 1 - k * 0.65f;
                Img(new Rect(g.x - sz / 2, g.y - sz / 2, sz, sz), ringTex, new Color(m.c.r, m.c.g, m.c.b, a));
                float ts2 = Mathf.Lerp(52, 38, k);
                Img(new Rect(g.x - ts2 / 2, g.y - ts2 * 0.75f, ts2, ts2 * 0.9f), triTex, new Color(m.c.r, m.c.g, m.c.b, a));
                Label(new Rect(g.x - 20, g.y - ts2 * 0.55f, 40, ts2 * 0.7f), "!", new GUIStyle(sTip) { fontSize = 26 }, new Color(0.08f, 0.07f, 0.1f, a));
                var lr = new Rect(g.x - 80, g.y + ts2 * 0.25f, 160, 24); Fill(lr, new Color(m.c.r, m.c.g, m.c.b, a * 0.9f));
                Label(lr, m.label, new GUIStyle(sSmall) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }, new Color(0.08f, 0.07f, 0.1f, a));
                if (m.c.g < 0.4f) Teach("ring", "<b>！の輪</b>は、手前から来る警察の通り道。<b>輪が縮むほど近い</b>。輪の外へよけろ", "帰り", 4, 0);
                if (m.label == "小銭 +5文") Teach("coin", "売上が足りない！ 帰り道の<b>落とし物の小銭</b>（1枚5文）を拾え", "帰り", 3.6f, 0);
            }
        }

        void DrawTitle(float W, float H)
        {
            Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.45f));
            float cw = Mathf.Min(900, W - 60), x = (W - cw) / 2;
            Label(new Rect(x, 60, cw, 30), "卒業制作 ／ 3D縦穴アクション ／ 1ステージ約2分", sBody, cDim);
            Label(new Rect(x, 90, cw, 90), "精霊馬タクシー 地獄営業所", new GUIStyle(sHuge) { alignment = TextAnchor.MiddleLeft }, cPaper);
            Label(new Rect(x, 180, cw, 60), "無許可営業の違法タクシー。罪人の魂をこっそり現世へ送り、通報されたら地獄まで逃げ帰る。", sBody, cPaper);
            string[] how = { "<b>① 行き：きゅうりの馬</b>\n光る罪人の魂に触れて乗せ、奥の現世の門へ。警察にぶつかると魂を1人捕まえられる。",
                             "<b>② 帰り：ナスの牛</b>\n現世の門で運賃をもらうと通報される。大警部から地獄の門まで逃げきる。牛は警察を吹っ飛ばせるが、運賃を落とす。",
                             "<b>③ 売上が目標に届けばクリア</b>\n地獄の門に着いたとき、売上が目標に届いていなければ赤字で失敗。" };
            float bw = (cw - 24) / 3;
            for (int i = 0; i < 3; i++) { var r = new Rect(x + i * (bw + 12), 250, bw, 140); Box(r, new Color(1, 1, 1, 0.15f)); Label(new Rect(r.x + 12, r.y + 10, bw - 24, 124), how[i], sSmall, cPaper); }
            Label(new Rect(x, 404, cw, 26), "<b>WASD</b> 移動　<b>左クリック / Z</b> 塩を撒く（白い輪の先へ飛ぶ）　<b>P</b> 一時停止　<b>M</b> 音", sBody, cDim);
            string[] names = { "ステージ1\n研修", "ステージ2\n一日目", "ステージ3\n二日目", "ステージ4\n大警部との決戦" };
            float sw = (cw - 36) / 4;
            for (int i = 0; i < 4; i++) if (GUI.Button(new Rect(x + i * (sw + 12), 450, sw, 72), names[i], sBtn)) StartStage(i + 1);
        }
        void DrawPause(float W, float H)
        {
            Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.55f));
            Label(new Rect(0, H * 0.3f, W, 80), "一時停止", sHuge, cPaper);
            if (GUI.Button(new Rect(W / 2 - 170, H * 0.5f, 160, 56), "つづける", sBtn)) Resume();
            if (GUI.Button(new Rect(W / 2 + 10, H * 0.5f, 160, 56), "タイトルへ", sBtn)) ToTitle();
        }
        void DrawResult(float W, float H)
        {
            Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.6f));
            Label(new Rect(0, 120, W, 30), $"ステージ{st.no}　{st.name}", sCenter, cDim);
            Label(new Rect(0, 160, W, 90), resultTitle, sHuge, resultOk ? cGold : cHell);
            Label(new Rect(W / 2 - 360, 260, 720, 60), resultText, sCenter, cPaper);
            string stats = st.boss ? $"大警部に与えた傷 {Mathf.RoundToInt((1 - bossHp / (float)bossHpMax) * 100)}%　祓った警察 {kills}体　吹っ飛ばした {rams}回"
                                   : $"売上 {fare}文　目標 {st.goal}文　運んだ魂 {delivered}人　落とした運賃 {lost}文";
            Label(new Rect(0, 330, W, 30), stats, sCenter, cPaper);
            float bx = W / 2 - 270;
            if (resultOk && st.no < 4 && GUI.Button(new Rect(bx, 400, 170, 56), "次のステージ", sBtn)) StartStage(st.no + 1);
            if (GUI.Button(new Rect(bx + 185, 400, 170, 56), "もう一度", sBtn)) StartStage(st.no);
            if (GUI.Button(new Rect(bx + 370, 400, 170, 56), "タイトルへ", sBtn)) ToTitle();
        }
    }
}
