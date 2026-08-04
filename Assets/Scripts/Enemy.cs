using System;
using UnityEngine;

/// <summary>
/// 플레이어를 향해 천천히 접근하는 적. 체력이 0이 되면 즉시 파괴되며 사망 위치를 알린다.
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

    /// <summary>아무 적이나 죽었을 때 사망 위치를 알린다. (VFX/사운드/스코어용)</summary>
    public static event Action<Vector3> AnyEnemyDied;

    /// <summary>이 적이 죽었을 때. 스포너가 생존 수를 세는 데 사용.</summary>
    public event Action<Enemy> Died;

    private Transform target;
    private int health;
    private bool isDead;

    public bool IsDead => isDead;

    /// <summary>파생 클래스가 쓰는 추격 대상.</summary>
    protected Transform Target => target;
    protected float MoveSpeed => moveSpeed;
    protected float RotationSpeed => rotationSpeed;

    protected virtual void Awake()
    {
        health = Mathf.Max(1, maxHealth);
    }

    protected virtual void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }
    }

    /// <summary>스포너가 스폰 직후 추격 대상을 지정한다.</summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    protected virtual void Update()
    {
        if (isDead || target == null) return;

        // XZ 평면에서만 추격 (높이는 프리팹이 놓인 그대로 유지)
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        float distance = toTarget.magnitude;
        if (distance < 0.001f) return;

        Vector3 dir = toTarget / distance;

        UpdateBehaviour(dir, distance);
        FaceDirection(dir);
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
        Quaternion look = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, rotationSpeed * Time.deltaTime);
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
