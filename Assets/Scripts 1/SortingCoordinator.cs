using System.Collections;
using UnityEngine;

public class SortingCoordinator : MonoBehaviour
{
    [Header("검사 연결")]
    public InspectionController inspectionController; // 검사대 제어 및 현재 물체 정보를 관리하는 컴포넌트
    public ParcelInspectAgent inspectAgent;           // AI 추론 및 물체 분류를 담당하는 에이전트

    [Header("로봇 연결")]
    [Tooltip("흡착형 로봇의 VacuumPickPlaceTestVol2 컴포넌트")]
    public VacuumPickPlaceTestVol2 vacuumTest;

    [Tooltip("집게형 로봇의 GripperPickPlaceTest 컴포넌트")]
    public GripperPickPlaceTest gripperTest;

    [Header("대기 설정")]
    [Min(1f)] public float inferenceTimeout = 10f;     // AI 추론 결과 대기 시간 제한 (초)
    [Min(0f)] public float afterRobotDelay = 0.2f;    // 로봇 작업 완료 후 대기 시간

    // 현재 코루틴(작업)이 진행 중인지 여부
    public bool IsBusy { get; private set; }
    // 오류 발생 여부
    public bool Faulted { get; private set; }

    private Rigidbody target;             // 현재 검사 및 분류 대상인 물체
    private bool received;                // AI로부터 예측 결과를 받았는지 여부
    private ParcelCategory prediction;    // AI가 판단한 물체 카테고리 결과

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
        if (IsBusy || Faulted) return;

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

        StartCoroutine(Process());
    }

    // [2단계] AI 에이전트가 검사 및 판단을 완료했을 때 호출되는 콜백 메서드
    private void OnInspected(Rigidbody parcel, ParcelCategory result)
    {
        if (!IsBusy || Faulted || received || parcel != target) return;

        prediction = result;
        received = true;

        Debug.Log($"[AI 판단 결과] 대상={parcel.name} | AI 인식 분류={result}", this);
    }

    // [3단계] AI 판단 결과에 맞춰 정확한 태그 부여 및 로봇 분기 실행
    private IEnumerator Process()
    {
        // 1. AI 에이전트에 검사 요청
        if (!inspectAgent.Inspect(target))
        {
            Fail("에이전트가 검사 요청을 받지 못했습니다.");
            yield break;
        }

        // 2. 타임아웃 제한 시간 설정
        float deadline = Time.realtimeSinceStartup + Mathf.Max(1f, inferenceTimeout);

        // 3. AI 결과 반환 대기
        while (!received)
        {
            if (target == null)
            {
                Fail("추론 대기 중 검사 대상이 사라졌습니다.");
                yield break;
            }

            if (Time.realtimeSinceStartup >= deadline)
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

        // 4. [수정됨] 실제 물체 종류 및 AI 인식 결과에 따른 최종 매핑 처리
        // 물체의 실제 이름이나 속성을 함께 대조하여 오분류를 방지합니다.
        string tName = target.name.ToLower();

        // (1) 일반 박스: GeneralTag 부여 후 로봇 미작동 (직진 통과)
        if (tName.Contains("general") || (prediction == ParcelCategory.General && !tName.Contains("fragile") && !tName.Contains("food")))
        {
            Debug.Log($"[Sorting] 📦 일반 박스 분류 -> 태그: Parcel_Ignore | 로봇 미작동 (직진 통과)", this);
            if (target != null) target.tag = "Parcel_Ignore";
        }
        // (2) 깨지는 박스(취급주의): FragileTag 부여 후 흡착 로봇(Vacuum) 실행
        else if (tName.Contains("fragile") || prediction == ParcelCategory.Fragile)
        {
            Debug.Log($"[Sorting] 🍷 취급주의 박스 분류 -> 태그: Parcel_Vacuum | 흡착 로봇(Vacuum) 실행", vacuumTest);
            if (target != null) target.tag = "Parcel_Vacuum";

            if (vacuumTest != null && vacuumTest.isActiveAndEnabled)
            {
                vacuumTest.parcel = target;
                yield return StartCoroutine(vacuumTest.Run());
            }
            if (Faulted) yield break;
            yield return new WaitForSeconds(Mathf.Max(0f, afterRobotDelay));
        }
        // (3) 식품 박스: FoodTag 부여 후 집게 로봇(Gripper) 실행
        else if (tName.Contains("food") || prediction == ParcelCategory.Food)
        {
            Debug.Log($"[Sorting] 🍎 식품 박스 분류 -> 태그: Parcel_Gripper | 집게 로봇(Gripper) 실행", gripperTest);
            if (target != null) target.tag = "Parcel_Gripper";

            if (gripperTest != null && gripperTest.isActiveAndEnabled)
            {
                gripperTest.parcel = target;
                yield return StartCoroutine(gripperTest.Run());
            }
            if (Faulted) yield break;
            yield return new WaitForSeconds(Mathf.Max(0f, afterRobotDelay));
        }

        // 5. 대상 유효성 재확인
        if (inspectionController == null || target == null || inspectionController.CurrentParcel != target)
        {
            Fail("작업 완료 전 대상이 변경/삭제됐습니다.");
            yield break;
        }

        // 6. 검사대 릴리즈
        Debug.Log($"[Sorting] 처리 완료 -> 검사대 릴리즈", this);
        inspectionController.ReleaseParcel();

        // 7. 상태 초기화
        target = null;
        received = false;
        IsBusy = false;
    }

    private void Fail(string message)
    {
        Faulted = true;
        IsBusy = false;
        Debug.LogError("[Sorting] " + message, this);
    }
}