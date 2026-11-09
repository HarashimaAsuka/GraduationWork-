using UnityEngine;

/// <summary>道に置いてある敵（v32：唐傘お化け・提灯お化けも）。座標 x,z は道の中心からのずれ。</summary>
public class Enemy
{
    public string type;
    public GameObject go;
    public Renderer body;
    public GameObject arils;

    public float y0, y, yPrev, nx, nz, x, z, r, cd, ph, hit, deadT;
    public float emT = -9f;   // v32：後ろ向きのとき、カメラの前に飛び出した時刻

    public int hp, max, vol;  // vol：撃った回数（撃ち方の切り替えに使う）
    public bool alive = true, shooter, bumped, ripe, emerged;
    public bool called;
}
