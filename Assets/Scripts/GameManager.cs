using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState
{
    Ready,
    Playing,
    GameOver,
}

/// <summary>
/// 게임 상태 머신. 시간 조작의 최종 권한을 가진다 —
/// 사망 연출/게임오버 중에는 PlayerDash가 timeScale에 손대지 않는다.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("사망 연출")]
    [Tooltip("죽는 순간 적용할 timeScale")]
    [SerializeField, Range(0.01f, 1f)] private float deathSlowScale = 0.3f;
    [Tooltip("게임오버 화면이 뜨기까지의 시간 (초, 실제 시간 기준)")]
    [SerializeField] private float deathSequenceDuration = 0.8f;

    [Header("입력")]
    [SerializeField] private KeyCode restartKey = KeyCode.R;

    [Header("참조")]
    [Tooltip("사망 시 조준/히트스톱을 정리하기 위해 필요. 비워두면 자동으로 찾는다.")]
    [SerializeField] private PlayerDash playerDash;

    public GameState State { get; private set; } = GameState.Ready;

    /// <summary>플레이 입력을 받아도 되는 상태인지. 사망 연출 중에는 false.</summary>
    public bool IsPlaying => State == GameState.Playing && !isDying;

    /// <summary>
    /// 게임을 시작시킨 바로 그 프레임인지.
    /// 시작 클릭이 곧바로 조준으로 이어지는 걸 막는 데 쓴다 (스크립트 실행 순서와 무관하게 동작).
    /// </summary>
    public bool StartedThisFrame => Time.frameCount == startFrame;

    /// <summary>플레이 시작 후 흐른 시간. 스포너 난이도 곡선의 입력값.</summary>
    public float PlayTime => State == GameState.Ready ? 0f : Time.time - playStartTime;

    private bool isDying;
    private int startFrame = -1;
    private float playStartTime;
    private float defaultFixedDeltaTime;

    void Awake()
    {
        Instance = this;

        // 씬 리로드 시점에는 이미 1로 복구된 상태라 여기서 잡아도 안전하다
        defaultFixedDeltaTime = Time.fixedDeltaTime;
        Time.timeScale = 1f;

        if (playerDash == null) playerDash = FindAnyObjectByType<PlayerDash>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        switch (State)
        {
            case GameState.Ready:
                // 버튼을 뗄 때가 아니라 누를 때 시작하되, 같은 프레임의 조준은 StartedThisFrame으로 막는다
                if (Input.GetMouseButtonDown(0)) StartGame();
                break;

            case GameState.GameOver:
                if (!isDying && Input.GetKeyDown(restartKey)) Restart();
                break;
        }
    }

    void StartGame()
    {
        State = GameState.Playing;
        startFrame = Time.frameCount;
        playStartTime = Time.time;

        Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDeltaTime;

        if (ScoreSystem.Instance != null) ScoreSystem.Instance.ResetAll();
    }

    /// <summary>PlayerHealth가 죽을 때 호출한다.</summary>
    public void ReportPlayerDeath()
    {
        if (State != GameState.Playing || isDying) return;
        StartCoroutine(DeathSequence());
    }

    IEnumerator DeathSequence()
    {
        isDying = true;

        // 먼저 대시 쪽 시간 조작을 정리한 뒤에 사망 감속을 건다.
        // 순서가 반대면 히트스톱 코루틴이 뒤늦게 끝나며 감속을 풀어버린다.
        if (playerDash != null) playerDash.CancelAll();

        Time.timeScale = deathSlowScale;
        Time.fixedDeltaTime = defaultFixedDeltaTime * deathSlowScale;

        yield return new WaitForSecondsRealtime(deathSequenceDuration);

        // 게임오버 중에는 완전히 정지 — 적도 투사체도 멈춘다
        Time.timeScale = 0f;
        Time.fixedDeltaTime = defaultFixedDeltaTime;

        isDying = false;
        State = GameState.GameOver;
    }

    public void Restart()
    {
        // 리로드 전에 반드시 되돌린다. timeScale/fixedDeltaTime은 씬을 넘어 유지되기 때문에
        // 0인 채로 넘어가면 새 씬이 멈춘 채로 시작한다.
        Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDeltaTime;

        Scene active = SceneManager.GetActiveScene();
        SceneManager.LoadScene(active.buildIndex >= 0 ? active.buildIndex : 0);
    }
}
