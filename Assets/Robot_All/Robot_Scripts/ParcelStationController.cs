using System.Collections;
using UnityEngine;

public class ParcelStationController : MonoBehaviour
{
    [Header("연결 컴포넌트")]
    public VacuumPickPlaceTestVol2 vacuumTest;   // 흡착형 테스트 스크립트 연결
    public GripperPickPlaceTest gripperTest;     // 집게형 테스트 스크립트 연결
    public Transform detectionZoneCenter;
    public Vector3 detectionBoxSize = new Vector3(0.5f, 0.3f, 0.5f);

    [Header("태그 3가지 분류 설정")]
    public string tagVacuum = "Parcel_Vacuum";    // 태그 1: 흡착형 동작 대상
    public string tagGripper = "Parcel_Gripper";  // 태그 2: 집게형 동작 대상
    public string tagIgnore = "Parcel_Ignore";    // 태그 3: 무시(동작 안 함) 대상

    [Header("감지 및 정지 조건")]
    public float requiredStableTime = 1.0f;   // 목표가 정지해 있어야 하는 최소 시간 (초)
    public float movementThreshold = 0.001f;  // 정지 판단 기준 거리

    [Header("동작 상태")]
    [SerializeField] private bool isRunning = false;
    private Rigidbody detectedParcel = null;
    private string detectedTagType = "";
    private Vector3 lastParcelPosition;
    private float stableTimer = 0f;

    void Start()
    {
        if (detectionZoneCenter == null)
            detectionZoneCenter = transform;

        // 컨트롤러가 제어하므로 개별 테스트 스크립트 자체의 자동 시작은 끔
        if (vacuumTest != null) vacuumTest.runOnStart = false;
        if (gripperTest != null) gripperTest.runOnStart = false;
    }

    void Update()
    {
        if (!isRunning)
        {
            CheckAndVerifyTarget();
        }
    }

    void CheckAndVerifyTarget()
    {
        Collider[] hitColliders = Physics.OverlapBox(
            detectionZoneCenter.position,
            detectionBoxSize * 0.5f,
            detectionZoneCenter.rotation
        );

        Rigidbody currentCandidate = null;
        string matchedTag = "";

        foreach (var hit in hitColliders)
        {
            // 태그별 분기 확인
            if (hit.CompareTag(tagVacuum))
            {
                matchedTag = tagVacuum;
            }
            else if (hit.CompareTag(tagGripper))
            {
                matchedTag = tagGripper;
            }
            else if (hit.CompareTag(tagIgnore))
            {
                continue; // 무시할 태그는 건너뜀
            }
            else
            {
                continue;
            }

            Rigidbody rb = hit.GetComponent<Rigidbody>();
            if (rb != null)
            {
                currentCandidate = rb;
                detectedTagType = matchedTag;
                break;
            }
        }

        if (currentCandidate != null)
        {
            float distanceMoved = Vector3.Distance(currentCandidate.position, lastParcelPosition);

            if (distanceMoved < movementThreshold)
            {
                stableTimer += Time.deltaTime;

                if (stableTimer >= requiredStableTime)
                {
                    detectedParcel = currentCandidate;
                    Debug.Log($"[StationController] 목표 정지 감지 [{detectedTagType}]: {detectedParcel.name}. 공정을 시작합니다.");

                    stableTimer = 0f;
                    StartCoroutine(RunStationProcess(detectedParcel, detectedTagType));
                    return;
                }
            }
            else
            {
                stableTimer = 0f;
            }

            lastParcelPosition = currentCandidate.position;
        }
        else
        {
            stableTimer = 0f;
            if (detectedParcel != null)
            {
                lastParcelPosition = detectedParcel.position;
            }
        }
    }

    IEnumerator RunStationProcess(Rigidbody targetParcel, string tagType)
    {
        isRunning = true;

        // 태그 종류에 따른 공정 분기 실행
        if (tagType == tagVacuum)
        {
            if (vacuumTest != null)
            {
                vacuumTest.parcel = targetParcel;
                Debug.Log("[StationController]  [흡착형] Pick & Place 공정 시작");
                yield return StartCoroutine(vacuumTest.Run());
            }
            else
            {
                Debug.LogError("[StationController] 흡착형 테스트 스크립트(VacuumTest)가 연결되지 않았습니다!");
            }
        }
        else if (tagType == tagGripper)
        {
            if (gripperTest != null)
            {
                gripperTest.parcel = targetParcel;
                Debug.Log("[StationController]  [집게형] Pick & Place 공정 시작");
                yield return StartCoroutine(gripperTest.Run());
            }
            else
            {
                Debug.LogError("[StationController] 집게형 테스트 스크립트(GripperTest)가 연결되지 않았습니다!");
            }
        }

        isRunning = false;
        detectedParcel = null;
        stableTimer = 0f;

        yield return new WaitForSeconds(1.0f); // 연속 감지 방지 딜레이
    }

    void OnDrawGizmos()
    {
        Transform center = detectionZoneCenter != null ? detectionZoneCenter : transform;
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Gizmos.matrix = Matrix4x4.TRS(center.position, center.rotation, Vector3.one);
        Gizmos.DrawCube(Vector3.zero, detectionBoxSize);

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(Vector3.zero, detectionBoxSize);
    }
}