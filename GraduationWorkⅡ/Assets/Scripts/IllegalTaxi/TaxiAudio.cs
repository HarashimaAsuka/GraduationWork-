using UnityEngine;

namespace ShoryoumaTaxi
{
    /// <summary>
    /// v42「違法タクシー」の BGM。Assets/Resources/BGM/ の sneak（行き）・chase（帰り）・boss（決戦）をループで流す。
    /// M キーで消音（PlayerPrefs に保存）。
    /// </summary>
    public class TaxiBgm
    {
        const float VOL = 0.6f;
        const string PREF = "shoryouma.bgm";
        readonly AudioSource src;
        string cur = "";
        bool paused;
        float level;
        public bool Muted { get; private set; }

        public TaxiBgm(GameObject host)
        {
            src = host.AddComponent<AudioSource>();
            src.playOnAwake = false; src.loop = true; src.volume = 0;
            Muted = PlayerPrefs.GetInt(PREF, 1) == 0;
        }

        public void Play(string name)
        {
            if (cur == name && src.isPlaying) return;
            cur = name; paused = false;
            var c = Resources.Load<AudioClip>("BGM/" + name);
            if (c == null) { Debug.LogWarning($"BGM: Resources/BGM/{name} が見つかりません"); src.Stop(); return; }
            src.clip = c; level = 0; src.volume = 0; src.Play();
        }
        public void Stop() { cur = ""; src.Stop(); }
        public void Pause(bool p) { paused = p; if (p) src.Pause(); else src.UnPause(); }
        public bool Toggle() { Muted = !Muted; PlayerPrefs.SetInt(PREF, Muted ? 0 : 1); PlayerPrefs.Save(); return Muted; }
        public void Tick(float dt)
        {
            float target = Muted || paused ? 0f : VOL;
            level = Mathf.MoveTowards(level, target, dt / 0.6f * VOL);
            src.volume = level;
        }
    }

    /// <summary>
    /// v42 の効果音。音声ファイルを使わず、コードで合成する（Web版の sfx と同じ作り方）。
    /// </summary>
    public class TaxiSe
    {
        const int RATE = 44100;
        readonly AudioSource src;
        static readonly System.Random rng = new System.Random(11);
        public AudioClip salt, hit, board, kill, whistle, cuffs, coins, ram, orin, puff, siren, sirenShort, roar, shoot, cuffShot, hurt, swoosh, water, wind, charm, bossHit, win, bell, beep0, beep1;

        public TaxiSe(GameObject host)
        {
            src = host.AddComponent<AudioSource>(); src.playOnAwake = false;
            salt = Clip("salt", Mix(Hiss(0.07f, 0.22f, 3000, 12000), Tone(1900, 900, 0.06f, W.Tri, 0.14f), Tone(220, 90, 0.05f, W.Sine, 0.16f)));
            hit = Clip("hit", Mix(Hiss(0.05f, 0.3f, 1500, 9000), Tone(1400, 500, 0.08f, W.Sq, 0.09f), Tone(160, 70, 0.1f, W.Sine, 0.2f)));
            board = Clip("board", Mix(Tone(1760, 1740, 0.6f, W.Sine, 0.2f, 0, 0, 1), Delay(Tone(2637, 2620, 0.45f, W.Sine, 0.1f, 0, 0, 1), 0.06f)));
            kill = Clip("kill", Mix(Hiss(0.35f, 0.4f, 300, 5000), Tone(420, 70, 0.3f, W.Tri, 0.3f), Tone(1400, 600, 0.12f, W.Sq, 0.08f)));
            whistle = Clip("whistle", Tone(2900, 2950, 0.45f, W.Sq, 0.09f, 140, 28, 1));
            var c = new float[0]; for (int i = 0; i < 3; i++) c = Mix(c, Delay(Mix(Hiss(0.06f, 0.3f, 2500, 9000), Tone(3100 - i * 300, 2600, 0.05f, W.Sq, 0.08f)), i * 0.05f));
            cuffs = Clip("cuffs", c);
            var co = new float[0]; for (int i = 0; i < 7; i++) co = Mix(co, Delay(Tone(2600 + (float)rng.NextDouble() * 900, 2100, 0.14f, W.Tri, 0.12f), i * 0.055f));
            coins = Clip("coins", co);
            ram = Clip("ram", Mix(Tone(120, 40, 0.45f, W.Sine, 0.8f), Hiss(0.25f, 0.6f, 80, 2500), Tone(300, 90, 0.2f, W.Saw, 0.15f)));
            orin = Clip("orin", Mix(Tone(1180, 1176, 2.2f, W.Sine, 0.25f, 0, 0, 1), Tone(2950, 2940, 1.4f, W.Sine, 0.08f, 0, 0, 1)));
            puff = Clip("puff", Mix(Hiss(0.6f, 0.4f, 200, 2500), Tone(2200, 2100, 0.08f, W.Sq, 0.1f), Delay(Tone(2200, 2100, 0.08f, W.Sq, 0.1f), 0.16f)));
            siren = Clip("siren", Siren(3f)); sirenShort = Clip("sirenS", Siren(1f));
            roar = Clip("roar", Mix(Tone(90, 55, 0.7f, W.Saw, 0.25f, 12, 18, 1), Hiss(0.6f, 0.2f, 100, 900)));
            shoot = Clip("shoot", Tone(900, 1500, 0.08f, W.Sq, 0.05f));
            cuffShot = Clip("cuffShot", Mix(Tone(500, 1200, 0.18f, W.Saw, 0.08f), Hiss(0.1f, 0.12f, 2000, 8000)));
            hurt = Clip("hurt", Mix(Tone(220, 60, 0.3f, W.Saw, 0.22f), Hiss(0.15f, 0.3f, 200, 2000)));
            swoosh = Clip("swoosh", Mix(Hiss(0.5f, 0.15f, 600, 3000), Tone(300, 900, 0.5f, W.Sine, 0.05f, 0, 0, 1)));
            water = Clip("water", Mix(Tone(600, 1400, 0.12f, W.Sine, 0.22f), Delay(Tone(900, 500, 0.15f, W.Sine, 0.14f), 0.1f)));
            wind = Clip("wind", Mix(Hiss(0.9f, 0.3f, 400, 2500), Tone(300, 900, 0.8f, W.Sine, 0.06f, 0, 0, 1)));
            var ch = new float[0]; for (int i = 0; i < 5; i++) ch = Mix(ch, Delay(Tone(3000 + i * 400, 2900 + i * 400, 0.4f, W.Sine, 0.08f, 0, 0, 1), i * 0.04f));
            charm = Clip("charm", ch);
            bossHit = Clip("bossHit", Mix(Tone(700, 300, 0.08f, W.Sq, 0.1f), Hiss(0.05f, 0.2f, 1500, 6000)));
            var wn = new float[0]; float[] fs = { 1046, 1318, 1568, 2093 }, ds = { 0, 0.15f, 0.3f, 0.6f };
            for (int i = 0; i < 4; i++) wn = Mix(wn, Delay(Tone(fs[i], fs[i], 0.8f, W.Tri, 0.16f, 0, 0, 1), ds[i]));
            win = Clip("win", wn);
            bell = Clip("bell", Mix(Tone(880, 880, 2f, W.Sine, 0.3f, 0, 0, 1), Tone(1320, 1320, 2f, W.Sine, 0.12f, 0, 0, 1)));
            beep0 = Clip("beep0", Tone(760, 760, 0.09f, W.Sq, 0.13f));
            beep1 = Clip("beep1", Tone(1100, 1100, 0.09f, W.Sq, 0.13f));
        }

        public void Play(AudioClip c, float vol = 1f) { if (c != null) src.PlayOneShot(c, vol); }

        enum W { Sine, Sq, Tri, Saw }
        static AudioClip Clip(string name, float[] d)
        {
            if (d.Length == 0) d = new float[1];
            var clip = AudioClip.Create(name, d.Length, 1, RATE, false); clip.SetData(d, 0); return clip;
        }
        // f0→f1 に音程を変える音。vib = 揺れの幅(Hz)、vibHz = 揺れの速さ、slow = 1 で長く伸びる減衰
        static float[] Tone(float f0, float f1, float dur, W w, float gain, float vib = 0, float vibHz = 0, int slow = 0)
        {
            int n = Mathf.CeilToInt(RATE * dur); var d = new float[n]; double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n, ts = (float)i / RATE;
                float f = f0 * Mathf.Pow(f1 / f0, t) + (vib > 0 ? Mathf.Sin(2 * Mathf.PI * vibHz * ts) * vib : 0);
                ph += f / RATE; float p = (float)(ph - System.Math.Floor(ph));
                float s = w == W.Sine ? Mathf.Sin(p * Mathf.PI * 2f) : w == W.Sq ? (p < 0.5f ? 1f : -1f) : w == W.Tri ? 1f - 4f * Mathf.Abs(p - 0.5f) : 2f * p - 1f;
                float env = slow == 1 ? Mathf.Exp(-t * 5f) : Mathf.Pow(1f - t, 2f);
                d[i] = s * gain * env * Mathf.Min(1f, i / (RATE * 0.004f));
            }
            return d;
        }
        static float[] Hiss(float dur, float gain, float hp, float lp)
        {
            int n = Mathf.CeilToInt(RATE * dur); var d = new float[n];
            float aL = Mathf.Exp(-2f * Mathf.PI * lp / RATE), aH = Mathf.Exp(-2f * Mathf.PI * hp / RATE), low = 0, lowH = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n, x = (float)(rng.NextDouble() * 2 - 1);
                low = (1 - aL) * x + aL * low; lowH = (1 - aH) * low + aH * lowH;
                d[i] = (low - lowH) * gain * Mathf.Pow(1f - t, 3f) * 2.2f;
            }
            return d;
        }
        static float[] Siren(float sec)
        {
            var d = new float[0];
            for (int i = 0; i < sec / 0.9f; i++) d = Mix(d, Delay(Tone(620, 1050, 0.45f, W.Saw, 0.08f), i * 0.9f), Delay(Tone(1050, 620, 0.45f, W.Saw, 0.08f), i * 0.9f + 0.45f));
            return d;
        }
        static float[] Delay(float[] a, float sec) { int k = Mathf.CeilToInt(sec * RATE); var d = new float[a.Length + k]; System.Array.Copy(a, 0, d, k, a.Length); return d; }
        static float[] Mix(params float[][] parts)
        {
            int n = 0; foreach (var p in parts) n = Mathf.Max(n, p.Length);
            var d = new float[n];
            foreach (var p in parts) for (int i = 0; i < p.Length; i++) d[i] += p[i];
            for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(d[i], -1f, 1f);
            return d;
        }
    }
}
