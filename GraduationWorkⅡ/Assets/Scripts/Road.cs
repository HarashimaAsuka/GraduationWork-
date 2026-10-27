using UnityEngine;

namespace ShoryoumaTaxi
{
    /// <summary>
    /// v32：曲がる道。道の中心は高さ y によって左右（x）と画面の上下（z）にゆれる。
    /// 遊びの座標（x,z）は「道の中心からのずれ」で持ち、表示するときに中心を足す。
    /// ステージ2（帰り）は世界を上下反転するので、遊びの側の道すじも反転する（Flip）。
    /// </summary>
    public static class Road
    {
        public static float L = 2200f;
        public static bool Flip;

        /// <summary>景色を置いたときの道すじ（反転しない）</summary>
        public static float CxB(float y) => 16f * Mathf.Sin(y / 150f) + 7f * Mathf.Sin(y / 61f + 1.3f);
        public static float CzB(float y) => 6f * Mathf.Sin(y / 120f + 2.1f) + 3f * Mathf.Sin(y / 47f);

        /// <summary>遊びの側の道すじ（ステージ2では反転）</summary>
        public static float Cx(float y) => CxB(Flip ? L - y : y);
        public static float Cz(float y) => CzB(Flip ? L - y : y);

        /// <summary>道の中心からのずれ (x,z) と高さ y → ワールド座標</summary>
        public static Vector3 WP(float x, float y, float z) => new Vector3(Cx(y) + x, y, Cz(y) + z);

        /// <summary>見た目の高さ（0 = 地獄、1 = 現世）。ステージ2は進むほど地獄へ</summary>
        public static float Zf(float y) => Mathf.Clamp01(Flip ? 1f - y / L : y / L);

        /// <summary>遊びの高さ → 景色の中の高さ（反転のぶんを戻す）</summary>
        public static float ToLocal(float y) => Flip ? L - y : y;
    }
}
