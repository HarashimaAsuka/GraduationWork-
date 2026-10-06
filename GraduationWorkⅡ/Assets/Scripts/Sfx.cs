using UnityEngine;

namespace ShoryoumaTaxi
{
    /// <summary>
    /// 効果音をコードで合成する（音声ファイル不要）。
    /// 本番の音に差し替えるときは、各 AudioClip をインスペクターから入れたものに置き換える。
    /// </summary>
    public class Sfx
    {
        readonly AudioSource src;
        public AudioClip salt, pop, hit, catchSilk, hurt, bell, swap;

        public Sfx(GameObject host)
        {
            src = host.AddComponent<AudioSource>();
            src.playOnAwake = false;
            salt = Tone(2400, 1800, 0.04f, Wave.Square, 0.12f);
            pop = Tone(420, 110, 0.2f, Wave.Triangle, 0.5f);
            hit = Tone(900, 700, 0.05f, Wave.Sine, 0.25f);
            catchSilk = Tone(660, 990, 0.1f, Wave.Sine, 0.4f);
            hurt = Tone(200, 50, 0.3f, Wave.Saw, 0.4f);
            swap = Tone(900, 300, 0.35f, Wave.Saw, 0.3f);
            bell = Bell();
        }

        public void Play(AudioClip c, float vol = 1f) { if (c != null) src.PlayOneShot(c, vol); }

        enum Wave { Sine, Square, Triangle, Saw }

        static AudioClip Tone(float f0, float f1, float dur, Wave w, float gain)
        {
            int rate = 44100, n = Mathf.CeilToInt(rate * dur);
            var data = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float f = f0 * Mathf.Pow(f1 / f0, t);          // 指数的に音程を変える
                phase += f / rate;
                float p = (float)(phase - System.Math.Floor(phase));
                float s = w == Wave.Sine ? Mathf.Sin(p * Mathf.PI * 2f)
                        : w == Wave.Square ? (p < 0.5f ? 1f : -1f)
                        : w == Wave.Triangle ? 1f - 4f * Mathf.Abs(p - 0.5f)
                        : 2f * p - 1f;
                data[i] = s * gain * Mathf.Pow(1f - t, 2f);
            }
            var clip = AudioClip.Create("tone", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Bell()
        {
            int rate = 44100, n = rate * 2;
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float env = Mathf.Exp(-t * 2.2f);
                data[i] = (Mathf.Sin(2 * Mathf.PI * 880 * t) * 0.35f + Mathf.Sin(2 * Mathf.PI * 1320 * t) * 0.15f) * env;
            }
            var clip = AudioClip.Create("bell", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
