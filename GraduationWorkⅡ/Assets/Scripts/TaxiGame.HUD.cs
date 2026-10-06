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
        GUIStyle sBody, sSmall, sBig, sTitle, sSign, sMsg, sBtn, sToggle, sCenter;
        static readonly Color cPaper = new Color(0.94f, 0.91f, 0.85f), cDim = new Color(0.65f, 0.62f, 0.71f);
        static readonly Color cWater = new Color(0.42f, 0.77f, 0.91f), cHell = new Color(0.88f, 0.21f, 0.29f);
        static readonly Color cLantern = new Color(0.96f, 0.64f, 0.29f), cFree = new Color(0.56f, 0.89f, 0.6f), cHire = new Color(1f, 0.6f, 0.48f);
        static readonly Color cCuc = new Color(0.49f, 0.76f, 0.33f), cNasu = new Color(0.63f, 0.48f, 0.88f), cNight = new Color(0.075f, 0.067f, 0.106f);

        void InitStyles()
        {
            if (sBody != null) return;
            GUIStyle Make(int size, FontStyle fs = FontStyle.Normal, TextAnchor a = TextAnchor.UpperLeft)
            {
                var st = new GUIStyle(GUI.skin.label) { font = jpFont, fontSize = size, fontStyle = fs, alignment = a, wordWrap = true, richText = true };
                st.normal.textColor = cPaper;
                return st;
            }
            sBody = Make(18); sSmall = Make(14); sBig = Make(30, FontStyle.Bold);
            sTitle = Make(84, FontStyle.Bold); sSign = Make(20, FontStyle.Bold, TextAnchor.MiddleCenter);
            sMsg = Make(24, FontStyle.Normal, TextAnchor.MiddleCenter); sCenter = Make(18, FontStyle.Normal, TextAnchor.MiddleCenter);
            sBtn = new GUIStyle(GUI.skin.button) { font = jpFont, fontSize = 20, fontStyle = FontStyle.Bold };
            sToggle = new GUIStyle(GUI.skin.toggle) { font = jpFont, fontSize = 18 };
            sToggle.normal.textColor = sToggle.onNormal.textColor = cPaper;
        }

        static void Fill(Rect r, Color c) { var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = o; }
        static void Frame(Rect r, Color c, float w = 2)
        {
            Fill(new Rect(r.x, r.y, r.width, w), c); Fill(new Rect(r.x, r.yMax - w, r.width, w), c);
            Fill(new Rect(r.x, r.y, w, r.height), c); Fill(new Rect(r.xMax - w, r.y, w, r.height), c);
        }
        static void Label(Rect r, string s, GUIStyle st, Color c) { var o = st.normal.textColor; st.normal.textColor = c; GUI.Label(r, s, st); st.normal.textColor = o; }

        void OnGUI()
        {
            if (cam == null) return;
            InitStyles();
            float scale = Screen.height / 720f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            float W = Screen.width / scale, H = 720f;

            // 被弾の赤・クリア時の暗転
            if (hurtT > 0) Fill(new Rect(0, 0, W, H), new Color(0.88f, 0.21f, 0.29f, hurtT / 0.35f * 0.35f));
            if (clearT > 0) Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, Mathf.Clamp01((clearT - 1) / 2) * 0.85f));

            if (mode == Mode.Play || mode == Mode.Pause) DrawHUD(W, H);
            if (mode == Mode.Title) DrawTitle(W, H);
            if (mode == Mode.Pause) DrawPause(W, H);
            if (mode == Mode.Result) DrawResult(W, H);
        }

        void DrawHUD(float W, float H)
        {
            // --- 左上：ゲージ ---
            float x = 16, yy = 14, gw = 210;
            Label(new Rect(x, yy, gw, 24), "水分", sSmall, cDim);
            Label(new Rect(x, yy - 2, gw, 26), $"<b>{Mathf.CeilToInt(Mathf.Max(0, water))}</b>", new GUIStyle(sBody) { alignment = TextAnchor.UpperRight }, cPaper);
            Fill(new Rect(x, yy + 24, gw, 9), new Color(1, 1, 1, 0.12f));
            Fill(new Rect(x, yy + 24, gw * Mathf.Clamp01(water / 100f), 9), water < 25 ? cHell : cWater);
            Label(new Rect(x, yy + 38, gw, 22), "クモの糸", sSmall, cDim);
            Fill(new Rect(x, yy + 60, gw, 4), new Color(1, 1, 1, 0.12f));
            Fill(new Rect(x, yy + 60, gw * silk / 100f, 4), new Color(0.87f, 0.91f, 0.95f, silkLock ? 0.35f : 1f));
            Label(new Rect(x, yy + 70, gw, 22), "送り届けた魂", sSmall, cDim);
            Label(new Rect(x, yy + 68, gw, 24), $"<b>{delivered} / {goalCount}</b>", new GUIStyle(sBody) { alignment = TextAnchor.UpperRight }, cPaper);
            Label(new Rect(x, yy + 92, gw, 22), "運賃", sSmall, cDim);
            Label(new Rect(x, yy + 90, gw, 24), $"<b>{mon} 文</b>", new GUIStyle(sBody) { alignment = TextAnchor.UpperRight }, cPaper);

            // --- 上中央：行灯（空車／賃走）---
            bool up = want > 0;
            string sign = carrying && goal != null ? $"賃走　{goal.name}まで　{(up ? "▲上り" : "▼下り")}" : $"空車　お客さんを探す　{(up ? "▲上り" : "▼下り")}";
            var sc = carrying ? cHire : cFree;
            float sw = Mathf.Min(520, W - 2 * (gw + 40));
            var sr = new Rect((W - sw) / 2, 12, sw, 40);
            Fill(sr, new Color(cNight.r, cNight.g, cNight.b, 0.75f)); Frame(sr, sc);
            Label(sr, sign, sSign, sc);

            // --- 右：高度メーター ---
            float mx = W - 30, top = 70, bot = H - 40, mh = bot - top;
            for (int i = 0; i < 60; i++)
            {
                float f0 = 1 - i / 60f;
                Fill(new Rect(mx, top + mh * i / 60f, 16, mh / 60f + 1), World.ZoneColor(f0, z => z.bg) * 1.8f + new Color(0.05f, 0.05f, 0.05f));
            }
            Frame(new Rect(mx, top, 16, mh), new Color(1, 1, 1, 0.25f), 1);
            void Zone(float f, string name, Color c) => Label(new Rect(mx - 96, top + mh * (1 - f) - 10, 88, 20), name, new GUIStyle(sSmall) { alignment = TextAnchor.MiddleRight }, c);
            Zone(1f, "現世", cLantern); Zone(0.83f, "夜空", cDim); Zone(0.58f, "雲海", cDim); Zone(0.33f, "三途の霧", cDim); Zone(0f, "地獄", cHell);
            // 流れる縞：進む向きへ流れる
            float flow = Mathf.Repeat(t * (up ? 30f : -15f), 16f);
            for (float s = -flow; s < mh; s += 16) if (s >= 0) Fill(new Rect(mx + 2, top + s, 12, 2), new Color(1, 1, 1, 0.35f));
            if (goal != null)
            {
                float gy = top + mh * (1 - goal.y / L);
                Fill(new Rect(mx - 6, gy - 3, 28, 6), new Color(1f, 0.42f, 0.29f));
                Label(new Rect(mx - 70, gy - 10, 60, 20), "行き先", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold }, new Color(1f, 0.7f, 0.6f));
            }
            float my = top + mh * (1 - Mathf.Clamp01(y / L));
            var dotC = up ? cCuc : cNasu;
            Fill(new Rect(mx - 6, my - 14, 28, 28), dotC);
            Label(new Rect(mx - 6, my - 14, 28, 28), up ? "▲" : "▼", sCenter, cNight);
            string tag = goal != null ? $"行き先まで {Mathf.CeilToInt(Mathf.Abs(goal.y - y) / 10)}丈" : World.ZoneName(y / L);
            var tr = new Rect(mx - 168, my - 13, 156, 26);
            Fill(tr, new Color(cNight.r, cNight.g, cNight.b, 0.85f));
            Label(tr, tag, sCenter, cPaper);

            // --- 下：メッセージ ---
            if (msg != null)
            {
                var m = msg.Value; float age = t - m.t0;
                float a = Mathf.Clamp01(Mathf.Min(age / 0.35f, (m.dur - age) / 0.4f));
                if (a > 0)
                {
                    var r = new Rect(W * 0.12f, H * 0.74f, W * 0.76f, 52);
                    Fill(r, new Color(cNight.r, cNight.g, cNight.b, 0.6f * a));
                    Label(r, m.text, sMsg, new Color(cPaper.r, cPaper.g, cPaper.b, a));
                }
            }
        }

        void Panel(float W, float H, float w, float h, out Rect r)
        {
            Fill(new Rect(0, 0, W, H), new Color(cNight.r, cNight.g, cNight.b, 0.78f));
            r = new Rect((W - w) / 2, (H - h) / 2, w, h);
        }

        void DrawTitle(float W, float H)
        {
            Panel(W, H, Mathf.Min(640, W - 32), 560, out var r);
            float yy = r.y;
            Label(new Rect(r.x, yy, r.width, 22), "3D 縦穴シューティング ／ 約4分", sSmall, cDim); yy += 28;
            Label(new Rect(r.x, yy, r.width, 100), "精霊馬", sTitle, cPaper); yy += 96;
            Label(new Rect(r.x, yy, r.width, 36), "タ ク シ ー", sBig, cLantern); yy += 50;
            Label(new Rect(r.x, yy, r.width, 110),
                "お盆の夜、地獄と現世をつなぐ縦の道で、魂のお客さんを送り届ける。上りはきゅうりの馬、下りはナスの牛。そのまま進むか折り返すかはお客さんしだい。下りは塩が撒けないので、避けて進む。5人送り届ければ、今夜の営業はおしまい。",
                sBody, cPaper); yy += 116;
            Label(new Rect(r.x, yy, r.width, 70),
                "魂に触れると乗車。乗せている間にほかの魂に触れると、乗っていたお客さんが飛ばされる。\n左クリック：塩（上りのみ）　右クリック：クモの糸　WASD：移動　P / Esc：一時停止",
                sSmall, cDim); yy += 74;
            mouseFollow = GUI.Toggle(new Rect(r.x, yy, r.width, 28), mouseFollow, " マウスの位置に馬がついていく", sToggle); yy += 40;
            if (GUI.Button(new Rect(r.x, yy, 220, 48), "営業を始める", sBtn)) StartGame();
        }

        void DrawPause(float W, float H)
        {
            Panel(W, H, 420, 200, out var r);
            Label(new Rect(r.x, r.y, r.width, 40), "一時停止", sBig, cPaper);
            mouseFollow = GUI.Toggle(new Rect(r.x, r.y + 56, r.width, 28), mouseFollow, " マウスの位置に馬がついていく", sToggle);
            if (GUI.Button(new Rect(r.x, r.y + 110, 180, 46), "続ける", sBtn)) mode = Mode.Play;
            if (GUI.Button(new Rect(r.x + 200, r.y + 110, 180, 46), "タイトルへ", sBtn)) { NewGame(); mode = Mode.Title; }
        }

        void DrawResult(float W, float H)
        {
            Panel(W, H, Mathf.Min(560, W - 32), 420, out var r);
            Label(new Rect(r.x, r.y, r.width, 24), resultTitle, sSmall, cDim);
            Label(new Rect(r.x, r.y + 30, r.width, 120), resultText, sBig, cPaper);
            string[] k = { "送り届けた魂", "運賃", "飛ばしてしまったお客さん", "祓った敵" };
            string[] v = { $"{delivered} 人", $"{mon} 文", $"{thrown} 人", $"{kills}" };
            for (int i = 0; i < 4; i++)
            {
                var cell = new Rect(r.x + (i % 2) * (r.width / 2 + 5), r.y + 170 + (i / 2) * 76, r.width / 2 - 5, 68);
                Fill(cell, new Color(0.12f, 0.105f, 0.17f));
                Label(new Rect(cell.x + 12, cell.y + 6, cell.width, 20), k[i], sSmall, cDim);
                Label(new Rect(cell.x + 12, cell.y + 26, cell.width, 36), $"<b>{v[i]}</b>", sBig, cPaper);
            }
            if (GUI.Button(new Rect(r.x, r.y + 340, 240, 48), "もう一度、営業する", sBtn)) StartGame();
            if (GUI.Button(new Rect(r.x + 260, r.y + 340, 180, 48), "タイトルへ", sBtn)) { NewGame(); mode = Mode.Title; }
        }
    }
}
