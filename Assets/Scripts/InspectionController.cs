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

    [Tooltip("대기 중 조명 색상")]
    public Color idleColor = Color.white;

    [Tooltip("검사 중 조명 색상")]
    public Color inspectionColor = Color.yellow;

    [Tooltip("검사 완료 후 조명 색상")]
    public Color completeColor = Color.green;

    [Tooltip("대기 중 조명 밝기")]
    public float idleIntensity = 0.5f;

    [Tooltip("검사 중 조명의 최소 밝기")]
    public float blinkMinIntensity = 1f;

    [Tooltip("검사 중 조명의 최대 밝기")]
    public float blinkMaxIntensity = 3f;

    [Tooltip("검사 중 조명이 초당 깜빡이는 횟수")]
    public float blinkSpeed = 2f;

    [Tooltip("검사 완료 조명 밝기")]
    public float completeIntensity = 3f;

    [Tooltip("검사 완료 조명이 켜져 있는 시간")]
    public float completeLightDuration = 2f;

    private Vector3 stopperUpPosition;
    private Vector3 stopperDownPosition;

    private Rigidbody currentParcel;
    private bool parcelStopped;

    // 완료 조명이 끝나는 시각
    private float completeLightUntil;

    private void Start()
    {
        // 에디터에서 배치한 위치를 올라온 위치로 사용
        stopperUpPosition = stopper.localPosition;
        stopperDownPosition = stopperUpPosition - Vector3.up * lowerDistance;

        // 시작할 때 스토퍼를 내려둠
        stopper.localPosition = stopperDownPosition;

        UpdateInspectionLight();
    }

    private void Update()
    {
        // R 키는 로봇 처리 완료를 대신하는 임시 테스트
        if (Input.GetKeyDown(KeyCode.R))
        {
            ReleaseParcel();
        }

        UpdateInspectionLight();
    }

    private void FixedUpdate()
    {
        // 검사 중인 박스가 있으면 스토퍼 상승
        Vector3 target = currentParcel != null
            ? stopperUpPosition
            : stopperDownPosition;

        stopper.localPosition = Vector3.MoveTowards(
            stopper.localPosition,
            target,
            stopperSpeed * Time.fixedDeltaTime
        );

        if (currentParcel == null || parcelStopped) return;

        // 검사 컨베이어 부모 기준으로 중앙 위치 비교
        Vector3 localParcelPosition =
            transform.parent.InverseTransformPoint(
                currentParcel.worldCenterOfMass
            );

        Vector3 localCenterPosition =
            transform.parent.InverseTransformPoint(centerPoint.position);

        float distance = Mathf.Abs(
            localParcelPosition.z - localCenterPosition.z
        );

        if (distance <= stopTolerance)
        {
            inspectionConveyor.Stop();
            parcelStopped = true;

            Debug.Log("박스가 검사 위치에 도착했습니다.");

            // 나중에 카메라와 CNN 기능을 연결할 이벤트
            onParcelReady?.Invoke();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 이미 검사 중인 박스가 있으면 추가 감지하지 않음
        if (currentParcel != null) return;

        Rigidbody parcelRb = other.attachedRigidbody;

        if (parcelRb == null) return;

        // 컨베이어 자체가 감지된 경우 제외
        if (parcelRb.GetComponent<LinearConveyor>() != null) return;

        currentParcel = parcelRb;
        parcelStopped = false;

        // 검사 구역 진입 시 입력 벨트 정지
        inputConveyor.Stop();
        inputConveyor2.Stop();

        // 박스 생성도 일시정지
        if (parcelSpawner != null)
        {
            parcelSpawner.SetPaused(true);
        }

        Debug.Log("검사 컨베이어에 박스 진입");
    }

    public void ReleaseParcel()
    {
        // 검사 위치에 박스가 멈춰 있을 때만 처리
        if (currentParcel == null || !parcelStopped)
        {
            return;
        }

        currentParcel = null;
        parcelStopped = false;

        // 완료 조명을 일정 시간 표시
        completeLightUntil =
            Time.time + Mathf.Max(0f, completeLightDuration);

        // 검사 벨트와 입력 벨트 재시작
        inspectionConveyor.Play();
        inputConveyor.Play();
        inputConveyor2.Play();

        // 박스 생성 재개
        if (parcelSpawner != null)
        {
            parcelSpawner.SetPaused(false);
        }
        else
        {
            Debug.LogWarning("Parcel Spawner가 연결되지 않았습니다.");
        }

        Debug.Log("검사 완료: 다음 박스 이동");
    }

    private void UpdateInspectionLight()
    {
        if (inspectionLight == null) return;

        // 새 박스가 검사 위치에 도착했다면 검사 조명을 우선 표시
        if (currentParcel != null && parcelStopped)
        {
            inspectionLight.color = inspectionColor;

            float blink = (
                Mathf.Sin(Time.time * blinkSpeed * Mathf.PI * 2f) + 1f
            ) * 0.5f;

            inspectionLight.intensity = Mathf.Lerp(
                blinkMinIntensity,
                blinkMaxIntensity,
                blink
            );

            return;
        }

        // 검사 완료 직후에는 초록색 조명 표시
        if (Time.time < completeLightUntil)
        {
            inspectionLight.color = completeColor;
            inspectionLight.intensity = completeIntensity;
            return;
        }

        // 대기 중에는 약한 조명 유지
        inspectionLight.color = idleColor;
        inspectionLight.intensity = idleIntensity;
    }
}