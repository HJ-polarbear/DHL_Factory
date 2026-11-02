
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RoundedOEEGauge : MaskableGraphic
{
    [Header("OEE 값 (0~100)")]
    [Range(0f, 100f)]
    public float oeeValue = 83.8f;

    [Header("게이지 모양")]
    public float thickness = 15f;
    public int segments = 180;

    [Header("색상")]
    public Color backgroundColor = new Color32(38, 55, 75, 255);
    public Color progressColor = new Color32(34, 211, 238, 255);

    [Header("숫자 텍스트")]
    public TMP_Text valueText;

    protected override void OnEnable()
    {
        base.OnEnable();
        UpdateText();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        float radius = Mathf.Min(
            rectTransform.rect.width,
            rectTransform.rect.height
        ) * 0.5f - thickness * 0.5f - 2f;

        if (radius <= 0f || thickness <= 0f)
            return;

        // 어두운 배경 원
        DrawArc(vh, 1f, radius, backgroundColor, false);

        // 하늘색 진행 원
        float progress = Mathf.Clamp01(oeeValue / 100f);

        if (progress > 0f)
            DrawArc(vh, progress, radius, progressColor, true);
    }

    private void DrawArc(
        VertexHelper vh,
        float progress,
        float radius,
        Color arcColor,
        bool rounded
    )
    {
        int steps = Mathf.Max(
            2,
            Mathf.CeilToInt(segments * progress)
        );

        Vector2 center = rectTransform.rect.center;
        float half = thickness * 0.5f;

        for (int i = 0; i < steps; i++)
        {
            float t1 = progress * i / steps;
            float t2 = progress * (i + 1) / steps;

            float a1 = Mathf.PI * 0.5f - t1 * Mathf.PI * 2f;
            float a2 = Mathf.PI * 0.5f - t2 * Mathf.PI * 2f;

            Vector2 d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
            Vector2 d2 = new Vector2(Mathf.Cos(a2), Mathf.Sin(a2));

            int index = vh.currentVertCount;

            AddVertex(vh, center + d1 * (radius - half), arcColor);
            AddVertex(vh, center + d1 * (radius + half), arcColor);
            AddVertex(vh, center + d2 * (radius + half), arcColor);
            AddVertex(vh, center + d2 * (radius - half), arcColor);

            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }

        if (rounded && progress < 1f)
        {
            DrawCap(vh, center + Vector2.up * radius, half, arcColor);

            float endAngle =
                Mathf.PI * 0.5f - progress * Mathf.PI * 2f;

            Vector2 end = center + new Vector2(
                Mathf.Cos(endAngle),
                Mathf.Sin(endAngle)
            ) * radius;

            DrawCap(vh, end, half, arcColor);
        }
    }

    private void DrawCap(
        VertexHelper vh,
        Vector2 center,
        float radius,
        Color capColor
    )
    {
        int count = 24;
        int start = vh.currentVertCount;

        AddVertex(vh, center, capColor);

        for (int i = 0; i <= count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;

            Vector2 point = center + new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)
            ) * radius;

            AddVertex(vh, point, capColor);
        }

        for (int i = 0; i < count; i++)
        {
            vh.AddTriangle(
                start,
                start + i + 1,
                start + i + 2
            );
        }
    }

    private void AddVertex(
        VertexHelper vh,
        Vector2 position,
        Color vertexColor
    )
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = vertexColor;
        vh.AddVert(vertex);
    }

    public void SetOEE(float value)
    {
        oeeValue = Mathf.Clamp(value, 0f, 100f);
        SetVerticesDirty();
        UpdateText();
    }

    private void UpdateText()
    {
        if (valueText != null)
            valueText.text = oeeValue.ToString("F1") + "%";
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();

        segments = Mathf.Max(12, segments);
        thickness = Mathf.Max(1f, thickness);

        SetVerticesDirty();
        UpdateText();
    }
#endif
}
