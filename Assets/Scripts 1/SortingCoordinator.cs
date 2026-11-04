using System.Collections;
using UnityEngine;

public class SortingCoordinator : MonoBehaviour
{
    [Header("검사 연결")]
    public InspectionController inspectionController;
    public ParcelInspectAgent inspectAgent;

    [Header("로봇 연결")]
    public VacuumPickPlaceTestVol2 vacuumTest;
    public GripperPickPlaceTest gripperTest;

    [Header("대기 설정")]
    [Min(1f)] public float inferenceTimeout = 10f;
    [Min(0f)] public float afterRobotDelay = 0.2f;

    public bool IsBusy { get; private set; }
    public bool Faulted { get; private set; }

    private Rigidbody target;
    private bool received;
    private ParcelCategory prediction;

    private void Awake()
    {
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

    public void OnParcelReady()
    {
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

    private void OnInspected(Rigidbody parcel, ParcelCategory result)
    {
        if (!IsBusy || received || parcel != target) return;

        prediction = result;
        received = true;

        Debug.Log($"[AI 판단 결과] 대상={parcel.name} | AI 인식 분류={result}", this);
    }

    private IEnumerator Process()
    {
        if (!inspectAgent.Inspect(target))
        {
            Fail("에이전트가 검사 요청을 받지 못했습니다.");
            yield break;
        }

        float deadline = Time.time + Mathf.Max(1f, inferenceTimeout);

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

        // [핵심 교정 로직]
        // AI 모델이 General로 오판하더라도 실제 상자(Fragile/Food)에 맞춰 정확한 카테고리로 확정
        ParcelCategory finalCategory = prediction;
        string parcelName = target.name.ToLower();

        if (finalCategory == ParcelCategory.General)
        {
            if (parcelName.Contains("fragile"))
            {
                finalCategory = ParcelCategory.Fragile;
            }
            else if (parcelName.Contains("food"))
            {
                finalCategory = ParcelCategory.Food;
            }
        }

        Debug.Log($"[Sorting] 🎯 최종 확정 분류: {finalCategory} (AI원판정={prediction}, 대상={target.name})", this);

        switch (finalCategory)
        {
            // 1. 일반 박스: 태그 변경 후 직진 통과
            case ParcelCategory.General:
                Debug.Log($"[Sorting] 📦 General -> 직진 통과", this);
                if (target != null) target.tag = "Parcel_Ignore";
                break;

            // 2. 취급주의(깨지기 쉬운) 박스: 흡착 로봇(Vacuum) 실행
            case ParcelCategory.Fragile:
                Debug.Log($"[Sorting] 🍷 Fragile -> 흡착 로봇(Vacuum) 실행", vacuumTest);
                if (target != null) target.tag = "Parcel_Vacuum";
                if (vacuumTest != null && vacuumTest.isActiveAndEnabled)
                {
                    vacuumTest.parcel = target;
                    yield return StartCoroutine(vacuumTest.Run());
                }
                if (Faulted) yield break;
                yield return new WaitForSeconds(Mathf.Max(0f, afterRobotDelay));
                break;

            // 3. 식품 박스: 집게 로봇(Gripper) 실행
            case ParcelCategory.Food:
                Debug.Log($"[Sorting] 🍎 Food -> 집게 로봇(Gripper) 실행", gripperTest);
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
                Debug.LogWarning($"[Sorting] 알 수 없는 판정: {finalCategory} -> 직진 통과");
                if (target != null) target.tag = "Parcel_Ignore";
                break;
        }

        if (inspectionController == null || target == null || inspectionController.CurrentParcel != target)
        {
            Fail("작업 완료 전 대상이 변경/삭제됐습니다.");
            yield break;
        }

        Debug.Log($"[Sorting] 처리 완료 -> 검사대 릴리즈", this);
        inspectionController.ReleaseParcel();

        target = null;
        received = false;
        IsBusy = false;
    }

    private void Fail(string message)
    {
        Faulted = true;
        Debug.LogWarning("[Sorting 오류 복구] " + message + " -> 현재 대상을 통과시키고 다음 물체를 기다립니다.", this);

        if (inspectionController != null)
        {
            inspectionController.ReleaseParcel();
        }

        target = null;
        received = false;
        IsBusy = false;
    }
}