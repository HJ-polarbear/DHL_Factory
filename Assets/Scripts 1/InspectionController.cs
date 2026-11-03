using UnityEngine;
using UnityEngine.Events;
using RoboLacyTech.Conveyors;

public class InspectionController : MonoBehaviour
{
    [Header("컨베이어 연결")]
    public LinearConveyor inspectionConveyor;
    public LinearConveyor inputConveyor;
    public LinearConveyor inputConveyor2;

    [Header("스토퍼와 검사 위치")]
    public Transform stopper;
    public Transform centerPoint;

    [Header("택배 생성기")]
    public ParcelSpawner parcelSpawner;

    [Header("스토퍼 이동")]
    public float lowerDistance = 0.5f;
    public float stopperSpeed = 2f;

    [Header("중앙 정지")]
    public float stopTolerance = 0.05f;

    [Header("박스 검사 이벤트")]
    public UnityEvent onParcelReady;

    [Header("검사대 조명")]
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

    private void Start()
    {
        if (stopper == null)
        {
            Debug.LogError(
                "[InspectionController] Stopper가 연결되지 않았습니다."
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

    private void Update()
    {
        // 기존 R키 테스트 유지
        if (Input.GetKeyDown(KeyCode.R))
        {
            ReleaseParcel();
        }

        UpdateInspectionLight();
    }

    private void FixedUpdate()
    {
        Vector3 targetPosition = currentParcel != null
            ? stopperUpPosition
            : stopperDownPosition;

        stopper.localPosition = Vector3.MoveTowards(
            stopper.localPosition,
            targetPosition,
            stopperSpeed * Time.fixedDeltaTime
        );

        if (currentParcel == null || parcelStopped)
            return;

        if (transform.parent == null || centerPoint == null)
            return;

        Vector3 localParcelPosition =
            transform.parent.InverseTransformPoint(
                currentParcel.worldCenterOfMass
            );

        Vector3 localCenterPosition =
            transform.parent.InverseTransformPoint(
                centerPoint.position
            );

        float distance = Mathf.Abs(
            localParcelPosition.z - localCenterPosition.z
        );

        if (distance <= stopTolerance)
        {
            if (inspectionConveyor != null)
                inspectionConveyor.Stop();

            parcelStopped = true;

            Debug.Log("박스가 검사 위치에 도착했습니다.");

            onParcelReady?.Invoke();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (currentParcel != null)
            return;

        Rigidbody parcelRb = other.attachedRigidbody;

        if (parcelRb == null)
            return;

        if (parcelRb.GetComponent<LinearConveyor>() != null)
            return;

        currentParcel = parcelRb;
        parcelStopped = false;

        if (inputConveyor != null)
            inputConveyor.Stop();

        if (inputConveyor2 != null)
            inputConveyor2.Stop();

        if (parcelSpawner != null)
            parcelSpawner.SetPaused(true);

        Debug.Log("검사 컨베이어에 박스 진입");
    }

    public void ReleaseParcel()
    {
        if (currentParcel == null || !parcelStopped)
            return;

        // 해제 전에 어떤 택배인지 저장
        Rigidbody releasedParcel = currentParcel;

        bool isGeneralParcel =
            releasedParcel.CompareTag(generalParcelTag);

        currentParcel = null;
        parcelStopped = false;

        completeLightUntil =
            Time.time + Mathf.Max(0f, completeLightDuration);

        if (inspectionConveyor != null)
            inspectionConveyor.Play();

        if (inputConveyor != null)
            inputConveyor.Play();

        if (inputConveyor2 != null)
            inputConveyor2.Play();

        if (parcelSpawner != null)
        {
            parcelSpawner.SetPaused(false);
        }
        else
        {
            Debug.LogWarning(
                "Parcel Spawner가 연결되지 않았습니다."
            );
        }

        // 일반 택배만 여기서 집계
        // 파손과 식품은 각 로봇의 놓기 동작에서 집계
        if (isGeneralParcel && dashboardStats != null)
        {
            dashboardStats.RegisterParcel(
                releasedParcel,
                DashboardStatsController.ParcelType.General
            );
        }

        Debug.Log("검사 완료: 다음 박스 이동");
    }

    private void UpdateInspectionLight()
    {
        if (inspectionLight == null)
            return;

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

        if (Time.time < completeLightUntil)
        {
            inspectionLight.color = completeColor;
            inspectionLight.intensity = completeIntensity;
            return;
        }

        inspectionLight.color = idleColor;
        inspectionLight.intensity = idleIntensity;
    }
}