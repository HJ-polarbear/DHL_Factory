using UnityEngine;
using TMPro;

public class FactoryOEEController : MonoBehaviour
{
    [Header("대시보드 연결")]
    public DashboardStatsController dashboardStats;

    [Header("OEE 표시 텍스트")]
    public TMP_Text oeeText;

    [Header("목표 처리시간")]
    [Tooltip("택배 1개를 처리하는 목표시간(초)")]
    [Min(0.1f)]
    public float idealCycleTime = 30f;

    [Header("설비 가동 상태")]
    [Tooltip("체크하면 고장/비가동 상태로 계산합니다.")]
    public bool isDowntime = false;

    [Header("OEE 갱신 간격")]
    [Min(0.1f)]
    public float updateInterval = 0.5f;

    [Header("누적 운영시간")]
    [SerializeField] private float totalTime = 0f;

    [SerializeField] private float operatingTime = 0f;

    [SerializeField] private float downtime = 0f;

    [Header("현재 OEE 지표")]
    [Range(0f, 100f)]
    [SerializeField] private float availability = 100f;

    [Range(0f, 100f)]
    [SerializeField] private float performance = 0f;

    [Range(0f, 100f)]
    [SerializeField] private float quality = 0f;

    [Range(0f, 100f)]
    [SerializeField] private float oee = 0f;

    private float updateTimer = 0f;

    public float Availability => availability;
    public float Performance => performance;
    public float Quality => quality;
    public float OEE => oee;

    public float TotalTime => totalTime;
    public float OperatingTime => operatingTime;
    public float Downtime => downtime;

    private void Start()
    {
        totalTime = 0f;
        operatingTime = 0f;
        downtime = 0f;

        updateTimer = 0f;

        availability = 100f;
        performance = 0f;
        quality = 0f;
        oee = 0f;

        UpdateOEE();
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        // 전체 운영시간 누적
        totalTime += deltaTime;

        if (isDowntime)
        {
            // 설비 고장 또는 비가동 시간
            downtime += deltaTime;
        }
        else
        {
            // 정상 가동시간
            // 정상 검사 대기 및 로봇 작업시간 포함
            operatingTime += deltaTime;
        }

        updateTimer += deltaTime;

        if (updateTimer >= updateInterval)
        {
            updateTimer = 0f;
            UpdateOEE();
        }
    }

    private void UpdateOEE()
    {
        if (dashboardStats == null)
        {
            availability = 0f;
            performance = 0f;
            quality = 0f;
            oee = 0f;

            UpdateText();
            return;
        }

        int totalParcels = dashboardStats.TotalCount;
        int successParcels = dashboardStats.SuccessCount;

        // 1. 가동률
        if (totalTime > 0f)
        {
            availability = Mathf.Clamp(
                operatingTime / totalTime * 100f,
                0f,
                100f
            );
        }
        else
        {
            availability = 100f;
        }

        // 2. 성능
        // 목표 처리시간 × 전체 처리 수량 ÷ 실제 가동시간
        if (operatingTime > 0f)
        {
            float expectedProcessingTime =
                totalParcels * idealCycleTime;

            performance = Mathf.Clamp(
                expectedProcessingTime / operatingTime * 100f,
                0f,
                100f
            );
        }
        else
        {
            performance = 0f;
        }

        // 3. 품질
        if (totalParcels > 0)
        {
            quality = Mathf.Clamp(
                (float)successParcels / totalParcels * 100f,
                0f,
                100f
            );
        }
        else
        {
            quality = 0f;
        }

        // 4. 최종 OEE
        oee =
            (availability / 100f) *
            (performance / 100f) *
            (quality / 100f) *
            100f;

        oee = Mathf.Clamp(oee, 0f, 100f);

        UpdateText();
    }

    private void UpdateText()
    {
        if (oeeText != null)
        {
            oeeText.text =
                $"OEE\n{oee:F1}%";
        }
    }

    // 다른 스크립트에서 고장 상태를 제어할 때 사용
    public void SetDowntime(bool stopped)
    {
        isDowntime = stopped;

        Debug.Log(
            stopped
                ? "[OEE] 설비 비가동 시작"
                : "[OEE] 설비 정상 가동 재개"
        );
    }
}