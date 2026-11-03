using System.Collections;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ParcelInspectAgent : Agent
{
    [Header("택배 프리팹 - 루트에 ParcelInfo 필요")]
    public ParcelInfo generalPrefab;
    public ParcelInfo foodPrefab;
    public ParcelInfo fragilePrefab;

    [Header("검사 위치")]
    public Transform inspectionPoint;

    [Header("수동 테스트 모드")]
    public bool manualTestMode = false;

    [Header("생성 위치 변화 (검사 위치 로컬 기준)")]
    [Tooltip("컨베이어 좌우 방향 흔들림 (기본: ±3cm)")]
    [Range(0f, 0.1f)]
    public float lateralJitter = 0.03f;

    [Tooltip("컨베이어 진행 방향 흔들림 (기본: ±1cm)")]
    [Range(0f, 0.1f)]
    public float forwardJitter = 0.01f;

    [Header("생성 회전 변화 - Y축")]
    [Tooltip("박스 회전 각도 흔들림 (기본: ±15°)")]
    [Range(0f, 180f)]
    public float yawJitter = 15f;

    [Header("보상")]
    public float correctReward = 1f;
    public float wrongReward = -1f;

    [Header("로그")]
    public bool logEachDecision = false;

    [Min(1)]
    public int logEvery = 100;

    private ParcelInfo currentParcel;
    private ParcelCategory actualCategory;
    private bool ready;

    private int totalCount;
    private int correctCount;
    private readonly int[,] confusionTable = new int[3, 3];

    public ParcelCategory LastPrediction { get; private set; }
    public bool LastCorrect { get; private set; }

    public override void Initialize()
    {
        ready =
            inspectionPoint != null &&
            generalPrefab != null &&
            foodPrefab != null &&
            fragilePrefab != null;

        if (!ready)
        {
            Debug.LogError("[ParcelInspectAgent] 검사 위치와 프리팹 3개를 연결하세요.", this);
            return;
        }

        ready =
            generalPrefab.category == ParcelCategory.General &&
            foodPrefab.category == ParcelCategory.Food &&
            fragilePrefab.category == ParcelCategory.Fragile;

        if (!ready)
        {
            Debug.LogError("[ParcelInspectAgent] 프리팹의 Category 설정이 잘못됐습니다.", this);
        }
    }

    public override void OnEpisodeBegin()
    {
        StopAllCoroutines();
        ClearParcel();

        if (!ready) return;

        // 상자 생성 후 카메라 캡처 대기 코루틴 시작
        StartCoroutine(PrepareAndRequestDecision());
    }

    private IEnumerator PrepareAndRequestDecision()
    {
        // 1. 새 상자 생성
        SpawnParcel();

        // 2. 상자가 카메라 화면에 완전히 렌더링될 때까지 1프레임 대기
        yield return null;
        yield return new WaitForEndOfFrame();

        // 3. 카메라가 상자를 확실히 본 상태에서 AI에게 판단 요청
        if (!manualTestMode)
        {
            RequestDecision();
        }
    }

    private void SpawnParcel()
    {
        int selectedIndex = Random.Range(0, 3);
        ParcelInfo selectedPrefab = GetPrefab(selectedIndex);

        Vector3 localOffset = new Vector3(
            Random.Range(-lateralJitter, lateralJitter),
            0f,
            Random.Range(-forwardJitter, forwardJitter)
        );

        Vector3 spawnPosition =
            inspectionPoint.position +
            inspectionPoint.rotation * localOffset;

        Quaternion spawnRotation =
            inspectionPoint.rotation *
            Quaternion.Euler(0f, Random.Range(-yawJitter, yawJitter), 0f);

        currentParcel = Instantiate(selectedPrefab, spawnPosition, spawnRotation);
        currentParcel.gameObject.SetActive(true);
        actualCategory = currentParcel.category;

        Rigidbody[] bodies = currentParcel.GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody body in bodies)
        {
            body.isKinematic = true;
        }
    }

    private ParcelInfo GetPrefab(int index)
    {
        switch (index)
        {
            case 0: return generalPrefab;
            case 1: return foodPrefab;
            default: return fragilePrefab;
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (currentParcel == null) return;

        int predictedIndex = actions.DiscreteActions[0];
        if (predictedIndex < 0 || predictedIndex >= 3) predictedIndex = 0;

        int actualIndex = (int)actualCategory;
        bool correct = predictedIndex == actualIndex;

        LastPrediction = (ParcelCategory)predictedIndex;
        LastCorrect = correct;

        SetReward(correct ? correctReward : wrongReward);
        RecordResult(actualIndex, predictedIndex, correct);

        Academy.Instance.StatsRecorder.Add("Parcel/Accuracy", correct ? 1f : 0f);

        if (logEachDecision)
        {
            Debug.Log($"[Parcel] 정답: {actualCategory} / 예측: {LastPrediction} / {(correct ? "⭕ 정답!" : "❌ 오답!")}", this);
        }

        if (totalCount % Mathf.Max(1, logEvery) == 0)
        {
            float accuracy = 100f * correctCount / totalCount;
            Debug.Log($"[Parcel] 누적 {totalCount}회 / 정답 {correctCount}회 / 정확도 {accuracy:F1}%", this);
        }

        // 판정 즉시 에피소드 종료
        EndEpisode();
    }

    private void ClearParcel()
    {
        if (currentParcel != null)
        {
            currentParcel.gameObject.SetActive(false);
            Destroy(currentParcel.gameObject);
            currentParcel = null;
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        if (actionsOut.DiscreteActions.Length < 1) return;

        var discreteActions = actionsOut.DiscreteActions;
        int selected = 0;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit3Key.isPressed) selected = 2;
            else if (keyboard.digit2Key.isPressed) selected = 1;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.Alpha3)) selected = 2;
        else if (Input.GetKey(KeyCode.Alpha2)) selected = 1;
#endif

        discreteActions[0] = selected;
    }

    private void RecordResult(int actualIndex, int predictedIndex, bool correct)
    {
        totalCount++;
        if (correct) correctCount++;
        confusionTable[actualIndex, predictedIndex]++;
    }

    [ContextMenu("Print Confusion Table")]
    private void PrintConfusionTable()
    {
        Debug.Log(
            "[Parcel] 혼동표\n" +
            $"General: {confusionTable[0, 0]}, {confusionTable[0, 1]}, {confusionTable[0, 2]}\n" +
            $"Food: {confusionTable[1, 0]}, {confusionTable[1, 1]}, {confusionTable[1, 2]}\n" +
            $"Fragile: {confusionTable[2, 0]}, {confusionTable[2, 1]}, {confusionTable[2, 2]}",
            this
        );
    }
}