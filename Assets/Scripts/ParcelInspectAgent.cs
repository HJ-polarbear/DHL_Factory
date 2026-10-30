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

    [Header("수동 테스트 모드 (AI 학습 시 반드시 체크 해제!)")]
    public bool manualTestMode = false;

    [Header("생성 위치 변화 - 검사 위치의 로컬 X/Z 기준")]
    [Min(0f)]
    public float positionJitter = 0.01f;

    [Header("생성 회전 변화 - Y축")]
    [Range(0f, 180f)]
    public float yawJitter = 10f;

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
        if (!ready) return;
        SpawnNewParcel();
    }

    // 새로운 상자로 교체 생성
    private void SpawnNewParcel()
    {
        if (currentParcel != null)
        {
            Destroy(currentParcel.gameObject);
            currentParcel = null;
        }

        int selectedIndex = Random.Range(0, 3);
        ParcelInfo selectedPrefab = GetPrefab(selectedIndex);

        Vector3 localOffset = new Vector3(
            Random.Range(-positionJitter, positionJitter),
            0f,
            Random.Range(-positionJitter, positionJitter)
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

        // 보상 부여
        SetReward(correct ? correctReward : wrongReward);
        RecordResult(actualIndex, predictedIndex, correct);

        Academy.Instance.StatsRecorder.Add("Parcel/Accuracy", correct ? 1f : 0f);

        if (totalCount % Mathf.Max(1, logEvery) == 0)
        {
            float accuracy = 100f * correctCount / totalCount;
            Debug.Log($"[Parcel] 누적 {totalCount}회 / 정답 {correctCount}회 / 정확도 {accuracy:F1}%", this);
        }

        // 다음 상자 준비
        StartCoroutine(PrepareNextParcel());
    }

    private IEnumerator PrepareNextParcel()
    {
        if (currentParcel != null)
        {
            Destroy(currentParcel.gameObject);
            currentParcel = null;
        }

        // 오브젝트 삭제 대기
        yield return null;

        // 새 상자 생성
        SpawnNewParcel();

        // 새 상자가 화면에 완전히 렌더링될 때까지 1프레임 대기
        yield return new WaitForEndOfFrame();
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