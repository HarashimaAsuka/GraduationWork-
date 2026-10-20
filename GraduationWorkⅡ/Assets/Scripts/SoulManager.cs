using System.Collections.Generic;
using UnityEngine;

namespace ShoryoumaTaxi
{
    public partial class TaxiGame
    {
        void SpawnSoul(float yy)
            {
                float a = R(0, Mathf.PI * 2), r = Mathf.Sqrt(Random.value) * 0.8f;
                var s = new Soul { go = ModelFactory.Soul(soulPrefab), y0 = yy, y = yy, yPrev = yy, nx = Mathf.Cos(a) * r, nz = Mathf.Sin(a) * r, ph = R(0, 6) };
                s.go.SetActive(false);
                souls.Add(s);
            }

            void LaunchFlying(Vector3 p, bool home)
            {
                var go = ModelFactory.Soul(soulPrefab);
                float a = R(0, Mathf.PI * 2);
                var f = new Flying { go = go, x = p.x, y = p.y, z = p.z, home = home };
                if (home) { f.vx = 0; f.vy = 3; f.vz = 2.5f; f.life = f.max = 2.2f; }
                else { f.vx = Mathf.Cos(a) * 9; f.vy = R(-2, 2); f.vz = Mathf.Abs(Mathf.Sin(a)) * 6 + 2; f.life = f.max = 1.4f; f.spin = 800; }
                flying.Add(f);
            }

            /// <summary>魂に触れた：乗車。乗せていた客がいれば飛ばされる。向きはお客さんしだい。</summary>
            void Board(Soul s)
            {
                s.gone = true;
                Burst(new Vector3(s.x, s.y, s.z), new Color(0.62f, 0.9f, 1f), 20, 3.5f);
                if (carrying)
                {
                    LaunchFlying(new Vector3(px, y + 1, pz), false);
                    thrown++; sfx.Play(sfx.swap);
                    Say("「ひゃあっ！」乗っていたお客さんが飛ばされた！", 2.8f);
                }
                else sfx.Play(sfx.catchSilk);
                carrying = true; steed.rider.SetActive(true);
            
                float roomUp = L - 45 - y, roomDown = y - 45;
                var can = new List<int>();
                if (roomUp > 220) can.Add(1);
                if (roomDown > 220) can.Add(-1);
                int nd = can.Count == 2 ? (Random.value < 0.5f ? want : -want) : can.Count == 1 ? can[0] : -want;

                float gy;
                if (nd < 0) { float hi = y - 200; if (hi < 50) hi = y - 40; gy = R(Mathf.Max(45, y - 600), Mathf.Max(46, hi)); }
                else { float lo = y + 200; if (lo > L - 50) lo = y + 40; gy = R(Mathf.Min(L - 46, lo), Mathf.Min(L - 45, y + 600)); }
                var zone = STOPS[Mathf.Clamp(gy / L < 0.2f ? 0 : gy / L < 0.45f ? 1 : gy / L < 0.7f ? 2 : 3, 0, 3)];
                goal = new Goal { y = gy, goalName = zone[Random.Range(0, zone.Length)] };
                gateLabel.text = "行き先\n" + goal.goalName;

                bool turned = nd != want;
                want = nd;
                Say($"「{goal.goalName}まで、お願いします」 {(turned ? "折り返して" : "このまま")}{(nd > 0 ? "上へ" : "下へ")}", 3.4f);
                if (flags.Add("boardTip"))
                {
                    Say("そのまま進むか、折り返すかは、お客さんしだい。", 3f);
                    Say("乗せている間は、ほかの魂に触れないように。", 3.6f);
                }
            }
            /// <summary>行き先に着いた：運賃をもらって折り返す。</summary>
            void Deliver()
            {
                delivered++; mon += fare;
                water = Mathf.Min(100, water + 15);
                LaunchFlying(new Vector3(px, y + 1, pz), true);
                Burst(new Vector3(px, y, pz), new Color(1f, 0.82f, 0.48f), 36, 5f);
                carrying = false; steed.rider.SetActive(false); goal = null;
                want = -want;
                sfx.Play(sfx.bell);
                Say("「ありがとう。六文、置いていくよ」 水分 +15", 3f);
                if (flags.Add("backTip")) Say("折り返して、来た道の魂を拾いに行こう。", 3.2f);
                if (delivered >= goalCount) { clearT = 0.001f; Say("今夜の営業、おしまい。", 3f); }
            }

            void TurnAround(bool top)
            {
                want = -want;
                if (top) { water = Mathf.Min(100, water + 20); Say("現世の井戸の口。お供えの水をもらって折り返す。 水分 +20", 3.2f); sfx.Play(sfx.bell); }
                else Say("地獄の門だ。折り返す。", 2.6f);
            }
    }
}