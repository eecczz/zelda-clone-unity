using UnityEngine;

/// <summary>
/// 연출 색 팔레트. 톤을 바꾸고 싶으면 여기만 고치면 된다.
/// (인스펙터에 이미 직렬화된 값이 있으면 그쪽이 우선하므로,
///  일괄 적용하려면 컴포넌트 우클릭 → Reset으로 기본값을 다시 받으면 된다)
/// </summary>
public static class LightningPalette
{
    /// <summary>번개 겉광 — 청백 #B8E8FF</summary>
    public static readonly Color Glow = new Color(0.722f, 0.910f, 1f);

    /// <summary>번개 심지 — 순백</summary>
    public static readonly Color Core = Color.white;

    /// <summary>처치 강조 — 레드 (번개 청백과 대비시키는 역할)</summary>
    public static readonly Color Kill = new Color(1f, 0.35f, 0.2f);

    /// <summary>반사 강조 — 골드</summary>
    public static readonly Color Reflect = new Color(1f, 0.85f, 0.3f);

    /// <summary>플레이어 사망</summary>
    public static readonly Color PlayerDeath = new Color(0.4f, 0.8f, 1f);

    /// <summary>풀스크린 플래시</summary>
    public static readonly Color Flash = Color.white;
}
