# IllegalTaxi（v42「違法タクシー」版）の使い方

今までの `TaxiGame`（v32 のルール）はそのまま残し、新しいルールを別のスクリプトにしました。

## 遊び方（シーンの作り方）
1. メニュー「File > New Scene」で新しいシーンを作り、`Assets/Scenes/IllegalTaxi.unity` などの名前で保存する。
2. ヒエラルキーで右クリック >「Create Empty」で空のオブジェクトを作る。
3. そのオブジェクトに `IllegalTaxi` コンポーネントを付ける（インスペクターの「Add Component」で IllegalTaxi と入力）。
4. 班のモデルを使うときは、インスペクターの欄に FBX を入れる。
   - Soul Color Models：Element 0=白、1=黄、2=紫
   - Conductor Prefab：車掌
5. 再生する。

**注意**：`TaxiGame` と同じシーンに置かないでください（道の長さなどの共通の設定がぶつかります）。

## ファイル
| ファイル | 中身 |
|---|---|
| IllegalTaxi.cs | 部品の用意・ステージの配置・毎フレームの進行・研修の説明 |
| IllegalTaxi.Boss.cs | 大警部（追いかけ）と、ステージ4の決戦のしかけ |
| IllegalTaxi.View.cs | 毎フレームの見た目（カメラ・馬・敵・ねらいの輪など） |
| IllegalTaxi.HUD.cs | 画面表示（売上・水分・今やること・道のり・手前から来るものの知らせ） |
| ModelFactory.Police.cs | 鬼の巡査・パト提灯・大警部・白手袋・手錠・小銭の仮モデル |
| TaxiAudio.cs | BGM（`Resources/BGM/sneak`・`chase`・`boss`）と効果音（コードで合成） |

数字（目標の売上・大警部の速さなど）は `IllegalTaxi.cs` の `MakeStages()` にまとめてあります。
Web版（`WebPrototype/v42/index.html`）と同じ数字です。
