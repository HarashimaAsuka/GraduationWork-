using UnityEngine;

namespace ShoryoumaTaxi
{
    public partial class TaxiGame
    {
        // PickType()
        // MakeEnemies()
        // Ripen()
        // Revive()
        // Kill()
    string PickType(float f)
            {
                float wz = 5f, wh = f > 0.2f ? 2.2f : 1.2f, wt = f > 0.28f ? 0.7f : 0f;
                float r = R(0, wz + wh + wt);
                if (r < wz) return "zakuro";
                if (r < wz + wh) return "hozuki";
                return "tongue";
            }

            void MakeEnemies()
            {
                for (float h = 70; h < L - 50; h += R(14, 20))
                {
                    int n = Random.value < 0.15f ? 2 : 1;
                    for (int i = 0; i < n; i++)
                    {
                        string type = PickType(h / L);
                        var e = new Enemy { type = type, y0 = h + i * 3 };
                        e.y = e.yPrev = e.y0;
                        float a = R(0, Mathf.PI * 2), r = Mathf.Sqrt(Random.value) * 0.85f;
                        e.nx = Mathf.Cos(a) * r; e.nz = Mathf.Sin(a) * r;
                        e.cd = R(0.4f, 2f); e.ph = R(0, 6);
                        if (type == "zakuro") { e.go = ModelFactory.Zakuro(zakuroPrefab, out e.body, out e.arils); e.hp = 2; e.r = 1.0f; e.shooter = Random.value < 0.5f; }
                        else if (type == "hozuki") { e.go = ModelFactory.Hozuki(hozukiPrefab); e.hp = 1; e.r = 0.9f; e.shooter = true; }
                        else { e.go = ModelFactory.Tongue(tonguePrefab); e.hp = 5; e.r = 1.5f; e.shooter = true; }
                        e.max = e.hp;
                        e.go.SetActive(false);
                        enemies.Add(e);
                    }
                }
            }

            void Ripen(Enemy e)
            {
                e.ripe = true;
                if (e.type == "zakuro" && e.body != null && zakuroPrefab == null) e.body.sharedMaterial = ModelFactory.ZakuroRipeMat;
            }

            void Revive(Enemy e)
            {
                e.alive = true; e.hp = e.max; e.ripe = e.emerged = e.bumped = false; e.y = e.yPrev = e.y0;
                if (e.type == "zakuro" && zakuroPrefab == null) { e.body.sharedMaterial = ModelFactory.ZakuroMat; e.arils.SetActive(false); }
            }

            void Kill(Enemy e)
            {
                e.alive = false; e.go.SetActive(false); e.deadT = 0; kills++;
                effectManager.Burst(new Vector3(e.x, e.y, e.z), e.type == "hozuki" ? new Color(1f, 0.55f, 0.2f) : new Color(1f, 0.2f, 0.35f), 22, 4f);
                effectManager.Burst(new Vector3(e.x, e.y, e.z), Color.white, 10, 3f);
                sfx.Play(sfx.pop);
            }
    }    
}