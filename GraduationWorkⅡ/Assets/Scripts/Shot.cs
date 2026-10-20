using UnityEngine;

namespace ShoryoumaTaxi
{
    public class Shot
    {
        public GameObject go; public bool onibi; public float x, y, z, vx, vy, vz; public bool dead;
        public bool chase;   // v18：「追われる」モードの追っ手が撃った弾（上から落ちてくる）
    }

}
