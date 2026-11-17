
using UnityEngine;
using UnityEngine.UI;

namespace ShoryoumaTaxi
{
    public partial class IllegalTaxi
    {
        [Header("Canvas UI")]
        [SerializeField] bool useCanvasHUD = false;

        [Header("画面 Panel")]
        [SerializeField] GameObject titlePanel;
        [SerializeField] GameObject gamePanel;
        [SerializeField] GameObject pausePanel;
        [SerializeField] GameObject resultPanel;
        [SerializeField] GameObject tutorialPanel;
        [SerializeField] GameObject bannerPanel;
        [SerializeField] GameObject bossPanel;

        [Header("ゲーム中の Text")]
        [SerializeField] Text stageNameText;
        [SerializeField] Text fareText;
        [SerializeField] Text waterText;
        [SerializeField] Text objectiveText;
        [SerializeField] Text riderText;
        [SerializeField] Text distanceText;
        [SerializeField] Text bossHpText;

        [Header("ゲーム中の Gauge（Image Type = Filled）")]
        [SerializeField] Image fareFill;
        [SerializeField] Image waterFill;
        [SerializeField] Image bossHpFill;

        [Header("研修・バナー Text")]
        [SerializeField] Text tutorialHeaderText;
        [SerializeField] Text tutorialBodyText;
        [SerializeField] Text bannerMainText;
        [SerializeField] Text bannerSubText;

        [Header("タイトル・リザルト Text")]
        [SerializeField] Text resultStageText;
        [SerializeField] Text resultTitleText;
        [SerializeField] Text resultBodyText;
        [SerializeField] Text resultStatsText;

        [Header("タイトル画面のステージ選択ボタン")]
        [SerializeField] Button[] stageButtons = new Button[4];

        [Header("画面遷移ボタン")]
        [SerializeField] Button resumeButton;
        [SerializeField] Button pauseToTitleButton;
        [SerializeField] Button nextStageButton;
        [SerializeField] Button retryButton;
        [SerializeField] Button resultToTitleButton;

        // ---- v43 で増えたもの（どれも空でよい。入れたものだけ使われる） ----
        [Header("v43：制限時間・星・社名（空でもよい）")]
        [SerializeField] Text timerText;              // 夜明けまで 2:30
        [SerializeField] Text titleCompanyText;       // ○○タクシー 地獄営業所
        [SerializeField] Text[] stageStarTexts = new Text[4];   // タイトルの各ステージの ★★☆ ベスト
        [SerializeField] Text resultStarsText;        // リザルトの ★★☆ ベスト更新！
        [SerializeField] Image bossHpTrailFill;       // 決戦：減った分が白く残るバー（bossHpFill の後ろに置く）
        [SerializeField] Image flashImage;            // 画面全体の白い閃光（全画面の白い Image、Raycast Target はオフ）
        [SerializeField] GameObject namePanel;        // 社名を決める画面（空なら IMGUI で出す）
        [SerializeField] InputField nameInput;
        [SerializeField] Button nameOkButton, nameCancelButton;
        [SerializeField] Button titleRenameButton, pauseRenameButton, pauseRetryButton, charmRetryButton;

        bool CanvasHUDActive => useCanvasHUD && titlePanel != null;

        bool canvasButtonsBound;

        void OnEnable()
        {
            if (canvasButtonsBound) return;
            canvasButtonsBound = true;

            if (stageButtons != null)
            {
                for (int i = 0; i < stageButtons.Length; i++)
                {
                    int stageNo = i + 1;
                    if (stageButtons[i] == null) continue;

                    stageButtons[i].onClick.AddListener(
                        () => BeginStage(stageNo)
                    );
                }
            }

            Bind(resumeButton, Resume);
            Bind(pauseToTitleButton, ToTitle);
            Bind(nextStageButton, () => StartStage(st.no + 1));
            Bind(retryButton, Retry);
            Bind(resultToTitleButton, ToTitle);
            Bind(pauseRetryButton, Retry);
            Bind(charmRetryButton, RetryWithCharm);
            Bind(titleRenameButton, OpenName);
            Bind(pauseRenameButton, OpenName);
            Bind(nameOkButton, () => { if (nameInput != null) nameDraft = nameInput.text; CloseName(true); });
            Bind(nameCancelButton, () => CloseName(false));
        }

        static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
                button.onClick.AddListener(action);
        }
        
        void RefreshCanvasHUD()
        {
            bool isTitle = mode == Mode.Title;
            bool isPlay = mode == Mode.Play;
            bool isPause = mode == Mode.Pause;
            bool isResult = mode == Mode.Result;

            SetPanel(titlePanel, isTitle);
            SetPanel(gamePanel, isPlay || isPause);
            SetPanel(pausePanel, isPause);
            SetPanel(resultPanel, isResult);
            bool isName = mode == Mode.Name;
            if (namePanel != null)
            {
                if (isName && !namePanel.activeSelf && nameInput != null) nameInput.text = nameDraft;
                SetPanel(namePanel, isName);
            }
            if (flashImage != null) { var fc = flashImage.color; fc.a = (isPlay || isPause) ? Mathf.Clamp01(hurtFlash * 2.5f) * 0.35f : 0; flashImage.color = fc; }
            SetText(titleCompanyText, $"{TaxiSession.CoName}タクシー 地獄営業所");
            if (stageStarTexts != null)
                for (int i = 0; i < stageStarTexts.Length && i < 4; i++)
                {
                    int s = TaxiSession.Stars(i + 1);
                    SetText(stageStarTexts[i], s > 0 ? $"{new string('★', s)}{new string('☆', 3 - s)}　ベスト {TaxiSession.Best(i + 1)}" : "まだ遊んでいない");
                }

            bool tutorialOn = isPlay && tipCur != null && st != null && st.tutorial;
            SetPanel(tutorialPanel, tutorialOn);

            bool bannerOn = (isPlay || isPause) && bannerT > 0;
            SetPanel(bannerPanel, bannerOn);

            bool bossOn = isPlay && st != null && st.boss && B != null;
            SetPanel(bossPanel, bossOn);

            if (isTitle || st == null) return;

            // ステージ名
            SetText(stageNameText, $"STAGE {st.no}　{st.name}");
            SetText(timerText, st.time > 0 && !st.boss ? $"夜明けまで {FmtTime(timeLeft)}" : "");

            // 売上
            SetText(fareText, $"売上　{fare} / {st.goal}文");
            SetFill(fareFill, st.goal > 0 ? (float)fare / st.goal : 0);

            // 水分
            SetText(waterText, $"水分　{Mathf.RoundToInt(water)}%");
            SetFill(waterFill, water / 100f);

            // 現在の目的
            bool outP = phase == "out";
            int toEnd = Mathf.Max(0, Mathf.RoundToInt((END - y) / 10));
            int toHell = Mathf.Max(0, Mathf.RoundToInt((y - START) / 10));

            string objective = st.boss
                ? $"大警部を塩で祓え！　地獄の門まで あと{toHell}丈"
                : outP
                    ? (riders.Count > 0
                        ? $"現世の門へ　あと{toEnd}丈"
                        : "罪人の魂を乗せて、現世の門へ")
                    : phase == "alarm"
                        ? "運賃をもらった！"
                        : $"大警部から逃げろ！　地獄の門まで あと{toHell}丈";

            SetText(objectiveText, objective);

            // 乗せた魂と運賃の見込み
            if (outP)
            {
                int[] count = new int[3];
                foreach (int rare in riders)
                    if (rare >= 0 && rare < count.Length) count[rare]++;

                int estimate = count[0] * 10 + count[1] * 20 + count[2] * 30;

                string seat = riders.Count == 0
                    ? "まだ誰も乗っていない"
                    : $"白×{count[0]}　黄×{count[1]}　紫×{count[2]}　運賃見込み {estimate}文";

                SetText(riderText, seat);
            }
            else
            {
                SetText(riderText, "");
            }

            // 道のり
            string distance = outP
                ? $"現世の門まで あと{toEnd}丈"
                : $"地獄の門まで あと{toHell}丈";

            if (phase == "back" && !st.boss && bossM != null && bossM.root.activeSelf)
            {
                float gap = Mathf.Max(0, (bossY - y) / 10);
                distance += $"\n大警部まで {gap:0.0}丈";
                if (charm) distance += "　お守りあり";
            }

            SetText(distanceText, distance);

            // ステージ4：大警部の体力
            if (bossOn)
            {
                SetText(
                    bossHpText,
                    "大警部" +
                    (B.shield ? "（護衛が塩をはじく）" :
                    B.stunT > 0 ? "（ひるんでいる！）" : "")
                );

                SetFill(
                    bossHpFill,
                    bossHpMax > 0 ? (float)bossHp / bossHpMax : 0
                );
                SetFill(bossHpTrailFill, bossHpMax > 0 ? hpTrail / bossHpMax : 0);
            }

            // 研修の説明
            if (tutorialOn)
            {
                SetText(
                    tutorialHeaderText,
                    "運転手" +
                    (string.IsNullOrEmpty(tipCur.step) ? "" : "　" + tipCur.step)
                );

                SetText(tutorialBodyText, tipCur.html);
            }

            // バナー
            if (bannerOn)
            {
                SetText(bannerMainText, bannerMain);
                SetText(bannerSubText, bannerSub);
            }

            // リザルト
            if (isResult)
            {
                SetText(resultStageText, $"ステージ{st.no}　{st.name}");
                SetText(resultTitleText, resultTitle);
                SetText(resultBodyText, resultText);

                string stats = st.boss
                    ? $"大警部に与えた傷 {Mathf.RoundToInt((1 - bossHp / (float)bossHpMax) * 100)}%　祓った警察 {kills}体　吹っ飛ばした {rams}回"
                    : $"売上 {fare}文　目標 {st.goal}文　運んだ魂 {delivered}人　落とした運賃 {lost}文";

                SetText(resultStatsText, stats);

                if (nextStageButton != null)
                    nextStageButton.gameObject.SetActive(resultOk && st.no < 4);
                if (charmRetryButton != null)
                    charmRetryButton.gameObject.SetActive(offerCharmRetry);
                SetText(resultStarsText, resultOk ? $"{new string('★', resultStars)}{new string('☆', 3 - resultStars)}" + (resultNewBest ? "　ベスト更新！" : "") : "");
            }
        }

        static void SetPanel(GameObject panel, bool active)
        {
            if (panel != null && panel.activeSelf != active)
                panel.SetActive(active);
        }

        static void SetText(Text target, string value)
        {
            if (target != null)
                target.text = value;
        }

        static void SetFill(Image target, float value)
        {
            if (target != null)
                target.fillAmount = Mathf.Clamp01(value);
        }
    }
}
