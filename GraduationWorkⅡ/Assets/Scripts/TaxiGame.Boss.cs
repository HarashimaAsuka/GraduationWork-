using UnityEngine;
using Random = UnityEngine.Random;   // System.Random と取り違えないように

namespace ShoryoumaTaxi
{
    /// <summary>
    /// ステージ3の大ボス「鬼ザクロの親玉」（Web版 v32）。
    /// 一の段（340〜227）・二の段 熟れ（226〜114）・三の段 割れ（113〜0）。段が変わるたびに果汁で水分 +30、鬼の手が生え直す。
    /// 弱点：親玉がひるむと（突進のあと・鬼の手を祓ったとき）割れ目から金色の核が出る。そのあいだは1発が2発ぶん。
    /// </summary>
    public partial class TaxiGame
    {
        const int BOSS_HP = 340, HAND_HP = 28;
        static readonly string[][] BOSS_ATK = {
            null,
            new[] { "fan", "slam", "rain", "sweep", "rush", "wave" },
            new[] { "spiral", "slam", "escort", "sweep", "onibi", "rush", "wall" },
            new[] { "ring", "slam2", "spiral", "rush2", "wall", "sweep", "onibi", "escort" },
        };

        void MakeBoss()
        {
            var b = new Boss { hp = BOSS_HP, max = BOSS_HP };
            b.m = ModelFactory.Boss(zakuroPrefab, jpFont);
            b.m.root.transform.SetParent(fx, false);
            b.m.root.SetActive(false);
            b.hands = new BossHand[2];
            for (int i = 0; i < 2; i++)
            {
                int sd = i == 0 ? -1 : 1;
                var h = new BossHand { sd = sd, hp = HAND_HP, x = sd * 9, relY = 140 };
                h.go = ModelFactory.Hand(sd, out h.glow); h.go.transform.SetParent(fx, false); h.go.SetActive(false);
                b.hands[i] = h;
            }
            boss = b;
        }

        int BossPhase(Boss b) => b.hp > BOSS_HP * 2 / 3 ? 1 : b.hp > BOSS_HP / 3 ? 2 : 3;

        void SpawnGuards(Boss b)
        {
            if (b.guards == null)
            {
                b.guards = new BossGuard[4];
                for (int i = 0; i < 4; i++)
                {
                    var g = ModelFactory.Hozuki(hozukiPrefab); g.transform.SetParent(fx, false); g.SetActive(false);
                    b.guards[i] = new BossGuard { go = g, a = i / 4f * Mathf.PI * 2, hp = 1 };
                }
            }
            foreach (var gd in b.guards) { gd.dead = false; gd.hp = 1; }
            b.guardT = 0;
        }

        void BossShot(Boss b, string type, float ox, float oz, float lead = 1, float dy = 0)
        {
            float vs = Mathf.Max(11, Mathf.Abs(vel) + 10), time = Mathf.Max(0.6f, (b.relY - 3) / vs);
            Emit(type, b.x, b.y - 3 + dy, b.z, (px + ox - b.x) / time * lead, vel - vs, (pz + oz - b.z) / time * lead, true);
        }

        void BossVolley(Boss b, string type)
        {
            float vs = Mathf.Max(11, Mathf.Abs(vel) + 10);
            int vol = b.nextAtk;
            Volley(type, ref vol, b.phase > 1, b.x, b.y - 3, b.z, Mathf.Max(0.6f, (b.relY - 3) / vs), vel - vs, true, 1);
        }

        void Beep(bool hi) => sfx.Play(hi ? sfx.beep1 : sfx.beep0, 0.8f);

        bool HandAttack(Boss b, string kind, float delay = 0)
        {
            BossHand pick = null; float best = float.MaxValue; int free = 0;
            foreach (var h in b.hands) if (!h.dead && h.mode == "idle") free++;
            if (free == 0) return false;
            if (kind == "sweep")
            {
                int k = Mathf.Min(free - 1, Mathf.FloorToInt(R(0, free)));
                foreach (var h in b.hands) if (!h.dead && h.mode == "idle") { if (k-- == 0) { pick = h; break; } }
            }
            else foreach (var h in b.hands) if (!h.dead && h.mode == "idle") { float d = Mathf.Abs(h.x - px); if (d < best) { best = d; pick = h; } }
            if (pick == null) return false;
            pick.mode = kind == "sweep" ? "aimS" : "aim"; pick.t = -delay; pick.beeps = 0;
            return true;
        }

        void BossAttackFallback(Boss b) { int n = b.phase == 3 ? 4 : 3; for (int i = -n; i <= n; i++) BossShot(b, "seed", i * 1.5f, Mathf.Abs(i) * 0.3f); }

        void BossAttack(Boss b)
        {
            var list = BOSS_ATK[b.phase]; string atk = list[b.nextAtk++ % list.Length];
            bool angry = b.phase > 1;
            b.cd = R(1.5f, 2.0f);
            switch (atk)
            {
                case "fan":
                {
                    int n = b.phase == 3 ? 4 : 3;
                    for (int i = -n; i <= n; i++) BossShot(b, "seed", i * 1.5f, Mathf.Abs(i) * 0.3f);
                    Say("種の雨が来る！", 1.2f);
                    break;
                }
                case "slam":
                case "slam2":
                    if (!HandAttack(b, "slam")) { BossAttackFallback(b); break; }
                    if (atk == "slam2") HandAttack(b, "slam", 0.7f);
                    Say(atk == "slam2" ? "鬼の手が両方とも叩きつけてくる！　赤い輪から逃げて！" : "鬼の手が叩きつけてくる！　赤い輪から逃げて！", 1.8f);
                    break;
                case "sweep":
                    if (!HandAttack(b, "sweep")) { BossAttackFallback(b); break; }
                    Say("鬼の手が薙ぎ払う！　赤い帯から上か下へよけて！", 1.8f);
                    break;
                case "wave": SpawnWave("boss" + b.nextAtk, "cross", angry ? "hozuki" : "zakuro", 3); break;
                case "escort": SpawnWave("boss" + b.nextAtk, "escort", b.phase == 3 ? "kasa" : "zakuro", 4); break;
                case "rain": BossVolley(b, "kasa"); b.pend.Add(new System.Collections.Generic.KeyValuePair<float, string>(t + 0.65f, "kasa")); Say("雨粒の列！　すき間を抜けて。", 1.4f); break;
                case "ring": BossVolley(b, "chochin"); b.pend.Add(new System.Collections.Generic.KeyValuePair<float, string>(t + 0.5f, "chochin")); Say("火の輪が来る！", 1.2f); break;
                case "onibi": foreach (float o in new[] { -2.5f, 0f, 2.5f }) BossShot(b, "onibi", o, 0.5f, 0.5f); Say("鬼火が追ってくる！", 1.2f); break;
                case "spiral": b.spiral = b.phase == 3 ? 2.2f : 1.6f; b.spT = 0; b.spA = R(0, 6); Say("親玉が回って種をまき散らす！", 1.4f); b.cd += 1.2f; break;
                case "wall":
                    for (int row = 0; row < 2; row++)
                    {
                        int gap = Mathf.Min(8, Mathf.FloorToInt(R(0, 9)));
                        for (int i = 0; i < 9; i++) if (i != gap) BossShot(b, "seed", (i - 4) * 1.1f, row == 1 ? 1.2f : -0.6f, 1, row * 6);
                    }
                    Say("種の壁！　すき間を探して。", 1.4f);
                    break;
                case "rush":
                case "rush2":
                    b.mode = "windup"; b.modeT = 0; b.rushes = atk == "rush2" ? 2 : 1; b.beeps = 0;
                    Say("突進が来る！　「！」が出たら、赤い輪から逃げて！", 2.2f);
                    break;
            }
        }

        void StunBoss(Boss b, float sec, string text)
        {
            if (b.dead) return;
            b.stun = Mathf.Max(b.stun, sec); b.spiral = 0;
            if (b.mode == "windup") b.mode = "idle";
            Say(text, 2.2f); sfx.Play(sfx.bell);
            if (flags.Add("coreTip")) Say("光る核が出ているあいだは、塩1発が2発ぶん効く！", 2.6f);
        }

        void UpdateHands(Boss b, float dt)
        {
            foreach (var h in b.hands)
            {
                h.hit = Mathf.Max(0, h.hit - dt);
                if (h.dead) { h.ring = false; h.band = false; continue; }
                float homeX = b.x + h.sd * (RX * 0.6f + 6), homeZ = b.z - 1 + Mathf.Sin(b.t * 1.4f + h.sd) * 0.8f, homeY = b.relY - 6;
                float ease = 1 - Mathf.Exp(-5 * dt);
                h.ring = false; h.band = false;
                if (h.mode == "idle" || b.intro < 3.4f)
                {
                    h.x += (homeX - h.x) * ease; h.z += (homeZ - h.z) * ease; h.relY += (homeY - h.relY) * ease;
                    if (b.intro < 3.4f) h.mode = "idle";
                }
                else if (h.mode == "aim")
                {
                    // 叩きつけの狙い：プレイヤーの上へ回り込み、赤い輪が追いかける。最後の0.35秒で輪が止まる
                    h.t += dt;
                    if (h.t < 0) { h.x += (homeX - h.x) * ease; h.z += (homeZ - h.z) * ease; h.relY += (homeY - h.relY) * ease; h.y = y + h.relY; continue; }
                    if (h.t < 0.85f) { h.tx = px; h.tz = pz; }
                    h.x += (h.tx - h.x) * ease * 1.4f; h.z += (h.tz - h.z) * ease * 1.4f; h.relY += (20 - h.relY) * ease;
                    h.ring = true;
                    int nb = Mathf.FloorToInt(h.t / 0.4f); if (nb > h.beeps) { h.beeps = nb; Beep(false); }
                    if (h.t > 1.2f) { h.mode = "slam"; h.t = 0; Beep(true); }
                }
                else if (h.mode == "slam")
                {
                    h.t += dt; float prev = h.relY;
                    h.relY = Mathf.Lerp(20, -5, Smooth(h.t / 0.32f)); h.x = h.tx; h.z = h.tz;
                    h.ring = h.t < 0.32f;
                    if (prev > 0 && h.relY <= 0) { shake = 0.5f; Burst(h.x, y, h.z, 0xff6a3a, 20, 5); if (Hyp(h.x - px, h.z - pz) < 2.4f) Bump(); }
                    if (h.t > 0.9f) { h.mode = "back"; h.t = 0; }
                }
                else if (h.mode == "aimS")
                {
                    // 薙ぎ払いの狙い：画面の横の外に構え、赤い帯（その高さを手が通る）が出る
                    h.t += dt; if (h.t < 0) { h.y = y + h.relY; continue; }
                    if (h.t < 0.9f) h.tz = Mathf.Clamp(pz, -RZ, RZ);
                    h.x += (h.sd * (RX + 6) - h.x) * ease * 1.4f; h.z += (h.tz - h.z) * ease * 1.4f; h.relY += (0.5f - h.relY) * ease * 1.4f;
                    h.band = true;
                    int nb = Mathf.FloorToInt(h.t / 0.4f); if (nb > h.beeps) { h.beeps = nb; Beep(false); }
                    if (h.t > 1.3f) { h.mode = "sweep"; h.t = 0; h.sx = h.x; Beep(true); }
                }
                else if (h.mode == "sweep")
                {
                    h.t += dt; h.relY = 0.5f; h.z = h.tz;
                    h.x = Mathf.Lerp(h.sx, -h.sd * (RX + 6), Smooth(h.t / 0.65f));
                    h.band = h.t < 0.65f;
                    if (!h.swept && Mathf.Abs(h.x - px) < 2.0f && Mathf.Abs(h.z - pz) < 1.9f) { h.swept = true; Bump(); }
                    if (h.t > 0.9f) { h.mode = "back"; h.t = 0; h.swept = false; }
                }
                else if (h.mode == "back")
                {
                    h.t += dt; h.x += (homeX - h.x) * ease; h.z += (homeZ - h.z) * ease; h.relY += (homeY - h.relY) * ease;
                    if (h.t > 0.9f) h.mode = "idle";
                }
                h.y = y + h.relY;
            }
        }

        void UpdateBoss(float dt)
        {
            var b = boss;
            if (b == null || b.dead) return;
            b.t += dt; b.hit = Mathf.Max(0, b.hit - dt);
            // 少し遅れて撃つ弾（雨粒の2列目・火の輪の2つ目）
            for (int i = b.pend.Count - 1; i >= 0; i--) if (t >= b.pend[i].Key) { BossVolley(b, b.pend[i].Value); b.pend.RemoveAt(i); }
            if (b.intro < 3.4f)
            {
                if (b.intro == 0) { Say("幻の三日目。道の奥から、鬼ザクロの親玉がやってくる……！", 3f); shake = 0.8f; sfx.Play(sfx.bell); }
                b.intro += dt;
                float k = Smooth(b.intro / 3.4f);
                b.relY = Mathf.Lerp(150, 34, k); b.x = Mathf.Sin(b.t * 0.8f) * RX * 0.4f * k; b.z = 2.6f;
                b.yPrev = b.y; b.y = y + b.relY; UpdateHands(b, dt);
                if (b.intro >= 3.4f) Say("鬼の手を祓うか、突進をよけると親玉がひるむ。そのとき出る光る核が弱点！", 3.4f);
                return;
            }
            int ph = BossPhase(b);
            if (ph != b.phase)
            {
                b.phase = ph; b.mode = "idle"; b.spiral = 0; b.cd = 1.8f; b.relY = 34; b.stun = 0;
                water = Mathf.Min(100, water + 30); shake = 0.7f; sfx.Play(sfx.bell);
                for (int i = 0; i < 3; i++) Burst(b.x + R(-2, 2), b.y, b.z + R(-2, 2), 0xffd27a, 30, 6);
                foreach (var h in b.hands) { h.dead = false; h.hp = HAND_HP; h.mode = "idle"; }
                if (ph == 2)
                {
                    if (b.m.body != null && zakuroPrefab == null) b.m.body.sharedMaterial = ModelFactory.ZakuroRipeMat;
                    Say("親玉が熟れた！　鬼の手が生え直し、攻撃も速くなる。（果汁で 水分 +30）", 3f);
                }
                else
                {
                    if (b.m.arils != null) b.m.arils.SetActive(true);
                    SpawnGuards(b);
                    Say("親玉が割れた！　鬼灯の守りが塩をはじく。先に守りを祓え。（果汁で 水分 +30）", 3.4f);
                }
            }
            float sp = ph == 1 ? 1 : ph == 2 ? 1.3f : 1.55f;
            b.stun = Mathf.Max(0, b.stun - dt);
            if (b.mode != "rush" && b.mode != "dizzy")
            {
                float w = ph == 1 ? 0.5f : 0.75f;
                b.x += (Mathf.Sin(b.t * 0.6f * sp) * RX * w - b.x) * (1 - Mathf.Exp(-3 * dt));
                b.z += (2.4f + (ph == 1 ? Mathf.Sin(b.t * 0.9f) * RZ * 0.3f : Mathf.Sin(b.t * 1.2f * sp) * RZ * 0.45f) - b.z) * (1 - Mathf.Exp(-3 * dt));
            }
            if (b.guards != null)
            {
                int alive = 0; foreach (var g in b.guards) if (!g.dead) alive++;
                b.shield = alive > 0;
                if (alive == 0) { b.guardT += dt; if (b.guardT > 10) { SpawnGuards(b); Say("鬼灯の守りが、また実った。", 1.6f); } }
                foreach (var gd in b.guards)
                {
                    gd.hit = Mathf.Max(0, gd.hit - dt); gd.a += dt * 1.2f;
                    gd.x = b.x + Mathf.Cos(gd.a) * 5.0f; gd.z = b.z + Mathf.Sin(gd.a) * 3.4f; gd.y = b.y - 5;
                }
            }
            if (b.spiral > 0)
            {
                b.spiral -= dt; b.spT -= dt;
                while (b.spT <= 0)
                {
                    b.spT += ph == 3 ? 0.06f : 0.08f; b.spA += 0.5f;
                    float[] offs = ph == 3 ? new[] { 0f, 2.09f, 4.19f } : new[] { 0f, Mathf.PI };
                    foreach (float off in offs) BossShot(b, "seed", Mathf.Cos(b.spA + off) * 4.5f, Mathf.Sin(b.spA + off) * 3, 1);
                }
            }
            b.lockState = 0;
            if (b.mode == "windup")
            {
                // 突進の合図：1.5秒。親玉が下がって震え、「！」と赤い輪。輪は1.1秒までプレイヤーを追い、そこで止まる
                b.modeT += dt;
                b.relY = Mathf.Lerp(34, 46, Smooth(b.modeT / 0.8f));
                if (b.modeT < 1.1f) { b.rx = px; b.rz = pz; b.lockState = 1; } else b.lockState = 2;
                int nb = Mathf.FloorToInt(b.modeT / 0.37f); if (nb > b.beeps) { b.beeps = nb; Beep(b.modeT > 1.1f); }
                if (b.modeT > 1.5f) { b.mode = "rush"; b.modeT = 0; b.sx = b.x; b.sz = b.z; shake = 0.3f; }
            }
            else if (b.mode == "rush")
            {
                b.modeT += dt; float k = b.modeT;
                b.relY = Mathf.Lerp(46, 6, Smooth(k / 0.7f));
                float kk = Smooth(k / 0.7f); b.x = Mathf.Lerp(b.sx, b.rx, kk); b.z = Mathf.Lerp(b.sz, b.rz, kk);
                b.lockState = k < 0.7f ? 2 : 0;
                if (b.relY < 9 && !b.rushHit && Hyp(b.x - px, b.z - pz) < 3.4f) { b.rushHit = true; Bump(); }
                if (k > 0.8f)
                {
                    b.rushHit = false;
                    if (--b.rushes > 0) { b.mode = "windup"; b.modeT = 0.4f; b.beeps = 1; }
                    else { b.mode = "dizzy"; b.modeT = 0; StunBoss(b, 2.1f, "親玉が目を回した！　光る核を狙え！"); }
                }
            }
            else if (b.mode == "dizzy")
            {
                // 突進のあと：すぐ前で目を回している。いちばんのチャンス
                b.modeT += dt;
                b.relY = Mathf.Lerp(6, 16, Smooth(b.modeT / 0.6f));
                if (b.modeT > 2.1f) { b.mode = "return"; b.modeT = 0; }
            }
            else if (b.mode == "return")
            {
                b.modeT += dt; b.relY = Mathf.Lerp(16, 34, Smooth(b.modeT / 1.0f));
                if (b.modeT > 1.0f) { b.mode = "idle"; b.cd = 1.2f; }
            }
            else
            {
                b.relY = 34 + Mathf.Sin(b.t * 0.8f) * 2;
                if (b.spiral <= 0 && b.stun <= 0) { b.cd -= dt * sp; if (b.cd <= 0) BossAttack(b); }
            }
            b.yPrev = b.y; b.y = y + b.relY;
            UpdateHands(b, dt);
        }

        void BossHit(int n = 1)
        {
            var b = boss;
            b.hp -= n; b.hit = 0.12f; water = Mathf.Min(100, water + 0.3f);
            if (b.hp <= 0 && !b.dead)
            {
                b.dead = true; kills++; b.dyingT = 0.0001f; b.spiral = 0;
                if (b.guards != null) foreach (var gd in b.guards) gd.dead = true;
                foreach (var h in b.hands) h.dead = true;
                sfx.Play(sfx.pop); sfx.Play(sfx.bell); shake = 1.2f;
                clearT = 0.001f;
                Say("親玉を祓った！　ステージ3 クリア", 3f);
            }
        }
    }
}
