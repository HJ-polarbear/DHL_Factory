using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DashboardDetailStatsController : MonoBehaviour
{
    [Header("OEE 데이터 연결")]
    public FactoryOEEController factoryOEE;

    [Header("택배 통계 데이터 연결")]
    public DashboardStatsController dashboardStats;

    [Header("적재함 데이터 연결")]
    public BinCounter generalBin;
    public BinCounter fragileBin;
    public BinCounter foodBin;

    [Header("OEE 원형 게이지")]
    public RoundedOEEGauge oeeRing;
    public TMP_Text oeeValueText;

    [Header("설비 효율 분석 - 숫자")]
    public TMP_Text availabilityValue;
    public TMP_Text performanceValue;
    public TMP_Text qualityValue;

    [Header("설비 효율 분석 - 막대 Fill")]
    public Image availabilityBar;
    public Image performanceBar;
    public Image qualityBar;

    [Header("설비 효율 분석 - 막대 배경")]
    public Image availabilityBarBG;
    public Image performanceBarBG;
    public Image qualityBarBG;

    [Header("실시간 생산 현황")]
    public TMP_Text totalProcessedText;
    public TMP_Text successCountText;
    public TMP_Text failureCountText;

    [Header("적재함 현황")]
    public TMP_Text generalBinText;
    public TMP_Text fragileBinText;
    public TMP_Text foodBinText;

    [Header("갱신 설정")]
    public float updateInterval = 0.2f;

    private float nextUpdateTime;

    // Inspector에서 설정한 원본 텍스트 보관
    private string totalTemplate;
    private string successTemplate;
    private string failureTemplate;

    private string generalBinTemplate;
    private string fragileBinTemplate;
    private string foodBinTemplate;

    // 숫자가 들어 있는 텍스트 부분만 찾기
    private static readonly Regex numberPattern = new Regex(
        @"(?<=>)\s*\d+(?:\s*/\s*\d+)?\s*(?=</size>)",
        RegexOptions.Compiled
    );

    private void Awake()
    {
        // Play 시작 시 Inspector의 원본 서식 저장
        totalTemplate = GetTemplate(totalProcessedText);
        successTemplate = GetTemplate(successCountText);
        failureTemplate = GetTemplate(failureCountText);

        generalBinTemplate = GetTemplate(generalBinText);
        fragileBinTemplate = GetTemplate(fragileBinText);
        foodBinTemplate = GetTemplate(foodBinText);
    }

    private string GetTemplate(TMP_Text target)
    {
        if (target == null)
            return string.Empty;

        return target.text;
    }

    private void OnEnable()
    {
        UpdateDetailPanel();
    }

    private void Update()
    {
        if (Time.time < nextUpdateTime)
            return;

        nextUpdateTime =
            Time.time + Mathf.Max(0.05f, updateInterval);

        UpdateDetailPanel();
    }

    private void UpdateDetailPanel()
    {
        UpdateOEE();
        UpdateProductionStats();
        UpdateBinStats();
    }

    // ======================================
    // OEE 실시간 업데이트
    // ======================================

    private void UpdateOEE()
    {
        if (factoryOEE == null)
            return;

        // [변경] 공정 시작 전(측정 시간 0)에는 숫자 대신 "--" 표시
        if (!factoryOEE.HasData)
        {
            if (oeeRing != null) oeeRing.SetOEE(0f);
            if (oeeValueText != null) oeeValueText.text = "--";
            if (availabilityValue != null) availabilityValue.text = "--";
            if (performanceValue != null) performanceValue.text = "--";
            if (qualityValue != null) qualityValue.text = "--";

            UpdateBar(availabilityBar, availabilityBarBG, 0f);
            UpdateBar(performanceBar, performanceBarBG, 0f);
            UpdateBar(qualityBar, qualityBarBG, 0f);
            return;
        }

        float oee =
            Mathf.Clamp(factoryOEE.OEE, 0f, 100f);

        float availability =
            Mathf.Clamp(factoryOEE.Availability, 0f, 100f);

        float performance =
            Mathf.Clamp(factoryOEE.Performance, 0f, 100f);

        float quality =
            Mathf.Clamp(factoryOEE.Quality, 0f, 100f);

        // OEE 원형 게이지
        if (oeeRing != null)
        {
            oeeRing.SetOEE(oee);
        }

        if (oeeValueText != null)
        {
            oeeValueText.text = $"{oee:F1}%";
        }

        // A / P / Q 숫자
        if (availabilityValue != null)
        {
            availabilityValue.text =
                $"{availability:F1}%";
        }

        if (performanceValue != null)
        {
            performanceValue.text =
                $"{performance:F1}%";
        }

        if (qualityValue != null)
        {
            qualityValue.text =
                $"{quality:F1}%";
        }

        // 막대그래프
        UpdateBar(
            availabilityBar,
            availabilityBarBG,
            availability
        );

        UpdateBar(
            performanceBar,
            performanceBarBG,
            performance
        );

        UpdateBar(
            qualityBar,
            qualityBarBG,
            quality
        );
    }

    private void UpdateBar(
        Image fill,
        Image background,
        float percentage
    )
    {
        if (fill == null || background == null)
            return;

        RectTransform fillRect = fill.rectTransform;
        RectTransform backgroundRect = background.rectTransform;

        float maxWidth = backgroundRect.rect.width;

        float normalized =
            Mathf.Clamp01(percentage / 100f);

        fillRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            maxWidth * normalized
        );
    }

    // ======================================
    // 생산 현황
    // ======================================

    private void UpdateProductionStats()
    {
        if (dashboardStats == null)
            return;

        UpdateNumberOnly(
            totalProcessedText,
            totalTemplate,
            dashboardStats.TotalCount.ToString()
        );

        UpdateNumberOnly(
            successCountText,
            successTemplate,
            dashboardStats.SuccessCount.ToString()
        );

        UpdateNumberOnly(
            failureCountText,
            failureTemplate,
            dashboardStats.FailureCount.ToString()
        );
    }

    // ======================================
    // 적재함 현황
    // ======================================

    private void UpdateBinStats()
    {
        if (generalBin != null)
        {
            UpdateNumberOnly(
                generalBinText,
                generalBinTemplate,
                $"{generalBin.Count}/{generalBin.capacity}"
            );
        }

        if (fragileBin != null)
        {
            UpdateNumberOnly(
                fragileBinText,
                fragileBinTemplate,
                $"{fragileBin.Count}/{fragileBin.capacity}"
            );
        }

        if (foodBin != null)
        {
            UpdateNumberOnly(
                foodBinText,
                foodBinTemplate,
                $"{foodBin.Count}/{foodBin.capacity}"
            );
        }
    }

    // ======================================
    // 기존 디자인을 유지하며 숫자만 변경
    // ======================================

    private void UpdateNumberOnly(
        TMP_Text target,
        string originalTemplate,
        string newNumber
    )
    {
        if (target == null)
            return;

        if (string.IsNullOrEmpty(originalTemplate))
            return;

        // <size=20>0</size>의 숫자만 교체
        Match match = numberPattern.Match(originalTemplate);

        if (!match.Success)
            return;

        string updatedText =
            originalTemplate.Substring(0, match.Index)
            + newNumber
            + originalTemplate.Substring(
                match.Index + match.Length
            );

        // 값이 달라졌을 때만 갱신
        if (target.text != updatedText)
        {
            target.text = updatedText;
        }
    }
}