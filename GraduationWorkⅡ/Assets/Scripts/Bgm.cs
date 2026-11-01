using UnityEngine;

namespace ShoryoumaTaxi
{
    /// <summary>
    /// v30：BGM。ステージごとに用意した mp3 をループで流す。
    /// 曲は Assets/Resources/BGM/stage1.mp3・stage2.mp3・stage3.mp3 に置く（Resources.Load で読む）。
    /// 曲の前後の無音を飛ばして、つなぎ目を詰める（LOOP の秒数）。
    /// M キー・左下のボタンで消音（PlayerPrefs に保存）。残り時間が少ないときや親玉の三の段では少しだけ速くする。
    /// </summary>
    public class Bgm
    {
        static readonly float[][] LOOP = { null, new[] { 0f, 123.34f }, new[] { 0.11f, 104.75f }, new[] { 1.6f, 124.54f } };
        const float VOL = 0.6f;
        const string PREF = "shoryouma.bgm";

        readonly AudioSource src;
        readonly AudioClip[] clips = new AudioClip[4];
        int cur;
        bool paused, panic;
        float level;    // 今の音量（なめらかに変える）
        public bool Muted { get; private set; }

        public Bgm(GameObject host)
        {
            src = host.AddComponent<AudioSource>();
            src.playOnAwake = false; src.loop = false; src.volume = 0;
            Muted = PlayerPrefs.GetInt(PREF, 1) == 0;
            for (int i = 1; i <= 3; i++) clips[i] = Resources.Load<AudioClip>("BGM/stage" + i);
        }

        public void Play(int no)
        {
            if (no < 1 || no > 3) return;
            if (cur == no && src.isPlaying) return;
            cur = no; paused = false; panic = false;
            var c = clips[no];
            if (c == null) { Debug.LogWarning($"BGM: Resources/BGM/stage{no} が見つかりません"); src.Stop(); return; }
            src.clip = c; src.pitch = 1f; level = 0; src.volume = 0;
            src.Play();
            src.time = Mathf.Min(LOOP[no][0], c.length - 0.05f);
        }

        public void Stop() { cur = 0; src.Stop(); panic = false; }
        public void Pause(bool p) { paused = p; }
        public void SetPanic(bool p) { panic = p; }

        public bool Toggle()
        {
            Muted = !Muted;
            PlayerPrefs.SetInt(PREF, Muted ? 0 : 1); PlayerPrefs.Save();
            return Muted;
        }

        /// <summary>毎フレーム呼ぶ：ループ、音量のフェード、テンポ。</summary>
        public void Tick(float dt)
        {
            if (cur == 0 || src.clip == null) return;
            float target = Muted || paused ? 0f : VOL;
            level = Mathf.MoveTowards(level, target, dt / 0.6f * VOL * (target < level ? 4f : 1f));
            src.volume = level;
            src.pitch = Mathf.MoveTowards(src.pitch, panic ? 1.06f : 1f, dt * 0.2f);
            float a = LOOP[cur][0], b = Mathf.Min(LOOP[cur][1], src.clip.length - 0.02f);
            if (src.isPlaying && src.time >= b) src.time = a + (src.time - b);
            else if (!src.isPlaying && !paused) { src.Play(); src.time = a; }
        }
    }
}
