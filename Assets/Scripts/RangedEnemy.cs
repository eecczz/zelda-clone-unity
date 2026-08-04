using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어와 일정 거리를 유지하며 돌을 던지는 적.
/// 근접 적(Enemy)의 체력/사망/베기 판정을 그대로 물려받는다.
/// </summary>
public class RangedEnemy : Enemy
{
    [Header("거리 유지")]
    [Tooltip("이보다 가까우면 후퇴한다.")]
    [SerializeField] private float preferredMinDistance = 8f;
    [Tooltip("이보다 멀면 접근한다. 사이에 있으면 제자리에서 조준.")]
    [SerializeField] private float preferredMaxDistance = 12f;
    [Tooltip("후퇴 속도 배율 (뒷걸음질은 조금 느리게)")]
    [SerializeField] private float retreatSpeedMultiplier = 0.8f;

    [Header("사격")]
    [SerializeField] private RockProjectile rockPrefab;
    [Tooltip("발사 간격 최소 (초)")]
    [SerializeField] private float minFireInterval = 2f;
    [Tooltip("발사 간격 최대 (초)")]
    [SerializeField] private float maxFireInterval = 4f;
    [Tooltip("이 거리 안에 들어와야 발사한다.")]
    [SerializeField] private float fireRange = 14f;
    [Tooltip("돌이 생성되는 몸 앞쪽 거리")]
    [SerializeField] private float muzzleForward = 0.8f;
    [Tooltip("돌이 생성되는 높이 (발밑 기준)")]
    [SerializeField] private float muzzleHeight = 1f;

    [Header("예비동작 (텔레그래프)")]
    [Tooltip("발사 전 몸을 부풀리는 시간 (초). 이 동안은 움직이지 않는다.")]
    [SerializeField] private float windupDuration = 0.5f;
    [Tooltip("예비동작 최대 크기 배율")]
    [SerializeField] private float windupScale = 1.35f;

    private Vector3 baseScale;
    private float nextFireTime;
    private bool isWindingUp;

    protected override void Awake()
    {
        base.Awake();
        baseScale = transform.localScale;
    }

    protected override void Start()
    {
        base.Start();
        nextFireTime = Time.time + Random.Range(minFireInterval, maxFireInterval);
    }

    protected override void UpdateBehaviour(Vector3 dirToTarget, float distance)
    {
        // 예비동작 중에는 발을 묶어서 "지금 던진다"가 읽히게 한다
        if (isWindingUp) return;

        if (distance < preferredMinDistance)
            MoveInDirection(-dirToTarget, MoveSpeed * retreatSpeedMultiplier);
        else if (distance > preferredMaxDistance)
            MoveInDirection(dirToTarget, MoveSpeed);

        if (Time.time >= nextFireTime && distance <= fireRange)
            StartCoroutine(FireRoutine());
    }

    IEnumerator FireRoutine()
    {
        isWindingUp = true;

        // 몸을 부풀리는 예비동작. 슬로모 중에는 함께 느려져서 조준할 시간이 생긴다.
        float elapsed = 0f;
        while (elapsed < windupDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / windupDuration);
            transform.localScale = baseScale * Mathf.Lerp(1f, windupScale, t);
            yield return null;
        }

        // 던지는 순간 원래 크기로 탁 돌아온다
        transform.localScale = baseScale;
        Throw();

        isWindingUp = false;
        nextFireTime = Time.time + Random.Range(minFireInterval, maxFireInterval);
    }

    void Throw()
    {
        if (rockPrefab == null || Target == null) return;

        Vector3 toTarget = Target.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f) return;

        Vector3 dir = toTarget.normalized;
        Vector3 spawnPosition = transform.position + dir * muzzleForward + Vector3.up * muzzleHeight;

        RockProjectile rock = Instantiate(rockPrefab, spawnPosition, Quaternion.LookRotation(dir));
        rock.Launch(dir);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        DrawRing(preferredMinDistance);
        Gizmos.color = Color.blue;
        DrawRing(preferredMaxDistance);
    }

    void DrawRing(float radius, int segments = 40)
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
