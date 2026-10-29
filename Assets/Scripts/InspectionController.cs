using UnityEngine;
using RoboLacyTech.Conveyors;

public class InspectionController : MonoBehaviour
{
    [Header("연결할 오브젝트")]
    public LinearConveyor inspectionConveyor;
    public Transform stopper;
    public Transform centerPoint;

    [Header("스토퍼 이동")]
    public float lowerDistance = 0.5f;
    public float stopperSpeed = 2f;

    [Header("중앙 정지")]
    public float stopTolerance = 0.05f;

    private Vector3 stopperUpPosition;
    private Vector3 stopperDownPosition;
    private Rigidbody currentParcel;
    private bool parcelStopped;

    private void Start()
    {
        // 에디터에서 배치한 현재 위치를 스토퍼가 올라온 위치로 사용
        stopperUpPosition = stopper.localPosition;
        stopperDownPosition = stopperUpPosition - Vector3.up * lowerDistance;

        // 시작할 때는 스토퍼를 내려둠
        stopper.localPosition = stopperDownPosition;
    }

    private void FixedUpdate()
    {
        // 스토퍼를 목표 위치로 이동
        Vector3 target = currentParcel != null
            ? stopperUpPosition
            : stopperDownPosition;

        stopper.localPosition = Vector3.MoveTowards(
            stopper.localPosition,
            target,
            stopperSpeed * Time.fixedDeltaTime
        );

        if (currentParcel == null || parcelStopped) return;

        // 부모 컨베이어의 로컬 Z축을 이동 방향으로 사용
        Vector3 localParcelPosition =
            transform.parent.InverseTransformPoint(currentParcel.worldCenterOfMass);

        Vector3 localCenterPosition =
            transform.parent.InverseTransformPoint(centerPoint.position);

        float distance = Mathf.Abs(localParcelPosition.z - localCenterPosition.z);

        if (distance <= stopTolerance)
        {
            inspectionConveyor.Stop();
            parcelStopped = true;
            Debug.Log("박스가 검사 위치에 도착했습니다.");
        }
    }

  
    private void OnTriggerEnter(Collider other)
    {
        if (currentParcel != null) return;

        Rigidbody parcelRb = other.attachedRigidbody;
        if (parcelRb == null) return;

        // 컨베이어 자체가 센서에 닿는 경우는 제외
        if (parcelRb.GetComponent<LinearConveyor>() != null) return;

        currentParcel = parcelRb;
        parcelStopped = false;
        Debug.Log("검사 컨베이어에 박스 진입");
    }

    // 나중에 로봇이 박스를 가져간 뒤 호출할 예정
    public void ReleaseParcel()
    {
        currentParcel = null;
        parcelStopped = false;
        inspectionConveyor.Play();
    }
}