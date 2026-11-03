using System.Collections;
using UnityEngine;

public class SortingCoordinator : MonoBehaviour
{
    [Header("검사 연결")]
    public InspectionController inspectionController; // 검사대 제어 및 현재 물체 정보를 관리하는 컴포넌트
    public ParcelInspectAgent inspectAgent;           // AI 추론 및 물체 분류를 담당하는 에이전트

    [Header("로봇 연결")]
    [Tooltip("흡착형 로봇의 VacuumPickPlaceTestVol2 컴포넌트")]
    public VacuumPickPlaceTestVol2 vacuumTest;

    [Tooltip("집게형 로봇의 GripperPickPlaceTest 컴포넌트")]
    public GripperPickPlaceTest gripperTest;

    [Header("대기 설정")]
    [Min(1f)] public float inferenceTimeout = 10f;     // AI 추론 결과 대기 시간 제한 (초)
    [Min(0f)] public float afterRobotDelay = 0.2f;    // 로봇 작업 완료 후 대기 시간

    // 현재 코루틴(작업)이 진행 중인지 여부
    public bool IsBusy { get; private set; }
    // 오류 발생 여부
    public bool Faulted { get; private set; }

    private Rigidbody target;             // 현재 검사 및 분류 대상인 물체
    private bool received;                // AI로부터 예측 결과를 받았는지 여부
    private ParcelCategory prediction;    // AI가 판단한 물체 카테고리 결과

    private void Awake()
    {
        // 게임 시작 시 로봇들이 자동으로 실행되지 않도록 초기 설정
        if (vacuumTest != null) vacuumTest.runOnStart = false;
        if (gripperTest != null) gripperTest.runOnStart = false;
    }

    private void OnEnable()
    {
        // 이벤트 리스너 등록: 검사대에 물체가 준비되었을 때와 AI 검사가 완료되었을 때 호출될 메서드 연결
        if (inspectionController != null)
            inspectionController.onParcelReady.AddListener(OnParcelReady);

        if (inspectAgent != null)
            inspectAgent.Inspected += OnInspected;
    }

    private void OnDisable()
    {
        // 컴포넌트 비활성화 시 이벤트 해제 및 실행 중인 코루틴 중단
        if (inspectionController != null)
            inspectionController.onParcelReady.RemoveListener(OnParcelReady);

        if (inspectAgent != null)
            inspectAgent.Inspected -= OnInspected;

        StopAllCoroutines();

        if (IsBusy)
            Fail("작업 도중 Coordinator가 비활성화됐습니다.");
    }

    // [1단계] 검사대에 물체가 준비되었을 때 호출되는 메서드
    public void OnParcelReady()
    {
        // 이미 작업 중이거나 에러 상태라면 무시
        if (IsBusy || Faulted) return;

        // 필수 컴포넌트 연결 여부 확인
        if (inspectionController == null || inspectAgent == null)
        {
            Fail("Inspection Controller와 Inspect Agent를 연결하세요.");
            return;
        }

        // 에이전트 활성 상태 및 프로세스 모드 확인
        if (!inspectAgent.processMode || !inspectAgent.isActiveAndEnabled)
        {
            Fail("Inspect Agent 활성 상태와 Process Mode를 확인하세요.");
            return;
        }

        // 현재 검사대에 있는 대상 물체 가져오기
        target = inspectionController.CurrentParcel;
        if (target == null)
        {
            Fail("정지한 검사 대상이 없습니다.");
            return;
        }

        // 로봇 자동 시작 방지 유지
        if (vacuumTest != null) vacuumTest.runOnStart = false;
        if (gripperTest != null) gripperTest.runOnStart = false;

        // 상태값 초기화 및 작업 시작 플래그 켜기
        IsBusy = true;
        received = false;

        // 비동기 흐름 제어를 위한 Process 코루틴 시작
        StartCoroutine(Process());
    }

    // [2단계] AI 에이전트가 검사 및 판단을 완료했을 때 호출되는 콜백 메서드
    private void OnInspected(Rigidbody parcel, ParcelCategory result)
    {
        // 현재 작업 중이 아니거나, 에러가 났거나, 대상 물체가 일치하지 않으면 무시
        if (!IsBusy || Faulted || received || parcel != target) return;

        prediction = result; // AI 예측 결과 저장
        received = true;     // 결과 수신 완료 플래그 켜기

        Debug.Log($"[AI 판단 결과] 대상={parcel.name} | AI 인식 분류={result}", this);
    }

    // [3단계] AI 판단 결과를 기다리고, 결과에 따라 태그 변경 및 로봇을 제어하는 핵심 코루틴
    private IEnumerator Process()
    {
        // 1. AI 에이전트에 검사 요청 전송
        if (!inspectAgent.Inspect(target))
        {
            Fail("에이전트가 검사 요청을 받지 못했습니다.");
            yield break;
        }

        // 2. 타임아웃 제한 시간 설정 (현재 시간 + 제한 초)
        float deadline = Time.realtimeSinceStartup + Mathf.Max(1f, inferenceTimeout);

        // 3. AI가 결과를 반환할 때까지 대기하는 루프
        while (!received)
        {
            // 대기 중 물체가 사라졌는지 확인
            if (target == null)
            {
                Fail("추론 대기 중 검사 대상이 사라졌습니다.");
                yield break;
            }

            // 제한 시간이 초과되었는지 확인
            if (Time.realtimeSinceStartup >= deadline)
            {
                Fail("AI 추론 대기 시간이 초과됐습니다.");
                yield break;
            }

            yield return null; // 한 프레임 대기
        }

        // 로봇 실행 직전 대상 물체 유효성 재확인
        if (target == null)
        {
            Fail("로봇 실행 전 대상이 사라졌습니다.");
            yield break;
        }

        // 4. [핵심] AI 예측 결과(prediction)에 따른 분류 처리 및 태그 변경 후속 조치
        switch (prediction)
        {
            case ParcelCategory.General:
                Debug.Log($"[Sorting] AI 분류: General -> 로봇 작업 없이 통과", this);

                //  AI 판단 후 물체 태그 변경 (GeneralTag)[cite: 5]
                if (target != null) target.tag = "Parcel_Ignore";
                break;

            case ParcelCategory.Food:
                Debug.Log($"[Sorting] AI 분류: Food -> 집게 로봇(Gripper) 실행", gripperTest);

                //  AI 판단 후 물체 태그 변경 (FoodTag)[cite: 5]
                if (target != null) target.tag = "Parcel_Gripper";

                // 집게형 로봇 실행
                if (gripperTest != null && gripperTest.isActiveAndEnabled)
                {
                    gripperTest.parcel = target;
                    yield return StartCoroutine(gripperTest.Run());
                }
                if (Faulted) yield break;
                yield return new WaitForSeconds(Mathf.Max(0f, afterRobotDelay));
                break;

            case ParcelCategory.Fragile:
                Debug.Log($"[Sorting] AI 분류: Fragile -> 흡착 로봇(Vacuum) 실행", vacuumTest);

                //  AI 판단 후 물체 태그 변경 (FragileTag)[cite: 5]
                if (target != null) target.tag = "Parcel_Vacuum";

                // 흡착형 로봇 실행
                if (vacuumTest != null && vacuumTest.isActiveAndEnabled)
                {
                    vacuumTest.parcel = target;
                    yield return StartCoroutine(vacuumTest.Run());
                }
                if (Faulted) yield break;
                yield return new WaitForSeconds(Mathf.Max(0f, afterRobotDelay));
                break;

            default:
                Fail($"알 수 없는 AI 결과: {prediction}");
                yield break;
        }

        // 5. 작업 완료 전 컨트롤러 및 대상 상태 최종 확인
        if (inspectionController == null || target == null || inspectionController.CurrentParcel != target)
        {
            Fail("작업 완료 전 대상이 변경/삭제됐습니다.");
            yield break;
        }

        // 6. 검사대 릴리즈 (다음 물체를 받을 수 있도록 상태 해제)
        Debug.Log($"[Sorting] 처리 완료 -> 검사대 릴리즈", this);
        inspectionController.ReleaseParcel();

        // 7. 변수 초기화 및 작업 종료 상태로 전환
        target = null;
        received = false;
        IsBusy = false;
    }

    // 에러 발생 시 호출되어 로그를 남기고 상태를 멈추는 메서드
    private void Fail(string message)
    {
        Faulted = true;
        IsBusy = false;
        Debug.LogError("[Sorting] " + message, this);
    }
}