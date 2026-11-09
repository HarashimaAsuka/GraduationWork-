using UnityEngine;

public class Enemy
{   
    public string type;
    public GameObject go;
    public Renderer body;
    public GameObject arils;
    
    public float y0, y, yPrev, nx, nz, x, z, r, cd, ph, hit, deadT;
    
    public int hp, max;
    public bool alive = true,shooter, bumped, ripe, emerged;
}
