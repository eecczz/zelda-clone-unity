using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 거점 중앙의 토템. 대시로 벨 수 있고, 정해진 횟수만큼 맞으면 부서지며 거점을 클리어시킨다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CampTotem : MonoBehaviour, IDamageable
{
    [Header("스탯")]
    [SerializeField] private int maxHealth = 3;

    [Header("피격 연출")]
    [Tooltip("한 대 맞을 때마다 움츠러드는 정도")]
    [SerializeField] private float hitShrink = 0.12f;
    [SerializeField] private float hitShrinkRecovery = 6f;
    [SerializeField] private Color hitFlashColor = new Color(1f, 0.6f, 0.4f);

    /// <summary>부서졌을 때. 거점이 이 신호로 클리어 처리를 한다.</summary>
    public event Action<CampTotem> Destroyed;

    private int health;
    private bool isDead;
    private Vector3 baseScale;
    private Renderer[] renderers;
    private Color[] baseColors;
    private float flashAmount;

    public bool IsDead => isDead;
    public int Health => health;
    public int MaxHealth => maxHealth;

    void Awake()
    {
        health = Mathf.Max(1, maxHealth);
        baseScale = transform.localScale;

        // material(인스턴스)을 건드려야 이 토템만 색이 바뀐다.
        // _Color가 없는 셰이더를 쓴 프리팹이면 색 연출은 건너뛰고 크기 반응만 남긴다.
        var tinted = new List<Renderer>();
        var colors = new List<Color>();

        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (r == null || !r.material.HasProperty("_Color")) continue;
            tinted.Add(r);
            colors.Add(r.material.color);
        }

        renderers = tinted.ToArray();
        baseColors = colors.ToArray();
    }

    public void TakeDamage(int amount)
    {
        if (isDead) return;

        health -= Mathf.Max(1, amount);
        flashAmount = 1f;
        transform.localScale = baseScale * (1f - hitShrink);

        if (health <= 0) Break();
    }

    void Update()
    {
        if (isDead) return;

        // 움츠러든 크기와 붉은 기를 원래대로 되돌린다
        transform.localScale = Vector3.Lerp(transform.localScale, baseScale, hitShrinkRecovery * Time.deltaTime);

        if (flashAmount <= 0f) return;

        flashAmount = Mathf.Max(0f, flashAmount - hitShrinkRecovery * Time.deltaTime);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].material.color = Color.Lerp(baseColors[i], hitFlashColor, flashAmount);
        }
    }

    void Break()
    {
        isDead = true;
        Destroyed?.Invoke(this);
        Destroy(gameObject);
    }
}
