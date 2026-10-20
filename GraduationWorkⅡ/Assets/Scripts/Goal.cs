using UnityEngine;
using ShoryoumaTaxi;

public class Goal
{
    public float y;
    public string goalName;
    public int jo, fare;     // v18：乗車距離（丈）と運賃（文）
    public GuestDef guest;   // v18：乗せているお客さんの種類
}