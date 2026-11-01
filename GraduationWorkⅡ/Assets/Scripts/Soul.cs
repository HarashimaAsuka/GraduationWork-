using UnityEngine;
using ShoryoumaTaxi;

/// <summary>v32：配置表で置いた魂のお客さん。行き先・距離・運賃は乗せる前から決まっている。</summary>
public class Soul
{
    public GameObject go;
    public ModelFactory.Plate tag;  // 頭の上の札（行き先・距離・運賃）
    public float y0, y, yPrev, nx, nz, x, z, ph;
    public float destY; public string destName; public int jo, fare;
    public float emT = -9f;
    public bool emerged, gone;
    public GuestDef guest;
    public bool called;
}
