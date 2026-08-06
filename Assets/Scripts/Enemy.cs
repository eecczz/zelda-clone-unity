using System;
using UnityEngine;

/// <summary>적의 행동 모드.</summary>
public enum EnemyMode
{
    /// <summary>플레이어 추격. 거점이 없는 야생 적은 항상 이 모드.</summary>
    Chase,
    /// <summary>거점 주변 배회.</summary>
    Wander,
    /// <summary>거점으로 복귀.</summary>
    Return,
    /// <summary>거점이 무너져 도망치는 중. 잠시 뒤 소멸.</summary>
    Flee,
}

/// <summary>
/// 플레이어를 추격하는 적. 거점에 배속되면 평소엔 주변을 배회하다가
/// 어그로 반경에 플레이어가 들어오면 추격하고, 멀어지면 복귀한다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Enemy : MonoBehaviour, IDamageable
{
    [Header("스탯")]
    [SerializeField] private int maxHealth = 1;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float rotationSpeed = 8f;
    [Tooltip("플레이어에게 이 거리까지 붙으면 더 접근하지 않는다.")]
    [SerializeField] private float stopDistance = 1f;

    [Header("접촉 공격")]
    [Tooltip("이 거리 안에 들어오면 플레이어에게 피해를 준다. 대시 중에는 무효.")]
    [SerializeField] private float contactDistance = 1.2f;
    [SerializeField] private int contactDamage = 1;
    [Tooltip("붙어 있는 동안 다시 때리기까지의 간격 (초). 매 프레임 피해가 들어가지 않게 한다.")]
    [SerializeField] private float contactRepeatDelay = 1f;

    [Header("거점 경비")]
    [Tooltip("배회 시 이동 속도 배율")]
    [SerializeField] private float wanderSpeedMultiplier = 0.5f;
    [Tooltip("배회 목표점에 이만큼 붙으면 새 목표를 고른다.")]
    [SerializeField] private float wanderArriveDistance = 0.8f;
    [Tooltip("도망칠 때의 속도 배율")]
    [SerializeField] private float fleeSpeedMultiplier = 2.2f;
    [Tooltip("도망 시작 후 소멸까지의 시간 (초)")]
    [SerializeField] private float fleeDuration = 1.5f;

    /// <summary>아무 적이나 죽었을 때 사망 위치를 알린다. (VFX/사운드/스코어용)</summary>
    public static event Action<Vector3> AnyEnemyDied;

    /// <summary>이 적이 죽었을 때. 스포너가 생존 수를 세는 데 사용.</summary>
    public event Action<Enemy> Died;

    private Transform target;
    private IDamageable targetDamageable;
    private PlayerDash targetDash;
    private int health;
    private bool isDead;

    private EnemyCamp camp;
    private Vector3 homePosition;
    private float patrolRadius;
    private float aggroRadius;
    private float leashRadius;

    private EnemyMode mode = EnemyMode.Chase;
    private Vector3 wanderPoint;
    private float fleeEndTime;
    private Vector3 fleeDirection;
    private Vector3 initialScale;

    private bool touchingPlayer;
    private float nextContactTime;

    public bool IsDead => isDead;

    /// <summary>파생 클래스가 쓰는 추격 대상.</summary>
    protected Transform Target => target;
    protected float MoveSpeed => moveSpeed;
    protected float RotationSpeed => rotationSpeed;
    protected EnemyMode Mode => mode;

    protected virtual void Awake()
    {
        health = Mathf.Max(1, maxHealth);
        initialScale = transform.localScale;
    }

    protected virtual void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }

        CacheTargetComponents();
    }

    /// <summary>스포너가 스폰 직후 추격 대상을 지정한다.</summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        CacheTargetComponents();
    }

    /// <summary>
    /// 거점 경비로 배속한다. 이후로는 거점 주변을 배회하며 어그로 반경만 지킨다.
    /// </summary>
    public void AssignToCamp(EnemyCamp owner, Vector3 home, float patrol, float aggro, float leash)
    {
        camp = owner;
        homePosition = home;
        patrolRadius = Mathf.Max(1f, patrol);
        aggroRadius = aggro;
        leashRadius = Mathf.Max(aggro + 1f, leash);

        mode = EnemyMode.Wander;
        PickWanderPoint();
    }

    /// <summary>거점이 무너졌을 때. 잠시 도망치다 사라진다.</summary>
    public void StartFleeing()
    {
        if (isDead || mode == EnemyMode.Flee) return;

        mode = EnemyMode.Flee;
        fleeEndTime = Time.time + fleeDuration;

        Vector3 away = target != null ? transform.position - target.position : transform.position - homePosition;
        away.y = 0f;
        fleeDirection = away.sqrMagnitude > 0.0001f ? away.normalized : transform.forward;

        OnFleeStarted();
    }

    /// <summary>도망 시작 시 파생 클래스가 진행 중인 동작을 정리할 훅.</summary>
    protected virtual void OnFleeStarted() { }

    void CacheTargetComponents()
    {
        if (target == null) return;
        targetDamageable = target.GetComponentInParent<IDamageable>();
        targetDash = target.GetComponentInParent<PlayerDash>();
    }

    protected virtual void Update()
    {
        if (isDead) return;

        if (mode == EnemyMode.Flee)
        {
            UpdateFlee();
            return;
        }

        if (target == null) return;

        // XZ 평면에서만 판단 (높이는 프리팹이 놓인 그대로 유지)
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        float distance = toTarget.magnitude;
        Vector3 dirToTarget = distance > 0.001f ? toTarget / distance : transform.forward;

        UpdateMode(distance);

        switch (mode)
        {
            case EnemyMode.Chase:
                UpdateBehaviour(dirToTarget, distance);
                FaceDirection(dirToTarget);
                break;

            case EnemyMode.Wander:
                MoveTowardPoint(wanderPoint, moveSpeed * wanderSpeedMultiplier, wanderArriveDistance, PickWanderPoint);
                break;

            case EnemyMode.Return:
                MoveTowardPoint(homePosition, moveSpeed, patrolRadius, null);
                break;
        }

        UpdateContact(distance);
    }

    /// <summary>어그로/리쉬 판정. 거점이 없는 적은 항상 추격이다.</summary>
    void UpdateMode(float distanceToTarget)
    {
        if (camp == null)
        {
            mode = EnemyMode.Chase;
            return;
        }

        if (mode == EnemyMode.Chase)
        {
            if (distanceToTarget > leashRadius) mode = EnemyMode.Return;
            return;
        }

        if (distanceToTarget <= aggroRadius)
        {
            mode = EnemyMode.Chase;
            return;
        }

        if (mode == EnemyMode.Return && HorizontalDistance(transform.position, homePosition) <= patrolRadius)
        {
            mode = EnemyMode.Wander;
            PickWanderPoint();
        }
    }

    void MoveTowardPoint(Vector3 point, float speed, float arriveDistance, Action onArrive)
    {
        Vector3 toPoint = point - transform.position;
        toPoint.y = 0f;

        float distance = toPoint.magnitude;
        if (distance <= arriveDistance)
        {
            onArrive?.Invoke();
            return;
        }

        Vector3 dir = toPoint / distance;
        MoveInDirection(dir, speed);
        FaceDirection(dir);
    }

    void PickWanderPoint()
    {
        Vector2 offset = UnityEngine.Random.insideUnitCircle * patrolRadius;
        wanderPoint = homePosition + new Vector3(offset.x, 0f, offset.y);
    }

    void UpdateFlee()
    {
        if (Time.time >= fleeEndTime)
        {
            Destroy(gameObject);
            return;
        }

        MoveInDirection(fleeDirection, moveSpeed * fleeSpeedMultiplier);
        FaceDirection(fleeDirection);

        // 도망치면서 작아지다 사라진다
        float remaining = Mathf.InverseLerp(fleeEndTime, fleeEndTime - fleeDuration, Time.time);
        transform.localScale = initialScale * Mathf.Clamp01(remaining);
    }

    /// <summary>
    /// 접촉 피해. 닿는 순간 1회가 원칙이고, 계속 붙어 있으면
    /// contactRepeatDelay 간격으로만 다시 들어간다 (매 프레임 틱 방지).
    /// </summary>
    void UpdateContact(float distance)
    {
        bool inRange = distance <= contactDistance;

        if (inRange)
        {
            bool justTouched = !touchingPlayer;
            if (justTouched || Time.time >= nextContactTime) TryContactDamage();
        }

        touchingPlayer = inRange;
    }

    void TryContactDamage()
    {
        // 대시 중인 플레이어는 관통 특권이 있다
        if (targetDash != null && targetDash.IsDashing) return;
        if (targetDamageable == null || targetDamageable.IsDead) return;

        nextContactTime = Time.time + contactRepeatDelay;
        targetDamageable.TakeDamage(contactDamage);
    }

    /// <summary>거리에 따른 행동. 원거리 적은 이걸 오버라이드해 거리 유지 + 사격을 한다.</summary>
    protected virtual void UpdateBehaviour(Vector3 dirToTarget, float distance)
    {
        if (distance > stopDistance)
            MoveInDirection(dirToTarget, moveSpeed);
    }

    protected void MoveInDirection(Vector3 dir, float speed)
    {
        transform.position += dir * speed * Time.deltaTime;
    }

    protected void FaceDirection(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion look = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, rotationSpeed * Time.deltaTime);
    }

    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    public void TakeDamage(int amount)
    {
        if (isDead) return;

        health -= Mathf.Max(1, amount);
        if (health <= 0) Die();
    }

    /// <summary>체력과 무관하게 즉사시킨다. (대시 베기용)</summary>
    public void Kill()
    {
        if (isDead) return;
        Die();
    }

    void Die()
    {
        isDead = true;

        Vector3 deathPosition = transform.position;
        Died?.Invoke(this);
        AnyEnemyDied?.Invoke(deathPosition);

        Destroy(gameObject);
    }
}
