using UnityEngine;
using ShoryoumaTaxi;

/// <summary>v32：乗っているお客さん1人ぶん（5人まで）。y が行き先の高さ。</summary>
public class Goal
{
    public float y;
    public string goalName;
    public int jo, fare;
    public GuestDef guest;
}
