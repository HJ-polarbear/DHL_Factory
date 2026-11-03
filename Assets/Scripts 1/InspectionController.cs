using UnityEngine;
using UnityEngine.Events;
using RoboLacyTech.Conveyors;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class InspectionController : MonoBehaviour
{
    [Header("컨베이어 연결")]
    public LinearConveyor inspectionConveyor;
    public LinearConveyor inputConveyor;
    public LinearConveyor inputConveyor2;

    [Header("검사대 위치")]
    public Transform stopper;
    public Transform centerPoint;
    public ParcelSpawner parcelSpawner;

    [Header("스토퍼 설정")]
    public float lowerDistance = 0.5f;
    public float stopperSpeed = 2f;
    public float stopTolerance = 0.05f;

    [Header("검사 완료 이벤트")]
    public UnityEvent onParcelReady = new UnityEvent();

    [Header("수동 R키 해제 테스트 - 자동 운전에서는 OFF")]
    public bool allowManualRelease = false;

    [Header("검사 조명")]
    public Light inspectionLight;

    public Color idleColor = Color.white;
    public Color inspectionColor = Color.yellow;
    public Color completeColor = Color.green;

    public float idleIntensity = 0.5f;
    public float blinkMinIntensity = 1f;
    public float blinkMaxIntensity = 3f;
    public float blinkSpeed = 2f;
    public float completeIntensity = 3f;
    public float completeLightDuration = 2f;

    [Header("대시보드 연동")]
    public DashboardStatsController dashboardStats;

    [Tooltip("일반 택배가 사용하는 태그")]
    public string generalParcelTag = "Parcel_Ignore";

    private Vector3 stopperUpPosition;
    private Vector3 stopperDownPosition;

    private Rigidbody currentParcel;
    private bool parcelStopped;

    private float completeLightUntil;

    public Rigidbody CurrentParcel =>
        parcelStopped ? currentParcel : null;

    // ======================================
    // 초기 설정
    // ======================================

    private void Start()
    {
        if (stopper == null ||
            centerPoint == null ||
            inspectionConveyor == null ||
            inputConveyor == null ||
            inputConveyor2 == null)
        {
            Debug.LogError(
                "[Inspection] 스토퍼, 중앙 위치, 컨베이어 3개를 연결하세요.",
                this
            );

            enabled = false;
            return;
        }

        stopperUpPosition = stopper.localPosition;

        stopperDownPosition =
            stopperUpPosition - Vector3.up * lowerDistance;

        stopper.localPosition = stopperDownPosition;

        UpdateInspectionLight();
    }

    // ======================================
    // 수동 해제 및 조명 업데이트
    // ======================================

    private void Update()
    {
        if (allowManualRelease)
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null &&
                Keyboard.current.rKey.wasPressedThisFrame)
            {
                ReleaseParcel();
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.R))
            {
                ReleaseParcel();
            }
#endif
        }

        UpdateInspectionLight();
    }

    // ======================================
    // 검사 위치 감지 및 스토퍼 제어
    // ======================================

    private void FixedUpdate()
    {
        Vector3 targetPosition =
            currentParcel != null
                ? stopperUpPosition
                : stopperDownPosition;

        stopper.localPosition = Vector3.MoveTowards(
            stopper.localPosition,
            targetPosition,
            stopperSpeed * Time.fixedDeltaTime
        );

        if (currentParcel == null || parcelStopped)
            return;

        Transform basis =
            transform.parent != null
                ? transform.parent
                : transform;

        Vector3 localParcelPosition =
            basis.InverseTransformPoint(
                currentParcel.worldCenterOfMass
            );

        Vector3 localCenterPosition =
            basis.InverseTransformPoint(
                centerPoint.position
            );

        float distance = Mathf.Abs(
            localParcelPosition.z - localCenterPosition.z
        );

        if (distance > stopTolerance)
            return;

        inspectionConveyor.Stop();

        parcelStopped = true;

        Debug.Log(
            "[Inspection] 박스 정지, 검사 요청",
            this
        );

        onParcelReady?.Invoke();
    }

    // ======================================
    // 택배 진입 감지
    // ======================================

    private void OnTriggerEnter(Collider other)
    {
        if (currentParcel != null)
            return;

        Rigidbody parcelRb = other.attachedRigidbody;

        if (parcelRb == null)
            return;

        // 컨베이어 자체는 택배로 감지하지 않음
        if (parcelRb.GetComponent<LinearConveyor>() != null)
            return;

        currentParcel = parcelRb;
        parcelStopped = false;

        inputConveyor.Stop();
        inputConveyor2.Stop();

        if (parcelSpawner != null)
        {
            parcelSpawner.SetPaused(true);
        }

        Debug.Log(
            "[Inspection] 검사 컨베이어에 박스 진입",
            this
        );
    }

    // ======================================
    // 검사 완료 후 택배 해제
    // ======================================

    public void ReleaseParcel()
    {
        if (currentParcel == null || !parcelStopped)
            return;

        // 해제 전에 택배 정보 저장
        Rigidbody releasedParcel = currentParcel;

        bool isGeneralParcel =
            releasedParcel.CompareTag(generalParcelTag);

        // 현재 검사 중인 택배 해제
        currentParcel = null;
        parcelStopped = false;

        completeLightUntil =
            Time.time + Mathf.Max(0f, completeLightDuration);

        // 컨베이어 재가동
        inspectionConveyor.Play();
        inputConveyor.Play();
        inputConveyor2.Play();

        // 택배 생성 재개
        if (parcelSpawner != null)
        {
            parcelSpawner.SetPaused(false);
        }
        else
        {
            Debug.LogWarning(
                "[Inspection] Parcel Spawner 연결 없음",
                this
            );
        }

        // 일반 택배만 이곳에서 집계
        // 파손주의와 식품은 기존 분류 센서에서 집계
        if (isGeneralParcel && dashboardStats != null)
        {
            dashboardStats.RegisterParcel(
                releasedParcel,
                DashboardStatsController.ParcelType.General
            );
        }

        Debug.Log(
            "[Inspection] 검사 완료: 다음 박스 이동",
            this
        );
    }

    // ======================================
    // 검사 조명 업데이트
    // ======================================

    private void UpdateInspectionLight()
    {
        if (inspectionLight == null)
            return;

        // 검사 중: 노란색 점멸
        if (currentParcel != null && parcelStopped)
        {
            inspectionLight.color = inspectionColor;

            float blink =
                (Mathf.Sin(
                    Time.time * blinkSpeed * Mathf.PI * 2f
                ) + 1f) * 0.5f;

            inspectionLight.intensity = Mathf.Lerp(
                blinkMinIntensity,
                blinkMaxIntensity,
                blink
            );

            return;
        }

        // 검사 완료: 초록색
        if (Time.time < completeLightUntil)
        {
            inspectionLight.color = completeColor;
            inspectionLight.intensity = completeIntensity;

            return;
        }

        // 대기: 흰색
        inspectionLight.color = idleColor;
        inspectionLight.intensity = idleIntensity;
    }
}