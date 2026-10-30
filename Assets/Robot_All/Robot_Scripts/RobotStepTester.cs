using System.Collections;
using UnityEngine;

public class RobotStepTester : MonoBehaviour
{
    [Header("연결 컴포넌트")]
    [Tooltip("로봇에 부착된 Industrial6AxisIK 스크립트")]
    public Industrial6AxisIK robotIK;

    [Tooltip("잡을 대상 오브젝트 (큐브)")]
    public Transform targetCube;

    [Tooltip("물건을 내려놓을 목표 지점 (Empty Object 등)")]
    public Transform dropPosition;

    [Header("동작 설정")]
    public float approachHeight = 0.2f;   // 상단 접근 높이
    public float moveDuration = 1.0f;     // 이동 소요 시간 (초)

    [Header("테스트 모드 및 상태")]
    [Tooltip("체크하면 자동으로 전 과정을 순회, 해제하면 수동 단계별 진행")]
    public bool isAutoMode = false;

    [Tooltip("현재 진행 중인 공정 단계 번호")]
    public int currentStep = 0;

    [Tooltip("현재 상태 메시지")]
    public string statusMessage = "대기 중";

    private bool isMoving = false;
    private Vector3 initialCubePos;
    private Quaternion initialCubeRot;

    // 공정 단계 정의
    public enum Step
    {
        Waiting = 0,        // 대기
        ApproachPick = 1,   // 목표 상단 접근
        DescentPick = 2,    // 하강
        Grip = 3,           // 잡기
        LiftPick = 4,       // 상승
        Transfer = 5,       // 이송
        DescentDrop = 6,    // 내려놓기 하강
        Release = 7,        // 놓기
        LiftDrop = 8,       // 복귀 전 상승
        ReturnHome = 9      // 복귀 및 완료
    }

    void Start()
    {
        if (robotIK == null || targetCube == null || dropPosition == null)
        {
            Debug.LogError("[RobotStepTester] 필수 컴포넌트(IK, 큐브, 드롭존)가 연결되지 않았습니다!");
            enabled = false;
            return;
        }

        // 큐브의 초기 위치 저장 (리셋용)
        initialCubePos = targetCube.position;
        //initialCubeRot = targetCube.rotation;
        
    }

    void Update()
    {
        // 자동 모드일 때 현재 단계가 끝나면 다음 단계로 자동 진행
        if (isAutoMode && !isMoving)
        {
            // 예시로 각 단계별 대기 시간 부여 후 자동 진행 로직을 넣을 수 있습니다.
        }

        // 키보드 단축키 테스트 지원 (Space를 누르면 다음 단계로 진행)
        if (Input.GetKeyDown(KeyCode.Space) && !isMoving)
        {
            ExecuteNextStep();
        }
    }

    /// <summary>
    /// 다음 단계로 한 칸씩 진행하는 함수 (UI 버튼과 연동 가능)
    /// </summary>
    public void ExecuteNextStep()
    {
        Quaternion topDownRot = Quaternion.Euler(90f, 0f, 0f);
        if (isMoving) return;
        switch ((Step)currentStep)
        {
            case Step.Waiting:
                statusMessage = "1. 목표 상단 접근 중...";
                Vector3 pickAbove = targetCube.position + Vector3.up * approachHeight;
                // targetCube.rotation 대신 topDownRot 적용
                StartCoroutine(MoveTargetRoutine(pickAbove, topDownRot, () => currentStep = 1));
                break;

            case Step.ApproachPick:
                statusMessage = "2. 하강 중...";
                // targetCube.rotation 대신 topDownRot 적용
                StartCoroutine(MoveTargetRoutine(targetCube.position, topDownRot, () => currentStep = 2));
                break;
            case Step.DescentPick:
                statusMessage = "3. 물건 잡기 (Grip)...";
                targetCube.SetParent(robotIK.transform); // 손에 부착
                currentStep = 3;
                break;
            case Step.Grip:
                statusMessage = "4. 물건 상승 중...";
                Vector3 pickAbove2 = targetCube.position + Vector3.up * approachHeight;
                StartCoroutine(MoveTargetRoutine(pickAbove2, topDownRot, () => currentStep = 4));
                break;

            case Step.LiftPick:
                statusMessage = "5. 이송 중 (Transfer)...";
                Vector3 dropAbove = dropPosition.position + Vector3.up * approachHeight;
                StartCoroutine(MoveTargetRoutine(dropAbove, topDownRot, () => currentStep = 5));
                break;

            case Step.Transfer:
                statusMessage = "6. 내려놓기 하강 중...";
                StartCoroutine(MoveTargetRoutine(dropPosition.position, topDownRot, () => currentStep = 6));
                break;

            case Step.Release:
                statusMessage = "8. 복귀 전 상승 중...";
                Vector3 dropAbove2 = dropPosition.position + Vector3.up * approachHeight;
                StartCoroutine(MoveTargetRoutine(dropAbove2, topDownRot, () => currentStep = 8));
                break;
            //switch ((Step)currentStep)
            //{
            //    case Step.Waiting:
            //        statusMessage = "1. 목표 상단 접근 중...";
            //        Vector3 pickAbove = targetCube.position + Vector3.up * approachHeight;
            //        StartCoroutine(MoveTargetRoutine(pickAbove, targetCube.rotation, () => currentStep = 1));
            //        break;

            //    case Step.ApproachPick:
            //        statusMessage = "2. 하강 중...";
            //        StartCoroutine(MoveTargetRoutine(targetCube.position, targetCube.rotation, () => currentStep = 2));
            //        break;

            //    case Step.DescentPick:
            //        statusMessage = "3. 물건 잡기 (Grip)...";
            //        targetCube.SetParent(robotIK.transform); // 손에 부착
            //        currentStep = 3;
            //        break;

            //    case Step.Grip:
            //        statusMessage = "4. 물건 상승 중...";
            //        Vector3 pickAbove2 = targetCube.position + Vector3.up * approachHeight;
            //        StartCoroutine(MoveTargetRoutine(pickAbove2, targetCube.rotation, () => currentStep = 4));
            //        break;

            //    case Step.LiftPick:
            //        statusMessage = "5. 이송 중 (Transfer)...";
            //        Vector3 dropAbove = dropPosition.position + Vector3.up * approachHeight;
            //        StartCoroutine(MoveTargetRoutine(dropAbove, dropPosition.rotation, () => currentStep = 5));
            //        break;

            //    case Step.Transfer:
            //        statusMessage = "6. 내려놓기 하강 중...";
            //        StartCoroutine(MoveTargetRoutine(dropPosition.position, dropPosition.rotation, () => currentStep = 6));
            //        break;

            //    case Step.DescentDrop:
            //        statusMessage = "7. 물건 놓기 (Release)...";
            //        targetCube.SetParent(null); // 부모 해제
            //        currentStep = 7;
            //        break;

            //    case Step.Release:
            //        statusMessage = "8. 복귀 전 상승 중...";
            //        Vector3 dropAbove2 = dropPosition.position + Vector3.up * approachHeight;
            //        StartCoroutine(MoveTargetRoutine(dropAbove2, dropPosition.rotation, () => currentStep = 8));
            //        break;

            case Step.LiftDrop:
                statusMessage = "9. 초기 대기 상태로 복귀 완료";
                currentStep = 0; // 첫 단계로 순환
                break;
        }
    }

    /// <summary>
    /// 모든 상태를 초기화하는 함수
    /// </summary>
    public void ResetSimulation()
    {
        StopAllCoroutines();
        isMoving = false;
        currentStep = 0;
        statusMessage = "리셋됨 - 대기 중";
        targetCube.SetParent(null);
        targetCube.position = initialCubePos;
        targetCube.rotation = initialCubeRot;
    }

    /// <summary>
    /// 타겟 위치 보간 이동 코루틴
    /// </summary>
    IEnumerator MoveTargetRoutine(Vector3 targetPos, Quaternion targetRot, System.Action onComplete)
    {
        isMoving = true;
        Vector3 startPos = robotIK.target.position;
        Quaternion startRot = robotIK.target.rotation;
        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / moveDuration);

            robotIK.target.position = Vector3.Lerp(startPos, targetPos, t);
            robotIK.target.rotation = Quaternion.Slerp(startRot, targetRot, t);

            yield return null;
        }

        robotIK.target.position = targetPos;
        robotIK.target.rotation = targetRot;
        isMoving = false;

        onComplete?.Invoke();
    }

    // ==========================================
    // 화면에 간단한 조작용 UI (OnGUI) 표시
    // ==========================================
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(30, 120, 320, 260), GUI.skin.box);
        GUILayout.Label("<b>[ 로봇 공정 단계별 테스트 패널 ]</b>");
        GUILayout.Space(5);
        GUILayout.Label($"현재 단계: [Step {currentStep}]");
        GUILayout.Label($"상태: {statusMessage}");
        GUILayout.Label($"동작 중 여부: {(isMoving ? "이동 중..." : "대기/정지")}");
        GUILayout.Space(10);

        // 버튼 조작부
        if (GUILayout.Button(isMoving ? "이동 중..." : "▶ 다음 단계 실행 (Space)", GUILayout.Height(35)))
        {
            ExecuteNextStep();
        }

        if (GUILayout.Button(isAutoMode ? "■ 자동 모드 끄기" : "▶ 자동 모드 켜기 (준비중)", GUILayout.Height(30)))
        {
            isAutoMode = !isAutoMode;
        }

        if (GUILayout.Button("🔄 시뮬레이션 리셋", GUILayout.Height(30)))
        {
            ResetSimulation();
        }

        GUILayout.EndArea();
    }
}