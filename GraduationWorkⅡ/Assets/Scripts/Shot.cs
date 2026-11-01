using UnityEngine;

namespace ShoryoumaTaxi
{
    /// <summary>敵の弾。type は "seed"（種）・"onibi"（追ってくる鬼火）・"drop"（雨粒）・"fire"（火の玉）。</summary>
    public class Shot
    {
        public GameObject go; public string type = "seed"; public float x, y, z, vx, vy, vz; public bool dead;
        public bool chase;   // 上（奥）から落ちてくる弾。プレイヤーより上にあるかで当たりを判定する
        public bool onibi => type == "onibi";
    }
}
