using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParcelStationController : MonoBehaviour
{
    [Header("컨베이어 제어 연결")]
    public InspectionController inspectionController;

    [Header("연결 컴포넌트")]
    public VacuumPickPlaceTestVol2 vacuumTest;
    public GripperPickPlaceTest gripperTest;

    public Transform detectionZoneCenter;

    public Vector3 detectionBoxSize =
        new Vector3(0.5f, 0.3f, 0.5f);

    [Header("태그 3가지 분류 설정")]
    public string tagVacuum = "Parcel_Vacuum";
    public string tagGripper = "Parcel_Gripper";
    public string tagIgnore = "Parcel_Ignore";

    [Header("감지 및 정지 조건")]
    public float requiredStableTime = 1.0f;
    public float movementThreshold = 0.001f;

    [Header("연속 감지 방지")]
    public float processCooldown = 1.0f;

    [Header("동작 상태")]
    [SerializeField] private bool isRunning = false;

    private Rigidbody detectedParcel = null;
    private string detectedTagType = "";

    private Rigidbody currentCandidate = null;
    private Vector3 lastParcelPosition;
    private float stableTimer = 0f;

    // 일반 택배 로그 중복 출력 방지
    private readonly HashSet<Rigidbody> loggedGeneralParcels =
        new HashSet<Rigidbody>();

    // 공정 완료 후 잠깐 감지 방지
    private float nextDetectionTime = 0f;

    private void Start()
    {
        if (detectionZoneCenter == null)
        {
            detectionZoneCenter = transform;
        }

        // 로봇 테스트 스크립트 자동 시작 방지
        if (vacuumTest != null)
        {
            vacuumTest.runOnStart = false;
        }

        if (gripperTest != null)
        {
            gripperTest.runOnStart = false;
        }
    }

    private void Update()
    {
        if (isRunning)
            return;

        if (Time.time < nextDetectionTime)
            return;

        CheckAndVerifyTarget();
    }

    private void CheckAndVerifyTarget()
    {
        Collider[] hitColliders = Physics.OverlapBox(
            detectionZoneCenter.position,
            detectionBoxSize * 0.5f,
            detectionZoneCenter.rotation
        );

        Rigidbody candidate = null;
        string matchedTag = "";

        foreach (Collider hit in hitColliders)
        {
            Rigidbody rb = hit.attachedRigidbody;

            if (rb == null)
                continue;

            // Rigidbody 또는 감지된 Collider의 태그 확인
            if (HasTag(hit, rb, tagVacuum))
            {
                candidate = rb;
                matchedTag = tagVacuum;
                break;
            }

            if (HasTag(hit, rb, tagGripper))
            {
                candidate = rb;
                matchedTag = tagGripper;
                break;
            }

            if (HasTag(hit, rb, tagIgnore))
            {
                candidate = rb;
                matchedTag = tagIgnore;
                break;
            }
        }

        // 감지된 택배가 없으면 초기화
        if (candidate == null)
        {
            currentCandidate = null;
            stableTimer = 0f;
            return;
        }

        // 새로운 택배를 감지한 경우
        if (candidate != currentCandidate)
        {
            currentCandidate = candidate;
            lastParcelPosition = candidate.position;
            stableTimer = 0f;
            return;
        }

        float distanceMoved = Vector3.Distance(
            candidate.position,
            lastParcelPosition
        );

        // 박스가 정지했는지 확인
        if (distanceMoved < movementThreshold)
        {
            stableTimer += Time.deltaTime;
        }
        else
        {
            stableTimer = 0f;
        }

        lastParcelPosition = candidate.position;

        // 설정된 시간만큼 정지한 경우에만 처리
        if (stableTimer < requiredStableTime)
            return;

        // 일반 택배: 로봇 동작 없이 통과
        if (matchedTag == tagIgnore)
        {
            ProcessGeneralParcel(candidate);
            return;
        }

        // 파손주의 / 식품 택배
        detectedParcel = candidate;
        detectedTagType = matchedTag;

        stableTimer = 0f;

        Debug.Log(
            $"[StationController] 목표 정지 감지 " +
            $"[{detectedTagType}]: {detectedParcel.name}. " +
            "공정을 시작합니다."
        );

        StartCoroutine(
            RunStationProcess(detectedParcel, detectedTagType)
        );
    }

    private bool HasTag(
        Collider hit,
        Rigidbody rb,
        string targetTag
    )
    {
        if (string.IsNullOrEmpty(targetTag))
            return false;

        return rb.CompareTag(targetTag)
            || hit.CompareTag(targetTag);
    }

    private void ProcessGeneralParcel(Rigidbody parcel)
    {
        if (parcel == null)
            return;

        // 동일 박스 로그는 한 번만 출력
        if (loggedGeneralParcels.Add(parcel))
        {
            Debug.Log(
                $"[StationController] 일반 택배 감지: " +
                $"{parcel.name}. 로봇 공정을 건너뜁니다."
            );
        }

        // 일반 박스는 로봇 없이 컨베이어 재가동
        if (inspectionController != null)
        {
            inspectionController.ReleaseParcel();
        }
        else
        {
            Debug.LogError(
                "[StationController] InspectionController가 연결되지 않았습니다!"
            );
        }

        // 감지 상태 초기화
        stableTimer = 0f;
        currentCandidate = null;

        nextDetectionTime =
            Time.time + Mathf.Max(0f, processCooldown);
    }

    private IEnumerator RunStationProcess(
        Rigidbody targetParcel,
        string tagType
    )
    {
        isRunning = true;

        // 흡착 로봇
        if (tagType == tagVacuum)
        {
            if (vacuumTest != null)
            {
                vacuumTest.parcel = targetParcel;

                Debug.Log(
                    "[StationController] [흡착형] Pick & Place 공정 시작"
                );

                yield return StartCoroutine(vacuumTest.Run());
            }
            else
            {
                Debug.LogError(
                    "[StationController] VacuumTest가 연결되지 않았습니다!"
                );
            }
        }

        // 집게 로봇
        else if (tagType == tagGripper)
        {
            if (gripperTest != null)
            {
                gripperTest.parcel = targetParcel;

                Debug.Log(
                    "[StationController] [집게형] Pick & Place 공정 시작"
                );

                yield return StartCoroutine(gripperTest.Run());
            }
            else
            {
                Debug.LogError(
                    "[StationController] GripperTest가 연결되지 않았습니다!"
                );
            }
        }

        // 로봇 공정 종료 후 컨베이어 재가동
        if (inspectionController != null)
        {
            inspectionController.ReleaseParcel();
        }

        detectedParcel = null;
        detectedTagType = "";

        currentCandidate = null;
        stableTimer = 0f;

        nextDetectionTime =
            Time.time + Mathf.Max(0f, processCooldown);

        isRunning = false;

        Debug.Log(
            "[StationController] 공정 종료. 다음 택배를 기다립니다."
        );
    }

    private void OnDrawGizmos()
    {
        Transform center =
            detectionZoneCenter != null
                ? detectionZoneCenter
                : transform;

        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);

        Gizmos.matrix = Matrix4x4.TRS(
            center.position,
            center.rotation,
            Vector3.one
        );

        Gizmos.DrawCube(
            Vector3.zero,
            detectionBoxSize
        );

        Gizmos.color = Color.green;

        Gizmos.DrawWireCube(
            Vector3.zero,
            detectionBoxSize
        );
    }
}