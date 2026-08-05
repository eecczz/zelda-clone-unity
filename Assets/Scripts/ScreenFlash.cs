using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 풀스크린 플래시. 전용 Canvas를 코드로 만들어 쓰므로 에디터 세팅이 필요 없다.
/// 히트스톱 중에도 제 속도로 사라져야 하니 시간은 전부 실제 시간 기준.
/// </summary>
public class ScreenFlash : MonoBehaviour
{
    private static ScreenFlash instance;

    private Image image;
    private Coroutine fadeRoutine;

    /// <summary>화면을 alpha만큼 덮었다가 duration에 걸쳐 지운다.</summary>
    public static void Play(Color color, float alpha, float duration)
    {
        if (!Application.isPlaying || alpha <= 0f) return;

        ScreenFlash flash = GetOrCreate();
        if (flash != null) flash.Begin(color, alpha, duration);
    }

    static ScreenFlash GetOrCreate()
    {
        // Unity의 == null은 파괴된 오브젝트도 잡아주므로 씬 리로드 후에도 안전하다
        if (instance != null) return instance;

        var root = new GameObject("ScreenFlash");

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;   // HUD 위

        var imageObject = new GameObject("Flash");
        imageObject.transform.SetParent(root.transform, false);

        var image = imageObject.AddComponent<Image>();
        image.raycastTarget = false;   // 클릭을 가로채지 않게
        image.color = Color.clear;

        // 화면 전체로 늘리기
        var rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        instance = root.AddComponent<ScreenFlash>();
        instance.image = image;
        return instance;
    }

    void Begin(Color color, float alpha, float duration)
    {
        if (image == null) return;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(color, alpha, duration));
    }

    IEnumerator FadeRoutine(Color color, float alpha, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;

            color.a = Mathf.Lerp(alpha, 0f, t);
            image.color = color;

            yield return null;
        }

        image.color = Color.clear;
        fadeRoutine = null;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
