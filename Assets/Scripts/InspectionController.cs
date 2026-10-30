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

    private Vector3 stopperUpPosition;
    private Vector3 stopperDownPosition;

    private Rigidbody currentParcel;
    private bool parcelStopped;

    private void Start()
    {
        // 에디터에서 설정한 위치가 스토퍼가 올라온 위치
        stopperUpPosition = stopper.localPosition;
        stopperDownPosition = stopperUpPosition - Vector3.up * lowerDistance;

        // 시작할 때는 스토퍼를 내려둠
        stopper.localPosition = stopperDownPosition;
    }

    private void Update()
    {
        // 임시 테스트: R 키를 누르면 박스 출발
        if (Input.GetKeyDown(KeyCode.R))
        {
            ReleaseParcel();
        }
    }

    private void FixedUpdate()
    {
        // 박스가 있으면 스토퍼를 올리고, 없으면 내림
        Vector3 target = currentParcel != null
            ? stopperUpPosition
            : stopperDownPosition;

        stopper.localPosition = Vector3.MoveTowards(
            stopper.localPosition,
            target,
            stopperSpeed * Time.fixedDeltaTime
        );

        if (currentParcel == null || parcelStopped) return;

        // 검사대 부모 기준으로 박스와 중앙 지점의 위치 비교
        Vector3 localParcelPosition =
            transform.parent.InverseTransformPoint(currentParcel.worldCenterOfMass);

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

            // 나중에 카메라·CNN 기능 연결
            onParcelReady?.Invoke();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 이미 검사 중인 박스가 있으면 무시
        if (currentParcel != null) return;

        Rigidbody parcelRb = other.attachedRigidbody;

        if (parcelRb == null) return;

        // 센서가 컨베이어 자체를 감지하는 경우 제외
        if (parcelRb.GetComponent<LinearConveyor>() != null) return;

        currentParcel = parcelRb;
        parcelStopped = false;

        // 두 입력 벨트를 함께 정지
        inputConveyor.Stop();
        inputConveyor2.Stop();

        Debug.Log("검사 컨베이어에 박스 진입");
    }

    public void ReleaseParcel()
    {
        // 박스가 검사 위치에 멈춰 있을 때만 처리
        if (currentParcel == null || !parcelStopped)
        {
            return;
        }

        currentParcel = null;
        parcelStopped = false;

        inspectionConveyor.Play();

        // 두 입력 벨트를 함께 다시 시작
        inputConveyor.Play();
        inputConveyor2.Play();

        if (parcelSpawner != null)
        {
            parcelSpawner.SpawnNextParcel();
        }
        else
        {
            Debug.LogWarning("Parcel Spawner가 연결되지 않았습니다.");
        }
    }
}