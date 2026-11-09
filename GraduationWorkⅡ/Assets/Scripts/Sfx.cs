using UnityEngine;

namespace ShoryoumaTaxi
{
    /// <summary>
    /// 効果音をコードで合成する（音声ファイル不要）。
    /// 本番の音に差し替えるときは、各 AudioClip をインスペクターから入れたものに置き換える。
    /// v31：塩は「シャッ」＋はじく高い音＋低い押し出し、当たりは「パキッ」。
    /// </summary>
    public class Sfx
    {
        readonly AudioSource src;
        public AudioClip salt, pop, hit, catchSilk, hurt, bell, swap, call;
        public AudioClip beep0, beep1, emerge, soulPop, thunder, firework;
        const int RATE = 44100;
        static readonly System.Random rng = new System.Random(7);

        public Sfx(GameObject host)
        {
            src = host.AddComponent<AudioSource>();
            src.playOnAwake = false;
            // 塩：シャッ（高いノイズ）＋ キン（三角波）＋ ドッ（低い正弦波）
            salt = Clip("salt", Mix(Hiss(0.07f, 0.22f, 3000, 12000), ToneData(1900, 900, 0.06f, Wave.Triangle, 0.14f), ToneData(220, 90, 0.05f, Wave.Sine, 0.16f)));
            // 当たり：パキッ
            hit = Clip("hit", Mix(Hiss(0.05f, 0.3f, 1500, 9000), ToneData(1400, 500, 0.08f, Wave.Square, 0.09f), ToneData(160, 70, 0.1f, Wave.Sine, 0.2f)));
            pop = Clip("pop", ToneData(420, 110, 0.2f, Wave.Triangle, 0.5f));
            catchSilk = Clip("catch", ToneData(660, 990, 0.1f, Wave.Sine, 0.4f));
            hurt = Clip("hurt", ToneData(200, 50, 0.3f, Wave.Saw, 0.4f));
            swap = Clip("swap", ToneData(900, 300, 0.35f, Wave.Saw, 0.3f));
            bell = Bell();
            call = Call();
            beep0 = Clip("beep0", ToneData(760, 760, 0.09f, Wave.Square, 0.13f));
            beep1 = Clip("beep1", ToneData(1100, 1100, 0.09f, Wave.Square, 0.13f));
            emerge = Clip("emerge", ToneData(300, 900, 0.12f, Wave.Square, 0.09f));
            soulPop = Clip("soulPop", ToneData(900, 1400, 0.1f, Wave.Sine, 0.12f));
            thunder = Clip("thunder", Mix(ToneData(80, 40, 0.6f, Wave.Saw, 0.07f), Hiss(0.6f, 0.08f, 60, 900)));
            firework = Clip("firework", ToneData(180, 60, 0.4f, Wave.Triangle, 0.08f));
        }

        public void Play(AudioClip c, float vol = 1f) { if (c != null) src.PlayOneShot(c, vol); }

        enum Wave { Sine, Square, Triangle, Saw }

        static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float[] ToneData(float f0, float f1, float dur, Wave w, float gain)
        {
            int n = Mathf.CeilToInt(RATE * dur);
            var data = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float f = f0 * Mathf.Pow(f1 / f0, t);          // 指数的に音程を変える
                phase += f / RATE;
                float p = (float)(phase - System.Math.Floor(phase));
                float s = w == Wave.Sine ? Mathf.Sin(p * Mathf.PI * 2f)
                        : w == Wave.Square ? (p < 0.5f ? 1f : -1f)
                        : w == Wave.Triangle ? 1f - 4f * Mathf.Abs(p - 0.5f)
                        : 2f * p - 1f;
                data[i] = s * gain * Mathf.Pow(1f - t, 2f);
            }
            return data;
        }

        /// <summary>ノイズを帯域で絞った「シャッ」。hp〜lp の間の音を残す（1次フィルター2つ）。</summary>
        static float[] Hiss(float dur, float gain, float hp, float lp)
        {
            int n = Mathf.CeilToInt(RATE * dur);
            var data = new float[n];
            float aL = Mathf.Exp(-2f * Mathf.PI * lp / RATE), aH = Mathf.Exp(-2f * Mathf.PI * hp / RATE);
            float low = 0, lowH = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float x = (float)(rng.NextDouble() * 2 - 1);
                low = (1 - aL) * x + aL * low;            // 高い音を切る
                lowH = (1 - aH) * low + aH * lowH;        // 低い音を取り出して引く
                data[i] = (low - lowH) * gain * Mathf.Pow(1f - t, 3f) * 2.2f;
            }
            return data;
        }

        static float[] Mix(params float[][] parts)
        {
            int n = 0; foreach (var p in parts) n = Mathf.Max(n, p.Length);
            var d = new float[n];
            foreach (var p in parts) for (int i = 0; i < p.Length; i++) d[i] += p[i];
            for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(d[i], -1f, 1f);
            return d;
        }

        /// <summary>声の知らせ：短い「ピピッ」</summary>
        static AudioClip Call()
        {
            int n = Mathf.CeilToInt(RATE * 0.17f), b = Mathf.CeilToInt(RATE * 0.07f), gap = Mathf.CeilToInt(RATE * 0.09f);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                int j = i < b ? i : i - gap;
                if (j < 0 || j >= b) continue;
                float t = (float)j / b, f = 1250f * Mathf.Pow(1500f / 1250f, t);
                data[i] = Mathf.Sin(2 * Mathf.PI * f * j / RATE) * 0.3f * (1 - t) * (i < b ? 1f : 0.8f);
            }
            return Clip("call", data);
        }

        static AudioClip Bell()
        {
            int n = RATE * 2;
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / RATE;
                float env = Mathf.Exp(-t * 2.2f);
                data[i] = (Mathf.Sin(2 * Mathf.PI * 880 * t) * 0.35f + Mathf.Sin(2 * Mathf.PI * 1320 * t) * 0.15f) * env;
            }
            return Clip("bell", data);
        }
    }
}
