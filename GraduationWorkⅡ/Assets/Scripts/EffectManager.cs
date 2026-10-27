
using System.Collections.Generic;
using UnityEngine;

namespace ShoryoumaTaxi
{
    public class EffectManager
    {
        // 火花の一覧
        private readonly List<Spark> sparks = new List<Spark>();

        // マテリアルの色を変更するための設定
        private readonly MaterialPropertyBlock mpb =
            new MaterialPropertyBlock();

        private readonly Material sparkMat;

        public EffectManager(Material sparkMat)
        {
            this.sparkMat = sparkMat;

            // 火花オブジェクトを初期化
            for (int i = 0; i < 260; i++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Cube);

                Object.Destroy(s.GetComponent<Collider>());

                s.GetComponent<Renderer>().sharedMaterial = sparkMat;
                s.SetActive(false);

                sparks.Add(new Spark
                {
                    tr = s.transform,
                    rd = s.GetComponent<Renderer>()
                });
            }
        }

        // 火花を生成
        public void Burst(Vector3 p, Color c, int n, float sp)
        {
            int made = 0;

            foreach (var s in sparks)
            {
                if (made >= n) break;
                if (s.life > 0) continue;

                s.p = p;
                s.v = Random.onUnitSphere * Random.Range(0.4f, 1f) * sp;
                s.life = s.max = Random.Range(0.4f, 1f);
                s.c = c;

                s.tr.gameObject.SetActive(true);
                made++;
            }
        }

        // 火花を更新
        public void UpdateSparks(float dt)
        {
            foreach (var s in sparks)
            {
                if (s.life <= 0) continue;

                s.life -= dt;
                s.p += s.v * dt;
                s.v *= Mathf.Pow(0.08f, dt);

                if (s.life <= 0)
                {
                    s.tr.gameObject.SetActive(false);
                    continue;
                }

                float a = s.life / s.max;

                s.tr.position = s.p;
                s.tr.localScale = Vector3.one * 0.12f * a;

                mpb.Clear();
                mpb.SetColor("_BaseColor", s.c);
                mpb.SetColor("_Color", s.c);

                s.rd.SetPropertyBlock(mpb);
            }
        }
    }
}
