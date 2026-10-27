using System.Collections.Generic;
using UnityEngine;

namespace ShoryoumaTaxi
{
    /// <summary>v32：敵の波で出てくる敵（横切り cross・追い越し side・並走 escort・突っ込み drop）。y は relY（馬からの高さ）で決まる。</summary>
    public class Mover
    {
        public string type, pattern, wave; public GameObject go; public Renderer body; public GameObject arils;
        public float r, t, hit, cd, ph, relY, relPrev, x, z, y, yPrev;
        public float sx, bz, lane, holdY, hold, dart; public int side, slot, fired, hp, max, vol;
        public bool dead, gone, bumped, called, dartShot;
    }

    /// <summary>v32：道中のアイテム（水 water・追い風 wind・線香 incense・お守り charm・ナス nasu）。</summary>
    public class Pickup
    {
        public string kind; public float y, nx, nz, x, z, ph; public bool taken;
        public ModelFactory.ItemModel im; public Transform halo; public Renderer haloRd;
        public ModelFactory.Plate tag;
    }

    /// <summary>v32：親玉の左右に浮かぶ鬼の手。mode：idle → aim（叩きつけの狙い）→ slam → back、aimS（薙ぎ払いの狙い）→ sweep → back</summary>
    public class BossHand
    {
        public int sd, hp, beeps; public GameObject go; public Renderer glow;
        public bool dead, ring, band, swept;
        public float x, z, relY, y, t, tx, tz, sx, hit; public string mode = "idle";
    }

    /// <summary>v32：三の段で親玉のまわりを回る鬼灯の守り。</summary>
    public class BossGuard
    {
        public GameObject go; public float a, x, y, z, hit; public int hp; public bool dead;
    }

    /// <summary>v32：ステージ3の大ボス「鬼ザクロの親玉」。</summary>
    public class Boss
    {
        public ModelFactory.BossModel m;
        public BossHand[] hands; public BossGuard[] guards;
        public int hp, max, phase = 1, nextAtk, rushes, beeps, lockState;
        public float relY = 150, x, z = 2.6f, y, yPrev, t, cd = 1.5f, modeT, hit, intro, spiral, spT, spA, rx, rz, sx, sz, dyingT, stun, guardT;
        public string mode = "idle";
        public bool dead, shield, rushHit;
        public readonly List<KeyValuePair<float, string>> pend = new List<KeyValuePair<float, string>>();   // 少し遅れて撃つ弾（時刻, 撃ち方）
    }
}
