using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ShoryoumaTaxi
{
    /// <summary>
    /// 大警部。ステージ1〜3：倒せない、奥から追いかけてくる。
    /// ステージ4：決戦（v33 の親玉戦のしかけを移したもの）。
    ///   一の段・二の段・三の段。段が変わると水分 +30、白手袋が生え直す。
    ///   白手袋：叩きつけ（赤い輪）／薙ぎ払い（赤い帯）。手袋を祓うと大警部がひるむ。
    ///   突進：「！」と赤い輪のあと突っ込んでくる。よけると目を回す。ひるんでいるあいだは塩1発が2発ぶん。
    ///   三の段：パト提灯の護衛が塩をはじく。先に護衛を祓う。
    /// </summary>
    public partial class IllegalTaxi
    {
        const int HAND_HP = 20;
        static readonly string[][] BOSS_ATK = {
            null,
            new[] { "fan", "slam", "sweep", "rush", "wave" },
            new[] { "spiral", "slam", "sweep", "wall", "rush", "wave" },
            new[] { "spiral", "slam2", "wall", "rush2", "sweep", "fan", "wave" } };

        class Hand { public GameObject go; public int sd, hp = HAND_HP; public bool dead, ring, band, swept; public string mode = "idle"; public float t, x, y, z = 1, relY = 20, tx, tz, sx, hit; public int beeps; }
        class Guard { public GameObject go; public GameObject[] siren; public float a, x, y, z, hit; public int hp = 2; public bool dead; }
        class BossFight
        {
            public float relY = 120, intro, t, modeT, cd = 2, stunT, spiral, spT, spA, x, z = 3.2f, rx, rz, sx, sz, guardT, hit, dmgT;
            public int phase = 1, nextAtk, rushes, beeps, lockMode, dmg; public bool shield, rushHit, weakHit; public string mode = "idle";
            public Hand[] hands; public List<Guard> guards = new List<Guard>();
        }
        BossFight B;
        GameObject[] gloves = new GameObject[2];
        GameObject[] slamRings = new GameObject[2]; GameObject sweepBand, rushRing, rushMark;

        void BuildBossFightParts()
        {
            for (int i = 0; i < 2; i++) { gloves[i] = ModelFactory.Glove(); gloves[i].transform.SetParent(fx, false); gloves[i].SetActive(false); }
            var red = new Color(1, 0.19f, 0.25f, 0.6f);
            for (int i = 0; i < 2; i++) { slamRings[i] = ModelFactory.MeshObj("slamRing", MeshKit.Ring(2.0f, 2.4f, 40), fx, ModelFactory.OverlayMat(red, 3050), Vector3.zero, Vector3.one, new Vector3(90, 0, 0)); slamRings[i].SetActive(false); }
            sweepBand = ModelFactory.Prim(PrimitiveType.Quad, fx, Vector3.zero, new Vector3(16, 3.6f, 1), ModelFactory.OverlayMat(new Color(1, 0.19f, 0.25f, 0.4f), 3049), new Vector3(90, 0, 0)); sweepBand.SetActive(false);
            rushRing = ModelFactory.MeshObj("rushRing", MeshKit.Ring(3.0f, 3.5f, 40), fx, ModelFactory.OverlayMat(red, 3051), Vector3.zero, Vector3.one, new Vector3(90, 0, 0)); rushRing.SetActive(false);
            rushMark = ModelFactory.MeshObj("rushMark", MeshKit.Tri(new Vector3(0, 1.6f, 0), new Vector3(1.4f, -0.9f, 0), new Vector3(-1.4f, -0.9f, 0)), fx, ModelFactory.OverlayMat(new Color(1, 0.19f, 0.25f, 0.95f), 3052), Vector3.zero, Vector3.one); rushMark.SetActive(false);
        }
        void ClearBossFight()
        {
            if (B != null) foreach (var g in B.guards) Destroy(g.go);
            B = null;
            foreach (var g in gloves) if (g != null) g.SetActive(false);
        }
        void NewBossFight()
        {
            B = new BossFight();
            B.hands = new Hand[2];
            for (int i = 0; i < 2; i++) { int sd = i == 0 ? -1 : 1; B.hands[i] = new Hand { go = gloves[i], sd = sd, x = sd * 9 }; }
        }

        // ============================================================
        //  ステージ1〜3：追いかけてくる大警部
        // ============================================================
        void BossChase(float dt)
        {
            float v = st.bossV;
            if (st.lunge)
            {
                lungeT -= dt;
                if (lungeT <= 0 && lunging <= 0) { lunging = 1; lungeT = R(6, 9); se.Play(se.sirenShort, 0.7f); se.Play(se.roar); sirenT = 1; }
                if (lunging > 0) { lunging -= dt; v += 7; }
            }
            bossY -= v * dt;
            float k = 1 - Mathf.Exp(-0.5f * dt); bossX += (px * 0.6f - bossX) * k; bossZ += (pz * 0.5f - bossZ) * k;
            roarT -= dt; if (roarT <= 0) { roarT = R(6, 9); se.Play(se.roar); }
            if (st.bossAtk > 0 && phaseT > 2)
            {
                bossAtkT -= dt;
                if (bossAtkT <= 0)
                {
                    bossAtkT = st.bossAtk;
                    float[] offs = st.bossVolley == 1 ? new[] { 0f } : new[] { -2.2f, 0f, 2.2f };
                    foreach (float o in offs) FireAt(bossX, bossY - 2.5f, bossZ, true, o, 0, 16);
                    se.Play(se.cuffShot); bossFlash = 0.2f;
                }
            }
            if (bossY - y < 2.5f) { over = true; endReason = "caught"; se.Play(se.whistle); se.Play(se.cuffs); Banner("御用だ！", "", ModelFactory.Hex(0xff6a5a), 2); Later(1.4f, () => Finish("caught")); return; }
            if (y <= START)
            {
                over = true; bool ok = fare >= st.goal; endReason = ok ? "clear" : "red";
                Banner(ok ? "逃げきった！" : "逃げきった……けど", "", ModelFactory.Hex(ok ? 0xffd060 : 0xff8a7a), 1.6f);
                Later(1.5f, () => Finish(ok ? "clear" : "red"));
            }
        }

        // ============================================================
        //  ステージ4：決戦
        // ============================================================
        int BossPhase() => bossHp > bossHpMax * 2f / 3f ? 1 : bossHp > bossHpMax / 3f ? 2 : 3;
        void Beep(bool hi) => se.Play(hi ? se.beep1 : se.beep0, 0.8f);
        void BossShot(float ox, float oz, float dy = 0) => FireAt(B.x, y + B.relY - 3 + dy, B.z, false, ox, oz, 14);

        bool HandAttack(string kind, float delay = 0)
        {
            Hand h = null;
            foreach (var c in B.hands)
            {
                if (c.dead || c.mode != "idle") continue;
                if (h == null) h = c;
                else if (kind == "sweep" ? Random.value < 0.5f : Mathf.Abs(c.x - px) < Mathf.Abs(h.x - px)) h = c;
            }
            if (h == null) return false;
            h.mode = kind == "sweep" ? "aimS" : "aim"; h.t = -delay; h.beeps = 0;
            return true;
        }
        void SpawnGuards()
        {
            foreach (var g in B.guards) Destroy(g.go);
            B.guards.Clear();
            for (int i = 0; i < 4; i++)
            {
                var pm = ModelFactory.Patochin(); pm.root.transform.SetParent(fx, false);
                B.guards.Add(new Guard { go = pm.root, siren = pm.siren, a = i / 4f * Mathf.PI * 2 });
            }
            B.guardT = 0;
        }
        void BossAttack()
        {
            var list = BOSS_ATK[B.phase]; string atk = list[B.nextAtk++ % list.Length];
            B.cd = R(1.6f, 2.2f);
            switch (atk)
            {
                case "fan": { int n = B.phase == 3 ? 4 : 3; for (int i = -n; i <= n; i++) BossShot(i * 1.5f, Mathf.Abs(i) * 0.3f); se.Play(se.cuffShot); break; }
                case "slam": if (!HandAttack("slam")) BossAttack(); break;
                case "slam2": if (!HandAttack("slam")) BossAttack(); else HandAttack("slam", 0.7f); break;
                case "sweep": if (!HandAttack("sweep")) BossAttack(); break;
                case "wave": SpawnChase(); Later(0.5f, () => { if (B != null && !over) SpawnChase(); }); break;
                case "spiral": B.spiral = B.phase == 3 ? 2.2f : 1.6f; B.spT = 0; B.spA = R(0, 6); B.cd += 1.2f; break;
                case "wall":
                    for (int row = 0; row < 2; row++) { int gap = Random.Range(0, 9); for (int i = 0; i < 9; i++) if (i != gap) BossShot((i - 4) * 1.1f, row == 1 ? 1.2f : -0.6f, row * 6); }
                    se.Play(se.cuffShot); break;
                default: B.mode = "windup"; B.modeT = 0; B.rushes = atk == "rush2" ? 2 : 1; B.beeps = 0; break;
            }
        }
        void StunBoss(float sec) { B.stunT = Mathf.Max(B.stunT, sec); B.spiral = 0; if (B.mode == "windup") B.mode = "idle"; se.Play(se.bell, 0.6f); }
        void BossDamage(int n) { bossHp = Mathf.Max(0, bossHp - n); B.hit = 0.12f; bossFlash = 0.08f; B.dmg += n; B.weakHit |= n > 1; }

        void SaltBossFight(SaltW s)
        {
            if (B == null || B.intro < 3) return;
            foreach (var gd in B.guards)
            {
                if (gd.dead || !SaltHits(s, gd.y, gd.x, gd.z, 1.4f)) continue;
                s.dead = true; gd.hp--; gd.hit = 0.12f; se.Play(se.hit, 0.8f); Burst(gd.x, gd.y, gd.z, 0xffffff, 6, 3);
                if (gd.hp <= 0) { gd.dead = true; gd.go.SetActive(false); kills++; se.Play(se.kill); Burst(gd.x, gd.y, gd.z, 0xffb060, 24, 5); }
                return;
            }
            foreach (var h in B.hands)
            {
                if (h.dead || !SaltHits(s, h.y, h.x, h.z, 1.8f)) continue;
                s.dead = true; h.hp--; h.hit = 0.12f; se.Play(se.hit, 0.8f); Burst(h.x, h.y, h.z, 0xffffff, 6, 3);
                if (h.hp <= 0) { h.dead = true; h.go.SetActive(false); Burst(h.x, h.y, h.z, 0xffffff, 40, 7); se.Play(se.kill); StunBoss(3); PopAt(h.x, h.y, h.z, "白手袋を祓った！", Color.white, 28); }
                return;
            }
            float by = y + B.relY;
            if (SaltHits(s, by, B.x, B.z, 3.6f))
            {
                s.dead = true;
                if (B.shield && B.stunT <= 0) { Burst(B.x, by - 3, B.z, 0x80c8ff, 6, 2.5f); return; }
                se.Play(se.bossHit); Burst(B.x, by - 2, B.z, B.stunT > 0 ? 0xffd040 : 0xffffff, 8, 3);
                BossDamage(B.stunT > 0 ? 2 : 1);
            }
        }

        void UpdateHands(float dt)
        {
            foreach (var h in B.hands)
            {
                h.hit = Mathf.Max(0, h.hit - dt); h.ring = h.band = false;
                if (h.dead) continue;
                float homeX = B.x + h.sd * (RX * 0.6f + 6), homeZ = B.z - 1 + Mathf.Sin(B.t * 1.4f + h.sd) * 0.8f, homeY = B.relY - 6;
                float ease = 1 - Mathf.Exp(-5 * dt);
                void Home() { h.x += (homeX - h.x) * ease; h.z += (homeZ - h.z) * ease; h.relY += (homeY - h.relY) * ease; }
                if (h.mode == "idle" || B.intro < 3) Home();
                else if (h.mode == "aim")
                {
                    h.t += dt;
                    if (h.t < 0) Home();
                    else
                    {
                        if (h.t < 0.85f) { h.tx = px; h.tz = pz; }
                        h.x += (h.tx - h.x) * ease * 1.4f; h.z += (h.tz - h.z) * ease * 1.4f; h.relY += (20 - h.relY) * ease;
                        h.ring = true;
                        int nb = Mathf.FloorToInt(h.t / 0.4f); if (nb > h.beeps) { h.beeps = nb; Beep(false); }
                        if (h.t > 1.2f) { h.mode = "slam"; h.t = 0; Beep(true); }
                    }
                }
                else if (h.mode == "slam")
                {
                    h.t += dt; float prev = h.relY;
                    h.relY = Mathf.Lerp(20, -5, Smooth(h.t / 0.32f)); h.x = h.tx; h.z = h.tz;
                    h.ring = h.t < 0.32f;
                    if (prev > 0 && h.relY <= 0)
                    {
                        shake = 0.5f; Burst(h.x, y, h.z, 0xff6a3a, 20, 5); se.Play(se.ram);
                        if (inv <= 0 && Hyp(h.x - px, h.z - pz) < 2.4f && !UseCharm()) { Hurt(12); stun = 0.6f; }
                    }
                    if (h.t > 0.9f) { h.mode = "back"; h.t = 0; }
                }
                else if (h.mode == "aimS")
                {
                    h.t += dt;
                    if (h.t < 0) Home();
                    else
                    {
                        if (h.t < 0.9f) h.tz = Mathf.Clamp(pz, -RZ, RZ);
                        h.x += (h.sd * (RX + 6) - h.x) * ease * 1.4f; h.z += (h.tz - h.z) * ease * 1.4f; h.relY += (0.5f - h.relY) * ease * 1.4f;
                        h.band = true;
                        int nb = Mathf.FloorToInt(h.t / 0.4f); if (nb > h.beeps) { h.beeps = nb; Beep(false); }
                        if (h.t > 1.3f) { h.mode = "sweep"; h.t = 0; h.sx = h.x; Beep(true); }
                    }
                }
                else if (h.mode == "sweep")
                {
                    h.t += dt; h.relY = 0.5f; h.z = h.tz;
                    h.x = Mathf.Lerp(h.sx, -h.sd * (RX + 6), Smooth(h.t / 0.65f));
                    h.band = h.t < 0.65f;
                    if (!h.swept && inv <= 0 && Mathf.Abs(h.x - px) < 2f && Mathf.Abs(h.z - pz) < 1.9f) { h.swept = true; if (!UseCharm()) { Hurt(12); stun = 0.6f; } }
                    if (h.t > 0.9f) { h.mode = "back"; h.t = 0; h.swept = false; }
                }
                else if (h.mode == "back") { h.t += dt; Home(); if (h.t > 0.9f) h.mode = "idle"; }
                h.y = y + h.relY;
            }
        }

        void BossFightUpdate(float dt)
        {
            B.t += dt; B.hit = Mathf.Max(0, B.hit - dt);
            if (B.intro < 3)
            {
                B.intro += dt;
                B.relY = Mathf.Lerp(120, 24, Smooth(B.intro / 3f));
                B.x = Mathf.Sin(B.t * 0.8f) * RX * 0.3f; B.z = 3.2f;
            }
            else
            {
                int ph = BossPhase();
                if (ph != B.phase)
                {
                    B.phase = ph; B.mode = "idle"; B.spiral = 0; B.cd = 1.8f; B.relY = 24; B.stunT = 0;
                    water = Mathf.Min(100, water + 30); shake = 0.7f; se.Play(se.bell); se.Play(se.roar);
                    foreach (var h in B.hands) { h.dead = false; h.hp = HAND_HP; h.mode = "idle"; }
                    if (ph == 2) Banner("二の段", "大警部が本気を出した！（水分 +30）", ModelFactory.Hex(0xff8a7a), 2.6f);
                    else { Banner("三の段", "護衛のパト提灯が塩をはじく。先に護衛を祓え（水分 +30）", ModelFactory.Hex(0xff8a7a), 2.6f); SpawnGuards(); }
                }
                float sp = ph == 1 ? 1 : ph == 2 ? 1.3f : 1.55f;
                B.stunT = Mathf.Max(0, B.stunT - dt);
                if (B.mode != "rush" && B.mode != "dizzy")
                {
                    float w = ph == 1 ? 0.45f : 0.7f, e = 1 - Mathf.Exp(-3 * dt);
                    B.x += (Mathf.Sin(B.t * 0.6f * sp) * RX * w - B.x) * e;
                    B.z += (3.2f + Mathf.Sin(B.t * 0.9f * sp) * RZ * 0.2f - B.z) * e;
                }
                // 護衛
                int alive = 0; foreach (var g in B.guards) if (!g.dead) alive++;
                B.shield = alive > 0;
                if (B.phase == 3 && alive == 0) { B.guardT += dt; if (B.guardT > 10) SpawnGuards(); }
                foreach (var g in B.guards) { g.hit = Mathf.Max(0, g.hit - dt); g.a += dt * 1.2f; g.x = B.x + Mathf.Cos(g.a) * 5f; g.z = B.z + Mathf.Sin(g.a) * 3f; g.y = y + B.relY - 5; }
                // 回りながら撒く
                if (B.spiral > 0)
                {
                    B.spiral -= dt; B.spT -= dt;
                    while (B.spT <= 0)
                    {
                        B.spT += ph == 3 ? 0.09f : 0.12f; B.spA += 0.5f;
                        float[] offs = ph == 3 ? new[] { 0f, 2.09f, 4.19f } : new[] { 0f, Mathf.PI };
                        foreach (float o in offs) BossShot(Mathf.Cos(B.spA + o) * 4.5f, Mathf.Sin(B.spA + o) * 3f);
                    }
                }
                B.lockMode = 0;
                if (B.mode == "windup")
                {
                    B.modeT += dt;
                    B.relY = Mathf.Lerp(24, 34, Smooth(B.modeT / 0.8f));
                    if (B.modeT < 1.1f) { B.rx = px; B.rz = pz; B.lockMode = 1; } else B.lockMode = 2;
                    int nb = Mathf.FloorToInt(B.modeT / 0.37f); if (nb > B.beeps) { B.beeps = nb; Beep(B.modeT > 1.1f); }
                    if (B.modeT > 1.5f) { B.mode = "rush"; B.modeT = 0; B.sx = B.x; B.sz = B.z; shake = 0.3f; se.Play(se.roar); }
                }
                else if (B.mode == "rush")
                {
                    B.modeT += dt; float k = Smooth(B.modeT / 0.7f);
                    B.relY = Mathf.Lerp(34, 5, k); B.x = Mathf.Lerp(B.sx, B.rx, k); B.z = Mathf.Lerp(B.sz, B.rz, k);
                    B.lockMode = B.modeT < 0.7f ? 2 : 0;
                    if (B.relY < 8 && !B.rushHit && inv <= 0 && Hyp(B.x - px, B.z - pz) < 3.2f) { B.rushHit = true; if (!UseCharm()) { Hurt(15); stun = 0.8f; } }
                    if (B.modeT > 0.8f)
                    {
                        B.rushHit = false;
                        if (--B.rushes > 0) { B.mode = "windup"; B.modeT = 0.4f; B.beeps = 1; }
                        else { B.mode = "dizzy"; B.modeT = 0; StunBoss(2.2f); PopAt(B.x, y + B.relY, B.z, "目を回した！　帽章をねらえ", ModelFactory.Hex(0xffd040), 28); }
                    }
                }
                else if (B.mode == "dizzy") { B.modeT += dt; B.relY = Mathf.Lerp(5, 14, Smooth(B.modeT / 0.6f)); if (B.modeT > 2.2f) { B.mode = "return"; B.modeT = 0; } }
                else if (B.mode == "return") { B.modeT += dt; B.relY = Mathf.Lerp(14, 24, Smooth(B.modeT)); if (B.modeT > 1) { B.mode = "idle"; B.cd = 1.2f; } }
                else
                {
                    B.relY = 24 + Mathf.Sin(B.t * 0.8f) * 2;
                    if (B.spiral <= 0 && B.stunT <= 0) { B.cd -= dt * sp; if (B.cd <= 0) BossAttack(); }
                }
            }
            // 与えた傷の数字は、まとめて出す（重ならないように）
            B.dmgT -= dt;
            if (B.dmg > 0 && B.dmgT <= 0)
            {
                B.dmgT = 0.9f;
                PopAt(B.x + R(-1, 1), y + B.relY + 2, B.z + 2.5f, B.weakHit ? $"−{B.dmg}　弱点！" : $"−{B.dmg}", B.weakHit ? ModelFactory.Hex(0xffd040) : Color.white, B.weakHit ? 32 : 26);
                B.dmg = 0; B.weakHit = false;
            }
            UpdateHands(dt);
            bossY = y + B.relY; bossX = B.x; bossZ = B.z;
            roarT -= dt; if (roarT <= 0) { roarT = R(7, 10); se.Play(se.roar); }
            if (bossHp <= 0)
            {
                over = true; endReason = "bossWin"; se.Play(se.kill); se.Play(se.roar);
                foreach (var h in B.hands) h.go.SetActive(false);
                foreach (var g in B.guards) g.go.SetActive(false);
                for (int i = 0; i < 6; i++) { int k = i; Later(i * 0.15f, () => Burst(bossX + R(-3, 3), bossY + R(-3, 3), bossZ + R(-2, 2), k % 2 == 1 ? 0xffffff : 0xff5040, 40, 10)); }
                Later(0.9f, () => bossM.root.SetActive(false));
                Banner("大警部を祓った！", "", ModelFactory.Hex(0xffd060), 2.4f); bgm.Stop(); Later(2.2f, () => Finish("bossWin"));
                return;
            }
            if (y <= START) { over = true; endReason = "gate"; Banner("門が開かない！", "", ModelFactory.Hex(0xff8a7a), 1.6f); Later(1.5f, () => Finish("gate")); }
        }
    }
}
