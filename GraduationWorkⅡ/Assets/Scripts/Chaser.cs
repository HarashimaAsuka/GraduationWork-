using UnityEngine;

/// <summary>v32：後ろ向きのとき、道の奥（背景の中）から赤い尾を引いて飛んでくる敵。</summary>
public class Chaser
{
    public string type; public GameObject go, aura, core; public Renderer auraRd, coreRd;
    public GameObject[] trail; public Renderer[] trailRd;
    public readonly System.Collections.Generic.List<Vector3> hist = new System.Collections.Generic.List<Vector3>();
    public float histT, start, sx, sz, tx, tz, weave;
    public bool far = true;
    public float x, y, yPrev, z, r, close, cd, hit, ph; public int hp, max, vol; public bool bumped, called, dead;
}
