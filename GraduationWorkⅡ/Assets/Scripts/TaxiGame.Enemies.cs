using UnityEngine;
using Random = UnityEngine.Random;   // System.Random と取り違えないように

namespace ShoryoumaTaxi
{
    /// <summary>
    /// 敵の弾と撃ち方・敵の波・奥から飛んでくる敵（Web版 v32 の emit / volley / spawnWave / updateMovers / spawnChaser）。
    /// </summary>
    public partial class TaxiGame
    {
        const int TRAIL_N = 10; const float FAR_IN = 40f;
        static float ShotDmg(string type) => type == "seed" ? 7 : type == "onibi" ? 8 : type == "drop" ? 5 : 6;

        /// <summary>弾を1つ出す。x,z は道の中心からのずれ。chase = 上（奥）から落ちてくる弾。</summary>
        void Emit(string type, float x, float yy, float z, float vx, float vy, float vz, bool chase)
        {
            GameObject g;
            if (type == "onibi")
            {
                g = new GameObject("onibi");
                ModelFactory.GlowQ(g.transform, 0x80c8ff, 1.6f, 0.95f, Vector3.zero);
                ModelFactory.GlowQ(g.transform, 0xffffff, 0.6f, 1f, new Vector3(0, 0, -0.01f));
            }
            else if (type == "fire")
            {
                g = new GameObject("fire");
                ModelFactory.GlowQ(g.transform, 0xff8a30, 1.5f, 0.95f, Vector3.zero);
                ModelFactory.GlowQ(g.transform, 0xfff0c0, 0.55f, 1f, new Vector3(0, 0, -0.01f));
            }
            else
            {
                g = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(g.GetComponent<Collider>());
                if (type == "drop") { g.GetComponent<Renderer>().sharedMaterial = ModelFactory.StdE(0x8fd0ff, 0x2a6aa0, 0.9f, 0.9f); g.transform.localScale = new Vector3(0.36f, 0.36f, 0.6f); }
                else { g.GetComponent<Renderer>().sharedMaterial = ModelFactory.SeedMat; g.transform.localScale = Vector3.one * 0.4f; }
            }
            g.transform.SetParent(fx, false);
            shots.Add(new Shot { go = g, type = type, x = x, y = yy, z = z, vx = vx, vy = vy, vz = vz, chase = chase });
        }

        /// <summary>
        /// 敵ごとの撃ち方。time = 届くまでの秒数、vy = 弾の縦の速さ、up = 「奥」の向き（遅れて届く弾をずらす向き）。
        /// vol は撃った回数（3回に1回の3連射などに使う）。
        /// </summary>
        void Volley(string type, ref int vol, bool ripe, float sx, float sy, float sz, float time, float vy, bool chase, float up)
        {
            int n = ++vol;
            float tt = Mathf.Max(0.05f, time);
            void Shot1(string ty, float ox, float oz, float lead, float dy = 0)
                => Emit(ty, sx, sy + up * dy, sz, (px + ox - sx) / tt * lead, vy, (pz + oz - sz) / tt * lead, chase);
            switch (type)
            {
                case "zakuro":   // ふつうは1粒。3回に1回（熟れたらいつも）3連射
                    if (ripe || n % 3 == 0) { Shot1("seed", 0, 0, 0.85f); Shot1("seed", 0, 0, 0.85f, 1.8f); Shot1("seed", 0, 0, 0.85f, 3.6f); }
                    else Shot1("seed", 0, 0, 0.6f);
                    break;
                case "hozuki":   // 追ってくる鬼火。熟れると2つ
                    Shot1("onibi", 0, 0, 0.5f); if (ripe) Shot1("onibi", n % 2 == 1 ? 1.8f : -1.8f, 0.6f, 0.5f, 1.5f);
                    break;
                case "tongue":   // 3方向と5方向を交互に
                    if (n % 2 == 1) for (int i = -2; i <= 2; i++) Shot1("seed", i * 1.8f, Mathf.Abs(i) * 0.3f, 1);
                    else { Shot1("seed", 0, 0, 0.6f); Shot1("seed", -2.2f, 0.6f, 0.6f); Shot1("seed", 2.2f, -0.6f, 0.6f); }
                    break;
                case "kasa":     // 雨粒を横一列。1か所だけすき間
                {
                    const int cols = 7; int gap = Mathf.Min(cols - 1, Mathf.FloorToInt(R(0, cols)));
                    for (int i = 0; i < cols; i++) if (i != gap) Emit("drop", sx, sy, sz, (px + (i - 3) * 1.3f - sx) / tt, vy, (pz - sz) / tt, chase);
                    break;
                }
                case "chochin":  // 火の輪：広がりながら来る
                {
                    int k = ripe ? 10 : 8; float rot = n * 0.4f, Rr = 2.3f / tt;
                    for (int i = 0; i < k; i++)
                    {
                        float a = rot + i / (float)k * Mathf.PI * 2;
                        Emit("fire", sx, sy, sz, Mathf.Cos(a) * Rr + (px - sx) / tt * 0.7f, vy, Mathf.Sin(a) * Rr * 0.8f + (pz - sz) / tt * 0.7f, chase);
                    }
                    break;
                }
                default: Shot1("seed", 0, 0, 0.6f); break;
            }
        }

        /// <summary>道に置いてある敵が撃つ。</summary>
        void Fire(Enemy e, float d, float V)
        {
            float vs = e.type == "hozuki" ? 7 : e.type == "chochin" ? 6 : 9, time = Mathf.Max(0.4f, d / (vs + V));
            Volley(e.type, ref e.vol, e.ripe, e.x, e.y, e.z, time, -dir * vs, false, dir);
        }

        void MoverShoot(Mover m)
        {
            float above = m.y - y;
            if (above < 6) return;
            float vs = Mathf.Max(10, Mathf.Abs(vel) + 9);
            Volley(m.type, ref m.vol, false, m.x, m.y, m.z, above / vs, vel - vs, true, 1);
        }

        // ---- 敵の出方：並走（後ろから追いついて前に出る）・横切り（画面の外から編隊で）・降下（奥から迫る） ----
        void SpawnWave(string id, string pattern, string type, int n)
        {
            int side = Random.value < 0.5f ? -1 : 1;
            waveLeft[id] = n;
            for (int i = 0; i < n; i++)
            {
                var m = new Mover { type = type, wave = id, pattern = pattern, r = TypeR(type), hp = TypeHp(type), t = -i * 0.35f, cd = R(0.6f, 1.4f), ph = R(0, 6), y = y, yPrev = y };
                m.max = m.hp;
                m.go = BuildEnemy(type, out m.body, out m.arils); m.go.SetActive(false);
                if (pattern == "cross") { m.sx = side * (RX + 7); m.lane = R(30, 40) + i * 1.5f; m.bz = R(-0.6f, 0.6f) * RZ; m.relY = m.lane; m.x = m.sx; m.z = m.bz; }
                else if (pattern == "side") { m.sx = (i % 2 == 1 ? -side : side) * RX * R(0.6f, 0.9f); m.bz = R(-0.5f, 0.5f) * RZ; m.relY = -16; m.x = m.sx; m.z = m.bz; m.t = -i * 0.6f; }
                else if (pattern == "escort")
                {
                    // 並んでついてくる：後ろから左右の端に入ってきて、しばらく一緒に飛び、ときどき前に出て撃つ
                    m.side = i % 2 == 1 ? -side : side; m.slot = i / 2;
                    m.holdY = R(9, 13) + m.slot * 5; m.bz = (m.slot % 2 == 1 ? -0.35f : 0.3f) * RZ + R(-0.4f, 0.4f);
                    m.relY = -20 - i * 2; m.x = m.side * (RX + 4); m.z = m.bz; m.t = -i * 0.45f; m.hold = R(8, 10); m.dart = 0;
                }
                else { m.relY = 95 + i * 6; m.x = Mathf.Clamp(px + R(-2.5f, 2.5f), -RX, RX); m.z = Mathf.Clamp(pz + R(-1.5f, 1.5f), -RZ, RZ); m.t = 0; }
                m.relPrev = m.relY; m.y = y + m.relY; m.yPrev = m.y;
                movers.Add(m);
            }
            if (flags.Add("wave" + pattern))
                Say(pattern == "cross" ? "横から編隊が来る！　全部祓うと心づけがもらえる。" : pattern == "side" ? "後ろから敵が追い越してくる！"
                  : pattern == "escort" ? "敵が横に並んでついてくる！　全部祓うと心づけがもらえる。" : "奥から敵が突っ込んでくる！", 2.4f);
        }

        void UpdateMovers(float dt)
        {
            foreach (var m in movers)
            {
                if (m.dead) continue;
                m.t += dt; m.hit = Mathf.Max(0, m.hit - dt); m.relPrev = m.relY;
                if (m.t < 0) { m.y = y + m.relY; m.yPrev = m.y; continue; }
                if (m.pattern == "cross")
                {
                    const float dur = 4.2f; float k = m.t / dur;
                    m.x = Mathf.Lerp(m.sx, -m.sx, Smooth(k)); m.z = m.bz + Mathf.Sin(m.t * 3 + m.ph) * 0.7f;
                    m.relY = m.lane + Mathf.Sin(m.t * 2 + m.ph) * 1.2f;
                    if (k > 0.25f && k < 0.7f && m.fired == 0 && Random.value < dt * 1.2f) { m.fired = 1; MoverShoot(m); }
                    if (k >= 1) m.gone = true;
                }
                else if (m.pattern == "side")
                {
                    if (m.t < 2.4f) m.relY = Mathf.Lerp(-16, 24, Smooth(m.t / 2.4f));
                    else if (m.t < 7) { m.relY = 24 + Mathf.Sin(m.t * 1.3f + m.ph) * 2; m.cd -= dt; if (m.cd <= 0) { m.cd = R(1.4f, 2f); MoverShoot(m); } }
                    else { m.relY += (m.relY * 1.2f + 10) * dt; if (m.relY > 110) m.gone = true; }
                    m.x = m.sx + Mathf.Sin(m.t * 1.7f + m.ph) * 0.8f; m.z = m.bz + Mathf.Cos(m.t * 1.3f + m.ph) * 0.5f;
                }
                else if (m.pattern == "escort")
                {
                    float wob = Mathf.Sin(m.t * 1.6f + m.ph);
                    if (m.t < 2.6f)
                    {
                        float k = Smooth(m.t / 2.6f);
                        m.relY = Mathf.Lerp(-20, m.holdY, k); m.x = m.side * Mathf.Lerp(RX + 4, RX * 0.88f, k); m.z = m.bz;
                    }
                    else if (m.t < 2.6f + m.hold)
                    {
                        // 一緒に飛ぶ。ときどき内側へ寄って前に出て、撃ってから戻る
                        m.cd -= dt;
                        if (m.cd <= 0 && m.dart == 0) { m.dart = 0.001f; m.cd = R(2.2f, 3.4f); }
                        float ins = 0, fwd = 0;
                        if (m.dart > 0)
                        {
                            m.dart += dt; float k = m.dart / 1.6f, b = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
                            ins = b * 0.45f; fwd = b * 10;
                            if (k > 0.5f && !m.dartShot) { m.dartShot = true; MoverShoot(m); }
                            if (k >= 1) { m.dart = 0; m.dartShot = false; }
                        }
                        m.relY = m.holdY + fwd + wob * 1.4f;
                        m.x = m.side * RX * (0.88f - ins) + Mathf.Sin(m.t * 0.9f + m.ph) * 0.5f;
                        m.z = m.bz + Mathf.Cos(m.t * 1.2f + m.ph) * 0.6f;
                    }
                    else
                    {
                        // 外へ抜けて去る
                        m.x += m.side * (6 + (m.t - 2.6f - m.hold) * 6) * dt; m.relY += 4 * dt;
                        if (Mathf.Abs(m.x) > RX + 9) m.gone = true;
                    }
                }
                else
                {
                    m.relY -= (10 + Mathf.Abs(vel) * 0.4f) * dt;
                    m.x += Mathf.Clamp(px - m.x, -1, 1) * 0.8f * dt; m.z += Mathf.Clamp(pz - m.z, -1, 1) * 0.6f * dt;
                    if (m.fired == 0 && m.relY < 40 && m.relY > 22) { m.fired = 1; MoverShoot(m); }
                    if (m.relY < -14) m.gone = true;
                }
                m.yPrev = m.y; m.y = y + m.relY;
                // 馬の高さを通り過ぎるとき、馬の筋を通ったら体当たり
                if ((m.relPrev > 0) != (m.relY > 0) && !m.bumped && Hyp(m.x - px, m.z - pz) < m.r + 0.55f) { m.bumped = true; Bump(); }
            }
            movers.RemoveAll(m =>
            {
                if (!m.dead && !m.gone) return false;
                Destroy(m.go);
                if (m.gone && !m.dead) waveLeft[m.wave] = -99;   // 1匹でも逃したら、その波の心づけはなし
                return true;
            });
        }

        void MoverKilled(Mover m)
        {
            m.dead = true; kills++;
            Burst(m.x, m.y, m.z, KCol(m.type), 26, 4); Burst(m.x, m.y, m.z, Color.white, 12, 3); sfx.Play(sfx.pop);
            int left = waveLeft.TryGetValue(m.wave, out var lv) ? lv - 1 : -1;
            waveLeft[m.wave] = left;
            if (left == 0 && (m.pattern == "cross" || m.pattern == "escort"))
            {
                mon += 3; purse += 3; SaveProfile();
                Burst(m.x, m.y, m.z, 0xffd27a, 30, 5);
                Say(m.pattern == "escort" ? "ついてきた敵を全部祓った！　心づけ +3文" : "編隊を全部祓った！　心づけ +3文", 2.2f);
            }
        }

        /// <summary>v20：後ろ向きのとき前から来る敵は、道のずっと奥（背景の中）から赤い尾を引いて飛んできて、景色の間を抜けて道に入ってくる。</summary>
        void SpawnChaser()
        {
            float f = Road.Zf(y), q = Random.value;
            string type = q < (f > 0.2f ? 0.25f : 0.12f) ? "hozuki" : q < 0.45f ? "kasa" : q < 0.6f && f > 0.3f ? "chochin" : "zakuro";
            float a = R(0, Mathf.PI * 2), r = Mathf.Sqrt(Random.value) * 0.8f;
            var c = new Chaser { type = type, r = TypeR(type) };
            c.hp = c.max = TypeHp(type) + 1;
            c.go = BuildEnemy(type, out _, out _);
            c.go.transform.SetParent(fx, false);
            // 赤い気配と白い芯（霧の中でも見える）。敵はカメラに顔を向けるので、気配は体の奥（-Z）に置く
            c.aura = ModelFactory.Glow(c.go.transform, ModelFactory.GlowMat(new Color(1f, 0.23f, 0.16f, 0.75f)), 4.2f * c.r, new Vector3(0, 0, -0.6f));
            c.auraRd = c.aura.GetComponent<Renderer>();
            c.core = ModelFactory.Glow(c.go.transform, ModelFactory.GlowMat(new Color(1f, 0.88f, 0.69f, 1f)), 2f, new Vector3(0, 0, 1.2f));
            c.coreRd = c.core.GetComponent<Renderer>();
            c.trail = new GameObject[TRAIL_N]; c.trailRd = new Renderer[TRAIL_N];
            for (int i = 0; i < TRAIL_N; i++)
            {
                var s = ModelFactory.Glow(fx, ModelFactory.GlowMat(i % 2 == 1 ? new Color(1f, 0.69f, 0.38f, 0.6f) : new Color(1f, 0.29f, 0.16f, 0.6f)), 1f, Vector3.zero);
                s.SetActive(false); c.trail[i] = s; c.trailRd[i] = s.GetComponent<Renderer>();
            }
            float start = R(110, 135); int sideS = Random.value < 0.5f ? -1 : 1;
            c.start = start; c.far = true;
            c.sx = sideS * (RX + R(22, 32)); c.sz = R(-0.6f, 1f) * (RZ + 9);
            c.tx = Mathf.Cos(a) * r * RX; c.tz = Mathf.Sin(a) * r * RZ; c.weave = R(1.5f, 3f);
            c.y = c.yPrev = y + start; c.x = c.sx; c.z = c.sz;
            c.close = R(7, 10); c.cd = R(0.8f, 1.6f); c.ph = R(0, 6);
            chasers.Add(c);
            if (flags.Add("farTip")) Say("奥の赤い光は、こちらへ飛んでくる敵。景色の間を抜けて道に入ってくる。", 3.4f);
        }

        static float Hyp(float a, float b) => Mathf.Sqrt(a * a + b * b);
        float Dist(float x, float z) => Hyp(x - px, z - pz);
    }
}
