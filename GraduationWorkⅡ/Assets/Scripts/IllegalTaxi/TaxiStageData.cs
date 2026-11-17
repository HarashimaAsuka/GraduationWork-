using System.Collections.Generic;
using UnityEngine;

namespace ShoryoumaTaxi
{
    /// <summary>
    /// 1ステージぶんの配置と数字。メニュー「精霊馬タクシー > ステージデータを作る」で、今の4ステージぶんが作られる。
    /// IllegalTaxi の「Stage Assets」に順番に入れると、こちらが使われる（空なら、コードの中の値が使われる）。
    /// </summary>
    [CreateAssetMenu(menuName = "精霊馬タクシー/ステージデータ", fileName = "Stage")]
    public class TaxiStageData : ScriptableObject
    {
        [System.Serializable] public class SoulPos { public float y; [Range(0, 2)] public int rare; }
        [System.Serializable] public class CopPos { public float y; public CopType type; [Tooltip("横の位置。自動ならチェックを外す")] public bool fixedPos; public float x, z; }
        [System.Serializable] public class ItemPos { public float y; public ItemKind kind; }
        public enum CopType { 巡査, パト提灯 }
        public enum ItemKind { お供えの水, 追い風, お守り }

        [Header("基本")]
        public int no = 1;
        public string stageName = "研修";
        [Tooltip("目標の売上（文）。決戦では使わない")] public int goal = 20;
        [Tooltip("制限時間（秒）。0 で制限なし")] public float time = 200;
        [Tooltip("研修（下に説明を出す・ゆっくり見せる）")] public bool tutorial;
        [Tooltip("大警部との決戦（行きがなく、帰りだけ）")] public bool bossFight;
        [Tooltip("見た目：1 ふつう／2 夕暮れ／3 雨と稲光／4 赤い月（決戦）")] [Range(1, 4)] public int look = 1;

        [Header("速さ（10 = 1丈/秒）")]
        public float outSpeed = 16, homeSpeed = 12;

        [Header("大警部（ステージ1〜3の追いかけ）")]
        public float bossSpeed = 12.6f;
        [Tooltip("通報されたときの大警部との距離")] public float bossGap = 68;
        [Tooltip("ときどき一気に詰めてくる")] public bool bossLunge;
        [Tooltip("手錠を投げる間隔（秒）。0 で投げない")] public float bossAttack = 5.5f;
        [Tooltip("1回に投げる手錠の数（1 か 3）")] public int bossVolley = 1;
        [Tooltip("決戦の大警部の体力")] public int bossHp = 200;

        [Header("湧いてくる警察（秒ごと。0 で湧かない）")]
        public float waveOut = 6.5f;
        public float waveBack = 4f;
        public float firstWave = 3f;

        [Header("配置（高さ y は 30〜660。10 = 1丈）")]
        public List<SoulPos> souls = new List<SoulPos>();
        public List<CopPos> cops = new List<CopPos>();
        public List<ItemPos> items = new List<ItemPos>();
    }
}
