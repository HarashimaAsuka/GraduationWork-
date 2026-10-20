using UnityEngine;

/// <summary>v18：「追われる」モードの追っ手。上（奥）から牛より速く落ちてくる。</summary>
public class Chaser
{
    public string type; public GameObject go, aura; public Renderer auraRd;
    public float x, y, yPrev, z, r, close, cd, hit, ph; public int hp, max; public bool bumped, called, dead;
}
