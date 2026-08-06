using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 체력을 마름모 아이콘으로 표시한다. 아이콘은 코드로 생성하므로
/// 에디터에서는 빈 UI 오브젝트에 이 컴포넌트만 붙이면 된다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class HealthDisplay : MonoBehaviour
{
    [Header("모양")]
    [SerializeField] private float iconSize = 26f;
    [SerializeField] private float spacing = 8f;

    [Header("색")]
    [SerializeField] private Color filledColor = Color.white;
    [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.18f);

    [Header("참조")]
    [Tooltip("비워두면 Player 태그에서 찾는다.")]
    [SerializeField] private PlayerHealth playerHealth;

    private readonly List<Image> icons = new List<Image>();
    private int lastHealth = -1;
    private int lastMax = -1;

    void Start()
    {
        if (playerHealth == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerHealth = player.GetComponentInParent<PlayerHealth>();
        }

        Refresh();
    }

    void Update()
    {
        Refresh();
    }

    void Refresh()
    {
        if (playerHealth == null) return;

        int max = playerHealth.MaxHealth;
        int current = Mathf.Clamp(playerHealth.Health, 0, max);

        if (max != lastMax) Rebuild(max);
        if (current == lastHealth && max == lastMax) return;

        lastHealth = current;
        lastMax = max;

        for (int i = 0; i < icons.Count; i++)
        {
            icons[i].color = i < current ? filledColor : emptyColor;
        }
    }

    void Rebuild(int count)
    {
        foreach (Image icon in icons)
        {
            if (icon != null) Destroy(icon.gameObject);
        }
        icons.Clear();

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject($"Health{i}", typeof(RectTransform));
            go.transform.SetParent(transform, false);

            var image = go.AddComponent<Image>();
            image.sprite = UiSprites.Diamond;
            image.raycastTarget = false;
            image.color = filledColor;

            var rect = image.rectTransform;
            rect.sizeDelta = new Vector2(iconSize, iconSize);

            // 부모의 좌상단 기준으로 가로로 늘어놓는다
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(i * (iconSize + spacing), 0f);

            icons.Add(image);
        }
    }
}
