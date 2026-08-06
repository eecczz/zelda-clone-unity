using TMPro;
using UnityEngine;

/// <summary>
/// 점수/콤보/거점/상태 패널 표시. 값이 바뀐 프레임에만 문자열을 다시 만든다.
/// (매 프레임 문자열을 생성하면 그만큼 GC가 쌓인다)
/// </summary>
public class HUD : MonoBehaviour
{
    [Header("플레이 중")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text comboText;
    [Tooltip("콤보가 이 값 이상일 때만 표시한다.")]
    [SerializeField] private int minComboToShow = 2;
    [SerializeField] private TMP_Text campText;

    [Header("경계 경고")]
    [SerializeField] private TMP_Text boundaryWarningText;

    [Header("패널")]
    [SerializeField] private GameObject readyPanel;
    [SerializeField] private TMP_Text readyText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TMP_Text gameOverText;
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private TMP_Text victoryText;

    // 기본 TMP 폰트 아틀라스에는 한글 글리프가 없어 □로 깨진다. 문구는 전부 영문 대문자로 유지할 것.
    [Header("문구")]
    [SerializeField] private string readyMessage = "CLICK TO START";
    [SerializeField] private string gameOverTitle = "GAME OVER";
    [SerializeField] private string victoryTitle = "FIELD CLEARED";
    [SerializeField] private string restartMessage = "PRESS R TO RESTART";
    [SerializeField] private string boundaryMessage = "RETURN TO FIELD";

    private int lastScore = -1;
    private int lastCombo = -1;
    private int lastCleared = -1;
    private int lastCampTotal = -1;
    private bool lastNearEdge;
    private GameState lastState = (GameState)(-1);

    void Start()
    {
        if (readyText != null) readyText.text = readyMessage;
        if (boundaryWarningText != null) boundaryWarningText.text = boundaryMessage;

        Refresh(true);
    }

    void Update()
    {
        Refresh(false);
    }

    void Refresh(bool force)
    {
        ScoreSystem score = ScoreSystem.Instance;
        GameManager game = GameManager.Instance;

        GameState state = game != null ? game.State : GameState.Playing;
        if (force || state != lastState)
        {
            lastState = state;
            ApplyState(state, score, game);
        }

        RefreshCamps(force);
        RefreshBoundary(force, state);

        if (score == null) return;

        if (force || score.Score != lastScore)
        {
            lastScore = score.Score;
            if (scoreText != null) scoreText.text = score.Score.ToString();
        }

        if (force || score.Combo != lastCombo)
        {
            lastCombo = score.Combo;
            if (comboText != null)
            {
                bool visible = score.Combo >= minComboToShow;
                comboText.enabled = visible;
                if (visible) comboText.text = $"x{score.Combo}";
            }
        }
    }

    void ApplyState(GameState state, ScoreSystem score, GameManager game)
    {
        if (readyPanel != null) readyPanel.SetActive(state == GameState.Ready);
        if (gameOverPanel != null) gameOverPanel.SetActive(state == GameState.GameOver);
        if (victoryPanel != null) victoryPanel.SetActive(state == GameState.Victory);

        if (score == null) return;

        if (state == GameState.GameOver && gameOverText != null)
        {
            gameOverText.text =
                $"{gameOverTitle}\n\nSCORE {score.Score}  /  BEST COMBO x{score.BestCombo}\n\n{restartMessage}";
        }
        else if (state == GameState.Victory && victoryText != null)
        {
            string clearTime = game != null ? GameManager.FormatTime(game.ClearTime) : "-";
            victoryText.text =
                $"{victoryTitle}\n\nTIME {clearTime}\nSCORE {score.Score}  /  BEST COMBO x{score.BestCombo}\n\n{restartMessage}";
        }
    }

    void RefreshCamps(bool force)
    {
        if (campText == null) return;

        int cleared = EnemyCamp.ClearedCount;
        int total = EnemyCamp.TotalCount;

        if (!force && cleared == lastCleared && total == lastCampTotal) return;

        lastCleared = cleared;
        lastCampTotal = total;

        // 거점이 없는 씬이면 카운터 자체를 숨긴다
        campText.enabled = total > 0;
        if (total > 0) campText.text = $"CAMPS {cleared}/{total}";
    }

    void RefreshBoundary(bool force, GameState state)
    {
        if (boundaryWarningText == null) return;

        FieldBounds bounds = FieldBounds.Instance;
        bool warn = bounds != null && bounds.PlayerNearEdge && state == GameState.Playing;

        if (!force && warn == lastNearEdge) return;

        lastNearEdge = warn;
        boundaryWarningText.enabled = warn;
    }
}
