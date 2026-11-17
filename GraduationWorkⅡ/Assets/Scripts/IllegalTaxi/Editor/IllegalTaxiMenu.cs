using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShoryoumaTaxi.EditorTools
{
    /// <summary>
    /// メニュー「精霊馬タクシー」。
    ///   1. データを作ってつなぐ …… ステージ1〜4・調整用の数値・モデルの差し替えのファイルを作り、開いているシーンの IllegalTaxi に入れる
    ///   2. 今のシーンをタイトルとゲームに分ける …… 開いているシーンを2つに複製して、Build Settings に並べる
    /// </summary>
    public static class IllegalTaxiMenu
    {
        const string DATA_DIR = "Assets/IllegalTaxiData";

        [MenuItem("精霊馬タクシー/1. データを作ってつなぐ（ステージ・調整・モデル）")]
        static void MakeData()
        {
            var taxi = Object.FindFirstObjectByType<IllegalTaxi>();
            if (!AssetDatabase.IsValidFolder(DATA_DIR)) AssetDatabase.CreateFolder("Assets", "IllegalTaxiData");

            var stages = new List<TaxiStageData>();
            var built = IllegalTaxi.BuiltInStageAssets();
            for (int i = 0; i < built.Length; i++)
            {
                string path = $"{DATA_DIR}/Stage{i + 1}_{built[i].stageName}.asset";
                var have = AssetDatabase.LoadAssetAtPath<TaxiStageData>(path);
                if (have == null) { AssetDatabase.CreateAsset(built[i], path); have = built[i]; }   // すでにあれば上書きしない（調整した値を消さない）
                stages.Add(have);
            }
            var tuning = LoadOrCreate<TaxiTuning>($"{DATA_DIR}/TaxiTuning.asset");
            var models = LoadOrCreate<TaxiModelSet>($"{DATA_DIR}/TaxiModels.asset");
            AssetDatabase.SaveAssets();

            if (taxi != null)
            {
                Undo.RecordObject(taxi, "精霊馬タクシーのデータをつなぐ");
                taxi.stageAssets = stages.ToArray(); taxi.tuning = tuning; taxi.models = models;
                EditorUtility.SetDirty(taxi); EditorSceneManager.MarkSceneDirty(taxi.gameObject.scene);
            }
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(DATA_DIR);
            EditorUtility.DisplayDialog("精霊馬タクシー",
                $"{DATA_DIR} にデータを作りました。\n\n" +
                "・Stage1〜4 …… 目標・制限時間・魂や警察の配置\n・TaxiTuning …… 速さや攻撃の間隔（再生中に変えてもOK）\n・TaxiModels …… 差し替えたいモデルのプレハブを入れる\n\n" +
                (taxi != null ? "開いているシーンの IllegalTaxi につなぎました。シーンを保存してください。" : "シーンに IllegalTaxi が見つからなかったので、つなぐのは手で行ってください。"), "OK");
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(a, path); return a;
        }

        [MenuItem("精霊馬タクシー/2. 今のシーンをタイトルとゲームに分ける")]
        static void SplitScenes()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var taxi = Object.FindFirstObjectByType<IllegalTaxi>();
            if (taxi == null) { EditorUtility.DisplayDialog("精霊馬タクシー", "開いているシーンに IllegalTaxi がありません。IllegalTaxi が入ったシーンを開いてから実行してください。", "OK"); return; }
            if (string.IsNullOrEmpty(scene.path)) { EditorUtility.DisplayDialog("精霊馬タクシー", "先にシーンを保存してください。", "OK"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string dir = Path.GetDirectoryName(scene.path).Replace('\\', '/');
            string titlePath = $"{dir}/{taxi.titleSceneName}.unity", gamePath = $"{dir}/{taxi.gameSceneName}.unity";
            if ((File.Exists(titlePath) || File.Exists(gamePath)) &&
                !EditorUtility.DisplayDialog("精霊馬タクシー", $"{titlePath} か {gamePath} がもうあります。上書きしますか？", "上書きする", "やめる")) return;

            var before = taxi.sceneRole;
            taxi.sceneRole = IllegalTaxi.SceneRole.Title; EditorSceneManager.SaveScene(scene, titlePath, true);
            taxi.sceneRole = IllegalTaxi.SceneRole.Game; EditorSceneManager.SaveScene(scene, gamePath, true);
            taxi.sceneRole = before;

            // Build Settings：タイトルを先頭に、ゲームを2番目に（ほかのシーンは後ろに残す）
            var list = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(titlePath, true), new EditorBuildSettingsScene(gamePath, true) };
            foreach (var s in EditorBuildSettings.scenes) if (s.path != titlePath && s.path != gamePath) list.Add(s);
            EditorBuildSettings.scenes = list.ToArray();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("精霊馬タクシー",
                $"2つのシーンを作りました。\n\n・{titlePath}（タイトル。ステージを選ぶとゲームのシーンへ移る）\n・{gamePath}（ゲーム。「タイトルへ」でタイトルのシーンへ戻る）\n\n" +
                "Build Settings の先頭に入れました。これからは、それぞれのシーンで UI やカメラを別々に編集できます。\n（元のシーンはそのまま残っています。1つのシーンで全部動かしたいときは元のシーンを使ってください）", "OK");
        }
    }
}
