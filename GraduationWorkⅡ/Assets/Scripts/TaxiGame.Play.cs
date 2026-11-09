using UnityEngine;
using Random = UnityEngine.Random;   // System.Random と取り違えないように

namespace ShoryoumaTaxi
{
    /// <summary>乗せる・降ろす・ぶつかる・アイテムを取る（Web版 v32 の board / deliver / bump / takeItem など）。</summary>
    public partial class TaxiGame
    {
        void LaunchFlying(float x, float yy, float z, bool home, int rare)
        {
            var go = ModelFactory.Soul(soulPrefab, rare);
            float a = R(0, Mathf.PI * 2);
            var p = WP(x, yy, z);
            var f = new Flying { go = go, x = p.x, y = p.y, z = p.z, home = home };
            if (home) { f.vx = 0; f.vy = 3; f.vz = 2.5f; f.life = f.max = 2.2f; }
            else { f.vx = Mathf.Cos(a) * 9; f.vy = R(-2, 2); f.vz = Mathf.Abs(Mathf.Sin(a)) * 6 + 2; f.life = f.max = 1.4f; f.spin = 800; }
            flying.Add(f);
        }

        /// <summary>魂に触れた：乗車。5人まで。満席なら、いちばん前のお客さんが押し出される。</summary>
        void Board(Soul s)
        {
            s.gone = true; s.tag.root.SetActive(false);
            Burst(s.x, s.y, s.z, 0x9fe4ff, 22, 3.5f);
            if (riders.Count >= MAX_RIDERS)
            {
                var old = riders[0]; riders.RemoveAt(0);
                LaunchFlying(px, y + 1, pz, false, old.guest.rare);
                thrown++; sfx.Play(sfx.hurt); fineFlash = 1.2f;
                Say($"満席！　{old.guest.name}が押し出されてしまった。", 2.6f);
            }
            else sfx.Play(sfx.catchSilk);
            riders.Add(new Goal { y = s.destY, goalName = s.destName, jo = s.jo, fare = s.fare, guest = s.guest });
            var g = s.guest;
            string star = g.rare == 1 ? "★珍しいお客さん★ " : g.rare == 2 ? "★★とても珍しいお客さん★★ " : "";
            Say($"{star}{g.name}「{g.hello}」", 2.4f);
            Say($"「{s.destName}まで」　{(s.destY > y ? "▲前" : "▼後ろ")}へ {s.jo}丈・{(g.tanuki ? "？？" : s.fare.ToString())}文　乗車 {riders.Count}/{MAX_RIDERS}", 3f);
            if (flags.Add("boardTip")) Say($"乗せるほど遅くなる（1人で −{Mathf.RoundToInt(SLOW_PER * 100)}%）。行き先の門をくぐると降りる。", 3.6f);
        }

        /// <summary>行き先の高さを通った：降りて運賃。</summary>
        void Deliver(Goal r)
        {
            riders.Remove(r);
            int fare = r.fare; string note = "";
            var g = r.guest;
            if (g.tanuki)   // 化け狸：半分の確率で木の葉、半分で3倍
            {
                if (Random.value < 0.5f) { fare = 0; note = "……と思ったら木の葉だった！"; }
                else { fare *= 3; note = "本物の小判だ！ 3倍！"; }
            }
            delivered++; mon += fare; purse += fare;
            AddBook(g.id); SaveProfile();
            water = Mathf.Min(100, g.fill ? 100 : water + 15);
            LaunchFlying(px, y + 1, pz, true, g.rare);
            Burst(px, y, pz, 0xffd27a, 40, 5);
            sfx.Play(sfx.bell);
            Say($"{g.name}「{g.thanks}」{note}", 2.6f);
            Say($"{r.goalName}に到着。{r.jo}丈で {fare}文　水分 {(g.fill ? "満タン" : "+15")}", 2.6f);
            if (st.quota > 0 && delivered >= st.quota && clearT == 0)
            {
                clearT = 0.001f;
                Say($"ノルマ達成！　ステージ{st.no} クリア", 3f);
            }
            else if (st.quota > 0) Say($"のこり {st.quota - delivered} 人", 1.8f);
        }

        /// <summary>道の端に着いた：自分で折り返す（ステージ2は反転しているので上下が入れ替わる）。</summary>
        void TurnAround(bool top)
        {
            want = -want;
            if (Road.Flip) top = !top;
            if (top)
            {
                water = Mathf.Min(100, water + 20);
                Say("現世の井戸の口。お供えの水をもらって、後ろへ折り返す。　水分 +20", 3f); sfx.Play(sfx.bell);
            }
            else Say($"{CoName()}タクシー地獄営業所の門。前へ折り返す。", 2.6f);
        }

        void TakeItem(Pickup it)
        {
            it.taken = true; it.im.root.SetActive(false); it.tag.root.SetActive(false);
            Burst(it.x, it.y, it.z, ItemHex(it.kind), 26, 4); sfx.Play(sfx.catchSilk);
            switch (it.kind)
            {
                case "water": water = Mathf.Min(100, water + 30); break;
                case "wind": wind = 5; shake = 0.25f; break;
                case "incense": timeLeft += 10; break;
                case "charm": charmOn = true; break;
                case "nasu":
                    // v32：ナス：取ると向きが反転する。前向きなら後ろ向きに（ナスの牛になる）、後ろ向きなら前向きに戻る。お客さんは落ちない
                    want = -want; flips++;
                    shake = 0.3f; Burst(px, y, pz, 0xb48cff, 40, 5);
                    Say(want < 0 ? "ナスを取った！　ナスの牛になって、後ろ向きに進む。" : "ナスを取った！　前向きに戻った。", 2.2f);
                    return;
            }
            Say($"{ItemName(it.kind)}を取った。{ItemText(it.kind)}", 2.2f);
        }

        /// <summary>実際に進む向きが変わった瞬間。</summary>
        void Flip(int nd)
        {
            dir = nd;
            if (nd > 0) ClearChasers();
            chaseCD = 1.2f;
            foreach (var b in salts) Destroy(b.go); salts.Clear();
            air.Clear();
            foreach (var e in enemies) { e.bumped = false; e.emerged = false; e.called = false; }
            foreach (var s in souls) { s.emerged = false; s.called = false; }
            if (nd < 0 && flags.Add("backTip"))
            {
                Say("後ろへ進んでいる。奥からも、カメラの後ろからも敵が来る。", 3.4f);
                Say(hint == Hint.Mirror ? "後ろ（手前）から来るものは、上のバックミラーに映る。"
                  : hint == Hint.Both ? "後ろ（手前）から来るものは、地獄の火の影とバックミラーで分かる。"
                  : hint == Hint.Ring ? "後ろ（手前）から来るものは、光る輪が通る場所を教えてくれる。"
                  : "後ろ（手前）から来るものは、地獄の火に照らされた影で分かる。", 3.8f);
            }
        }

        /// <summary>弾に当たった。v22：後ろ向きのときは、弾に当たっても元の向き（前）に戻る。</summary>
        void HurtShot(float amount)
        {
            if (inv > 0 || over) return;
            water -= amount; inv = 0.8f; hurtT = 0.35f; shake = 0.25f;
            Burst(px, y, pz, 0x6cc4e8, 14, 3); sfx.Play(sfx.hurt);
            if (want < 0 && clearT == 0)
            {
                want = 1; flips++;
                if (flags.Add("shotFlipTip")) Say("後ろ向きのときは、弾に当たっても前向きに戻る。", 2.6f);
                else Say("当たって、前向きに戻った！", 1.4f);
            }
        }

        /// <summary>体当たりされた：向きが反転して、お客さんを1人落とす。もう一度ぶつかると元に戻る。</summary>
        void Bump()
        {
            if (inv > 0 || over || clearT > 0) return;
            if (charmOn)
            {
                charmOn = false; inv = 1.0f; Burst(px, y, pz, 0xffe08a, 22, 3); sfx.Play(sfx.catchSilk);
                Say("厄除けのお守りが守ってくれた。向きもお客さんもそのまま。", 2.6f);
                return;
            }
            water -= 10; inv = 1.3f; hurtT = 0.35f; shake = 0.35f;
            Burst(px, y, pz, 0x6cc4e8, 16, 3); sfx.Play(sfx.hurt);
            want = -want; flips++;
            if (riders.Count > 0)
            {
                var r = riders[riders.Count - 1]; riders.RemoveAt(riders.Count - 1);
                LaunchFlying(px, y + 1, pz, false, r.guest.rare);
                thrown++; fineFlash = 1.2f;
                Say($"ぶつかった！　{r.guest.name}が落ちてしまった。", 2.6f);
            }
            else Say("ぶつかった！", 1.6f);
            if (flags.Add("flipTip"))
                Say(want < 0 ? "向きが反転して、後ろへ進む。もう一度ぶつかると元の向きに戻る。" : "元の向きに戻った。", 3.6f);
        }

        void Kill(Enemy e)
        {
            e.alive = false; e.go.SetActive(false); e.deadT = 0; kills++;
            Burst(e.x, e.y, e.z, KCol(e.type), 26, 4);
            Burst(e.x, e.y, e.z, Color.white, 12, 3);
            sfx.Play(sfx.pop);
        }

        void Ripen(Enemy e)
        {
            e.ripe = true;
            if (e.type == "zakuro" && e.body != null && zakuroPrefab == null) e.body.sharedMaterial = ModelFactory.ZakuroRipeMat;
        }

        void Revive(Enemy e)
        {
            e.alive = true; e.hp = e.max; e.ripe = e.emerged = e.bumped = false; e.y = e.yPrev = e.y0;
            if (e.type == "zakuro" && zakuroPrefab == null && e.body != null) { e.body.sharedMaterial = ModelFactory.ZakuroMat; if (e.arils != null) e.arils.SetActive(false); }
        }

        void ClearChasers()
        {
            foreach (var c in chasers) { Destroy(c.go); if (c.trail != null) foreach (var s in c.trail) Destroy(s); }
            chasers.Clear();
        }
    }
}
