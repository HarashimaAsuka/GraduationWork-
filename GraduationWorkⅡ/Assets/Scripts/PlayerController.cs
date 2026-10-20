using UnityEngine;

namespace ShoryoumaTaxi
{
    /// <summary>
    /// 精霊馬の移動入力、移動範囲の制限、位置と向きを管理する。
    /// TaxiGame から呼び出して使用する。
    /// </summary>
public class PlayerController
    {
         private readonly ModelFactory.Steed steed;

        public PlayerController(ModelFactory.Steed steed)
        {
            this.steed = steed;
        }

        /// <summary>
        /// 入力に応じて馬を移動させる。
        /// px、pz は TaxiGame 側のプレイヤー座標。
        /// </summary>
        public void Move(
            ref float px,
            ref float pz,
            Camera cam,
            float y,
            bool mouseFollow,
            bool go,
            float dt,
            float rx,
            float rz)
        {
            Vector2 mv = GameInput.Move();
            float sp = go ? 6.5f : 3.8f;

            Vector3 d3 =
                (cam.transform.right * mv.x +
                 cam.transform.up * mv.y) * sp * dt;

            px += d3.x;
            pz += d3.z;

            // マウス追従が有効で、キー入力がない場合
            if (mouseFollow && mv == Vector2.zero)
            {
                var ray = cam.ScreenPointToRay(
                    GameInput.MousePosition());

                var plane = new Plane(
                    Vector3.up,
                    new Vector3(0, y, 0));

                if (plane.Raycast(ray, out float enter))
                {
                    var hit = ray.GetPoint(enter);

                    float dx = hit.x - px;
                    float dz = hit.z - pz;
                    float d = Mathf.Sqrt(dx * dx + dz * dz);

                    float cap = (go ? 9f : 4.8f) * dt;

                    if (d > 0.01f)
                    {
                        float q = Mathf.Min(1f, cap / d);
                        px += dx * q;
                        pz += dz * q;
                    }
                }
            }

            // 楕円形の移動範囲から出ないようにする
            float distance = Mathf.Sqrt(
                (px / rx) * (px / rx) +
                (pz / rz) * (pz / rz));

            if (distance > 1f)
            {
                px /= distance;
                pz /= distance;
            }
        }

        /// <summary>
        /// 馬の位置と向きを更新する。
        /// </summary>
        public Transform UpdatePose(
            float px,
            float y,
            float pz,
            int dir,
            float vel,
            float step,
            float t,
            float bk)
        {
            bool go = dir > 0;
            float bks = Mathf.SmoothStep(0f, 1f, bk);

            float bob = go
                ? Mathf.Sin(step * 2f) * 0.1f
                : Mathf.Sin(t * 1.6f) * 0.15f;

            Transform tr = steed.root.transform;
            tr.position = new Vector3(px, y + bob, pz);

            Vector3 forward = Vector3.up;
            Vector3 up = Vector3.forward;

            Quaternion qUp = Quaternion.LookRotation(
                (0.7f * forward + 0.71f * up).normalized,
                (-0.71f * forward + 0.7f * up).normalized);

            Quaternion qDown = Quaternion.LookRotation(
                (-0.9f * forward - 0.42f * up).normalized,
                (-0.42f * forward + 0.9f * up).normalized);

            tr.rotation = Quaternion.Slerp(qUp, qDown, bks);

            if (go && Mathf.Abs(vel) > 3f)
            {
                tr.Rotate(
                    Mathf.Sin(step) * 3.5f,
                    0f,
                    0f,
                    Space.Self);
            }
            else if (!go)
            {
                tr.Rotate(
                    Mathf.Sin(t * 0.9f) * 6f,
                    0f,
                    Mathf.Sin(t * 1.3f) * 12f,
                    Space.Self);
            }

            return tr;
        }
    }
}