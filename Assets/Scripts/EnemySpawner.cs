using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 야생 적 스포너. 플레이어 주변 링(minRadius~maxRadius) 위에 드문드문 적을 흘려보내
/// 거점 사이를 이동하는 동안 필드가 완전히 비지 않게 하는 양념 역할이다.
/// 실제 밀도는 EnemyCamp가 담당한다.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("근접 적 프리팹")]
    [SerializeField] private Enemy meleeEnemyPrefab;
    [Tooltip("원거리 적 프리팹. 비워두면 근접만 스폰된다.")]
    [SerializeField] private RangedEnemy rangedEnemyPrefab;
    [Tooltip("원거리 적이 뽑힐 확률. 0.3 = 근접:원거리 7:3")]
    [SerializeField, Range(0f, 1f)] private float rangedSpawnChance = 0.3f;
    [Tooltip("비워두면 Player 태그를 가진 오브젝트를 자동으로 찾는다.")]
    [SerializeField] private Transform player;

    [Header("스폰")]
    [Tooltip("첫 스폰까지의 지연 (초)")]
    [SerializeField] private float startDelay = 1f;

    [Header("난이도 상승")]
    [Tooltip("이 시간에 걸쳐 최대 난이도까지 올라간다 (초)")]
    [SerializeField] private float rampDuration = 120f;
    [Tooltip("난이도 진행 곡선. 가로축=경과 비율, 세로축=난이도(0~1)")]
    [SerializeField] private AnimationCurve difficultyCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("시작 스폰 간격 (초)")]
    [SerializeField] private float startSpawnInterval = 12f;
    [Tooltip("최대 난이도에서의 스폰 간격 (초)")]
    [SerializeField] private float endSpawnInterval = 8f;
    [Tooltip("시작 최대 동시 마리수")]
    [SerializeField] private int startMaxAlive = 2;
    [Tooltip("최대 난이도에서의 최대 동시 마리수")]
    [SerializeField] private int endMaxAlive = 3;

    [Header("스폰 위치")]
    [Tooltip("플레이어로부터의 최소 거리")]
    [SerializeField] private float minRadius = 12f;
    [Tooltip("플레이어로부터의 최대 거리")]
    [SerializeField] private float maxRadius = 18f;
    [Tooltip("바닥에서 띄울 높이 (캡슐 중심이 바닥에 박히지 않게)")]
    [SerializeField] private float spawnHeight = 1f;

    private readonly List<Enemy> alive = new List<Enemy>();
    private float nextSpawnTime;

    public int AliveCount => alive.Count;

    void Start()
    {
        if (player == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found != null) player = found.transform;
        }

        nextSpawnTime = Time.time + startDelay;
    }

    void Update()
    {
        if (player == null) return;
        if (meleeEnemyPrefab == null && rangedEnemyPrefab == null) return;

        // 시작 전/게임오버 중에는 스폰하지 않고, 첫 스폰 타이머를 계속 미뤄둔다
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying)
        {
            nextSpawnTime = Time.time + startDelay;
            return;
        }

        if (Time.time < nextSpawnTime) return;

        // 거점 경비가 도망치다 사라지는 등 Died 없이 파괴되는 경우가 있어 빈 칸을 정리한다
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            if (alive[i] == null) alive.RemoveAt(i);
        }

        float difficulty = CurrentDifficulty();
        nextSpawnTime = Time.time + Mathf.Lerp(startSpawnInterval, endSpawnInterval, difficulty);

        int maxAlive = Mathf.RoundToInt(Mathf.Lerp(startMaxAlive, endMaxAlive, difficulty));
        if (alive.Count >= maxAlive) return;

        Spawn();
    }

    /// <summary>경과 시간을 0~1 난이도로 환산한다.</summary>
    float CurrentDifficulty()
    {
        float playTime = GameManager.Instance != null
            ? GameManager.Instance.PlayTime
            : Time.timeSinceLevelLoad;

        float t = rampDuration > 0f ? Mathf.Clamp01(playTime / rampDuration) : 1f;
        return Mathf.Clamp01(difficultyCurve.Evaluate(t));
    }

    void Spawn()
    {
        Enemy prefab = PickPrefab();
        if (prefab == null) return;

        float angle = Random.Range(0f, Mathf.PI * 2f);
        float radius = Random.Range(minRadius, maxRadius);

        Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        Vector3 position = player.position + offset;
        position.y = player.position.y + spawnHeight;

        Enemy enemy = Instantiate(prefab, position, Quaternion.identity);
        enemy.SetTarget(player);

        alive.Add(enemy);
        enemy.Died += HandleEnemyDied;
    }

    /// <summary>비율에 따라 근접/원거리 중 하나를 고른다. 한쪽이 비어 있으면 있는 쪽으로.</summary>
    Enemy PickPrefab()
    {
        if (rangedEnemyPrefab == null) return meleeEnemyPrefab;
        if (meleeEnemyPrefab == null) return rangedEnemyPrefab;

        return Random.value < rangedSpawnChance ? rangedEnemyPrefab : meleeEnemyPrefab;
    }

    void HandleEnemyDied(Enemy enemy)
    {
        enemy.Died -= HandleEnemyDied;
        alive.Remove(enemy);
    }

    void OnDrawGizmosSelected()
    {
        Transform origin = player != null ? player : transform;

        Gizmos.color = Color.yellow;
        DrawCircle(origin.position, minRadius);
        Gizmos.color = Color.red;
        DrawCircle(origin.position, maxRadius);
    }

    static void DrawCircle(Vector3 center, float radius, int segments = 48)
    {
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float a = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
