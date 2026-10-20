using UnityEngine;
using ShoryoumaTaxi;

public class Soul
{
    public GameObject go; public float y0, y, yPrev, nx, nz, x, z, ph; public bool emerged, gone;
    public GuestDef guest;   // v18：お客さんの種類
    public bool called;      // v18：声の知らせで、もう知らせたか
}
