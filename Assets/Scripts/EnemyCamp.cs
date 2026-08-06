using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 거점. 빈 오브젝트에 붙이고 위치만 잡으면 시작할 때 토템과 경비를 알아서 만든다.
/// 토템이 부서지면 클리어되고 남은 경비는 도망친다.
/// </summary>
public class EnemyCamp : MonoBehaviour
{
    private static readonly List<EnemyCamp> all = new List<EnemyCamp>();

    /// <summary>씬에 존재하는 모든 거점.</summary>
    public static IReadOnlyList<EnemyCamp> All => all;

    public static int TotalCount => all.Count;

    public static int ClearedCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].IsCleared) count++;
            }
            return count;
        }
    }

    /// <summary>모든 거점이 정리됐는지. 거점이 하나도 없으면 false.</summary>
    public static bool AllCleared => all.Count > 0 && ClearedCount >= all.Count;

    /// <summary>가장 가까운 미클리어 거점. 없으면 null.</summary>
    public static EnemyCamp NearestUncleared(Vector3 from)
    {
        EnemyCamp best = null;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < all.Count; i++)
        {
            EnemyCamp camp = all[i];
            if (camp == null || camp.IsCleared) continue;

            float sqr = (camp.transform.position - from).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = camp;
            }
        }

        return best;
    }

    [Header("구성")]
    [SerializeField] private CampTotem totemPrefab;
    [SerializeField] private Enemy meleeGuardPrefab;
    [SerializeField] private RangedEnemy rangedGuardPrefab;

    [Header("경비")]
    [SerializeField] private int minGuards = 4;
    [SerializeField] private int maxGuards = 8;
    [Tooltip("경비 중 원거리가 뽑힐 확률. 0.35 = 대략 근접 2 : 원거리 1")]
    [SerializeField, Range(0f, 1f)] private float rangedGuardRatio = 0.35f;

    [Header("반경")]
    [Tooltip("경비가 배회하는 범위")]
    [SerializeField] private float campRadius = 8f;
    [Tooltip("이 안에 플레이어가 들어오면 경비가 추격을 시작한다.")]
    [SerializeField] private float aggroRadius = 12f;
    [Tooltip("추격 중 플레이어가 이보다 멀어지면 거점으로 복귀한다.")]
    [SerializeField] private float leashRadius = 22f;

    [Header("배치")]
    [Tooltip("경비가 놓이는 높이 (바닥 기준)")]
    [SerializeField] private float guardSpawnHeight = 1f;
    [Tooltip("토템이 놓이는 높이 (바닥 기준)")]
    [SerializeField] private float totemHeight = 1f;
    [Tooltip("경비가 토템에 겹치지 않도록 띄우는 최소 거리")]
    [SerializeField] private float minGuardDistanceFromTotem = 2.5f;

    [Header("클리어 연출")]
    [SerializeField] private int explosionBoltCount = 12;
    [SerializeField] private float explosionRadius = 7f;
    [SerializeField] private float explosionFlashAlpha = 0.45f;
    [SerializeField] private float explosionFlashDuration = 0.25f;
    [SerializeField] private LightningBoltSettings explosionBolt = new LightningBoltSettings
    {
        minSegments = 10,
        maxSegments = 16,
        amplitude = 0.9f,
        glowWidth = 0.7f,
        coreWidth = 0.18f,
        minDuration = 0.3f,
        maxDuration = 0.5f,
        branchChance = 1f,
        minBranches = 2,
        maxBranches = 3,
        branchLengthRatio = 0.45f,
    };

    private readonly List<Enemy> guards = new List<Enemy>();
    private CampTotem totem;

    public bool IsCleared { get; private set; }
    public Vector3 Center => transform.position;

    void Awake()
    {
        all.Add(this);
    }

    void OnDestroy()
    {
        all.Remove(this);
    }

    void Start()
    {
        SpawnTotem();
        SpawnGuards();
    }

    void SpawnTotem()
    {
        if (totemPrefab == null)
        {
            Debug.LogWarning($"[EnemyCamp] {name}: Totem Prefab이 비어 있어 이 거점은 클리어할 수 없습니다.", this);
            return;
        }

        Vector3 position = transform.position + Vector3.up * totemHeight;
        totem = Instantiate(totemPrefab, position, transform.rotation, transform);
        totem.Destroyed += OnTotemDestroyed;
    }

    void SpawnGuards()
    {
        int count = Random.Range(minGuards, maxGuards + 1);
        Transform player = FindPlayer();

        for (int i = 0; i < count; i++)
        {
            Enemy prefab = PickGuardPrefab();
            if (prefab == null) continue;

            Enemy guard = Instantiate(prefab, RandomGuardPosition(), Quaternion.identity);
            if (player != null) guard.SetTarget(player);

            guard.AssignToCamp(this, transform.position, campRadius, aggroRadius, leashRadius);
            guard.Died += OnGuardDied;

            guards.Add(guard);
        }
    }

    Enemy PickGuardPrefab()
    {
        if (rangedGuardPrefab == null) return meleeGuardPrefab;
        if (meleeGuardPrefab == null) return rangedGuardPrefab;

        return Random.value < rangedGuardRatio ? rangedGuardPrefab : meleeGuardPrefab;
    }

    Vector3 RandomGuardPosition()
    {
        // 토템 바로 위에 겹치지 않도록 안쪽 반경을 비워둔다
        float radius = Random.Range(Mathf.Min(minGuardDistanceFromTotem, campRadius), campRadius);
        float angle = Random.Range(0f, Mathf.PI * 2f);

        Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        return transform.position + offset + Vector3.up * guardSpawnHeight;
    }

    static Transform FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.transform : null;
    }

    void OnGuardDied(Enemy guard)
    {
        guard.Died -= OnGuardDied;
        guards.Remove(guard);
    }

    void OnTotemDestroyed(CampTotem destroyed)
    {
        if (IsCleared) return;

        destroyed.Destroyed -= OnTotemDestroyed;
        IsCleared = true;

        PlayClearExplosion(destroyed.transform.position);

        // 남은 경비는 흩어져 사라진다
        for (int i = guards.Count - 1; i >= 0; i--)
        {
            Enemy guard = guards[i];
            if (guard == null || guard.IsDead) continue;
            guard.StartFleeing();
        }
        guards.Clear();
    }

    void PlayClearExplosion(Vector3 center)
    {
        // 사방으로 뻗는 번개 대폭발. 볼트 상한(20)에 걸리므로 개수는 적당히.
        for (int i = 0; i < explosionBoltCount; i++)
        {
            float angle = (i / (float)explosionBoltCount) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
            Vector3 dir = new Vector3(Mathf.Cos(angle), Random.Range(0.1f, 0.6f), Mathf.Sin(angle)).normalized;

            LightningBolt.Create(center, center + dir * explosionRadius * Random.Range(0.6f, 1.2f), explosionBolt);
        }

        SlashVfx.Play(center, LightningPalette.Glow, 60, 0.9f, 9f);
        ScreenFlash.Play(LightningPalette.Flash, explosionFlashAlpha, explosionFlashDuration);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        DrawRing(campRadius);
        Gizmos.color = Color.yellow;
        DrawRing(aggroRadius);
        Gizmos.color = Color.red;
        DrawRing(leashRadius);
    }

    void DrawRing(float radius, int segments = 48)
    {
        Vector3 center = transform.position;
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
