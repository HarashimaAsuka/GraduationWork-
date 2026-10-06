using UnityEngine;

namespace ShoryoumaTaxi
{
    /// <summary>毎フレームの処理（Web版の update / render に当たる）。</summary>
    public partial class TaxiGame
    {
        void Update()
        {
            frameDt = Mathf.Min(Time.deltaTime, 0.033f);
            if (Screen.width != lastW || Screen.height != lastH) FitField();
            if (GameInput.PausePressed())
            {
                if (mode == Mode.Play) mode = Mode.Pause;
                else if (mode == Mode.Pause) mode = Mode.Play;
            }
            if (mode == Mode.Play) Step(frameDt);
            else if (mode == Mode.Title) Idle(frameDt);
        }

        // ============================================================
        //  ゲームの進行
        // ============================================================
        void Step(float dt)
        {
            t += dt;
            inv = Mathf.Max(0, inv - dt); hurtT = Mathf.Max(0, hurtT - dt);
            webGlow = Mathf.Max(0, webGlow - dt); shake = Mathf.Max(0, shake - dt); cool -= dt;
            if (msg == null || t - msg.Value.t0 > msg.Value.dur)
            {
                if (msgQ.Count > 0) { var m = msgQ.Dequeue(); m.t0 = t; msg = m; } else msg = null;
            }
            UpdateFlying(dt);
            if (over) { overT += dt; if (overT > 1.5f) Finish(false); return; }
            if (clearT > 0)
            {
                clearT += dt; vel *= Mathf.Exp(-2 * dt); yPrev = y; y = Mathf.Clamp(y + vel * dt, 4, L - 4);
                if (clearT > 3.2f) Finish(true);
                return;
            }

            // --- 進む：速さは目標の向きへなめらかに近づく。向きが変わるときは一度止まってから逆へ ---
            float target = want * (want > 0 ? speedUp : speedDown);
            vel += Mathf.Clamp(target - vel, -24 * dt, 24 * dt);
            yPrev = y; y = Mathf.Clamp(y + vel * dt, 4, L - 4);
            int nd = vel > 0.05f ? 1 : vel < -0.05f ? -1 : dir;
            if (nd != dir) Flip(nd);
            bk = Mathf.MoveTowards(bk, want < 0 ? 1 : 0, 0.9f * dt);
            bool go = dir > 0, back = dir < 0;
            float V = Mathf.Abs(vel) + 1;
            step += dt * Mathf.Abs(vel) * (go ? 0.75f : 0.3f);
            if (go && vel > 6 && Mathf.FloorToInt(step / Mathf.PI) != lastStep) { lastStep = Mathf.FloorToInt(step / Mathf.PI); HoofRing(); }
            UpdateAir(dt);
            if (want > 0 && y > L - 30) TurnAround(true);
            if (want < 0 && y < 30) TurnAround(false);

            // --- 移動：画面の右・上に合わせて動かす ---
            Vector2 mv = GameInput.Move();
            float sp = go ? 6.5f : 3.8f;
            Vector3 d3 = (cam.transform.right * mv.x + cam.transform.up * mv.y) * sp * dt;
            px += d3.x; pz += d3.z;
            if (mouseFollow && mv == Vector2.zero)
            {
                var ray = cam.ScreenPointToRay(GameInput.MousePosition());
                var pl = new Plane(Vector3.up, new Vector3(0, y, 0));
                if (pl.Raycast(ray, out float enter))
                {
                    var h = ray.GetPoint(enter);
                    float dx = h.x - px, dz = h.z - pz, d = Mathf.Sqrt(dx * dx + dz * dz), cap = (go ? 9 : 4.8f) * dt;
                    if (d > 0.01f) { float q = Mathf.Min(1, cap / d); px += dx * q; pz += dz * q; }
                }
            }
            KeepIn();

            // --- 糸と塩（塩は上りだけ）---
            bool wantWeb = GameInput.Guard();
            if (silk <= 0) silkLock = true;
            if (silkLock && silk >= 30) silkLock = false;
            guarding = wantWeb && !silkLock;
            silk = Mathf.Clamp(silk + (guarding ? -20 : 28) * dt, 0, 100);
            if (GameInput.Fire() && !guarding && cool <= 0 && dir > 0 && want > 0)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(g.GetComponent<Collider>());
                g.GetComponent<Renderer>().sharedMaterial = ModelFactory.SaltMat; g.transform.localScale = Vector3.one * 0.16f;
                salts.Add(new Salt { go = g, x = px + R(-0.05f, 0.05f), y = y + dir * 0.8f, z = pz + R(-0.05f, 0.05f), yp = y, rot = R(0, 360) });
                fired++; water -= saltCost; cool = 0.14f; sfx.Play(sfx.salt, 0.6f);
            }

            float camY = cam.transform.position.y, gapNow = Mathf.Max(3, y - camY);

            // --- 敵 ---
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
                else { e.x = bx + Mathf.Sin(t * 0.9f + e.ph) * 0.12f; e.z = bz + Mathf.Cos(t * 0.7f + e.ph) * 0.1f; }
                e.hit = Mathf.Max(0, e.hit - dt);
                float d = Rel(e.y), dp = (e.yPrev - yPrev) * dir, inFront = e.y - camY;
                // 手前から来るときは、カメラの前に姿を見せてから攻撃する
                if (inFront < 0) e.emerged = false;
                if (back && !e.emerged && inFront > 1 && d > 0) { e.emerged = true; e.cd = Mathf.Min(e.cd, R(0.25f, 0.6f)); }
                if ((e.shooter || e.ripe) && (back ? (e.emerged && d > 4 && d < gapNow - 2.5f) : (d > 9 && d < 46)))
                {
                    e.cd -= dt;
                    if (e.cd <= 0) { e.cd = (e.type == "tongue" ? 3.2f : 3.0f) * (e.ripe ? 0.62f : 1f) * R(0.85f, 1.15f); Fire(e, d, V); }
                }
                if (dp > 0 && d <= 0)
                {
                    if (!e.bumped && Dist(e.x, e.z) < e.r + 0.55f) { e.bumped = true; Hurt(10); }
                    if (!e.ripe) Ripen(e);   // すれ違った敵は熟れて、次は手強くなる
                }
            }

            // --- 塩 ---
            foreach (var b in salts)
            {
                b.yp = b.y; b.y += dir * (V + 48) * dt; b.rot += dt * 600;
                foreach (var e in enemies)
                {
                    if (!e.alive || b.dead) continue;
                    float a0 = (b.yp - e.y) * dir, a1 = (b.y - e.y) * dir;
                    if (a0 <= 0.6f && a1 >= -0.6f && Hyp(b.x - e.x, b.z - e.z) < e.r + 0.3f)
                    {
                        b.dead = true; e.hp--; e.hit = 0.12f; Burst(new Vector3(b.x, e.y, b.z), Color.white, 4, 2); sfx.Play(sfx.hit, 0.6f);
                        if (e.type == "zakuro" && e.arils != null) e.arils.SetActive(true);
                        if (e.hp <= 0) Kill(e);
                    }
                }
                if (Rel(b.y) > (back ? gapNow - 0.8f : 55)) b.dead = true;
                if (b.dead) Destroy(b.go);
            }
            salts.RemoveAll(b => b.dead);

            // --- 敵の弾 ---
            foreach (var s in shots)
            {
                float dpS = Rel(s.y);
                if (s.onibi)   // 鬼火はゆっくり追ってくる
                {
                    float dx = px - s.x, dz = pz - s.z, dd = Mathf.Max(0.001f, Hyp(dx, dz));
                    s.vx += dx / dd * 4.4f * dt; s.vz += dz / dd * 4.4f * dt;
                    float hv = Hyp(s.vx, s.vz); if (hv > 3.2f) { s.vx *= 3.2f / hv; s.vz *= 3.2f / hv; }
                }
                s.x += s.vx * dt; s.y += s.vy * dt; s.z += s.vz * dt;
                float d = Rel(s.y), dist = Dist(s.x, s.z);
                if (guarding && dpS > -0.4f && d <= 1.5f && dist < WEB_R)
                {
                    s.dead = true; caught++; water = Mathf.Min(100, water + 7); webGlow = 0.3f;
                    Burst(new Vector3(s.x, s.y, s.z), new Color(0.42f, 0.77f, 0.91f), 8, 2.5f); sfx.Play(sfx.catchSilk);
                }
                else if (dpS > 0 && d <= 0 && dist < 0.6f) { s.dead = true; Hurt(s.onibi ? 8 : 7); }
                else if (d < -4) s.dead = true;
                if (s.dead) Destroy(s.go);
            }
            shots.RemoveAll(s => s.dead);

            // --- お客さん：前方の道に置く。乗せている間は行き先までの道に並べる（避ける相手） ---
            soulCD -= dt;
            if (soulCD <= 0 && dir == want)
            {
                float reach = goal != null ? Mathf.Min(Mathf.Abs(goal.y - y) - 15, 260) : 650;
                int ahead = 0; foreach (var s in souls) { float d = Rel(s.y); if (d > 0 && d < reach + 20) ahead++; }
                if (reach > 70 && ahead < 3)
                {
                    float ny = y + dir * R(60, reach);
                    bool clear = true; foreach (var o in souls) if (Mathf.Abs(o.y0 - ny) < 22) clear = false;
                    if (ny > 100 && ny < L - 100 && clear) { SpawnSoul(ny); soulCD = R(1.2f, 2.4f); }
                    else soulCD = 0.4f;
                }
                else soulCD = 0.8f;
            }
            foreach (var s in souls)
            {
                s.yPrev = s.y;
                s.y = s.y0 + Mathf.Sin(t * 1.1f + s.ph) * 0.4f;
                s.x = s.nx * RX + Mathf.Sin(t * 0.7f + s.ph) * 0.6f; s.z = s.nz * RZ + Mathf.Cos(t * 0.6f + s.ph) * 0.4f;
                float d = Rel(s.y), dp = (s.yPrev - yPrev) * dir, inFront = s.y - camY;
                if (inFront < 0) s.emerged = false;
                if (back && !s.emerged && inFront > 1 && d > 0) s.emerged = true;
                if (!flags.Contains("soulSeen") && go && d > 0 && d < 60) { flags.Add("soulSeen"); Say(carrying ? "前に魂。避けよう。" : "前に魂のお客さん。ぶつかれば乗せられる。", 3); }
                if (dp > 0 && d <= 0 && Dist(s.x, s.z) < 1.35f) Board(s);
                else if (Mathf.Abs(s.y - y) > 420) s.gone = true;
            }
            souls.RemoveAll(s => { if (s.gone) Destroy(s.go); return s.gone; });

            // --- 行き先の高さを通過したら到着 ---
            if (goal != null && (yPrev - goal.y) * (y - goal.y) <= 0 && yPrev != y) Deliver();

            // --- 層に初めて入ったとき ---
            string zn = World.ZoneName(y / L);
            if (!flags.Contains("zone" + zn))
            {
                flags.Add("zone" + zn);
                if (t > 1)
                {
                    if (zn == "三途の霧") Say("三途の霧に入った。", 2.6f);
                    else if (zn == "雲海") Say("雲の上へ抜けた。", 2.6f);
                    else if (zn == "夜空") Say("現世の夜空だ。提灯が浮かんでいる。", 2.6f);
                }
            }

            if (water <= 0) { water = 0; over = true; Burst(new Vector3(px, y, pz), new Color(0.6f, 0.56f, 0.4f), 30, 3); }
        }

        void Fire(Enemy e, float d, float V)
        {
            void Aim(float vs, float ox, float oz, bool onibi)
            {
                float time = d / (vs + V), lead = e.ripe ? 0.85f : 0.55f;
                var g = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(g.GetComponent<Collider>());
                g.GetComponent<Renderer>().sharedMaterial = onibi ? ModelFactory.OnibiMat : ModelFactory.SeedMat;
                g.transform.localScale = Vector3.one * (onibi ? 0.7f : 0.4f);
                shots.Add(new Shot { go = g, onibi = onibi, x = e.x, y = e.y, z = e.z,
                    vx = (px + ox - e.x) / time * lead, vy = -dir * vs, vz = (pz + oz - e.z) / time * lead });
            }
            if (e.type == "zakuro") Aim(9, 0, 0, false);
            else if (e.type == "hozuki") Aim(7, 0, 0, true);
            else { Aim(9, 0, 0, false); Aim(9, -2.2f, 0.6f, false); Aim(9, 2.2f, -0.6f, false); }
            if (flags.Add("webTip")) Say("右クリックでクモの糸。攻撃を受けると水分になる。", 3.8f);
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
            while (air.Count < AIR_N)
            {
                float a = R(0, Mathf.PI * 2), r = R(1.5f, 15);
                float ay = backAir ? camY - 4 + (first ? R(0, 70) : R(-2, 2)) : y + R(first ? -5 : 40, 75);
                air.Add(new Vector4(Mathf.Cos(a) * r, ay, Mathf.Sin(a) * r, backAir ? R(3, 7) : -R(6, 12)));
            }
            for (int i = air.Count - 1; i >= 0; i--)
            {
                var p = air[i]; p.y += p.w * dt; air[i] = p;
                bool keep = backAir ? (p.y < y + 70 && p.y > camY - 8) : Rel(p.y) > -8;
                if (!keep) air.RemoveAt(i);
            }
        }

        void HoofRing()
        {
            for (int i = 0; i < hoof.Count; i++)
                if (hoof[i].life <= 0)
                {
                    hoof[i].go.transform.position = new Vector3(px, y - 0.9f, pz);
                    hoof[i].go.SetActive(true);
                    hoof[i] = (hoof[i].go, 1.2f);
                    return;
                }
        }

        void KeepIn() { float d = Hyp(px / RX, pz / RZ); if (d > 1) { px /= d; pz /= d; } }
        float Dist(float x, float z) => Hyp(x - px, z - pz);
        static float Hyp(float a, float b) => Mathf.Sqrt(a * a + b * b);

        /// <summary>タイトル画面の背景：ゆっくり登り続ける。</summary>
        void Idle(float dt)
        {
            t += dt; yPrev = y; y = 30 + Mathf.Repeat(y - 30 + 7 * dt, L * 0.85f);
            step += dt * 8;
            px = Mathf.Sin(t * 0.5f) * 1.2f; pz = 0.6f + Mathf.Cos(t * 0.4f) * 0.6f;
            foreach (var e in enemies) { e.x = e.nx * RX; e.z = e.nz * RZ; }
            UpdateAir(dt);
        }

        // ============================================================
        //  描画（位置・向き・見た目の更新）
        // ============================================================
        void LateUpdate()
        {
            float dt = mode == Mode.Pause ? 0f : frameDt, f = Mathf.Clamp01(y / L);
            float bks = Mathf.SmoothStep(0, 1, bk);

            // 空・霧・環境光を層の色に
            cam.backgroundColor = World.ZoneColor(f, z => z.bg);
            RenderSettings.fogColor = World.ZoneColor(f, z => z.fog);
            RenderSettings.ambientSkyColor = World.ZoneColor(f, z => z.sky) * 0.9f;
            RenderSettings.ambientEquatorColor = World.ZoneColor(f, z => z.sky) * 0.5f;
            RenderSettings.ambientGroundColor = World.ZoneColor(f, z => z.ground);

            // カメラ：常に上を見上げる。上りは馬の後ろ、下りは牛の前（遠め・望遠気味）
            float gap = Mathf.Lerp(camBackUp, camBackDown, bks), lift = Mathf.Lerp(CAM_UP, 4.2f, bks);
            cam.fieldOfView = Mathf.Lerp(fovUp, fovDown, bks);
            RenderSettings.fogStartDistance = gap + 12;
            RenderSettings.fogEndDistance = Mathf.Lerp(95, 150, Mathf.Clamp01((f - 0.55f) * 3));
            float follow = 1 - Mathf.Exp(-7 * dt);
            camFX += (px * CAMF - camFX) * follow; camFZ += (pz * CAMF - camFZ) * follow;
            float sh = shake > 0 ? shake * 0.5f : 0;
            Vector3 camPos = new Vector3(camFX + R(-sh, sh), y - gap, camFZ + lift + R(-sh, sh));
            cam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(Vector3.up, Vector3.forward));
            world.Tick(y, t);

            // 精霊馬：上りは鼻先が奥、下りはこちらを向く。背中は常に画面の上
            bool go = dir > 0;
            float bob = go ? Mathf.Sin(step * 2) * 0.1f : Mathf.Sin(t * 1.6f) * 0.15f;
            var tr = steed.root.transform;
            tr.position = new Vector3(px, y + bob, pz);
            Vector3 F = Vector3.up, U = Vector3.forward;
            var qUp = Quaternion.LookRotation((0.7f * F + 0.71f * U).normalized, (-0.71f * F + 0.7f * U).normalized);
            var qDown = Quaternion.LookRotation((-0.9f * F - 0.42f * U).normalized, (-0.42f * F + 0.9f * U).normalized);
            tr.rotation = Quaternion.Slerp(qUp, qDown, bks);
            if (go && Mathf.Abs(vel) > 3) tr.Rotate(Mathf.Sin(step) * 3.5f, 0, 0, Space.Self);
            else if (!go) tr.Rotate(Mathf.Sin(t * 0.9f) * 6f, 0, Mathf.Sin(t * 1.3f) * 12f, Space.Self);
            if (over) shrink = Mathf.Max(0.35f, shrink - 0.5f * dt);
            tr.localScale = Vector3.one * Mathf.Lerp(1f, 2.3f, bks) * shrink;
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
            bool blink = inv > 0 && Mathf.FloorToInt(t * 16) % 2 == 0;
            if (steed.root.activeSelf == (blink && !over)) steed.root.SetActive(!(blink && !over));
            if (steed.rider.activeSelf)
            {
                steed.rider.transform.rotation = Quaternion.LookRotation(-cam.transform.forward, cam.transform.up);
                steed.rider.transform.localPosition = new Vector3(0, 1.0f + Mathf.Sin(t * 3) * 0.06f, 0.3f);
            }

            // 下りのあいだ、クモが上から糸で吊る
            thread.enabled = bks > 0.5f && mode != Mode.Title;
            if (thread.enabled)
            {
                var spos = steed.spider.transform.position;
                thread.SetPosition(0, spos); thread.SetPosition(1, new Vector3(spos.x * 0.5f, spos.y + 40, spos.z + 4));
            }

            // クモの巣（ガード）
            bool webOn = guarding || webGlow > 0;
            if (web.activeSelf != webOn) web.SetActive(webOn);
            if (webOn)
            {
                web.transform.position = new Vector3(px, y + dir * 1.4f, pz);
                web.transform.localScale = Vector3.one * WEB_R;
                web.transform.rotation = Quaternion.Euler(0, t * 35f, 0);
                Tint(web, webGlow > 0 ? new Color(0.42f, 0.77f, 0.91f) : new Color(0.87f, 0.91f, 0.95f));
            }

            // 敵・お客さん：手前から来るときは小さい状態から育つように現れる
            Quaternion faceCam(Vector3 p) => Quaternion.LookRotation(camPos - p, Vector3.forward);
            float Grow(float inFront) => dir < 0 ? Mathf.SmoothStep(0, 1, Mathf.Clamp01((inFront - 0.6f) / 4f)) : 1f;
            foreach (var e in enemies)
            {
                float d = Rel(e.y), inFront = e.y - camPos.y;
                bool vis = e.alive && d > -12 && inFront > 0.6f && d < 135;
                if (e.go.activeSelf != vis) e.go.SetActive(vis);
                if (!vis) continue;
                var p = new Vector3(e.x, e.y, e.z);
                e.go.transform.SetPositionAndRotation(p, faceCam(p));
                if (e.type == "tongue") e.go.transform.Rotate(0, 0, Mathf.Sin(t * 2 + e.ph) * 5f, Space.Self);
                e.go.transform.localScale = Vector3.one * (e.ripe ? 1.15f : 1f) * (e.hit > 0 ? 1.18f : 1f) * Grow(inFront);
            }
            foreach (var s in souls)
            {
                float d = Rel(s.y), inFront = s.y - camPos.y;
                bool vis = d > -12 && inFront > 0.6f && d < 140;
                if (s.go.activeSelf != vis) s.go.SetActive(vis);
                if (!vis) continue;
                var p = new Vector3(s.x, s.y, s.z);
                s.go.transform.SetPositionAndRotation(p, faceCam(p));
                s.go.transform.localScale = Vector3.one * 1.1f * Grow(inFront);
            }
            foreach (var fl in flying)
            {
                var p = new Vector3(fl.x, fl.y, fl.z);
                fl.go.transform.SetPositionAndRotation(p, faceCam(p) * Quaternion.Euler(0, 0, t * fl.spin));
                fl.go.transform.localScale = Vector3.one * Mathf.Max(0.01f, fl.life / fl.max) * (fl.home ? 0.8f : 0.6f);
            }
            foreach (var b in salts) { b.go.transform.position = new Vector3(b.x, b.y, b.z); b.go.transform.rotation = Quaternion.Euler(b.rot, b.rot * 1.3f, 0); }
            foreach (var s in shots) s.go.transform.position = new Vector3(s.x, s.y, s.z);

            // 行き先ゲート：道いっぱいの大きさに合わせる
            bool gateOn = goal != null;
            if (gate.activeSelf != gateOn) gate.SetActive(gateOn);
            if (gateOn)
            {
                var rad = world.ShaftRadius * 0.92f;
                gate.transform.position = new Vector3(0, goal.y, 0);
                gate.transform.localScale = new Vector3(rad.x / 7.4f, 1, rad.y / 7.4f);
                float gd = goal.y - camPos.y;
                bool labelOn = gd > 1.5f;
                if (gateLabel.gameObject.activeSelf != labelOn) gateLabel.gameObject.SetActive(labelOn);
                gateLabel.transform.SetPositionAndRotation(new Vector3(0, goal.y, rad.y + 1.3f), cam.transform.rotation);
                gateLabel.transform.localScale = Vector3.one * Mathf.Clamp(gd / 18f, 0.6f, 3.2f);
            }
            else if (gateLabel.gameObject.activeSelf) gateLabel.gameObject.SetActive(false);

            // 予兆の輪：カメラの後ろにいるものが、牛のどこを通るか
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
                w.SetActive(true);
                w.transform.SetPositionAndRotation(new Vector3(x, y - 1.2f, z), Quaternion.identity);
                w.transform.localScale = Vector3.one * r * Mathf.Lerp(2.2f, 1.15f, near);
                Tint(w, col * ((0.35f + 0.6f * near) * pulse * fade));
            }
            if (dir < 0 && mode == Mode.Play && !over)
            {
                foreach (var s in souls) Warn(s.x, s.y, s.z, 1.1f, new Color(0.56f, 0.88f, 1f));
                foreach (var e in enemies) if (e.alive) Warn(e.x, e.y, e.z, e.r, e.type == "zakuro" ? new Color(1f, 0.19f, 0.31f) : e.type == "hozuki" ? new Color(1f, 0.6f, 0.19f) : new Color(0.75f, 0.38f, 1f));
            }
            for (; wi < warnRings.Count; wi++) if (warnRings[wi].activeSelf) warnRings[wi].SetActive(false);

            // 照準の輪（上りだけ）。敵が射線に入ると赤
            bool aiming = mode == Mode.Play && !over && clearT == 0 && dir > 0 && want > 0 && vel > 2;
            bool locked = false;
            if (aiming) foreach (var e in enemies) { if (!e.alive) continue; float d = Rel(e.y); if (d > 1 && d < 55 && Dist(e.x, e.z) < e.r + 0.3f) { locked = true; break; } }
            for (int i = 0; i < reticles.Count; i++)
            {
                if (reticles[i].activeSelf != aiming) reticles[i].SetActive(aiming);
                if (!aiming) continue;
                reticles[i].transform.SetPositionAndRotation(new Vector3(px, y + (i == 0 ? 12 : 28), pz), Quaternion.identity);
                Tint(reticles[i], locked ? new Color(1f, 0.25f, 0.38f) : new Color(1f, 1f, 1f, 0.7f));
            }

            // 蹄の輪（上りで一歩ごと）
            for (int i = 0; i < hoof.Count; i++)
            {
                var h = hoof[i];
                if (h.life <= 0) continue;
                float life = h.life - dt; hoof[i] = (h.go, life);
                float k = 1 - life / 1.2f;
                h.go.transform.localScale = Vector3.one * (1 + k * 3);
                Tint(h.go, new Color(0.72f, 1f, 0.56f) * (1 - k));
                if (life <= 0) h.go.SetActive(false);
            }

            // 風の筋：尾は来た方向を指す
            float len = Mathf.Clamp(Mathf.Abs(vel) / 5f, 0.2f, 3.2f);
            Color ac = World.ZoneColor(f, z => z.air);
            for (int i = 0; i < AIR_N; i++)
            {
                if (i < air.Count)
                {
                    var p = air[i];
                    float fade = Mathf.Clamp01(1 - Mathf.Abs(Rel(p.y)) / 70) * (dir < 0 ? 1 : 0.8f);
                    airV[i * 2] = new Vector3(p.x, p.y, p.z); airV[i * 2 + 1] = new Vector3(p.x, p.y + dir * len, p.z);
                    airC[i * 2] = ac * fade; airC[i * 2 + 1] = new Color(0, 0, 0, 0);
                }
                else { airV[i * 2] = airV[i * 2 + 1] = new Vector3(0, -9999, 0); }
            }
            airMesh.vertices = airV; airMesh.colors = airC;
            airMesh.bounds = new Bounds(new Vector3(0, y, 0), new Vector3(60, 200, 60));

            // 火花
            foreach (var s in sparks)
            {
                if (s.life <= 0) continue;
                s.life -= dt; s.p += s.v * dt; s.v *= Mathf.Pow(0.08f, dt);
                if (s.life <= 0) { s.tr.gameObject.SetActive(false); continue; }
                float a = s.life / s.max;
                s.tr.position = s.p; s.tr.localScale = Vector3.one * 0.12f * a;
                mpb.SetColor("_BaseColor", s.c); mpb.SetColor("_Color", s.c); s.rd.SetPropertyBlock(mpb);
            }
        }

        void Tint(GameObject g, Color c)
        {
            var rd = g.GetComponent<Renderer>();
            mpb.SetColor("_Color", c); mpb.SetColor("_BaseColor", c); mpb.SetColor("_RendererColor", c);
            rd.SetPropertyBlock(mpb);
        }
    }
}
