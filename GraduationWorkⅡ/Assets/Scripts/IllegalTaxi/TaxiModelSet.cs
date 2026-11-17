using System.Collections.Generic;
using UnityEngine;

namespace ShoryoumaTaxi
{
    /// <summary>
    /// モデルの差し替え。空の欄は仮モデル（コードで作る形）が使われる。
    /// Project で右クリック →「Create > 精霊馬タクシー > モデルの差し替え」で作り、IllegalTaxi の「Models」欄に入れる。
    /// 正面は +Z（カメラの方）を向けて作る。大きさは仮モデルに合わせて Scale で調整する。
    /// </summary>
    [CreateAssetMenu(menuName = "精霊馬タクシー/モデルの差し替え", fileName = "TaxiModels")]
    public class TaxiModelSet : ScriptableObject
    {
        [Header("警察")]
        public GameObject junsa;
        public GameObject patochin;
        [Tooltip("大警部の顔（倒せない追っ手・決戦のボス）")] public GameObject boss;
        [Tooltip("決戦の白手袋（左手で作る。右手は自動で左右反転）")] public GameObject glove;
        [Header("弾・拾うもの")]
        public GameObject pellet;
        public GameObject cuffs;
        public GameObject coin;
        public GameObject itemWater, itemWind, itemCharm;
        [Header("大きさ（仮モデルと揃えるとき）")]
        public float scale = 1f;
    }
}
