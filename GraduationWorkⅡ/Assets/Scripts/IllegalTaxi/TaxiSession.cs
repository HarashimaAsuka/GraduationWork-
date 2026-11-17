using System.Collections.Generic;
using UnityEngine;

namespace ShoryoumaTaxi
{
    /// <summary>シーンをまたいで受け渡す値（タイトルで選んだステージなど）。</summary>
    public static class TaxiSession
    {
        /// <summary>ゲームのシーンで始めるステージ（0 のまま＝エディターでゲームのシーンを直接再生したときは、ステージ1から）。</summary>
        public static int StageToStart;
        /// <summary>お守りを持って始める（負けが続いたときの救済）。</summary>
        public static bool StartWithCharm;
        const string NAME_KEY = "shoryouma.name";
        public static string CompanyName
        {
            get => PlayerPrefs.GetString(NAME_KEY, "");
            set { PlayerPrefs.SetString(NAME_KEY, (value ?? "").Trim()); PlayerPrefs.Save(); }
        }
        public static string CoName => string.IsNullOrEmpty(CompanyName) ? "精霊馬" : CompanyName;
        public static int Stars(int stage) => PlayerPrefs.GetInt($"shoryouma.stars{stage}", 0);
        public static int Best(int stage) => PlayerPrefs.GetInt($"shoryouma.best{stage}", 0);
        public static void SaveRecord(int stage, int stars, int best)
        {
            PlayerPrefs.SetInt($"shoryouma.stars{stage}", Mathf.Max(Stars(stage), stars));
            PlayerPrefs.SetInt($"shoryouma.best{stage}", Mathf.Max(Best(stage), best));
            PlayerPrefs.Save();
        }
    }
}
