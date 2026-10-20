using System.Collections.Generic;
using UnityEngine;

namespace ShoryoumaTaxi
{
    /// <summary>
    /// 塩と敵の弾の生成・管理を担当する。
    /// Shot と Salt は MonoBehaviour を継承しないデータクラス。
    /// </summary>
    public class ProjectileManager
    {
        public List<Shot> Shots { get; } = new List<Shot>();
        public List<Salt> Salts { get; } = new List<Salt>();

        /// <summary>
        /// プレイヤーが投げる塩を生成する。
        /// </summary>
        public Salt CreateSalt(float px, float y, float pz, int dir)
        {
            GameObject go = GameObject.CreatePrimitive(
                PrimitiveType.Cube);

            Object.Destroy(go.GetComponent<Collider>());

            go.GetComponent<Renderer>().sharedMaterial =
                ModelFactory.SaltMat;

            go.transform.localScale = Vector3.one * 0.16f;

            var salt = new Salt
            {
                go = go,
                x = px + Random.Range(-0.05f, 0.05f),
                y = y + dir * 0.8f,
                z = pz + Random.Range(-0.05f, 0.05f),
                yp = y,
                rot = Random.Range(0f, 360f)
            };

            Salts.Add(salt);
            return salt;
        }

        /// <summary>
        /// 敵の種類に応じて弾を生成する。
        /// </summary>
        public void CreateEnemyShots(
            Enemy e,
            float d,
            float V,
            int dir,
            float px,
            float pz)
        {
            void Aim(float vs, float ox, float oz, bool onibi)
            {
                float time = d / (vs + V);
                float lead = e.ripe ? 0.85f : 0.55f;

                GameObject go = GameObject.CreatePrimitive(
                    PrimitiveType.Sphere);

                Object.Destroy(go.GetComponent<Collider>());

                go.GetComponent<Renderer>().sharedMaterial =
                    onibi
                        ? ModelFactory.OnibiMat
                        : ModelFactory.SeedMat;

                go.transform.localScale =
                    Vector3.one * (onibi ? 0.7f : 0.4f);

                Shots.Add(new Shot
                {
                    go = go,
                    onibi = onibi,
                    x = e.x,
                    y = e.y,
                    z = e.z,
                    vx = (px + ox - e.x) / time * lead,
                    vy = -dir * vs,
                    vz = (pz + oz - e.z) / time * lead
                });
            }

            if (e.type == "zakuro")
            {
                Aim(9f, 0f, 0f, false);
            }
            else if (e.type == "hozuki")
            {
                Aim(7f, 0f, 0f, true);
            }
            else
            {
                Aim(9f, 0f, 0f, false);
                Aim(9f, -2.2f, 0.6f, false);
                Aim(9f, 2.2f, -0.6f, false);
            }
        }

        /// <summary>
        /// 管理中の弾と塩をすべて破棄する。
        /// </summary>
        public void ClearAll()
        {
            foreach (var shot in Shots)
            {
                if (shot.go != null)
                    Object.Destroy(shot.go);
            }

            foreach (var salt in Salts)
            {
                if (salt.go != null)
                    Object.Destroy(salt.go);
            }

            Shots.Clear();
            Salts.Clear();
        }
    }
}