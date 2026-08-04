using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 주변 링(minRadius~maxRadius) 위 랜덤 위치에 주기적으로 적을 스폰한다.
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
    [Tooltip("스폰 간격 (초)")]
    [SerializeField] private float spawnInterval = 2f;
    [Tooltip("첫 스폰까지의 지연 (초)")]
    [SerializeField] private float startDelay = 1f;
    [Tooltip("동시에 존재할 수 있는 최대 마리수")]
    [SerializeField] private int maxAlive = 10;

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
        if (Time.time < nextSpawnTime) return;

        nextSpawnTime = Time.time + spawnInterval;

        if (alive.Count >= maxAlive) return;

        Spawn();
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
