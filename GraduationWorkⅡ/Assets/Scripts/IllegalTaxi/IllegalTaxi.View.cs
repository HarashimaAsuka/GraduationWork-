using UnityEngine;

namespace ShoryoumaTaxi
{
    /// <summary>毎フレームの見た目（Web版 v42 の render に当たる）。</summary>
    public partial class IllegalTaxi
    {
        float boltT = 3, flashT;

        // カメラのすぐ前では縮んで消え、遠くではふっと消える（急に消えないように）
        float FadeFor(float yy) => Smooth((yy - camPos.y - 1.5f) / 4.5f) * Mathf.Clamp01(1 - (yy - y - 70) / 30f);
        Quaternion FaceCam(Vector3 p) => Quaternion.LookRotation(camPos - p, cam.transform.up);
        static void SetVis(GameObject g, bool on) { if (g.activeSelf != on) g.SetActive(on); }

        void LateUpdate()
        {
            if (cam == null) return;
            bool frozen = mode != Mode.Play && mode != Mode.Title;
            float dt = frozen ? 0 : frameDt, f = Road.Zf(y), bks = Smooth(bk);
            bool back = phase != "out", playing = mode == Mode.Play;

            // ---- 空・霧・環境光（ステージ2＝夕暮れ、ステージ3＝雨と稲光、ステージ4＝赤い月の夜） ----
            System.Func<System.Func<World.Zone, Color>, Color> Z = pick => st != null && st.boss ? World.ZoneColorOf(World.ZonesM, f, pick) : World.ZoneColor(f, pick);
            Color bg = Z(z => z.bg), fog = Z(z => z.fog), sky = Z(z => z.sky), ground = Z(z => z.ground);
            if (stage == 2) { var tint = ModelFactory.Hex(0x3a1a12); bg = Color.Lerp(bg, tint, 0.3f); fog = Color.Lerp(fog, ModelFactory.Hex(0x5a2a1a), 0.3f); sky = Color.Lerp(sky, ModelFactory.Hex(0xffb070), 0.3f); }
            float flash = 1 + world.Flash * 3.2f;
            if (stage == 3)
            {
                bg = Color.Lerp(bg, ModelFactory.Hex(0x081428), 0.65f); fog = Color.Lerp(fog, ModelFactory.Hex(0x14223e), 0.65f);
                sky = Color.Lerp(sky, ModelFactory.Hex(0x7a9ad0), 0.6f); ground = Color.Lerp(ground, ModelFactory.Hex(0x0a1838), 0.6f);
                boltT -= dt; if (boltT <= 0 && playing) { boltT = R(3, 7); flashT = 0.35f; se.Play(se.roar, 0.15f); }
                flashT = Mathf.Max(0, flashT - dt);
                if (flashT > 0) { float k = Mathf.Min(1, flashT * 4); bg = Color.Lerp(bg, ModelFactory.Hex(0xc8d8ff), k * 0.6f); flash += k * 2.5f; }
            }
            cam.backgroundColor = bg; RenderSettings.fogColor = fog;
            RenderSettings.ambientSkyColor = sky * 0.9f * flash; RenderSettings.ambientEquatorColor = sky * 0.5f * flash; RenderSettings.ambientGroundColor = ground * flash;

            // ---- カメラ ----
            camGap += (Mathf.Lerp(CAM_BACK, DOWN_GAP, bks) - camGap) * (1 - Mathf.Exp(-3 * frameDt));
            float speedK = Mathf.Clamp01((Mathf.Abs(vel) - 8) / 12f);
            cam.fieldOfView = FOV_UP + speedK * 8 + (wind > 0 ? 18 * Mathf.Min(1, wind) : 0);
            RenderSettings.fogStartDistance = camGap + 16; RenderSettings.fogEndDistance = Mathf.Lerp(110, 170, Mathf.Clamp01((f - 0.55f) * 3));
            float follow = 1 - Mathf.Exp(-7 * frameDt);
            camFX += (px * CAMF - camFX) * follow; camFZ += (pz * CAMF - camFZ) * follow;
            float near = back && bossM.root.activeSelf && !st.boss ? Mathf.Clamp01(1 - (bossY - y) / 30f) : 0;
            float sh = (shake > 0 ? shake * 0.5f : 0) + near * 0.12f;
            float cy0 = y - camGap, baseX = Road.Cx(y), baseZ = Road.Cz(y);
            float flX = Mathf.Sin(t * 0.55f) * 0.35f, flZ = Mathf.Sin(t * 0.8f + 1) * 0.25f, flY = Mathf.Sin(t * 0.45f + 2) * 0.4f;
            camPos = new Vector3(baseX + camFX + flX + R(-sh, sh), cy0 + flY, baseZ + camFZ + CAM_UP + flZ + R(-sh, sh));
            const float ahead = 30;
            float tx = Road.Cx(y + ahead) - baseX, tz = Road.Cz(y + ahead) - baseZ;
            lookX += (tx * 0.55f - lookX) * follow; lookZ += (tz * 0.45f - lookZ) * follow;
            var camLook = new Vector3(camPos.x + lookX, cy0 + camGap + ahead, camPos.z + lookZ);
            float curv = (Road.Cx(y + 25) - 2 * Road.Cx(y) + Road.Cx(y - 25)) / 25f;
            camRoll += (Mathf.Clamp(-curv * 2.4f, -0.28f, 0.28f) - camRoll) * follow;
            cam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(camLook - camPos, new Vector3(Mathf.Sin(camRoll), 0, Mathf.Cos(camRoll))));
            keyLight.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(new Vector3(3, 44, -6), Vector3.forward));
            world.Tick(t, dt, y, camPos, cam.transform.rotation, f, st.boss ? 3 : 1, playing, c => se.Play(c, 0.6f), worldSfx);

            // ---- 精霊馬：行きはきゅうりの馬、帰りはナスの牛（こちらを向く） ----
            bool go = dir > 0, moving = Mathf.Abs(vel) > 3;
            stepT += dt * Mathf.Abs(vel) * (go ? 0.75f : 0.45f);
            var tr = steed.root.transform;
            tr.position = new Vector3(baseX + px, y + Mathf.Sin(stepT * 2) * (go ? 0.1f : 0.12f), baseZ + pz);
            horseLight.transform.position = new Vector3(tr.position.x, y + 2 * dir, tr.position.z + 1.5f);
            Vector3 F = Vector3.up, U = Vector3.forward;
            var qUp = Quaternion.LookRotation((0.7f * F + 0.71f * U).normalized, (-0.71f * F + 0.7f * U).normalized);
            var qDown = Quaternion.LookRotation((-0.9f * F - 0.42f * U).normalized, (-0.42f * F + 0.9f * U).normalized);
            tr.rotation = Quaternion.Slerp(qUp, qDown, bks);
            if (go && moving) tr.Rotate(Mathf.Sin(stepT) * 3.4f, 0, 0, Space.Self);
            if (!go && stun > 0) tr.Rotate(-14f, 0, 0, Space.Self);   // 体当たりでのけぞる
            tr.Rotate(0, 0, -camRoll * 0.6f * Mathf.Rad2Deg, Space.Self);
            float morph = phase == "alarm" ? Mathf.Sin(Mathf.Clamp01((alarmT - 1.6f) / 0.8f) * Mathf.PI) : 0;
            tr.localScale = Vector3.one * Mathf.Lerp(1f, 1.35f, bks) * (1 - 0.35f * morph);
            steed.cucumber.transform.localScale = Vector3.one * Mathf.Max(0.001f, 1 - bks);
            steed.nasu.transform.localScale = Vector3.one * Mathf.Max(0.001f, bks);
            float dry = Mathf.Clamp01(1 - water / 100f); var dryCol = new Color(0.6f, 0.56f, 0.4f);
            Mats.SetColor(ModelFactory.CucumberMat, Color.Lerp(new Color(0.37f, 0.61f, 0.25f), dryCol, dry * 0.7f));
            Mats.SetColor(ModelFactory.NasuMat, Color.Lerp(new Color(0.23f, 0.12f, 0.37f), dryCol, dry * 0.7f));
            for (int i = 0; i < steed.cucLegs.Length; i++) steed.cucLegs[i].localEulerAngles = new Vector3(Mathf.Sin(stepT + (i % 2 == 1 ? Mathf.PI : 0) + (i > 1 ? 0.6f : 0)) * 40f, 0, 0);
            for (int i = 0; i < steed.nasuLegs.Length; i++) steed.nasuLegs[i].localEulerAngles = new Vector3(Mathf.Sin(stepT + i * 1.7f) * 28f, 0, 0);
            SetVis(steed.root, over || !(inv > 0 && Mathf.FloorToInt(t * 16) % 2 == 0));
            // 自機の光：行きは緑、帰りは紫、お守りがあれば赤
            aura.transform.SetPositionAndRotation(new Vector3(tr.position.x, y - 0.6f * dir, tr.position.z - 0.2f), cam.transform.rotation);
            var ac = ModelFactory.Hex(charm ? 0xff9a9a : go ? 0x9dffb0 : 0xc8a0ff); ac.a = 0.45f + 0.12f * Mathf.Sin(t * 4);
            Tint(aura, ac);
            conductorRoot.localPosition = new Vector3(0, Mathf.Lerp(0.3f, 0.5f, bks), Mathf.Lerp(0.72f, 0.55f, bks));
            if (conductorModel != null)
            {
                conductorModel.localPosition = conductorOffset + new Vector3(0, Mathf.Sin(t * 2.2f) * 0.05f, 0);
                conductorModel.localEulerAngles = new Vector3((hurtT > 0 ? -0.45f : go ? 0.12f : -0.05f) * Mathf.Rad2Deg, (!go ? Mathf.Sin(t * 1.1f) * 0.35f : Mathf.Sin(t * 0.7f) * 0.1f) * Mathf.Rad2Deg, Mathf.Sin(t * 1.4f) * 0.08f * Mathf.Rad2Deg);
            }
            for (int i = 0; i < RIDER_SHOW; i++)
            {
                bool on = i < riders.Count; SetVis(riderGo[i], on); if (!on) continue;
                if (riderRare[i] != riders[i]) { ModelFactory.SetSoulRare(riderGo[i], riders[i]); riderRare[i] = riders[i]; }
                riderGo[i].transform.rotation = Quaternion.LookRotation(-cam.transform.forward, cam.transform.up);
                riderGo[i].transform.localPosition = new Vector3(i % 2 == 1 ? -0.2f : 0.2f, 1.0f + Mathf.Sin(t * 3 + i) * 0.06f, 0.35f - (i / 2) * 0.36f);
            }

            // ---- 魂・警察・弾・アイテム・小銭・塩 ----
            foreach (var s in souls)
            {
                if (s.gone) continue;
                float a = s.y - y < 140 ? FadeFor(s.y) : 0; SetVis(s.go, a > 0.01f); if (a <= 0.01f) continue;
                var p = Road.WP(s.x, s.y + Mathf.Sin(t * 1.8f + s.ph) * 0.25f, s.z);
                s.go.transform.SetPositionAndRotation(p, FaceCam(p)); s.go.transform.localScale = Vector3.one * 1.1f * a;
            }
            foreach (var c in cops)
            {
                float x = c.x, yy = c.y, z = c.z, a = FadeFor(c.y);
                if (c.mode == "enter") { float k = 1 - EaseOut(Mathf.Clamp01(c.enterT / c.enterDur)); x += c.ex * k; yy += c.ey * k; z += c.ez * k; a = 1; }
                SetVis(c.go, a > 0.01f); if (a <= 0.01f) continue;
                var p = Road.WP(x, yy + Mathf.Sin(t * 1.5f + c.ph) * 0.25f, z);
                c.go.transform.SetPositionAndRotation(p, FaceCam(p));
                if (c.mode == "enter") c.go.transform.Rotate(0, 0, (1 - c.enterT / c.enterDur) * 140f * Mathf.Sign(c.ex), Space.Self);
                c.go.transform.localScale = Vector3.one * a * (1 + c.flash * 1.5f);
                Siren(c.siren, 6);
            }
            foreach (var fl in flying)
            {
                fl.go.transform.position = Road.WP(fl.x, fl.y, fl.z);
                if (fl.spin > 0) fl.go.transform.Rotate(fl.spin * dt * Mathf.Rad2Deg, 0, fl.spin * 0.7f * dt * Mathf.Rad2Deg, Space.Self);
                else fl.go.transform.rotation = FaceCam(fl.go.transform.position);
            }
            foreach (var b in shots) { b.go.transform.SetPositionAndRotation(b.p, FaceCam(b.p)); if (b.cuff) b.go.transform.Rotate(0, 0, t * 460f, Space.Self); }
            foreach (var it in items)
            {
                if (it.taken) continue;
                float a = FadeFor(it.y); SetVis(it.im.root, a > 0.01f);
                bool tag = a > 0.01f && Mathf.Abs(it.y - y) < 50; SetVis(it.tag.root, tag);
                if (a <= 0.01f) continue;
                var p = Road.WP(it.x, it.y + Mathf.Sin(t * 2 + it.y) * 0.2f, it.z);
                it.im.root.transform.SetPositionAndRotation(p, FaceCam(p)); it.im.root.transform.localScale = Vector3.one * a;
                it.im.model.localEulerAngles = new Vector3(0, t * 92f, 0);
                if (it.im.spin != null) it.im.spin.localEulerAngles = new Vector3(0, 0, t * 344f);
                if (tag) { var q = p + cam.transform.up * 2.6f; it.tag.root.transform.SetPositionAndRotation(q, Quaternion.LookRotation(cam.transform.forward, cam.transform.up)); }
            }
            foreach (var cn in coins)
            {
                if (cn.taken) continue;
                float a = FadeFor(cn.y); SetVis(cn.go, a > 0.01f); if (a <= 0.01f) continue;
                var p = Road.WP(cn.x, cn.y + Mathf.Sin(t * 2 + cn.y) * 0.2f, cn.z);
                cn.go.transform.SetPositionAndRotation(p, FaceCam(p)); cn.go.transform.Rotate(0, 0, t * 170f, Space.Self); cn.go.transform.localScale = Vector3.one * a;
            }
            foreach (var s in salts) s.go.transform.SetPositionAndRotation(SaltPos(s), Quaternion.Euler(t * 500, t * 400, 0));

            // ---- ねらいの輪：塩が飛ぶ先。線上に敵がいると赤くなる ----
            {
                bool aiming = playing && !over && phase != "alarm", locked = false;
                if (aiming)
                {
                    foreach (var c in cops) if (!c.dead && c.mode != "enter" && InLine(c.y, c.x, c.z, c.r)) locked = true;
                    if (bossM.root.activeSelf && phase == "back" && InLine(bossY, bossX, bossZ, 3.6f)) locked = true;
                    if (B != null) { foreach (var h in B.hands) if (!h.dead && InLine(h.y, h.x, h.z, 1.8f)) locked = true; foreach (var g in B.guards) if (!g.dead && InLine(g.y, g.x, g.z, 1.4f)) locked = true; }
                }
                AimLine(out var w0, out var ddx, out var ddz);
                float[] dists = { 12, 28 };
                for (int i = 0; i < 2; i++)
                {
                    SetVis(reticles[i], aiming); if (!aiming) continue;
                    reticles[i].transform.SetPositionAndRotation(w0 + new Vector3(ddx * dists[i], dists[i], ddz * dists[i]), Quaternion.LookRotation(cam.transform.forward, cam.transform.up));
                    Tint(reticles[i], locked ? ModelFactory.Hex(0xff4060) : new Color(1, 1, 1, i == 0 ? 0.7f : 0.45f));
                }
            }

            // ---- 大警部 ----
            if (bossM.root.activeSelf)
            {
                float by = phase == "alarm" ? Mathf.Lerp(bossY + 40, bossY, Mathf.Clamp01((alarmT - 1.5f) / 1.2f)) : bossY;
                float nr = Mathf.Clamp01((by - y) / 40f);
                var p = Road.WP(bossX, by, bossZ);   // 当たり判定と同じ場所に出す
                bossM.root.transform.SetPositionAndRotation(p, FaceCam(p));
                bossM.root.transform.localScale = Vector3.one * (st.boss ? 4.2f : Mathf.Lerp(4.2f, 7f, nr)) * (1 + bossFlash * 0.6f);
                bossM.face.localEulerAngles = new Vector3(0, 0, B != null && B.mode == "dizzy" ? Mathf.Sin(t * 10) * 14f : Mathf.Sin(t * 3) * 3.4f);
                bossM.mouth.localScale = new Vector3(0.84f, (0.18f + (bossFlash > 0 ? 0.12f : 0)) * 2, 0.2f);
                SetVis(bossM.core, B != null && B.stunT > 0);
                Siren(bossM.siren, 5);
            }
            // ---- ステージ4のしかけ ----
            {
                int ri = 0; bool on = B != null && playing && !over;
                SetVis(sweepBand, false); SetVis(rushRing, false); SetVis(rushMark, false);
                if (on)
                {
                    foreach (var h in B.hands)
                    {
                        bool vis = !h.dead && B.intro > 0.5f; SetVis(h.go, vis); if (!vis) continue;
                        var p = Road.WP(h.x, h.y, h.z);
                        h.go.transform.SetPositionAndRotation(p, FaceCam(p));
                        h.go.transform.Rotate(0, 0, h.mode == "slam" ? 180f : h.mode == "sweep" || h.mode == "aimS" ? h.sd * 90f : h.sd * 17f, Space.Self);
                        h.go.transform.localScale = Vector3.one * 1.3f * (1 + h.hit * 2);
                        if (h.ring && ri < 2) { var r = slamRings[ri++]; SetVis(r, true); r.transform.position = Road.WP(h.tx, y, h.tz); }
                        if (h.band) { SetVis(sweepBand, true); sweepBand.transform.position = Road.WP(0, y, h.tz); }
                    }
                    if (B.lockMode > 0)
                    {
                        SetVis(rushRing, true); SetVis(rushMark, true);
                        rushRing.transform.position = Road.WP(B.rx, y, B.rz);
                        var mp = Road.WP(bossX, bossY - 1, bossZ + 4); rushMark.transform.SetPositionAndRotation(mp, Quaternion.LookRotation(cam.transform.forward, cam.transform.up));
                    }
                    foreach (var g in B.guards)
                    {
                        if (g.dead) continue;
                        var p = Road.WP(g.x, g.y, g.z); g.go.transform.SetPositionAndRotation(p, FaceCam(p)); g.go.transform.localScale = Vector3.one * 1.3f * (1 + g.hit * 2);
                        Siren(g.siren, 6);
                    }
                }
                else foreach (var g in gloves) SetVis(g, false);
                for (; ri < 2; ri++) SetVis(slamRings[ri], false);
            }
            // ---- 逃げている感じ：赤と青の回転灯 ----
            bool chase = back && bossM.root.activeSelf && playing;
            if (chase)
            {
                bool on = Mathf.FloorToInt(t * 4) % 2 == 0; float I = 2.2f + near * 3;
                strobeR.intensity = on ? I : 0.2f; strobeB.intensity = on ? 0.2f : I;
                strobeR.transform.position = new Vector3(tr.position.x - 6, y + 8, tr.position.z + 5); strobeB.transform.position = new Vector3(tr.position.x + 6, y + 8, tr.position.z + 5);
            }
            else strobeR.intensity = strobeB.intensity = 0;
            // ---- 門 ----
            SetVis(gateHell, Mathf.Abs(START - y) < 160 || back);
            SetVis(gateWorld, Mathf.Abs(END - y) < 200 && !st.boss);
            foreach (var (lb, gy, isHell) in new[] { (labelHell, START, true), (labelWorld, END, false) })
            {
                bool show = playing && Mathf.Abs(gy - y) > 150 && Mathf.Abs(gy - y) < 220 && isHell == back;   // 門が近づいたら札は消す（v27）
                SetVis(lb.root, show);
                if (show) lb.root.transform.SetPositionAndRotation(Road.WP(0, gy + 2.5f, 8.6f), Quaternion.LookRotation(cam.transform.forward, cam.transform.up));
            }

            // ---- 風の筋（ステージ3は雨）と火花 ----
            float len = Mathf.Clamp(Mathf.Abs(vel) / 5f, 0.2f, 3.2f) * (wind > 0 ? 1.8f : 1f) * (back ? 1.6f : 1f);
            for (int i = 0; i < AIR_N; i++)
            {
                if (i < air.Count)
                {
                    var p = air[i]; float fade = Mathf.Clamp01(1 - Mathf.Abs(p.y - y) / 70f) * 0.8f;
                    float ox = Road.Cx(p.y), oz = Road.Cz(p.y);
                    airV[i * 2] = new Vector3(ox + p.x, p.y, oz + p.z); airV[i * 2 + 1] = new Vector3(ox + p.x, p.y + dir * len, oz + p.z);
                    airC[i * 2] = new Color(p.c.r * fade, p.c.g * fade, p.c.b * fade, fade); airC[i * 2 + 1] = new Color(0, 0, 0, 0);
                }
                else airV[i * 2] = airV[i * 2 + 1] = new Vector3(0, -9999, 0);
            }
            airMesh.vertices = airV; airMesh.colors = airC; airMesh.bounds = new Bounds(new Vector3(baseX, y, baseZ), new Vector3(100, 260, 100));
            for (int i = 0; i < PART_N; i++)
            {
                if (i >= parts.Count) { partCloud.Hide(i); continue; }
                var p = parts[i]; partCloud.Set(i, p.p, 0.5f, new Color(p.c.r, p.c.g, p.c.b, Mathf.Clamp01(p.life / p.max)));
            }
            partCloud.Apply(new Bounds(camPos, Vector3.one * 500));
        }

        void Siren(GameObject[] s, float hz)
        {
            if (s == null || s.Length < 2) return;
            bool on = Mathf.FloorToInt(t * hz) % 2 == 0; SetVis(s[0], on); SetVis(s[1], !on);
        }
        MaterialPropertyBlock mpb;
        void Tint(GameObject g, Color c)
        {
            var rd = g.GetComponent<Renderer>(); if (rd == null) return;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            mpb.Clear(); mpb.SetColor("_Color", c); mpb.SetColor("_BaseColor", c); mpb.SetColor("_RendererColor", Color.white);
            rd.SetPropertyBlock(mpb);
        }
    }
}
