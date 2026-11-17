# IllegalTaxi（v43）の使い方

Web版 v43（`WebPrototype/v43/index.html`）と同じルールです。
**モデル・UI・シーン・数字を、コードを触らずに Unity 上で変えられる**ようにしてあります。

---

## まず最初に（1回だけ）
1. `IllegalTaxi` が付いたシーン（`New Scene` など）を開く。
2. メニュー **「精霊馬タクシー > 1. データを作ってつなぐ」** を押す。
   - `Assets/IllegalTaxiData/` に次のファイルができ、シーンの IllegalTaxi に自動でつながる。
   - シーンを保存する（Ctrl+S）。

| できるファイル | 何を変えられるか |
|---|---|
| `Stage1_研修` 〜 `Stage4_大警部との決戦` | 目標の売上・制限時間・見た目（夕暮れ/雨）・速さ・大警部・魂/警察/アイテムの配置 |
| `TaxiTuning`（調整用の数値） | 速さの倍率・追い風・敵の攻撃間隔・体当たりで落とす運賃・スリ・塩の水分・ヒットストップ・揺れ |
| `TaxiModels`（モデルの差し替え） | 巡査・パト提灯・大警部・白手袋・弾・手錠・小銭・アイテム3種のプレハブ |

> **再生中に TaxiTuning の数字を変えると、その場で反映されます**（再生をやめても数字は残ります）。
> Stage の数字は、ステージを始めたときに読み込まれます。

---

## モデルを変える
- **警察・大警部・弾など**：`TaxiModels` の欄にプレハブ（FBX）をドラッグ。空の欄は今までの仮モデルのまま。
  - 正面は **+Z（カメラの方）** に向けて作る。大きさが合わないときは `Scale` で揃える。
  - 大警部のモデルの中に `face`・`mouth`・`core`・`sirenRed`・`sirenBlue` という名前の子があれば、揺れ・口・弱点・回転灯に使われる（なくても動く）。
  - 白手袋は **左手** で作る。右手は自動で左右反転。
- **きゅうりの馬・ナスの牛・魂・車掌**：今までどおり IllegalTaxi のインスペクターの欄（Cucumber Prefab・Nasu Prefab・Soul Color Models・Conductor Prefab）。

## UI を変える
2つのやり方があり、どちらも使えます。

1. **Canvas（おすすめ）**：IllegalTaxi の `Use Canvas HUD` をオンにし、`New Scene` の Canvas の Panel・Text・Button を欄に入れる（班で作った `IllegalTaxi.CanvasHUD.cs` の仕組み）。
   - 見た目（フォント・色・位置・画像）は Canvas 上で自由に変えてよい。スクリプトは中身の文字と表示/非表示だけを変える。
   - v43 で増えた欄（**どれも空でよい**。入れたものだけ動く）：
     | 欄 | 中身 |
     |---|---|
     | Timer Text | 夜明けまで 2:30 |
     | Title Company Text | ○○タクシー 地獄営業所 |
     | Stage Star Texts（4つ） | タイトルの各ステージの ★★☆ ベスト |
     | Result Stars Text | ★★☆ ベスト更新！ |
     | Boss Hp Trail Fill | 決戦：減った分が白く残るバー（Boss Hp Fill の後ろに置く） |
     | Flash Image | 画面全体の白い閃光（全画面の白い Image。Raycast Target はオフ） |
     | Name Panel / Name Input / Name Ok・Cancel Button | 社名を決める画面（空なら IMGUI で出る） |
     | Title Rename / Pause Rename / Pause Retry / Charm Retry Button | 社名を変える・やり直す・お守りを持って再挑戦 |
2. **IMGUI（Canvas を使わないとき）**：`Use Canvas HUD` がオフなら、`IllegalTaxi.HUD.cs` がコードで描く（今までどおり）。

## シーンを分ける（タイトル／ゲーム）
1. IllegalTaxi が付いたシーンを開いて保存する。
2. メニュー **「精霊馬タクシー > 2. 今のシーンをタイトルとゲームに分ける」** を押す。
   - 同じフォルダに `IllegalTaxi_Title.unity` と `IllegalTaxi_Game.unity` ができ、Build Settings の先頭に並ぶ。
   - タイトルのシーン：ステージを選ぶとゲームのシーンへ移る。
   - ゲームのシーン：「タイトルへ」でタイトルのシーンへ戻る。エディターでゲームのシーンを直接再生すると、ステージ1から始まる。
3. 分けたあとは、それぞれのシーンで UI やカメラ・ライトを別々に編集できる。
   - 手で分けるときは、IllegalTaxi の **Scene Role** を `Title` / `Game` にして、`Title Scene Name` / `Game Scene Name` をシーンの名前に合わせる。
   - 1つのシーンで全部動かすときは `All`（初期値）。

---

## v43 で入ったもの（Web版と同じ）
- **制限時間**（夜明けまで）。間に合わないと「夜が明けた」
- **車掌のスリ**：行きで警察のすぐ脇を抜けると +2文（ぶつかると捕まる）
- 紫の魂のすぐ先に巡査（ステージ2・3）
- **手応え**：ヒットストップ・揺れの強さ3段階・ヒットマーク・白い閃光・大警部が白く光る
- **照準は1つだけ**、画面のまんなか（カメラは自機の真後ろにほぼ付いていく）
- **決戦**：ふだんは白手袋で顔を守る（顔に塩が効かない）。攻撃してくる瞬間・目を回しているあいだがスキ。体力 200。ダメージの数字は 0.25秒ごと。体力バーは減った分が白く残る
- **ご褒美**：星1〜3とベスト（PlayerPrefs に保存）、2回続けて負けたら「お守りを持って再挑戦」
- **リトライ**：結果画面・一時停止で R、結果画面の Enter で次へ
- **社名**：はじめて遊ぶときに決める。タイトル・一時停止から変えられる
- タイトルで「大警部からの逃走」を自動で見せる（展示で足を止めてもらう）
- 救済の小銭は「足りない分の6割」、通報から逃走までを短く

## ファイル
| ファイル | 中身 |
|---|---|
| IllegalTaxi.cs | 部品の用意・ステージ・毎フレームの進行・研修・社名・シーンの役割 |
| IllegalTaxi.Boss.cs | 大警部（追いかけ）と、ステージ4の決戦（守りとスキ） |
| IllegalTaxi.View.cs | 毎フレームの見た目（カメラ・馬・敵など） |
| IllegalTaxi.Juice.cs | 手応え（ヒットストップ・揺れ・照準・ヒットマーク）とモデルの差し替え |
| IllegalTaxi.HUD.cs | IMGUI の画面表示（Canvas を使わないとき） |
| IllegalTaxi.CanvasHUD.cs | Canvas の画面表示（班で作ったもの＋v43 の欄） |
| TaxiTuning.cs / TaxiStageData.cs / TaxiModelSet.cs | 上の3種類のデータの形 |
| TaxiSession.cs | シーンをまたぐ値（選んだステージ・社名・星とベスト） |
| Editor/IllegalTaxiMenu.cs | メニュー「精霊馬タクシー」 |
| ModelFactory.Police.cs | 警察などの仮モデル |
| TaxiAudio.cs | BGM（`Resources/BGM/sneak`・`chase`・`boss`）と効果音（コードで合成） |

**注意**：`TaxiGame`（v32 のルール）と同じシーンに置かないでください。
