using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;   // System.Random と取り違えないように

namespace ShoryoumaTaxi
{
    /// <summary>毎フレームの進行（Web版 v32 の update に当たる）。見た目は TaxiGame.Render.cs。</summary>
    public partial class TaxiGame
    {
        void Update()
        {
            frameDt = Mathf.Min(Time.deltaTime, 0.033f);
            if (Screen.width != lastW || Screen.height != lastH) FitField();
            if (GameInput.MutePressed() && mode != Mode.Name) bgm.Toggle();
            if (GameInput.PausePressed())
            {
                if (mode == Mode.Play) Pause();
                else if (mode == Mode.Pause) Resume();
            }
            if (mode == Mode.Play) Step(frameDt);
            else if (mode == Mode.Title) Idle(frameDt);
            bgm.Tick(Time.unscaledDeltaTime);
        }

        void Resume() { mode = Mode.Play; cool = Mathf.Max(cool, 0.35f); bgm.Pause(false); }

        // 見た目の層の色（ステージ3は夢の色）
        Color ZoneLook(float f, System.Func<World.Zone, Color> pick) => stage == 3 ? World.ZoneColorOf(World.ZonesM, f, pick) : World.ZoneColor(f, pick);

        // ============================================================
        //  ゲームの進行
        // ============================================================
        void Step(float dt)
        {
            t += dt;
            inv = Mathf.Max(0, inv - dt); hurtT = Mathf.Max(0, hurtT - dt); fineFlash = Mathf.Max(0, fineFlash - dt);
            shake = Mathf.Max(0, shake - dt); cool -= dt; wind = Mathf.Max(0, wind - dt);
            UpdateMsg();
            UpdateParts(dt); UpdateFlying(dt);
            if (over) { overT += dt; if (overT > overDelay) Finish(false); return; }
            if (clearT > 0)
            {
                clearT += dt; vel *= Mathf.Exp(-2 * dt); yPrev = y; y = Mathf.Clamp(y + vel * dt, 4, L - 4);
                if (clearT > 3.2f) Finish(true);
                return;
            }

            // --- 制限時間 ---
            timeLeft -= dt;
            if (timeLeft <= 0)
            {
                timeLeft = 0; timeUp = true; over = true; overDelay = 1.5f; sfx.Play(sfx.bell);
                Say("時間切れ。", 2.4f);
                return;
            }
            if (timeLeft < 20 && flags.Add("timeWarn")) Say("残り20秒！", 2f);
            bgm.SetPanic(timeLeft < 30 || water < 25 || (boss != null && boss.phase == 3));

            // --- 進む：乗せるほど遅い。追い風でぐんと速く（v28） ---
            float load = (1 - SLOW_PER * riders.Count) * (st.boss ? 0.55f : 1f), boost = wind > 0 ? 2.8f : 1f;
            float target = want * (want > 0 ? V_GO : V_BACK) * load * boost;
            vel += Mathf.Clamp(target - vel, -24 * dt, 24 * dt);
            yPrev = y; y = Mathf.Clamp(y + vel * dt, 4, L - 4);
            int nd = vel > 0.05f ? 1 : vel < -0.05f ? -1 : dir;
            if (nd != dir) Flip(nd);
            bk = Mathf.MoveTowards(bk, want < 0 ? 1 : 0, 0.9f * dt);
            bool go = dir > 0, back = dir < 0;
            float V = Mathf.Abs(vel) + 1;
            step += dt * Mathf.Abs(vel) * (go ? 0.75f : 0.3f);
            UpdateAir(dt);
            if (wind > 0 && Random.value < 0.8f)
                AddPart(WP(px + R(-0.4f, 0.4f), y - dir * 0.8f, pz + R(-0.4f, 0.4f)), new Vector3(R(-1, 1), -dir * R(4, 8), R(-1, 1)), R(0.3f, 0.6f), new Color(0.6f, 0.9f, 0.55f));
            // v22：親玉戦では、後ろ向きは4秒で終わる（精霊馬が踏んばって向き直る）
            if (st.boss && want < 0)
            {
                backT += dt;
                if (backT > 4) { want = 1; backT = 0; Say("精霊馬が踏んばって、前に向き直った！", 1.8f); }
            }
            else backT = 0;
            // 道の端
            if (st.boss)
            {
                // 親玉戦：道の終わり（現世の出口・地獄の底）に着いたら負け
                float toEnd = want > 0 ? L - 30 - y : y - 30;
                foreach (int k in new[] { 200, 80 })
                    if (toEnd < k && flags.Add("endWarn" + k + want))
                        Say(want > 0 ? $"現世の出口まで あと{Mathf.RoundToInt(toEnd / 10)}丈！　出てしまうと親玉を取り逃がす！" : $"地獄の底まで あと{Mathf.RoundToInt(toEnd / 10)}丈！", 2.4f);
                if (y > L - 30 || y < 30)
                {
                    endOut = y > L - 30 ? "top" : "bottom"; over = true; overDelay = 1.6f; sfx.Play(sfx.bell);
                    Say(endOut == "top" ? "現世の出口に着いてしまった……親玉が現世へ抜け出した！" : "地獄の底に落ちてしまった……", 3f);
                    return;
                }
            }
            else
            {
                if (want > 0 && y > L - 30) TurnAround(true);
                if (want < 0 && y < 30) TurnAround(false);
            }

            // --- 移動：画面の右・上に合わせて動かす ---
            Vector2 mv = GameInput.Move();
            float sp = go ? 6.5f : 4.4f;
            Vector3 cr = cam.transform.right, cu = cam.transform.up;
            var rgt = new Vector2(cr.x, cr.z).normalized; var upv = new Vector2(cu.x, cu.z).normalized;
            px += (rgt.x * mv.x + upv.x * mv.y) * sp * dt; pz += (rgt.y * mv.x + upv.y * mv.y) * sp * dt;
            if (mouseFollow && mv == Vector2.zero)
            {
                var ray = cam.ScreenPointToRay(GameInput.MousePosition());
                var pl = new Plane(Vector3.up, new Vector3(0, y, 0));
                if (pl.Raycast(ray, out float enter))
                {
                    var h = ray.GetPoint(enter);
                    float dx = h.x - Road.Cx(y) - px, dz = h.z - Road.Cz(y) - pz, d = Mathf.Sqrt(dx * dx + dz * dz), cap = (go ? 9 : 5.5f) * dt;
                    if (d > 0.01f) { float q = Mathf.Min(1, cap / d); px += dx * q; pz += dz * q; }
                }
            }
            KeepIn();

            // --- 塩：いつも画面の奥（道の先）へ投げる。v32：まっすぐ飛ぶ（ねらい補正なし） ---
            if (GameInput.Fire() && cool <= 0)
            {
                float vy = dir > 0 ? V + 48 : 48;
                AddSalt(px + R(-0.05f, 0.05f), pz + R(-0.05f, 0.05f), vy);
                fired++; water -= SaltCostNow; cool = 0.14f; sfx.Play(sfx.salt, 0.8f);
            }

            float camY = cam.transform.position.y, gapNow = Mathf.Max(3, y - camY);

            // --- 道に置いてある敵 ---
            foreach (var e in enemies)
            {
                if (!e.alive)
                {
                    e.deadT += dt;
                    if (e.deadT > 40 && Mathf.Abs(e.y0 - y) > 110) Revive(e);   // しばらくすると実り直す
                    continue;
                }
                float bx = e.nx * RX, bz = e.nz * RZ;
                e.yPrev = e.y;
                if (e.type == "hozuki") { e.x = bx + Mathf.Cos(t * 0.9f + e.ph) * 1.3f; e.z = bz + Mathf.Sin(t * 0.9f + e.ph) * 1.0f; e.y = e.y0 + Mathf.Sin(t * 1.3f + e.ph) * 0.5f; }
                else if (e.type == "kasa") { float hop = t * 1.3f + e.ph; e.x = bx * 0.4f + Mathf.Sin(hop) * RX * 0.6f; e.z = bz + Mathf.Abs(Mathf.Sin(hop * 2)) * 1.3f - 0.5f; }
                else if (e.type == "chochin") { e.x = bx + Mathf.Sin(t * 0.6f + e.ph) * 0.8f; e.z = bz + Mathf.Sin(t * 1.1f + e.ph) * 0.4f; e.y = e.y0 + Mathf.Sin(t * 0.8f + e.ph) * 0.8f; }
                else { e.x = bx + Mathf.Sin(t * 0.9f + e.ph) * 0.12f; e.z = bz + Mathf.Cos(t * 0.7f + e.ph) * 0.1f; }
                e.hit = Mathf.Max(0, e.hit - dt);
                float d = Rel(e.y), dp = (e.yPrev - yPrev) * dir, inFront = e.y - camY;
                // v28・v29：後ろ向きのとき、カメラの後ろ（手前）から来た敵は、カメラの前に飛び出してから攻撃する
                if (inFront < 0) e.emerged = false;
                if (back && !e.emerged && inFront > 0.6f && d > 0)
                {
                    e.emerged = true; e.emT = t; e.cd = Mathf.Min(e.cd, R(0.35f, 0.7f));
                    Burst(e.x, e.y, e.z, WarnColor(e.type), 26, 4); Burst(e.x, e.y, e.z, Color.white, 10, 3); sfx.Play(sfx.emerge, 0.7f);
                }
                if ((e.shooter || e.ripe) && (back ? (e.emerged && d > 4 && d < gapNow - 2.5f) : (d > 9 && d < 46)))
                {
                    e.cd -= dt;
                    if (e.cd <= 0)
                    {
                        e.cd = (e.type == "tongue" ? 3.3f : e.type == "kasa" ? 2.8f : e.type == "chochin" ? 3.8f : 3.0f) * (e.ripe ? 0.7f : 1f) * R(0.85f, 1.15f);
                        Fire(e, d, V);
                    }
                }
                if (dp > 0 && d <= 0)
                {
                    if (!e.bumped && Dist(e.x, e.z) < e.r + 0.55f) { e.bumped = true; Bump(); }
                    if (!e.ripe) Ripen(e);   // すれ違った敵は熟れて、次は手強くなる
                }
            }

            UpdateMovers(dt);
            if (st.boss) UpdateBoss(dt);

            // --- 塩の当たり ---
            foreach (var b in salts)
            {
                b.yp = b.y; b.y += b.vy * dt; b.rot += dt * 10;
                float sd = Mathf.Sign(b.vy);
                bool HitTest(float ox, float oz, float r, float yy)
                {
                    float a0 = (b.yp - yy) * sd, a1 = (b.y - yy) * sd;
                    return a0 <= 0.6f && a1 >= -0.6f && Hyp(b.x - ox, b.z - oz) < r + 0.3f;
                }
                foreach (var c in chasers)
                {
                    if (c.dead || b.dead || !HitTest(c.x, c.z, c.r, c.y)) continue;
                    b.dead = true; c.hp--; c.hit = 0.12f; Burst(b.x, c.y, b.z, Color.white, 5, 2); sfx.Play(sfx.hit, 0.7f);
                    if (c.hp <= 0) { c.dead = true; kills++; Burst(c.x, c.y, c.z, KCol(c.type), 26, 4); Burst(c.x, c.y, c.z, Color.white, 12, 3); sfx.Play(sfx.pop); }
                }
                foreach (var mv2 in movers)
                {
                    if (mv2.dead || b.dead || mv2.t < 0 || !HitTest(mv2.x, mv2.z, mv2.r, mv2.y)) continue;
                    b.dead = true; mv2.hp--; mv2.hit = 0.12f; Burst(b.x, mv2.y, b.z, Color.white, 5, 2); sfx.Play(sfx.hit, 0.7f);
                    if (mv2.type == "zakuro" && mv2.arils != null) mv2.arils.SetActive(true);
                    if (mv2.hp <= 0) MoverKilled(mv2);
                }
                foreach (var e in enemies)
                {
                    if (!e.alive || b.dead || !HitTest(e.x, e.z, e.r, e.y)) continue;
                    b.dead = true; e.hp--; e.hit = 0.12f; Burst(b.x, e.y, b.z, Color.white, 5, 2); sfx.Play(sfx.hit, 0.7f);
                    if (e.type == "zakuro" && e.arils != null) e.arils.SetActive(true);
                    if (e.hp <= 0) Kill(e);
                }
                if (boss != null && boss.guards != null && !boss.dead)
                    foreach (var gd in boss.guards)
                    {
                        if (gd.dead || b.dead || !HitTest(gd.x, gd.z, 1.7f, gd.y)) continue;
                        b.dead = true; gd.hp--; gd.hit = 0.12f; Burst(b.x, gd.y, b.z, Color.white, 5, 2); sfx.Play(sfx.hit, 0.7f);
                        if (gd.hp <= 0)
                        {
                            gd.dead = true; kills++; water = Mathf.Min(100, water + 3); Burst(gd.x, gd.y, gd.z, 0xff8a30, 24, 4); sfx.Play(sfx.pop);
                            bool all = true; foreach (var g2 in boss.guards) if (!g2.dead) all = false;
                            if (all) Say("守りが消えた！　今のうちに親玉を祓え！", 2f);
                        }
                    }
                if (boss != null && !boss.dead && boss.intro >= 3.4f)
                    foreach (var h in boss.hands)
                    {
                        if (h.dead || b.dead || !HitTest(h.x, h.z, 2.2f, h.y)) continue;
                        b.dead = true; h.hp--; h.hit = 0.12f; Burst(b.x, h.y, b.z, Color.white, 5, 2); sfx.Play(sfx.hit, 0.7f);
                        if (h.hp <= 0)
                        {
                            h.dead = true; h.mode = "idle"; kills++; water = Mathf.Min(100, water + 5);
                            for (int i = 0; i < 3; i++) Burst(h.x + R(-1, 1), h.y, h.z + R(-1, 1), 0xff5a3a, 26, 5);
                            StunBoss(boss, 3, "鬼の手を祓った！　親玉がひるんでいる。光る核を狙え！");
                        }
                    }
                if (boss != null && !boss.dead && boss.intro >= 3.4f && !b.dead && HitTest(boss.x, boss.z, 3.6f, boss.y))
                {
                    b.dead = true;
                    bool weak = boss.stun > 0;
                    if (boss.shield && !weak)
                    {
                        Burst(b.x, boss.y - 3, b.z, 0x80c8ff, 6, 2.5f);
                        if (flags.Add("shieldTip")) Say("鬼灯の守りが塩をはじく！　先に守りを祓おう。", 2.4f);
                    }
                    else if (weak) { Burst(b.x, boss.y - 2, b.z, 0xffd040, 14, 4); Burst(b.x, boss.y - 2, b.z, Color.white, 6, 3); sfx.Play(sfx.hit, 0.8f); BossHit(2); }
                    else { Burst(b.x, boss.y - 2, b.z, Color.white, 6, 2.5f); sfx.Play(sfx.hit, 0.7f); BossHit(); }
                }
                if (b.y - y > 60) b.dead = true;
                if (b.dead) Destroy(b.go);
            }
            salts.RemoveAll(b => b.dead);

            // --- 後ろ向き：道の奥から飛んでくる敵と、後ろから並んでついてくる敵の群れ ---
            if (back && clearT == 0 && !st.boss)
            {
                chaseCD -= dt; escCD -= dt;
                if (chaseCD <= 0 && chasers.Count < 6 && y > 60) { SpawnChaser(); chaseCD = R(1.1f, 1.8f); }
                if (escCD <= 0 && y > 60)
                {
                    escCD = R(5.5f, 8f);
                    string[] pool = { "zakuro", "hozuki", "kasa", "zakuro", "kasa" };
                    SpawnWave("b" + Mathf.FloorToInt(t * 10), "escort", pool[Mathf.Min(4, Mathf.FloorToInt(R(0, 5)))], stage == 1 ? 2 : 3);
                }
            }
            foreach (var c in chasers)
            {
                c.yPrev = c.y; c.hit = Mathf.Max(0, c.hit - dt);
                if (c.far)
                {
                    // 奥から：速く近づきながら、景色の外側から道の中へ弧を描いて入る
                    c.y += (vel - 30) * dt;
                    float above0 = c.y - y, k = Mathf.Clamp01((above0 - FAR_IN) / (c.start - FAR_IN)), e = Smooth(k);
                    c.x = Mathf.Lerp(c.tx, c.sx, e) + Mathf.Sin(k * 9 + c.ph) * c.weave * e;
                    c.z = Mathf.Lerp(c.tz, c.sz, e) + Mathf.Cos(k * 7 + c.ph) * c.weave * 0.6f * e;
                    if (above0 <= FAR_IN) c.far = false;
                }
                else
                {
                    c.y += (vel - c.close) * dt;
                    float toX = px - c.x, toZ = pz - c.z, dl = Mathf.Max(0.0001f, Hyp(toX, toZ)), steer = 1.2f * dt;
                    c.x += toX / dl * Mathf.Min(dl, steer); c.z += toZ / dl * Mathf.Min(dl, steer);
                    c.x += Mathf.Sin(t * 2.2f + c.ph) * 1.6f * dt;
                    c.z += Mathf.Cos(t * 1.7f + c.ph) * 1.0f * dt;
                }
                c.histT -= dt;
                if (c.histT <= 0) { c.histT = 0.06f; c.hist.Insert(0, new Vector3(c.x, c.y, c.z)); if (c.hist.Count > TRAIL_N) c.hist.RemoveAt(c.hist.Count - 1); }
                float above = c.y - y, abovePrev = c.yPrev - yPrev;
                c.cd -= dt;
                if (!c.far && c.cd <= 0 && above > 8 && above < 40)
                {
                    c.cd = (c.type == "hozuki" ? 3.0f : c.type == "chochin" ? 3.6f : c.type == "kasa" ? 2.8f : 2.6f) * R(0.85f, 1.15f);
                    float vs = c.close + 12;
                    Volley(c.type, ref c.vol, false, c.x, c.y, c.z, above / vs, vel - vs, true, 1);
                }
                if (abovePrev > 0 && above <= 0 && !c.bumped && Dist(c.x, c.z) < c.r + 0.55f) { c.bumped = true; Bump(); }
                if (above < -12) c.dead = true;
            }
            chasers.RemoveAll(c => { if (c.dead) { Destroy(c.go); foreach (var s in c.trail) Destroy(s); } return c.dead; });

            // --- 敵の弾 ---
            foreach (var s in shots)
            {
                if (s.onibi)   // 鬼火はゆっくり追ってくる
                {
                    float dx = px - s.x, dz = pz - s.z, dd = Mathf.Max(0.001f, Hyp(dx, dz));
                    s.vx += dx / dd * 4.4f * dt; s.vz += dz / dd * 4.4f * dt;
                    float hv = Hyp(s.vx, s.vz); if (hv > 3.2f) { s.vx *= 3.2f / hv; s.vz *= 3.2f / hv; }
                }
                // 奥から落ちてくる弾は、馬より上にあるかどうかで判定する
                float before = s.chase ? s.y - y : Rel(s.y);
                s.x += s.vx * dt; s.y += s.vy * dt; s.z += s.vz * dt;
                float d = s.chase ? s.y - y : Rel(s.y), dist = Dist(s.x, s.z);
                if (before > 0 && d <= 0 && dist < 0.6f) { s.dead = true; HurtShot(ShotDmg(s.type)); }
                else if (d < -4) s.dead = true;
                if (s.dead) Destroy(s.go);
            }
            shots.RemoveAll(s => s.dead);

            // --- 敵の波：前向きに進んで、その高さに着いたら出てくる ---
            if (go)
                for (int i = 0; i < st.waves.Length; i++)
                {
                    var w = st.waves[i];
                    if (waveDone[i] || y < w.y) continue;
                    waveDone[i] = true; SpawnWave("w" + i, w.pattern, w.type, w.n);
                }

            // --- 魂のお客さん：あらかじめ置いてあり、それぞれ行き先が決まっている ---
            bool full = riders.Count >= MAX_RIDERS;
            foreach (var s in souls)
            {
                if (s.gone) continue;
                s.yPrev = s.y;
                s.y = s.y0 + Mathf.Sin(t * 1.1f + s.ph) * 0.4f;
                s.x = s.nx * RX + Mathf.Sin(t * 0.7f + s.ph) * 0.6f; s.z = s.nz * RZ + Mathf.Cos(t * 0.6f + s.ph) * 0.4f;
                float d = Rel(s.y), dp = (s.yPrev - yPrev) * dir, inFront = s.y - camY;
                if (inFront < 0) s.emerged = false;
                if (back && !s.emerged && inFront > 0.6f && d > 0) { s.emerged = true; s.emT = t; Burst(s.x, s.y, s.z, 0x9fe4ff, 20, 3); sfx.Play(sfx.soulPop, 0.7f); }
                if (go && d > 0 && d < 70 && flags.Add("soulSeen")) Say("魂のお客さん。上の札が行き先と運賃。ぶつかれば乗せられる。", 3.2f);
                if (dp > 0 && d <= 0 && Dist(s.x, s.z) < 1.5f)
                {
                    Board(s);
                    if (full && flags.Add("fullTip")) Say("満席で乗せると、いちばん前のお客さんが押し出される。", 3f);
                }
            }

            // --- アイテム ---
            foreach (var it in items)
            {
                if (it.taken) continue;
                it.x = it.nx * RX + Mathf.Sin(t * 0.8f + it.ph) * 0.4f; it.z = it.nz * RZ + Mathf.Cos(t * 0.7f + it.ph) * 0.3f;
                if ((it.y - yPrev) * (it.y - y) <= 0 && yPrev != y && Dist(it.x, it.z) < 1.7f) TakeItem(it);
            }

            // --- 行き先の高さを通ったら降りる ---
            foreach (var r in riders.ToArray())
                if ((yPrev - r.y) * (y - r.y) <= 0 && yPrev != y) Deliver(r);

            // --- 層に初めて入ったとき ---
            string zn = World.ZoneName(Road.Zf(y));
            if (!st.boss && flags.Add("zone" + zn))
            {
                string tx = Road.Flip
                    ? (zn == "雲海" ? "雲の中へ下りてきた。" : zn == "三途の霧" ? "三途の霧に入った。もうすぐ地獄だ。" : zn == "地獄" ? "地獄に入った。赤い岩の間を抜けていく。" : null)
                    : (zn == "三途の霧" ? "三途の霧に入った。" : zn == "雲海" ? "雲の上へ抜けた。" : zn == "夜空" ? "現世の夜空だ。提灯が浮かんでいる。" : null);
                if (tx != null && t > 1) Say(tx, 2.4f);
            }

            if (water <= 0)
            {
                water = 0; over = true; overDelay = 1.5f;
                Burst(px, y, pz, 0x9a8f68, 40, 3);
            }
        }

        void AddSalt(float x, float z, float vy)
        {
            var g = new GameObject("salt"); g.transform.SetParent(fx, false);
            var c = ModelFactory.MeshObj("crystal", SaltMesh, g.transform, ModelFactory.SaltMat, Vector3.zero, Vector3.one);
            ModelFactory.GlowFlat(g.transform, 0xbfe8ff, 1.5f, 0.95f, Vector3.zero);
            for (int i = 1; i <= 3; i++) ModelFactory.GlowFlat(g.transform, 0x9fd8ff, 1.1f - i * 0.22f, 0.7f - i * 0.17f, new Vector3(0, -i * 0.55f, 0));
            salts.Add(new Salt { go = g, crystal = c.transform, x = x, y = y + 0.8f, z = z, yp = y, vy = vy, rot = R(0, 6) });
        }
        static Mesh saltMesh;
        // v31：白く光る結晶（正八面体）
        static Mesh SaltMesh => saltMesh != null ? saltMesh : (saltMesh = MeshKit.Lathe(new[] { new Vector2(0, -0.22f), new Vector2(0.22f, 0), new Vector2(0, 0.22f) }, 4));

        void UpdateParts(float dt)
        {
            float damp = Mathf.Pow(0.96f, dt * 60f);
            foreach (var p in parts) { p.p += p.v * dt; p.v *= damp; p.life -= dt; }
            parts.RemoveAll(p => p.life <= 0);
        }

        void UpdateFlying(float dt)
        {
            foreach (var f in flying)
            {
                f.x += f.vx * dt; f.y += f.vy * dt; f.z += f.vz * dt; f.life -= dt;
                if (f.home) f.vy += 6 * dt;
                if (f.life <= 0) Destroy(f.go);
            }
            flying.RemoveAll(f => f.life <= 0);
        }

        void UpdateAir(float dt)
        {
            bool backAir = dir < 0;
            float camY = cam.transform.position.y;
            bool first = air.Count == 0;
            var col = ZoneLook(Road.Zf(y), z => z.air);
            while (air.Count < AIR_N)
            {
                float a = R(0, Mathf.PI * 2), r = R(1.5f, 15);
                float ay = backAir ? camY - 4 + (first ? R(0, 70) : R(-2, 2)) : y + R(first ? -5 : 40, 75);
                air.Add(new AirP { x = Mathf.Cos(a) * r, y = ay, z = Mathf.Sin(a) * r, vy = backAir ? R(3, 7) : -R(6, 12), c = col });
            }
            for (int i = air.Count - 1; i >= 0; i--)
            {
                var p = air[i]; p.y += p.vy * dt; air[i] = p;
                bool keep = backAir ? (p.y < y + 70 && p.y > camY - 8) : Rel(p.y) > -8;
                if (!keep) air.RemoveAt(i);
            }
        }

        void KeepIn() { float d = Hyp(px / RX, pz / RZ); if (d > 1) { px /= d; pz /= d; } }

        /// <summary>タイトル画面の背景：ゆっくり登り続ける。</summary>
        void Idle(float dt)
        {
            t += dt; yPrev = y; y = 30 + Mathf.Repeat(y - 30 + 9 * dt, L * 0.85f);
            step += dt * 8;
            px = Mathf.Sin(t * 0.5f) * 1.2f; pz = 0.6f + Mathf.Cos(t * 0.4f) * 0.6f;
            foreach (var e in enemies) { e.x = e.nx * RX; e.z = e.nz * RZ; }
            foreach (var s in souls) { s.x = s.nx * RX; s.z = s.nz * RZ; }
            foreach (var it in items) { it.x = it.nx * RX; it.z = it.nz * RZ; }
            UpdateAir(dt); UpdateParts(dt);
        }

        // ============================================================
        //  バックミラー：ミラーのカメラで描くときだけ、カメラの後ろ（手前）にいる敵と魂を本当の位置に出す
        // ============================================================
        readonly List<(GameObject go, bool active, Vector3 pos, Quaternion rot, Vector3 scale)> mirrorSaved = new List<(GameObject, bool, Vector3, Quaternion, Vector3)>();

        void OnBeginCamera(ScriptableRenderContext ctx, Camera c)
        {
            if (c != mirrorCam || mirrorCam == null) return;
            mirrorSaved.Clear();
            void Reveal(GameObject g, float x, float yy, float z, float s)
            {
                float behind = camPos.y - yy;
                if (behind < -1 || behind > 80) return;
                var tr = g.transform;
                mirrorSaved.Add((g, g.activeSelf, tr.position, tr.rotation, tr.localScale));
                var p = WP(x, yy, z);
                g.SetActive(true); tr.SetPositionAndRotation(p, Quaternion.LookRotation(camPos - p, Vector3.forward)); tr.localScale = Vector3.one * s;
            }
            foreach (var e in enemies) if (e.alive) Reveal(e.go, e.x, e.y, e.z, e.ripe ? 1.15f : 1f);
            foreach (var s in souls) if (!s.gone) Reveal(s.go, s.x, s.y, s.z, 1.1f);
        }

        void OnEndCamera(ScriptableRenderContext ctx, Camera c)
        {
            if (c != mirrorCam || mirrorCam == null) return;
            foreach (var m in mirrorSaved)
            {
                if (m.go == null) continue;
                m.go.transform.SetPositionAndRotation(m.pos, m.rot); m.go.transform.localScale = m.scale;
                m.go.SetActive(m.active);
            }
            mirrorSaved.Clear();
        }
    }
}
