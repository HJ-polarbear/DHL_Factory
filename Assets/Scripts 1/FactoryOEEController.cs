using UnityEngine;
using TMPro;

/// <summary>
/// OEE = 가동률 × 성능률 × 양품률
///
/// [변경]
/// - 공정 시작 전(측정 시간 0)에는 가동률을 100%로 표시하지 않는다. HasData가 false면 화면에 "--" 표시.
/// - 성능률의 잘리기 전 원래 값(RawPerformance)을 따로 보관한다.
///   100%를 넘으면 idealCycleTime이 실제보다 길게 잡혔다는 뜻이므로 경고 로그를 남긴다.
/// - idealCycleTime 기본값을 5초로 변경 (Inspector 값이 우선)
/// </summary>
public class FactoryOEEController : MonoBehaviour
{
    [Header("대시보드 연결")]
    public DashboardStatsController dashboardStats;

    [Header("OEE 표시 텍스트")]
    public TMP_Text oeeText;

    [Header("목표 처리시간")]
    [Tooltip("택배 1개를 처리하는 가장 빠른 시간(초). CycleTimeMeasurer로 측정한 값을 넣으세요.")]
    [Min(0.1f)]
    public float idealCycleTime = 5f;

    [Header("설비 가동 상태")]
    [Tooltip("체크하면 고장/비가동 상태로 계산합니다.")]
    public bool isDowntime = false;

    [Header("OEE 갱신 간격")]
    [Min(0.1f)]
    public float updateInterval = 0.5f;

    [Header("검증")]
    [Tooltip("성능률 원래 값이 100%를 넘으면 경고 로그 출력")]
    public bool warnWhenPerformanceOver100 = true;

    [Header("누적 운영시간")]
    [SerializeField] private float totalTime = 0f;
    [SerializeField] private float operatingTime = 0f;
    [SerializeField] private float downtime = 0f;

    [Header("현재 OEE 지표")]
    [Range(0f, 100f)][SerializeField] private float availability = 0f;
    [Range(0f, 100f)][SerializeField] private float performance = 0f;
    [Range(0f, 100f)][SerializeField] private float quality = 0f;
    [Range(0f, 100f)][SerializeField] private float oee = 0f;

    [Tooltip("잘리기 전 성능률. 100을 넘으면 idealCycleTime이 너무 김")]
    [SerializeField] private float rawPerformance = 0f;

    private float updateTimer = 0f;
    private bool warnedOver100;

    public float Availability => availability;
    public float Performance => performance;
    public float RawPerformance => rawPerformance;
    public float Quality => quality;
    public float OEE => oee;

    public float TotalTime => totalTime;
    public float OperatingTime => operatingTime;
    public float Downtime => downtime;

    /// <summary>공정이 시작돼 측정 시간이 쌓였는지 (false면 화면에 "--" 표시)</summary>
    public bool HasData => totalTime > 0f;

    private void Start()
    {
        totalTime = 0f;
        operatingTime = 0f;
        downtime = 0f;
        updateTimer = 0f;

        availability = 0f;
        performance = 0f;
        rawPerformance = 0f;
        quality = 0f;
        oee = 0f;
        warnedOver100 = false;

        UpdateOEE();
    }

    private void Update()
    {
        // 공정 시작 전(timeScale = 0)에는 deltaTime이 0이라 시간이 쌓이지 않음
        float deltaTime = Time.deltaTime;

        totalTime += deltaTime;

        if (isDowntime)
            downtime += deltaTime;          // 설비 고장 또는 비가동 시간
        else
            operatingTime += deltaTime;     // 정상 가동시간 (검사 대기, 로봇 작업 포함)

        updateTimer += deltaTime;
        if (updateTimer >= updateInterval)
        {
            updateTimer = 0f;
            UpdateOEE();
        }
    }

    private void UpdateOEE()
    {
        if (dashboardStats == null || !HasData)
        {
            availability = 0f;
            performance = 0f;
            rawPerformance = 0f;
            quality = 0f;
            oee = 0f;
            UpdateText();
            return;
        }

        int totalParcels = dashboardStats.TotalCount;
        int successParcels = dashboardStats.SuccessCount;

        // 1. 가동률 = 가동시간 ÷ 전체 운영시간
        availability = Mathf.Clamp(operatingTime / totalTime * 100f, 0f, 100f);

        // 2. 성능률 = (처리 수량 × 목표 처리시간) ÷ 가동시간
        if (operatingTime > 0f)
        {
            rawPerformance = totalParcels * idealCycleTime / operatingTime * 100f;
            performance = Mathf.Clamp(rawPerformance, 0f, 100f);

            if (warnWhenPerformanceOver100 && rawPerformance > 100f && !warnedOver100 && totalParcels >= 5)
            {
                warnedOver100 = true;
                Debug.LogWarning(
                    $"[OEE] 성능률 원래 값이 {rawPerformance:F0}%입니다. " +
                    $"idealCycleTime({idealCycleTime}초)이 실제 처리시간보다 깁니다. " +
                    "CycleTimeMeasurer로 측정한 값으로 줄이세요.", this);
            }
        }
        else
        {
            rawPerformance = 0f;
            performance = 0f;
        }

        // 3. 양품률 = 성공 수량 ÷ 전체 처리 수량
        quality = totalParcels > 0
            ? Mathf.Clamp((float)successParcels / totalParcels * 100f, 0f, 100f)
            : 0f;

        // 4. 최종 OEE
        oee = Mathf.Clamp(
            (availability / 100f) * (performance / 100f) * (quality / 100f) * 100f,
            0f, 100f);

        UpdateText();
    }

    private void UpdateText()
    {
        if (oeeText == null) return;
        oeeText.text = HasData ? $"OEE\n{oee:F1}%" : "OEE\n--";
    }

    // 다른 스크립트에서 고장 상태를 제어할 때 사용
    public void SetDowntime(bool stopped)
    {
        isDowntime = stopped;
        Debug.Log(stopped ? "[OEE] 설비 비가동 시작" : "[OEE] 설비 정상 가동 재개");
    }
}