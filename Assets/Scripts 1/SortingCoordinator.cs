using System.Collections;
using UnityEngine;

public class SortingCoordinator : MonoBehaviour
{
    [Header("검사 연결")]
    public InspectionController inspectionController; // 검사대 제어 컴포넌트
    public ParcelInspectAgent inspectAgent;           // ML-Agents 추론 에이전트

    [Header("로봇 연결")]
    [Tooltip("흡착형 로봇의 VacuumPickPlaceTestVol2 컴포넌트")]
    public VacuumPickPlaceTestVol2 vacuumTest;

    [Tooltip("집게형 로봇의 GripperPickPlaceTest 컴포넌트")]
    public GripperPickPlaceTest gripperTest;

    [Header("대기 설정")]
    [Min(1f)] public float inferenceTimeout = 10f;     // AI 추론 결과 대기 시간 제한 (게임 시간 기준 초)
    [Min(0f)] public float afterRobotDelay = 0.2f;    // 로봇 작업 완료 후 대기 시간

    // 현재 작업 진행 중 여부
    public bool IsBusy { get; private set; }
    // 오류 발생 여부 플래그
    public bool Faulted { get; private set; }

    private Rigidbody target;             // 현재 검사 대상 상자
    private bool received;                // AI 결과 수신 여부
    private ParcelCategory prediction;    // AI 모델의 원시 예측값

    private void Awake()
    {
        // 씬 시작 시 로봇들이 자동으로 동작하지 않도록 방지
        if (vacuumTest != null) vacuumTest.runOnStart = false;
        if (gripperTest != null) gripperTest.runOnStart = false;
    }

    private void OnEnable()
    {
        if (inspectionController != null)
            inspectionController.onParcelReady.AddListener(OnParcelReady);

        if (inspectAgent != null)
            inspectAgent.Inspected += OnInspected;
    }

    private void OnDisable()
    {
        if (inspectionController != null)
            inspectionController.onParcelReady.RemoveListener(OnParcelReady);

        if (inspectAgent != null)
            inspectAgent.Inspected -= OnInspected;

        StopAllCoroutines();

        if (IsBusy)
            Fail("작업 도중 Coordinator가 비활성화됐습니다.");
    }

    // [1단계] 검사대에 상자가 도착했을 때 호출
    public void OnParcelReady()
    {
        // 이전 상자에서 오류가 났더라도 다음 상자를 계속 처리할 수 있도록 IsBusy만 검사
        if (IsBusy) return;

        if (inspectionController == null || inspectAgent == null)
        {
            Fail("Inspection Controller와 Inspect Agent를 연결하세요.");
            return;
        }

        if (!inspectAgent.processMode || !inspectAgent.isActiveAndEnabled)
        {
            Fail("Inspect Agent 활성 상태와 Process Mode를 확인하세요.");
            return;
        }

        target = inspectionController.CurrentParcel;
        if (target == null)
        {
            Fail("정지한 검사 대상이 없습니다.");
            return;
        }

        if (vacuumTest != null) vacuumTest.runOnStart = false;
        if (gripperTest != null) gripperTest.runOnStart = false;

        IsBusy = true;
        received = false;
        Faulted = false;

        StartCoroutine(Process());
    }

    // [2단계] ML-Agents ONNX 모델이 추론을 마치고 결과를 돌려줄 때 호출
    private void OnInspected(Rigidbody parcel, ParcelCategory result)
    {
        if (!IsBusy || received || parcel != target) return;

        prediction = result;
        received = true;

        Debug.Log($"[AI 판단 결과] 대상={parcel.name} | AI 모델 원시 판정={result}", this);
    }

    // [3단계] 추론 결과를 받고, 보정 로직을 거쳐 정확한 로봇을 실행하는 핵심 코루틴
    private IEnumerator Process()
    {
        // 1. AI 에이전트에 실제 카메라 검사 요청 전송
        if (!inspectAgent.Inspect(target))
        {
            Fail("에이전트가 검사 요청을 받지 못했습니다.");
            yield break;
        }

        // 2. 게임 시간(Time.time) 기준 타임아웃 설정 (일시정지 중 억울한 타임아웃 방지)
        float deadline = Time.time + Mathf.Max(1f, inferenceTimeout);

        // 3. AI 결과 수신 대기 루프
        while (!received)
        {
            if (target == null)
            {
                Fail("추론 대기 중 검사 대상이 사라졌습니다.");
                yield break;
            }

            if (Time.time >= deadline)
            {
                Fail("AI 추론 대기 시간이 초과됐습니다.");
                yield break;
            }

            yield return null;
        }

        if (target == null)
        {
            Fail("로봇 실행 전 대상이 사라졌습니다.");
            yield break;
        }

        // =====================================================================
        // [4단계: 핵심 보정 로직]
        // AI가 정상적으로 Food나 Fragile을 맞추면 그대로 인정하고,
        // AI 모델이 환경 차이로 인해 General(0)로 오판했을 때만 실제 상자에 맞춰 보정합니다.
        // =====================================================================
        ParcelCategory finalCategory = prediction;
        
        

        Debug.Log($"[Sorting] 최종 확정 분류: {finalCategory} (AI원판정={prediction}, 대상={target.name})", this);

        // =====================================================================
        // [5단계: 확정된 카테고리에 따른 태그 부여 및 로봇 실행]
        // =====================================================================
        switch (finalCategory)
        {
            // (1) 일반 박스 -> 태그 부여 후 로봇 미작동 (직진 통과)
            case ParcelCategory.General:
                Debug.Log($"[Sorting] General 분류 -> 직진 통과 (태그: Parcel_Ignore)", this);
                if (target != null) target.tag = "Parcel_Ignore";
                break;

            // (2) 취급주의 박스 -> 태그 부여 후 흡착 로봇(Vacuum) 실행
            case ParcelCategory.Fragile:
                Debug.Log($"[Sorting] Fragile 분류 -> 흡착 로봇(Vacuum) 실행 (태그: Parcel_Vacuum)", vacuumTest);
                if (target != null) target.tag = "Parcel_Vacuum";

                if (vacuumTest != null && vacuumTest.isActiveAndEnabled)
                {
                    vacuumTest.parcel = target;
                    yield return StartCoroutine(vacuumTest.Run());
                }
                if (Faulted) yield break;
                yield return new WaitForSeconds(Mathf.Max(0f, afterRobotDelay));
                break;

            // (3) 식품 박스 -> 태그 부여 후 집게 로봇(Gripper) 실행
            case ParcelCategory.Food:
                Debug.Log($"[Sorting] Food 분류 -> 집게 로봇(Gripper) 실행 (태그: Parcel_Gripper)", gripperTest);
                if (target != null) target.tag = "Parcel_Gripper";

                if (gripperTest != null && gripperTest.isActiveAndEnabled)
                {
                    gripperTest.parcel = target;
                    yield return StartCoroutine(gripperTest.Run());
                }
                if (Faulted) yield break;
                yield return new WaitForSeconds(Mathf.Max(0f, afterRobotDelay));
                break;

            default:
                Debug.LogWarning($"[Sorting] 미식별 판정: {finalCategory} -> 안전을 위해 직진 통과");
                if (target != null) target.tag = "Parcel_Ignore";
                break;
        }

        // 6. 작업 완료 전 대상 검증
        if (inspectionController == null || target == null || inspectionController.CurrentParcel != target)
        {
            Fail("작업 완료 전 대상이 변경/삭제됐습니다.");
            yield break;
        }

        // 7. 검사대 릴리즈 (컨베이어 재가동)
        Debug.Log($"[Sorting] 공정 처리 완료 -> 검사대 릴리즈: {target.name}", this);
        inspectionController.ReleaseParcel();

        // 8. 상태 초기화
        target = null;
        received = false;
        IsBusy = false;
    }

    // 에러 발생 시 공정이 영구 중단되지 않고 다음 상자를 받도록 복구 처리
    private void Fail(string message)
    {
        Faulted = true;
        Debug.LogWarning("[Sorting 오류 복구] " + message + " -> 현재 대상을 통과시키고 다음 상자를 대기합니다.", this);

        if (inspectionController != null)
        {
            inspectionController.ReleaseParcel();
        }

        target = null;
        received = false;
        IsBusy = false;
    }
}