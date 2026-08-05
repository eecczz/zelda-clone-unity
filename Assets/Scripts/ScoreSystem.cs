using UnityEngine;

/// <summary>처치 방식. 반사 처치는 난이도가 높으니 배점이 다르다.</summary>
public enum KillSource
{
    Slash,
    Reflect,
}

/// <summary>
/// 점수와 콤보. 콤보는 "대시 한 번이 무언가를 맞혔는가"로 유지/리셋된다.
/// </summary>
public class ScoreSystem : MonoBehaviour
{
    public static ScoreSystem Instance { get; private set; }

    [Header("배점")]
    [Tooltip("베기 처치 기본 점수 (콤보 배수가 곱해진다)")]
    [SerializeField] private int pointsPerSlashKill = 100;
    [Tooltip("반사 처치 기본 점수")]
    [SerializeField] private int pointsPerReflectKill = 200;

    public int Score { get; private set; }
    public int Combo { get; private set; }
    public int BestCombo { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void ResetAll()
    {
        Score = 0;
        Combo = 0;
        BestCombo = 0;
    }

    /// <summary>처치 1건. 콤보를 올리고 그 배수만큼 점수를 준다.</summary>
    public void RegisterKill(KillSource source)
    {
        Combo++;
        if (Combo > BestCombo) BestCombo = Combo;

        int basePoints = source == KillSource.Reflect ? pointsPerReflectKill : pointsPerSlashKill;
        Score += basePoints * Combo;
    }

    /// <summary>
    /// 대시가 끝났을 때 호출. 아무것도 맞히지 못한 대시는 콤보를 끊는다.
    /// </summary>
    public void OnDashResolved(bool connected)
    {
        if (!connected) Combo = 0;
    }
}
