using System.Collections.Generic;
using UnityEngine;

namespace ShoryoumaTaxi
{
    /// <summary>
    /// 調整用の数値（Web版の調整パネルと同じ【】の名前）。
    /// Project で右クリック →「Create > 精霊馬タクシー > 調整用の数値」で作り、IllegalTaxi の「Tuning」欄に入れる。
    /// 再生中にインスペクターで数値を変えると、その場で反映される（再生をやめても数値は残る）。
    /// </summary>
    [CreateAssetMenu(menuName = "精霊馬タクシー/調整用の数値", fileName = "TaxiTuning")]
    public class TaxiTuning : ScriptableObject
    {
        [Header("速さ")]
        [Tooltip("【行きの速さ倍率】")] public float outSpeedMul = 1f;
        [Tooltip("【帰りの速さ倍率】")] public float homeSpeedMul = 1f;
        [Tooltip("【乗客1人ごとの減速】0.06 なら1人で6%遅くなる")] public float slowPerRider = 0.06f;
        [Tooltip("【追い風の倍率】")] public float windMul = 2.6f;
        [Tooltip("【追い風の秒数】")] public float windSec = 4f;

        [Header("大警部・警察")]
        [Tooltip("【大警部の速さ倍率】ステージ1〜3の追いかけ")] public float bossSpeedMul = 1f;
        [Tooltip("【敵の攻撃間隔倍率】大きいほど撃ってこない")] public float enemyAttackMul = 1f;
        [Tooltip("【体当たりで落とす運賃】")] public int ramDropFare = 10;

        [Header("車掌のスリ（警察のすぐ横をすれ違うと小銭をスる）")]
        [Tooltip("【スリの届く距離】")] public float suriRange = 2.0f;
        [Tooltip("【スリの運賃】0 にするとスリはなくなる")] public int suriFare = 2;

        [Header("そのほか")]
        [Tooltip("【制限時間倍率】")] public float timeMul = 1f;
        [Tooltip("【塩1発の水分】")] public float saltWater = 0.25f;
        [Tooltip("【ヒットストップ倍率】0 で止まらない")] public float hitstopMul = 1f;
        [Tooltip("【画面の揺れ倍率】0 で揺れない")] public float shakeMul = 1f;
    }
}
