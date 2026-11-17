using System.Collections.Generic;
using UnityEngine;

namespace ShoryoumaTaxi
{
    /// <summary>
    /// v43 で増えた「手応え」と「差し替え」のまとめ。
    ///   ・ヒットストップ／揺れの強さ3段階（s/m/l）／ヒットマーク／照準（1つだけ、くっきり）
    ///   ・モデルの差し替え：TaxiModelSet にプレハブを入れると仮モデルの代わりに使う（空なら仮モデル）
    ///   ・調整用の数値：TaxiTuning（空なら初期値）
    /// </summary>
    public partial class IllegalTaxi
    {
        // ---- 調整用の数値（インスペクターで tuning が空なら初期値を使う） ----
        TaxiTuning tuningFallback;
        TaxiTuning T
        {
            get
            {
                if (tuning != null) return tuning;
                if (tuningFallback == null) tuningFallback = ScriptableObject.CreateInstance<TaxiTuning>();
                return tuningFallback;
            }
        }

        // ---- ヒットストップと揺れ ----
        // kind：'s' 小（塩が当たった）／'m' 中（被弾・撃破）／'l' 大（体当たり・決戦の段替わり）
        void Juice(float stop, char kind)
        {
            float k = T.hitstopMul;
            hitstop = Mathf.Max(hitstop, stop * k);
            float amp = kind == 's' ? 0.18f : kind == 'm' ? 0.42f : 0.8f, dur = kind == 's' ? 0.18f : kind == 'm' ? 0.3f : 0.55f;
            if (shake > 0 && shakeAmp > amp * T.shakeMul) return;   // 強い揺れの最中は弱い揺れで上書きしない
            shakeKind = kind; shakeAmp = amp * T.shakeMul; shakeDur = shake = dur;
        }
        // 最初に強く、だんだん収まる
        float ShakeNow() => shake > 0 ? shakeAmp * Mathf.Pow(Mathf.Clamp01(shake / Mathf.Max(0.01f, shakeDur)), 1.5f) : 0;

        // ---- 照準とヒットマーク ----
        GameObject aimMark; Material aimMat;
        class HitM { public GameObject star, ring; public Material sm, rm; public float t = 9, size, spin; public Vector3 w; }
        readonly List<HitM> hitMarks = new List<HitM>();
        const float AIM_DIST = 28f;   // 照準を置く距離（塩が飛んでいく先）

        void BuildJuice()
        {
            // 照準：四隅のカギと中心の点。ミップマップなしでくっきり。画面上の大きさは一定
            var tex = new Texture2D(256, 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[256 * 256];
            for (int j = 0; j < 256; j++) for (int i = 0; i < 256; i++)
            {
                float x = Mathf.Abs(i - 127.5f), yy = Mathf.Abs(j - 127.5f), a = 0;
                bool corner = (x > 78 && x < 118 && yy > 108 && yy < 118) || (yy > 78 && yy < 118 && x > 108 && x < 118);
                if (corner) a = 1;
                if (x * x + yy * yy < 7 * 7) a = 1;
                px[j * 256 + i] = new Color(1, 1, 1, a);
            }
            tex.SetPixels(px); tex.Apply(false, true);
            aimMat = ModelFactory.OverlayMat(Color.white, 3100); aimMat.mainTexture = tex;
            aimMark = ModelFactory.Prim(PrimitiveType.Quad, fx, Vector3.zero, Vector3.one, aimMat); aimMark.name = "aimMark"; aimMark.SetActive(false);

            // ヒットマーク（Web版 v43 と同じ）：黒い縁の8方向の星（白→黄→橙）と、広がる白い輪。明るい背景でも暗い背景でも見える
            var star = StarTex(); var ring = RingTex();
            for (int i = 0; i < 12; i++)
            {
                var h = new HitM { sm = new Material(SpriteShader()) { mainTexture = star, renderQueue = 3091 }, rm = new Material(SpriteShader()) { mainTexture = ring, renderQueue = 3090 } };
                h.star = ModelFactory.Prim(PrimitiveType.Quad, fx, Vector3.zero, Vector3.one, h.sm); h.star.name = "hitStar"; h.star.SetActive(false);
                h.ring = ModelFactory.Prim(PrimitiveType.Quad, fx, Vector3.zero, Vector3.one, h.rm); h.ring.name = "hitRing"; h.ring.SetActive(false);
                hitMarks.Add(h);
            }
        }
        // 色つきの絵をそのまま出す（奥行きを無視して一番手前に描く）シェーダー
        static Shader spriteSh;
        static Shader SpriteShader()
        {
            if (spriteSh == null) spriteSh = Shader.Find("Sprites/Default");
            if (spriteSh == null) spriteSh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            return spriteSh;
        }
        static Texture2D StarTex()
        {
            const int n = 128; var tx = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pts = new Vector2[16];
            for (int i = 0; i < 16; i++) { float r = i % 2 == 1 ? 0.16f : (i % 4 != 0 ? 0.34f : 0.46f), a = i / 16f * Mathf.PI * 2; pts[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r); }
            var px = new Color[n * n];
            for (int j = 0; j < n; j++) for (int i = 0; i < n; i++)
            {
                var p = new Vector2((i + 0.5f) / n - 0.5f, (j + 0.5f) / n - 0.5f);
                bool inside = false; float dEdge = 9;
                for (int k = 0, l = 15; k < 16; l = k++)
                {
                    var a = pts[k]; var b = pts[l];
                    if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
                    var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude); dEdge = Mathf.Min(dEdge, (a + ab * t - p).magnitude);
                }
                Color c = new Color(0, 0, 0, 0);
                if (inside)
                {
                    float rr = p.magnitude / 0.46f;
                    c = rr < 0.45f ? Color.Lerp(Color.white, new Color(1, 0.95f, 0.63f), rr / 0.45f) : Color.Lerp(new Color(1, 0.95f, 0.63f), new Color(1, 0.6f, 0.23f), (rr - 0.45f) / 0.55f);
                    c.a = 1;
                }
                if (dEdge < 0.035f) c = new Color(0.08f, 0.04f, 0.08f, 0.95f);   // 黒い縁
                px[j * n + i] = c;
            }
            tx.SetPixels(px); tx.Apply(false, true); return tx;
        }
        static Texture2D RingTex()
        {
            const int n = 128; var tx = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int j = 0; j < n; j++) for (int i = 0; i < n; i++)
            {
                float d = Mathf.Abs(new Vector2((i + 0.5f) / n - 0.5f, (j + 0.5f) / n - 0.5f).magnitude - 0.4f);
                px[j * n + i] = d < 0.025f ? Color.white : d < 0.045f ? new Color(0.08f, 0.04f, 0.08f, 0.8f) : new Color(0, 0, 0, 0);
            }
            tx.SetPixels(px); tx.Apply(false, true); return tx;
        }
        int hitIdx;
        void HitMark(Vector3 w, float size, Color c)
        {
            if (hitMarks.Count == 0) return;
            var h = hitMarks[hitIdx = (hitIdx + 1) % hitMarks.Count];
            h.t = 0; h.size = size; h.w = w; h.spin = R(0, 360); h.sm.color = c; h.rm.color = c;
        }
        void HitMarkAt(float x, float yy, float z, float size, int hex = 0xffffff) => HitMark(Road.WP(x, yy, z), size, ModelFactory.Hex(hex));
        void UpdateJuiceView(float rdt, bool aiming, bool locked)
        {
            // 照準：塩の通り道（カメラの正面）に1つだけ。敵が線上にいると赤
            SetVis(aimMark, aiming);
            if (aiming)
            {
                AimLine(out var w0, out var ddx, out var ddz);
                var p = w0 + new Vector3(ddx * AIM_DIST, AIM_DIST, ddz * AIM_DIST);
                float dist = Vector3.Distance(cam.transform.position, p);
                float size = dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 2 * 0.08f;   // 画面の高さの 8%
                aimMark.transform.SetPositionAndRotation(p, Quaternion.LookRotation(cam.transform.forward, cam.transform.up));
                aimMark.transform.localScale = Vector3.one * size;
                aimMat.color = locked ? ModelFactory.Hex(0xff4060) : new Color(1, 1, 1, 0.92f);
            }
            // ヒットマーク：星は少しふくらみながら回って消え、輪は大きく広がって消える（0.28秒。ヒットストップ中も動く）
            var face = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
            foreach (var h in hitMarks)
            {
                bool on = h.t <= 0.28f; SetVis(h.star, on); SetVis(h.ring, on); if (!on) continue;
                h.t += rdt; float k = Mathf.Clamp01(h.t / 0.28f);
                var hp = h.w - cam.transform.forward * 2.5f;   // 当たった物より少し手前に出す（物に隠れないように）
                h.star.transform.SetPositionAndRotation(hp, face * Quaternion.Euler(0, 0, h.spin + k * 34));
                h.star.transform.localScale = Vector3.one * h.size * (1.6f + 0.6f * k);
                h.ring.transform.SetPositionAndRotation(hp, face);
                h.ring.transform.localScale = Vector3.one * h.size * (1 + 3.2f * k);
                var sc = h.sm.color; sc.a = 1 - k * k; h.sm.color = sc;
                var rc = h.rm.color; rc.a = (1 - k) * 0.9f; h.rm.color = rc;
            }
            // 塩が当たった敵は一瞬白く光る
            foreach (var c in cops)
            {
                if (c.glow == null) continue;
                SetVis(c.glow, c.flash > 0 && c.go.activeSelf);
            }
            // 決戦の体力：赤いバーの後ろを白い残りがゆっくり追う
            if (trailWait > 0) trailWait -= rdt; else hpTrail = Mathf.MoveTowards(hpTrail, bossHp, rdt * bossHpMax * 0.6f);
            if (hpTrail < bossHp) hpTrail = bossHp;
        }

        // ---- キー入力（R：やり直し、Enter：次へ） ----
        static bool KeyDown(string key)
        {
#if ENABLE_INPUT_SYSTEM
            var k = UnityEngine.InputSystem.Keyboard.current; if (k == null) return false;
            return key == "r" ? k.rKey.wasPressedThisFrame : key == "enter" ? (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) : false;
#else
            return key == "r" ? Input.GetKeyDown(KeyCode.R) : key == "enter" ? (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) : false;
#endif
        }

        // ============================================================
        //  モデルの差し替え（TaxiModelSet が空なら、コードで作った仮モデル）
        // ============================================================
        GameObject Spawn(GameObject prefab)
        {
            var g = Instantiate(prefab);
            foreach (var c in g.GetComponentsInChildren<Collider>()) Destroy(c);   // 当たり判定は距離で計算する
            float s = models != null ? models.scale : 1; g.transform.localScale *= s;
            var root = new GameObject(prefab.name + "_root"); g.transform.SetParent(root.transform, false);
            return root;
        }
        // 差し替えたモデルにも、動かす部品（顔・口・弱点・ランプ）を用意しておく。名前が同じ子があればそれを使う
        ModelFactory.PoliceModel WrapPolice(GameObject root)
        {
            var pm = new ModelFactory.PoliceModel { root = root };
            Transform Find(string n) { foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t; return null; }
            pm.face = Find("face") ?? new GameObject("face").transform; if (pm.face.parent == null) pm.face.SetParent(root.transform, false);
            pm.mouth = Find("mouth") ?? new GameObject("mouth").transform; if (pm.mouth.parent == null) pm.mouth.SetParent(root.transform, false);
            var core = Find("core"); pm.core = core != null ? core.gameObject : new GameObject("core"); if (core == null) pm.core.transform.SetParent(root.transform, false);
            var r = Find("sirenRed"); var b = Find("sirenBlue");
            pm.siren = r != null && b != null ? new[] { r.gameObject, b.gameObject } : new GameObject[0];
            return pm;
        }
        ModelFactory.PoliceModel MakePolice(string type)
        {
            var pf = models == null ? null : type == "junsa" ? models.junsa : models.patochin;
            if (pf == null) return type == "junsa" ? ModelFactory.Junsa() : ModelFactory.Patochin();
            return WrapPolice(Spawn(pf));
        }
        ModelFactory.PoliceModel MakeBossModel() => models != null && models.boss != null ? WrapPolice(Spawn(models.boss)) : ModelFactory.DaiKeibu();
        GameObject MakeGlove() => models != null && models.glove != null ? Spawn(models.glove) : ModelFactory.Glove();
        GameObject MakePellet() => models != null && models.pellet != null ? Spawn(models.pellet) : ModelFactory.Pellet();
        GameObject MakeCuffs() => models != null && models.cuffs != null ? Spawn(models.cuffs) : ModelFactory.Cuffs();
        GameObject MakeCoin() => models != null && models.coin != null ? Spawn(models.coin) : ModelFactory.Coin();
        ModelFactory.ItemModel MakeItem(string kind)
        {
            var pf = models == null ? null : kind == "water" ? models.itemWater : kind == "wind" ? models.itemWind : models.itemCharm;
            if (pf == null) return ModelFactory.Item(kind);
            var root = Spawn(pf);
            return new ModelFactory.ItemModel { root = root, model = root.transform.GetChild(0), spin = null, smoke = null };
        }
    }
}
