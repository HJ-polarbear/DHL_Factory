using UnityEngine;

/// <summary>
/// ArmIK 스크립트와 연동하여 지정된 큐브(Target Cube)를 실시간으로 추종하는 팔로우 테스트 스크립트
/// </summary>
public class ArmFollowTest : MonoBehaviour
{
    [Header("연결 컴포넌트")]
    [Tooltip("로봇에 부착된 ArmIK 스크립트 드래그")]
    public ArmIK armIK;

    [Tooltip("로봇이 따라갈 대상 큐브 오브젝트")]
    public Transform targetCube;

    [Header("추종 설정")]
    [Tooltip("그리퍼 수직 아래 보기 여부")]
    public bool pointDown = true;

    [Tooltip("손목 Yaw 각도 (0도로 고정하거나 큐브의 회전값 연동 가능)")]
    public float wristYaw = 0f;

    [Tooltip("위치가 이만큼 이상 변했을 때만 IK를 재계산 (부하 방지)")]
    public float positionThreshold = 0.005f; // 5mm

    private Vector3 lastTargetPos;
    private Coroutine currentMoveRoutine;

    void Start()
    {
        if (armIK == null)
            armIK = FindObjectOfType<ArmIK>();

        if (armIK == null || targetCube == null)
        {
            Debug.LogError("[ArmFollowTest] ArmIK 또는 Target Cube가 연결되지 않았습니다!");
            enabled = false;
            return;
        }

        // ArmIK의 설정 동기화
        armIK.pointDown = pointDown;
        lastTargetPos = targetCube.position;
    }

    void Update()
    {
        if (armIK == null || !armIK.Ready || targetCube == null) return;

        // 큐브의 위치가 일정 이상 변했거나, 이동 중이 아닐 때 실시간 추종
        if (Vector3.Distance(targetCube.position, lastTargetPos) > positionThreshold)
        {
            lastTargetPos = targetCube.position;

            // 만약 기존에 움직이는 중이라면 중단하고 새로운 목표 위치로 갱신
            if (currentMoveRoutine != null)
            {
                StopCoroutine(currentMoveRoutine);
            }

            // 큐브의 Y축 회전(Yaw)을 손목 각도로 활용하고 싶다면 아래 주석 해제
            // wristYaw = targetCube.eulerAngles.y;

            // ArmIK의 MoveTo 코루틴 호출하여 목표 위치로 이동
            currentMoveRoutine = StartCoroutine(armIK.MoveTo(targetCube.position, wristYaw));
        }
    }
}