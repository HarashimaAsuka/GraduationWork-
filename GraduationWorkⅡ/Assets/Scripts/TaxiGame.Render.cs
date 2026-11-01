using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;   // System.Random と取り違えないように

namespace ShoryoumaTaxi
{
    /// <summary>毎フレームの見た目（Web版 v32 の render に当たる）。位置・向き・大きさ・色だけを変える。</summary>
    public partial class TaxiGame
    {
        readonly List<Soul> tagSort = new List<Soul>();
        readonly HashSet<Soul> tagged = new HashSet<Soul>();

        void LateUpdate()
        {
            if (cam == null) return;
            bool frozen = mode == Mode.Pause || mode == Mode.Shop || mode == Mode.Book || mode == Mode.Name || mode == Mode.Result;
            float dt = frozen ? 0f : frameDt, f = Road.Zf(y);
            float bks = Smooth(bk);
            bool playing = mode == Mode.Play;

            // ---- 空・霧・環境光を層の色に（ステージ3は夢の色。雷が光ると明るく） ----
            var bg = ZoneLook(f, z => z.bg);
            cam.backgroundColor = bg; mirrorCam.backgroundColor = bg;
            RenderSettings.fogColor = ZoneLook(f, z => z.fog);
            float flash = 1 + world.Flash * 3.2f;
            var sky = ZoneLook(f, z => z.sky);
            RenderSettings.ambientSkyColor = sky * 0.9f * flash;
            RenderSettings.ambientEquatorColor = sky * 0.5f * flash;
            RenderSettings.ambientGroundColor = ZoneLook(f, z => z.ground) * flash;

            // ---- カメラ：曲がる道にそって馬の後ろを飛び、道の先を見上げる。道の曲がりにだけ傾く（v26） ----
            float gap = Mathf.Lerp(CAM_BACK, DOWN_GAP, bks);
            float speedK = Mathf.Clamp01((Mathf.Abs(vel) - 8) / 12f) * (wind > 0 ? 1.4f : 1f);
            cam.fieldOfView = FOV_UP + speedK * 6 + (wind > 0 ? 16 * Mathf.Min(1, wind) : 0);
            RenderSettings.fogStartDistance = gap + 16;
            RenderSettings.fogEndDistance = Mathf.Lerp(110, 170, Mathf.Clamp01((f - 0.55f) * 3));
            float follow = 1 - Mathf.Exp(-7 * dt);
            camFX += (px * CAMF - camFX) * follow; camFZ += (pz * CAMF - camFZ) * follow;
            float sh = shake > 0 ? shake * 0.5f : 0;
            float cy0 = y - gap, baseX = Road.Cx(y), baseZ = Road.Cz(y);
            // v20：浮遊感 — カメラがゆっくり漂う
            float flX = Mathf.Sin(t * 0.55f) * 0.45f + Mathf.Sin(t * 1.3f) * 0.12f, flZ = Mathf.Sin(t * 0.8f + 1) * 0.32f, flY = Mathf.Sin(t * 0.45f + 2) * 0.6f;
            camPos = new Vector3(baseX + camFX + flX + R(-sh, sh), cy0 + flY, baseZ + camFZ + CAM_UP + flZ + R(-sh, sh));
            const float ahead = 30;
            float tx = Road.Cx(y + ahead) - baseX, tz = Road.Cz(y + ahead) - baseZ;
            lookX += (tx * 0.55f - lookX) * follow; lookZ += (tz * 0.45f - lookZ) * follow;
            var camLook = new Vector3(camPos.x + lookX, cy0 + gap + ahead, camPos.z + lookZ);
            float curv = (Road.Cx(y + 25) - 2 * Road.Cx(y) + Road.Cx(y - 25)) / 25f;
            camRoll += (Mathf.Clamp(-curv * 2.4f, -0.28f, 0.28f) - camRoll) * follow;
            var rollUp = new Vector3(Mathf.Sin(camRoll), 0, Mathf.Cos(camRoll));
            cam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(camLook - camPos, rollUp));
            var camRot = cam.transform.rotation; var camUp = cam.transform.up;
            world.Tick(t, dt, y, camPos, camRot, f, stage, playing, c => sfx.Play(c, 0.6f), sfx);

            // ---- 精霊馬：前向きは鼻先が奥、後ろ向きはこちらを向く（ナスの牛）。背中は常に画面の上 ----
            bool go = dir > 0, moving = Mathf.Abs(vel) > 3;
            float bob = go ? Mathf.Sin(step * 2) * 0.1f : Mathf.Sin(t * 1.6f) * 0.15f;
            var tr = steed.root.transform;
            tr.position = new Vector3(baseX + px, y + bob, baseZ + pz);
            horseLight.transform.position = new Vector3(tr.position.x, y + 2 * dir, tr.position.z + 1.5f);
            Vector3 F = Vector3.up, U = Vector3.forward;
            var qUp = Quaternion.LookRotation((0.7f * F + 0.71f * U).normalized, (-0.71f * F + 0.7f * U).normalized);
            var qDown = Quaternion.LookRotation((-0.9f * F - 0.42f * U).normalized, (-0.42f * F + 0.9f * U).normalized);
            tr.rotation = Quaternion.Slerp(qUp, qDown, bks);
            if (go && moving) tr.Rotate(Mathf.Sin(step) * 3.4f, 0, 0, Space.Self);
            else if (!go) tr.Rotate(Mathf.Sin(t * 0.9f) * 5.7f, 0, Mathf.Sin(t * 1.3f) * 12.6f, Space.Self);
            tr.Rotate(0, 0, -camRoll * 0.6f * Mathf.Rad2Deg, Space.Self);
            if (over) shrink = Mathf.Max(0.35f, shrink - 0.5f * dt);
            tr.localScale = Vector3.one * Mathf.Lerp(1f, 1.5f, bks) * shrink;
            steed.cucumber.transform.localScale = Vector3.one * Mathf.Max(0.001f, 1 - bks);
            steed.nasu.transform.localScale = Vector3.one * Mathf.Max(0.001f, bks);
            float dry = Mathf.Clamp01(1 - water / 100f);
            var dryCol = new Color(0.6f, 0.56f, 0.4f);
            Mats.SetColor(ModelFactory.CucumberMat, Color.Lerp(new Color(0.37f, 0.61f, 0.25f), dryCol, dry * 0.7f));
            Mats.SetColor(ModelFactory.NasuMat, Color.Lerp(new Color(0.23f, 0.12f, 0.37f), dryCol, dry * 0.7f));
            for (int i = 0; i < steed.cucLegs.Length; i++)
                steed.cucLegs[i].localEulerAngles = new Vector3(Mathf.Sin(step + (i % 2 == 1 ? Mathf.PI : 0) + (i > 1 ? 0.6f : 0)) * 40f, 0, 0);
            for (int i = 0; i < steed.nasuLegs.Length; i++)
                steed.nasuLegs[i].localEulerAngles = new Vector3(Mathf.Sin(t * 7 + i * 1.7f) * 28f, 0, 0);
            steed.spider.transform.localPosition = new Vector3(0, Mathf.Lerp(0.42f, 0.62f, bks), Mathf.Lerp(-0.3f, -0.15f, bks));
            bool blink = inv > 0 && Mathf.FloorToInt(t * 16) % 2 == 0 && !over;
            SetVis(steed.root, !blink);

            // ---- 車掌：走るとき（前向き）は弾み、後ろ向きのときは振り返る。ぶつかるとのけぞる ----
            {
                bool hit = hurtT > 0 || inv > 1.0f;
                conductorRoot.localPosition = new Vector3(0, Mathf.Lerp(0.3f, 0.5f, bks), Mathf.Lerp(0.72f, 0.55f, bks));   // ナスの牛は胴が丸いので少し高く
                if (conductorModel != null)
                {
                    // おばけの車掌：ふわふわ浮いて、後ろ向きのときはこちらを振り返る
                    conductorModel.localPosition = conductorOffset + new Vector3(0, Mathf.Sin(t * 2.2f) * 0.05f, 0);
                    conductorModel.localEulerAngles = new Vector3((hit ? -0.45f : go ? 0.12f : -0.05f) * Mathf.Rad2Deg,
                        (!go ? Mathf.Sin(t * 1.1f) * 0.35f : Mathf.Sin(t * 0.7f) * 0.1f) * Mathf.Rad2Deg, Mathf.Sin(t * 1.4f) * 0.08f * Mathf.Rad2Deg);
                }
                else if (conductorFallback != null)
                {
                    var c = conductorFallback;
                    c.body.localPosition = new Vector3(0, go && moving ? Mathf.Abs(Mathf.Sin(step * 2)) * 0.05f : Mathf.Sin(t * 1.6f) * 0.02f, 0);
                    c.body.localEulerAngles = new Vector3((hit ? -0.45f : go ? 0.12f : -0.05f) * Mathf.Rad2Deg, 0, 0);
                    c.head.localEulerAngles = new Vector3(hit ? -17f : 0, (!go ? Mathf.Sin(t * 1.1f) * 0.5f : Mathf.Sin(t * 0.7f) * 0.12f) * Mathf.Rad2Deg, 0);
                    for (int i = 0; i < c.arms.Length; i++)
                    {
                        var e = c.arms[i].localEulerAngles;
                        c.arms[i].localEulerAngles = new Vector3((-1.15f + (go && moving ? Mathf.Sin(step * 2 + i) * 0.12f : 0) + (hit ? 0.8f : 0)) * Mathf.Rad2Deg, e.y, e.z);
                    }
                }
            }

            // ---- 乗っているお客さん：いつもこちらを向く ----
            for (int i = 0; i < MAX_RIDERS; i++)
            {
                var r = i < riders.Count ? riders[i] : null;
                var m = riderGo[i];
                SetVis(m, r != null);
                if (r == null) continue;
                if (riderRare[i] != r.guest.rare) { ModelFactory.SetSoulRare(m, r.guest.rare); riderRare[i] = r.guest.rare; }
                m.transform.rotation = Quaternion.LookRotation(-cam.transform.forward, camUp);
                m.transform.localPosition = new Vector3(0, 1.0f + Mathf.Sin(t * 3 + i) * 0.06f, 0.3f - i * 0.42f);
                m.transform.localScale = Vector3.one * (r.guest.id == "sumo" ? 0.55f : 0.42f);
            }

            // 後ろ向きのあいだ、クモが上から糸で吊る
            thread.enabled = bks > 0.5f && mode != Mode.Title;
            if (thread.enabled)
            {
                var spos = steed.spider.transform.position;
                thread.SetPosition(0, spos); thread.SetPosition(1, new Vector3(spos.x, spos.y + 40, spos.z - 4));
            }

            // バックミラー（後ろ向きのとき）：カメラの後ろ（下＝手前）を別のカメラで映す
            bool useMirror = (hint == Hint.Mirror || hint == Hint.Both) && playing && bks > 0.6f && !over;
            if (mirrorCam.enabled != useMirror) mirrorCam.enabled = useMirror;
            if (useMirror)
            {
                var mt = new Vector3(camPos.x - baseX + Road.Cx(camPos.y - 10), camPos.y - 10, camPos.z - baseZ + Road.Cz(camPos.y - 10));
                mirrorCam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(mt - camPos, Vector3.forward));
            }

            // ---- 敵・お客さん ----
            Quaternion FaceCam(Vector3 p) => Quaternion.LookRotation(camPos - p, camUp);
            float Grow(float inFront) => dir < 0 ? Smooth((inFront - 0.6f) / 4f) : 1f;
            bool back = dir < 0;
            // v28・v29：後ろ向きのとき、カメラの後ろ（手前）から来るものは
            //   ① 道の奥の景色の中に現れ、道の横（遊ぶ範囲の外）をすーっとカメラの方へ流れてきて、横を抜けて見えなくなる
            //   ② 少しして、カメラのすぐ前に「ばっ」と飛び出す（大きく出て縮む・光る・音）
            //   見た目だけ。当たり判定は本当の位置のまま。k = 1 で景色の奥、k = 0 でカメラのすぐ前
            int swi = 0;
            Vector3 Swoop(float nx, float ox, float oz, float inFront, Color col)
            {
                float dB = -inFront, k = Mathf.Clamp01((dB - 20) / 40f); int side = nx >= 0 ? 1 : -1;
                float vx = side * (RX + 8 + 4 * k) + ox * 0.3f, vz = oz * 0.5f + 2.5f * k, vy = camPos.y + 3 + 125 * k;
                var p = WP(vx, vy, vz);
                if (swi < swoopGlows.Count)
                {
                    var sg = swoopGlows[swi++];
                    SetVis(sg.go, true);
                    sg.go.transform.SetPositionAndRotation(p, camRot); sg.go.transform.localScale = Vector3.one * (3 + 9 * k);
                    TintR(sg.rd, new Color(col.r, col.g, col.b, 0.85f));
                }
                return p;
            }
            bool SwoopRange(float inFront) => back && playing && inFront < -20 && inFront > -60;
            bool HiddenRange(float inFront) => back && inFront >= -20 && inFront <= 0.6f;
            float PopScale(float emT) { float a = t - emT; return a < 0.35f ? 1 + 0.8f * (1 - a / 0.35f) : 1; }

            foreach (var e in enemies)
            {
                float d = Rel(e.y), inFront = e.y - camPos.y;
                if (e.alive && SwoopRange(inFront))
                {
                    var p0 = Swoop(e.nx, e.x, e.z, inFront, WarnColor(e.type));
                    SetVis(e.go, true); e.go.transform.SetPositionAndRotation(p0, FaceCam(p0)); e.go.transform.localScale = Vector3.one * 1.2f;
                    continue;
                }
                if (HiddenRange(inFront)) { SetVis(e.go, false); continue; }
                bool vis = e.alive && d > -12 && inFront > 0.6f && d < 150;
                SetVis(e.go, vis);
                if (!vis) continue;
                var p = WP(e.x, e.y, e.z);
                e.go.transform.SetPositionAndRotation(p, FaceCam(p));
                if (e.type == "tongue") e.go.transform.Rotate(0, 0, Mathf.Sin(t * 2 + e.ph) * 4.6f, Space.Self);
                if (e.type == "kasa") e.go.transform.Rotate(0, 0, Mathf.Sin(t * 2.6f + e.ph) * 17f, Space.Self);
                e.go.transform.localScale = Vector3.one * (e.ripe ? 1.15f : 1f) * (e.hit > 0 ? 1.18f : 1f) * (back ? PopScale(e.emT) : Grow(inFront));
            }
            foreach (var m in movers)
            {
                float inFront = m.y - camPos.y;
                bool vis = m.t >= 0 && inFront > 0.6f;
                SetVis(m.go, vis);
                if (!vis) continue;
                var p = WP(m.x, m.y, m.z);
                m.go.transform.SetPositionAndRotation(p, FaceCam(p));
                m.go.transform.localScale = Vector3.one * (m.hit > 0 ? 1.18f : 1f) * Smooth((inFront - 0.6f) / 4f);
            }
            foreach (var c in chasers)
            {
                float inFront = c.y - camPos.y;
                bool vis = inFront > 0.6f;
                SetVis(c.go, vis);
                float farK = Mathf.Clamp01((c.y - y - 30) / 120f);
                if (vis)
                {
                    var p = WP(c.x, c.y, c.z);
                    c.go.transform.SetPositionAndRotation(p, FaceCam(p));
                    c.go.transform.localScale = Vector3.one * (c.hit > 0 ? 1.18f : 1f) * 1.3f;
                    c.aura.transform.localScale = Vector3.one * (c.r * Mathf.Lerp(4.2f, 16f, farK) / 1.3f);
                    TintR(c.auraRd, new Color(1f, 0.23f, 0.16f, Mathf.Clamp01(0.55f + 0.25f * Mathf.Sin(t * 8 + c.ph) + 0.25f * farK)));
                    SetVis(c.core, farK > 0.05f);
                    c.core.transform.localScale = Vector3.one * Mathf.Max(0.01f, c.r * 7 * farK / 1.3f);
                    TintR(c.coreRd, new Color(1f, 0.88f, 0.69f, farK));
                }
                // 赤い尾
                for (int i = 0; i < TRAIL_N; i++)
                {
                    bool has = i + 1 < c.hist.Count;
                    var h = has ? c.hist[i + 1] : Vector3.zero;
                    bool on = has && h.y - camPos.y > 0.6f;
                    SetVis(c.trail[i], on);
                    if (!on) continue;
                    float k = 1 - i / (float)TRAIL_N;
                    c.trail[i].transform.SetPositionAndRotation(WP(h.x, h.y, h.z), camRot);
                    c.trail[i].transform.localScale = Vector3.one * (c.r * Mathf.Lerp(1.6f, 6.5f, farK) * k + 0.5f);
                    TintR(c.trailRd[i], i % 2 == 1 ? new Color(1f, 0.69f, 0.38f, 0.65f * k) : new Color(1f, 0.29f, 0.16f, 0.65f * k));
                }
            }

            // ---- 親玉 ----
            if (boss != null)
            {
                var b = boss; var bm = b.m;
                if (b.dead && b.dyingT > 0 && b.dyingT < 2)
                {
                    // 倒したとき：震えながら何度もはじけて消える
                    b.dyingT += dt;
                    if (dt > 0 && Random.value < 0.5f) Burst(b.x + R(-3, 3), b.y + R(-3, 3), b.z + R(-3, 3), Random.value < 0.5f ? 0xffd27a : 0xff2d55, 24, 6);
                }
                bool dying = b.dead && b.dyingT > 0 && b.dyingT < 1.6f;
                bool bvis = (!b.dead || dying) && mode != Mode.Title;
                SetVis(bm.root, bvis);
                if (bvis)
                {
                    var p = WP(b.x + (dying ? R(-0.4f, 0.4f) : 0), b.y, b.z + (dying ? R(-0.4f, 0.4f) : 0));
                    bm.root.transform.SetPositionAndRotation(p, FaceCam(p));
                    bool windUp = b.mode == "windup";
                    if (b.mode == "dizzy") bm.root.transform.Rotate(0, 0, Mathf.Sin(t * 9) * 14.3f, Space.Self);
                    bm.root.transform.localScale = Vector3.one * (b.hit > 0 ? 1.06f : 1f) * (windUp ? 1 + 0.06f * Mathf.Sin(t * 40) : 1f) * (dying ? 1 + b.dyingT * 0.3f : 1f);
                    var ac = Hex(windUp ? 0xff1010 : 0xff3a2a); ac.a = windUp ? 0.95f : 0.45f + 0.2f * Mathf.Sin(t * 5);
                    TintR(bm.auraRd, ac);
                    bool sOn = b.shield && !b.dead && b.stun <= 0;
                    SetVis(bm.shield, sOn);
                    if (sOn) TintR(bm.shieldRd, new Color(0.5f, 0.78f, 1f, 0.3f + 0.12f * Mathf.Sin(t * 6)));
                    bm.halo.localEulerAngles = new Vector3(0, 0, t * (b.phase == 3 ? 0.9f : 0.4f) * Mathf.Rad2Deg);
                    for (int i = 0; i < bm.flames.Length; i++)
                    {
                        float k = 1 + 0.25f * Mathf.Sin(t * 9 + i * 1.7f);
                        bm.flames[i].localScale = new Vector3(1, k * (b.phase > 1 ? 1.25f : 1f), 1);
                    }
                    bool coreOn = b.stun > 0 && !b.dead;
                    SetVis(bm.core, coreOn);
                    if (coreOn) bm.core.transform.localScale = Vector3.one * (1 + 0.2f * Mathf.Sin(t * 12));
                    SetVis(bm.warn, (windUp || (b.mode == "rush" && b.modeT < 0.5f)) && Mathf.FloorToInt(t * 8) % 2 == 0);
                }
                foreach (var h in b.hands)
                {
                    bool hv = !h.dead && !b.dead && mode != Mode.Title;
                    SetVis(h.go, hv);
                    if (!hv) continue;
                    var p = WP(h.x, h.y, h.z);
                    h.go.transform.SetPositionAndRotation(p, FaceCam(p));
                    if (h.mode == "sweep" || h.mode == "aimS") h.go.transform.Rotate(0, 0, h.sd * 90f, Space.Self);
                    h.go.transform.localScale = Vector3.one * 1.6f * (h.hit > 0 ? 1.12f : 1f);
                    TintR(h.glow, new Color(1f, 0.19f, 0.13f, h.mode == "aim" || h.mode == "aimS" ? 0.6f + 0.3f * Mathf.Sin(t * 20) : 0.4f));
                }
                if (b.guards != null)
                    foreach (var gd in b.guards)
                    {
                        bool gv = !gd.dead && !b.dead && mode != Mode.Title;
                        SetVis(gd.go, gv);
                        if (!gv) continue;
                        var p = WP(gd.x, gd.y, gd.z);
                        gd.go.transform.SetPositionAndRotation(p, FaceCam(p));
                        gd.go.transform.localScale = Vector3.one * 1.4f * (gd.hit > 0 ? 1.18f : 1f);
                    }
            }
            // 合図：突進・叩きつけの赤い輪、薙ぎ払いの赤い帯
            int tri = 0, tbi = 0;
            void ShowRing(float x, float z, bool locked, int hex)
            {
                if (tri >= tRings.Count) return;
                var ring = tRings[tri++];
                SetVis(ring.g, true); ring.g.transform.SetPositionAndRotation(WP(x, y + 0.6f, z), Quaternion.identity);
                float blinkK = locked ? (Mathf.FloorToInt(t * 14) % 2 == 1 ? 1f : 0.55f) : 0.8f + 0.2f * Mathf.Sin(t * 10);
                float[] ops = { 0.95f, 0.22f, 0.9f, 0.6f };
                var col = Hex(hex);
                for (int k = 0; k < 4; k++) ring.mats[k].color = new Color(col.r, col.g, col.b, ops[k] * blinkK);
                ring.o2.localScale = Vector3.one * (locked ? 1f : 1f + Mathf.Repeat(t * 2, 1f) * 0.4f);
            }
            void ShowBand(float z)
            {
                if (tbi >= tBands.Count) return;
                var band = tBands[tbi++];
                SetVis(band.go, true);
                band.go.transform.position = WP(0, y + 0.6f, z); band.go.transform.localScale = new Vector3(2 * (RX + 4), 2.0f, 1);
                band.mat.color = new Color(1f, 0.19f, 0.19f, 0.16f + 0.12f * Mathf.Sin(t * 16));
            }
            if (boss != null && !boss.dead && playing)
            {
                if (boss.lockState != 0) ShowRing(boss.rx, boss.rz, boss.lockState == 2, 0xff2020);
                foreach (var h in boss.hands) { if (h.ring) ShowRing(h.tx, h.tz, h.mode == "slam" || h.t > 0.85f, 0xff8a20); if (h.band) ShowBand(h.tz); }
            }
            for (; tri < tRings.Count; tri++) SetVis(tRings[tri].g, false);
            for (; tbi < tBands.Count; tbi++) SetVis(tBands[tbi].go, false);

            // ---- お客さん：近い3人だけ、頭の上に行き先の札。馬に近づくと札は消える（v27） ----
            bool notFull = riders.Count < MAX_RIDERS;
            tagSort.Clear(); tagged.Clear();
            foreach (var s in souls) { float d = Rel(s.y); if (!s.gone && d > 12 && d < 120 && s.y - camPos.y > 3) tagSort.Add(s); }
            tagSort.Sort((a, b2) => Rel(a.y).CompareTo(Rel(b2.y)));
            for (int i = 0; i < tagSort.Count && i < 3; i++) tagged.Add(tagSort[i]);
            foreach (var s in souls)
            {
                float d = Rel(s.y), inFront = s.y - camPos.y;
                if (!s.gone && SwoopRange(inFront))
                {
                    var p0 = Swoop(s.nx, s.x, s.z, inFront, RARE_COL[s.guest.rare]);
                    SetVis(s.go, true); SetVis(s.tag.root, false);
                    s.go.transform.SetPositionAndRotation(p0, FaceCam(p0)); s.go.transform.localScale = Vector3.one * 1.4f;
                    continue;
                }
                if (HiddenRange(inFront)) { SetVis(s.go, false); SetVis(s.tag.root, false); continue; }
                bool vis = !s.gone && d > -12 && inFront > 0.6f && d < 150;
                SetVis(s.go, vis);
                bool tagOn = vis && playing && tagged.Contains(s);
                SetVis(s.tag.root, tagOn);
                if (!vis) continue;
                var p = WP(s.x, s.y, s.z);
                s.go.transform.SetPositionAndRotation(p, FaceCam(p));
                s.go.transform.localScale = Vector3.one * (notFull ? 1.25f + 0.1f * Mathf.Sin(t * 5 + s.ph) : 1.0f) * (back ? PopScale(s.emT) : Grow(inFront));
                if (tagOn)
                {
                    float k = Mathf.Clamp(inFront * 0.075f, 0.5f, 8f);
                    s.tag.root.transform.SetPositionAndRotation(WP(s.x, s.y, s.z + 1.2f + 0.9f * k), camRot);
                    s.tag.root.transform.localScale = Vector3.one * k;
                    s.tag.SetAlpha(Mathf.Clamp01((d - 12) / 6f));
                }
            }
            // 空きがあるとき：魂のまわりに、珍しさの色の光の輪と広がる波紋
            int ri = 0;
            if (notFull && playing)
                foreach (var s in souls)
                {
                    if (ri >= soulRings.Count || !s.go.activeSelf || s.gone) continue;
                    float d = Rel(s.y); if (d < 0 || d > 120) continue;
                    var (ring, ripple) = soulRings[ri++];
                    float k = Mathf.Repeat(t * 1.4f + s.ph, 1f), g = 1.5f * Grow(s.y - camPos.y);
                    var rc = RARE_COL[s.guest.rare];
                    var p = s.go.transform.position;
                    SetVis(ring, true); ring.transform.SetPositionAndRotation(p, Quaternion.identity); ring.transform.localScale = Vector3.one * g;
                    Tint(ring, new Color(rc.r, rc.g, rc.b, 0.9f));
                    SetVis(ripple, true); ripple.transform.SetPositionAndRotation(p, Quaternion.identity); ripple.transform.localScale = Vector3.one * g * (1 + k * 1.2f);
                    Tint(ripple, new Color(rc.r, rc.g, rc.b, 0.6f * (1 - k)));
                }
            for (; ri < soulRings.Count; ri++) { SetVis(soulRings[ri].ring, false); SetVis(soulRings[ri].ripple, false); }
            for (; swi < swoopGlows.Count; swi++) SetVis(swoopGlows[swi].go, false);

            // ---- アイテム（立体の小物）：プレイヤーの前を隠さないよう、通り過ぎたものは消し、近づくと名前の札も消える ----
            foreach (var it in items)
            {
                float d = Rel(it.y), inFront = it.y - camPos.y;
                bool vis = !it.taken && Mathf.Abs(d) < 150 && (dir > 0 ? it.y > y - 0.5f : inFront > 2.5f);
                SetVis(it.im.root, vis);
                if (!vis) { SetVis(it.tag.root, false); continue; }
                var p = WP(it.x, it.y, it.z);
                var root = it.im.root.transform;
                root.SetPositionAndRotation(p, FaceCam(p));
                float far = Mathf.Clamp(inFront * 0.035f, 1f, 2.4f), k = Mathf.Repeat(t * 1.1f + it.ph, 1f), sc = Grow(inFront) * far;
                root.localScale = Vector3.one * sc;
                it.im.model.localEulerAngles = new Vector3(0, (t * 1.6f + it.ph) * Mathf.Rad2Deg, 0);
                it.im.model.localPosition = new Vector3(0, Mathf.Sin(t * 2.4f + it.ph) * 0.18f, 0);
                if (it.im.spin != null) it.im.spin.localEulerAngles = it.kind == "water" ? new Vector3(0, t * 3 * Mathf.Rad2Deg, 0) : new Vector3(0, 0, -t * 9 * Mathf.Rad2Deg);
                if (it.im.smoke != null)
                {
                    TintR(it.im.smoke, new Color(0.91f, 0.88f, 0.82f, 0.35f + 0.15f * Mathf.Sin(t * 2 + it.ph)));
                    it.im.smoke.transform.localPosition = new Vector3(0, 2.0f + Mathf.Sin(t * 1.3f) * 0.2f, 0);
                }
                it.halo.localScale = Vector3.one * (1 + k * 0.5f);
                var hc = Hex(ItemHex(it.kind)); hc.a = 0.7f * (1 - k);
                TintR(it.haloRd, hc);
                bool tagOn = playing && inFront < 110 && inFront > 26;
                SetVis(it.tag.root, tagOn);
                if (tagOn) { it.tag.root.transform.SetPositionAndRotation(p + camUp * (2.4f * sc), camRot); it.tag.root.transform.localScale = Vector3.one * sc; }
            }
            foreach (var fl in flying)
            {
                var p = new Vector3(fl.x, fl.y, fl.z);
                fl.go.transform.SetPositionAndRotation(p, FaceCam(p) * Quaternion.Euler(0, 0, t * fl.spin));
                fl.go.transform.localScale = Vector3.one * Mathf.Max(0.01f, fl.life / fl.max) * (fl.home ? 0.8f : 0.6f);
            }
            foreach (var b in salts)
            {
                b.go.transform.SetPositionAndRotation(WP(b.x, b.y, b.z), Quaternion.identity);
                b.crystal.localEulerAngles = new Vector3(b.rot * Mathf.Rad2Deg, b.rot * 1.3f * Mathf.Rad2Deg, 0);
            }
            foreach (var s in shots)
            {
                s.go.transform.position = WP(s.x, s.y, s.z);
                if (s.type == "onibi" || s.type == "fire") s.go.transform.rotation = camRot;
            }

            // ---- 行き先の門：乗っているお客さん1人に1つ。道いっぱいの大きさに合わせる ----
            for (int i = 0; i < MAX_RIDERS; i++)
            {
                var r = i < riders.Count ? riders[i] : null;
                bool on = r != null && playing;
                SetVis(gates[i], on);
                var lb = gateLabels[i];
                if (!on) { SetVis(lb.root, false); continue; }
                float sx = (RX * 1.15f + 1.6f) * 0.92f, sz = (RZ * 1.15f + 1.6f) * 0.92f;
                gates[i].transform.SetPositionAndRotation(new Vector3(Road.Cx(r.y), r.y, Road.Cz(r.y)), Quaternion.identity);
                gates[i].transform.localScale = new Vector3(sx / 7.4f, 1, sz / 7.4f);
                TintR(gateDisc[i], new Color(1f, 0.75f, 0.44f, 0.1f + 0.08f * Mathf.Sin(t * 4 + i)));
                if (lb.text != r.goalName) { lb.text = r.goalName; lb.main.text = r.goalName; }
                float gdist = r.y - camPos.y;
                // v27：門が馬に近づいたら札を消す。操作キャラに被らないように
                bool lOn = gdist > 22;
                SetVis(lb.root, lOn);
                if (lOn)
                {
                    float ls = Mathf.Clamp(gdist / 18f, 0.6f, 3.2f);
                    lb.root.transform.SetPositionAndRotation(new Vector3(Road.Cx(r.y), r.y, Road.Cz(r.y) + sz + 1.3f), camRot);
                    lb.root.transform.localScale = Vector3.one * ls;
                    lb.SetAlpha(Mathf.Clamp01((gdist - 22) / 8f));
                }
            }

            // ---- 予兆の輪（設定「光る輪」）：カメラの後ろにいるものが、馬のどこを通るか ----
            int wi = 0;
            void Warn(float x, float yy, float z, float r, Color col)
            {
                if (wi >= warnRings.Count) return;
                float behind = camPos.y - yy;
                if (behind < -5 || behind > WARN_RANGE) return;
                var w = warnRings[wi++];
                float near = Mathf.Clamp01(1 - behind / WARN_RANGE);
                float fade = behind < 0 ? Mathf.Clamp01(1 + behind / 5) : 1;
                float pulse = 0.75f + 0.25f * Mathf.Sin(t * (6 + near * 10));
                SetVis(w, true);
                w.transform.SetPositionAndRotation(WP(x, y - 1.2f, z), Quaternion.identity);
                w.transform.localScale = Vector3.one * r * Mathf.Lerp(2.2f, 1.15f, near);
                Tint(w, new Color(col.r, col.g, col.b, (0.35f + 0.6f * near) * pulse * fade));
            }
            if (hint == Hint.Ring && dir < 0 && playing && !over)
            {
                foreach (var s in souls) if (!s.gone) Warn(s.x, s.y, s.z, 1.1f, new Color(0.56f, 0.88f, 1f));
                foreach (var e in enemies) if (e.alive) Warn(e.x, e.y, e.z, e.r, WarnColor(e.type));
            }
            for (; wi < warnRings.Count; wi++) SetVis(warnRings[wi], false);

            // ---- 影（設定「影」「影＋ミラー」）：地獄の火に下から照らされた影が、馬の手前の面に映る ----
            int si = 0;
            void Shadow(float ox, float oy, float oz, string type, float r)
            {
                if (si >= shadows.Count) return;
                float planeY = y - 1.4f, dist = planeY - oy, range = 52;
                if (dist < 0.4f || dist > range) return;
                float near = Mathf.Clamp01(1 - dist / range);
                var (root, soft, sharp) = shadows[si++];
                SetVis(root, true);
                // 板は XZ 平面に寝かせる（板の上＝画面の上＝+z）。画面の右が -x なので、x を裏返して左右を合わせる
                float sc = r * 2.3f * Mathf.Lerp(2.6f, 1.0f, near);
                root.transform.SetPositionAndRotation(WP(ox, planeY, oz), Quaternion.Euler(90f, 0, 0));
                root.transform.localScale = new Vector3(-sc, sc, sc);
                soft.sharedMaterial = ModelFactory.ShadowMat(type, true);
                sharp.sharedMaterial = ModelFactory.ShadowMat(type, false);
                Tint(soft.gameObject, new Color(1, 1, 1, 0.22f + 0.38f * near));
                Tint(sharp.gameObject, new Color(1, 1, 1, Mathf.Pow(near, 1.6f) * 0.95f));
            }
            if ((hint == Hint.Shadow || hint == Hint.Both) && dir < 0 && playing && !over)
            {
                foreach (var s in souls) if (!s.gone) Shadow(s.x, s.y, s.z, "soul", 1.0f);
                foreach (var e in enemies) if (e.alive) Shadow(e.x, e.y, e.z, e.type, e.r);
            }
            for (; si < shadows.Count; si++) SetVis(shadows[si].root, false);

            // ---- 照準の輪：塩はいつも道の先（画面の奥）へ飛ぶ。敵が射線に入ると赤 ----
            bool aiming = playing && !over && clearT == 0;
            bool locked = false;
            if (aiming)
            {
                bool InLine(float ox, float oy, float oz, float r) { float up = oy - y; return up > 1 && up < 55 && Dist(ox, oz) < r + 0.3f; }
                foreach (var e in enemies) if (e.alive && InLine(e.x, e.y, e.z, e.r)) { locked = true; break; }
                if (!locked) foreach (var m in movers) if (!m.dead && m.t >= 0 && InLine(m.x, m.y, m.z, m.r)) { locked = true; break; }
                if (!locked) foreach (var c in chasers) if (!c.dead && InLine(c.x, c.y, c.z, c.r)) { locked = true; break; }
                if (!locked && boss != null && !boss.dead)
                {
                    if (InLine(boss.x, boss.y, boss.z, 3.6f)) locked = true;
                    else if (boss.guards != null) foreach (var gd in boss.guards) if (!gd.dead && InLine(gd.x, gd.y, gd.z, 1.7f)) { locked = true; break; }
                }
            }
            for (int i = 0; i < reticles.Count; i++)
            {
                SetVis(reticles[i], aiming);
                if (!aiming) continue;
                reticles[i].transform.SetPositionAndRotation(WP(px, y + (i == 0 ? 12 : 28), pz), Quaternion.identity);
                Tint(reticles[i], locked ? new Color(1f, 0.25f, 0.38f) : new Color(1f, 1f, 1f, i == 0 ? 0.7f : 0.45f));
            }

            // ---- 風の筋：尾は来た方向を指す ----
            float len = Mathf.Clamp(Mathf.Abs(vel) / 5f, 0.2f, 3.2f) * (wind > 0 ? 1.8f : 1f);
            for (int i = 0; i < AIR_N; i++)
            {
                if (i < air.Count)
                {
                    var p = air[i];
                    float fade = Mathf.Clamp01(1 - Mathf.Abs(Rel(p.y)) / 70) * (dir < 0 ? 1 : 0.8f);
                    float ox = Road.Cx(p.y), oz = Road.Cz(p.y);
                    airV[i * 2] = new Vector3(ox + p.x, p.y, oz + p.z); airV[i * 2 + 1] = new Vector3(ox + p.x, p.y + dir * len, oz + p.z);
                    airC[i * 2] = new Color(p.c.r * fade, p.c.g * fade, p.c.b * fade, fade); airC[i * 2 + 1] = new Color(0, 0, 0, 0);
                }
                else { airV[i * 2] = airV[i * 2 + 1] = new Vector3(0, -9999, 0); }
            }
            airMesh.vertices = airV; airMesh.colors = airC;
            airMesh.bounds = new Bounds(new Vector3(baseX, y, baseZ), new Vector3(100, 260, 100));

            // ---- 火花 ----
            for (int i = 0; i < PART_N; i++)
            {
                if (i >= parts.Count) { partCloud.Hide(i); continue; }
                var p = parts[i];
                float a = Mathf.Clamp01(p.life / p.max);
                partCloud.Set(i, p.p, 0.5f, new Color(p.c.r, p.c.g, p.c.b, a));
            }
            partCloud.Apply(new Bounds(camPos, Vector3.one * 500));
        }

        static void SetVis(GameObject g, bool on) { if (g.activeSelf != on) g.SetActive(on); }

        /// <summary>
        /// 線・板の色を変える。Sprites/Default は「頂点の色 × _Color × _RendererColor」なので、
        /// _RendererColor は白にしておき、色（透明度も）は _Color で決める。
        /// </summary>
        void Tint(GameObject g, Color c) => TintR(g.GetComponent<Renderer>(), c);

        void TintR(Renderer rd, Color c)
        {
            if (rd == null) return;
            mpb.Clear();
            mpb.SetColor("_Color", c); mpb.SetColor("_BaseColor", c); mpb.SetColor("_RendererColor", Color.white);
            rd.SetPropertyBlock(mpb);
        }
    }
}
