using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StatusIndicatorController : MonoBehaviour
{
    public enum ProcessState
    {
        Waiting,
        Running,
        Paused
    }

    [Header("상태 텍스트")]
    public TMP_Text statusText;

    [Header("동그라미 위치")]
    public float dotSize = 11f;
    public Vector2 dotOffset = new Vector2(-16f, 8f);

    [Header("반짝임 주기")]
    public float runningBlinkSpeed = 0.7f;
    public float pausedBlinkSpeed = 1.5f;

    [Header("상태 색상")]
    public Color waitingColor = new Color(0.55f, 0.6f, 0.65f);
    public Color runningColor = new Color(0.15f, 0.95f, 0.45f);
    public Color pausedColor = new Color(1f, 0.8f, 0.15f);

    [Header("현재 공정 상태")]
    [SerializeField]
    private ProcessState currentState = ProcessState.Waiting;

    private Image statusDot;
    private Sprite circleSprite;
    private Texture2D circleTexture;

    private void Awake()
    {
        CreateStatusDot();
    }

    private void Start()
    {
        SetWaiting();
    }

    private void Update()
    {
        if (statusDot == null)
            return;

        // 대기 중: 회색 고정
        if (currentState == ProcessState.Waiting)
        {
            statusDot.color = waitingColor;
            return;
        }

        // 일시정지: 노란색 고정 (깜빡임 없음)
        if (currentState == ProcessState.Paused)
        {
            statusDot.color = pausedColor;
            return;
        }

        // 가동 중: 초록색 반짝임
        float duration = Mathf.Max(0.1f, runningBlinkSpeed);

        float pulse =
            (Mathf.Sin(
                Time.unscaledTime * Mathf.PI * 2f / duration
            ) + 1f) * 0.5f;

        float alpha = Mathf.Lerp(0.25f, 1f, pulse);

        Color color = runningColor;
        color.a = alpha;

        statusDot.color = color;
    }

    private void CreateStatusDot()
    {
        if (statusText == null)
        {
            Debug.LogWarning(
                "[StatusIndicator] Status Text가 연결되지 않았습니다."
            );
            return;
        }

        Transform existing =
            statusText.transform.Find("AutoStatusDot");

        if (existing != null)
        {
            statusDot = existing.GetComponent<Image>();

            if (statusDot != null)
            {
                SetupDotPosition();
                return;
            }
        }

        GameObject dotObject = new GameObject(
            "AutoStatusDot",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );

        dotObject.transform.SetParent(
            statusText.transform,
            false
        );

        statusDot = dotObject.GetComponent<Image>();

        circleSprite = CreateCircleSprite(64);

        statusDot.sprite = circleSprite;
        statusDot.color = waitingColor;
        statusDot.raycastTarget = false;

        SetupDotPosition();
    }

    private void SetupDotPosition()
    {
        if (statusDot == null)
            return;

        RectTransform rect =
            statusDot.GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        rect.sizeDelta = new Vector2(dotSize, dotSize);

        // 동그라미 위치
        rect.anchoredPosition = dotOffset;
    }

    private Sprite CreateCircleSprite(int resolution)
    {
        circleTexture = new Texture2D(
            resolution,
            resolution,
            TextureFormat.RGBA32,
            false
        );

        circleTexture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[resolution * resolution];

        float center = (resolution - 1) * 0.5f;
        float radius = center;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dx = x - center;
                float dy = y - center;

                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                float alpha = Mathf.Clamp01(
                    radius - distance
                );

                pixels[y * resolution + x] =
                    new Color(1f, 1f, 1f, alpha);
            }
        }

        circleTexture.SetPixels(pixels);
        circleTexture.Apply();

        return Sprite.Create(
            circleTexture,
            new Rect(0, 0, resolution, resolution),
            new Vector2(0.5f, 0.5f),
            100f
        );
    }

    public void SetState(ProcessState state)
    {
        currentState = state;

        if (statusText == null)
            return;

        switch (state)
        {
            case ProcessState.Waiting:
                statusText.text = "대기 중";
                statusText.color = waitingColor;
                break;

            case ProcessState.Running:
                statusText.text = "가동 중";
                statusText.color = runningColor;
                break;

            case ProcessState.Paused:
                statusText.text = "일시정지";
                statusText.color = pausedColor;
                break;
        }

        if (statusDot != null)
        {
            statusDot.color =
                state == ProcessState.Waiting
                    ? waitingColor
                    : state == ProcessState.Running
                        ? runningColor
                        : pausedColor;
        }
    }

    public void SetWaiting()
    {
        SetState(ProcessState.Waiting);
    }

    public void SetRunning()
    {
        SetState(ProcessState.Running);
    }

    public void SetPaused()
    {
        SetState(ProcessState.Paused);
    }

    private void OnDestroy()
    {
        if (circleSprite != null)
            Destroy(circleSprite);

        if (circleTexture != null)
            Destroy(circleTexture);
    }
}