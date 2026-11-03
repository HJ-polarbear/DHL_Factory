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
    [Tooltip("켜면 자동 판단을 요청하지 않습니다. Context Menu로 요청할 수 있습니다.")]
    public bool manualTestMode = false;

    [Header("로봇 분류 연동")]
    [Tooltip("학습할 때 OFF, 로봇 분류 시연할 때 ON")]
    public bool useRobotSorting = false;

    public ParcelSorterRobot sortingRobot;

    [Header("생성 위치 변화 - 검사 위치 로컬 기준")]
    [Tooltip("컨베이어 좌우 방향 흔들림")]
    [Range(0f, 0.1f)]
    public float lateralJitter = 0.03f;

    [Tooltip("컨베이어 진행 방향 흔들림")]
    [Range(0f, 0.1f)]
    public float forwardJitter = 0.01f;

    [Header("생성 회전 변화 - Y축")]
    [Tooltip("박스 회전 각도 흔들림")]
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
    private bool parcelPrepared;
    private bool waitingForDecision;
    private bool handlingRobot;

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
            Debug.LogError(
                "[ParcelInspectAgent] 검사 위치와 프리팹 3개를 연결하세요.",
                this
            );
            return;
        }

        bool categoryNumbersValid =
            (int)ParcelCategory.General == 0 &&
            (int)ParcelCategory.Food == 1 &&
            (int)ParcelCategory.Fragile == 2;

        bool prefabCategoriesValid =
            generalPrefab.category == ParcelCategory.General &&
            foodPrefab.category == ParcelCategory.Food &&
            fragilePrefab.category == ParcelCategory.Fragile;

        ready = categoryNumbersValid && prefabCategoriesValid;

        if (!ready)
        {
            Debug.LogError(
                "[ParcelInspectAgent] Category 설정을 확인하세요. " +
                "General=0, Food=1, Fragile=2여야 하며, " +
                "각 프리팹의 Category도 일치해야 합니다.",
                this
            );
        }
    }

    public override void OnEpisodeBegin()
    {
        StopAllCoroutines();

        parcelPrepared = false;
        waitingForDecision = false;
        handlingRobot = false;

        ClearParcel();

        if (!ready) return;

        StartCoroutine(PrepareAndRequestDecision());
    }

    private IEnumerator PrepareAndRequestDecision()
    {
        if (useRobotSorting)
        {
            if (sortingRobot == null)
            {
                Debug.LogError(
                    "[ParcelInspectAgent] Sorting Robot을 연결하세요.",
                    this
                );
                yield break;
            }

            while (sortingRobot.IsBusy)
            {
                yield return null;
            }
        }

        SpawnParcel();

        // 기존 학습 코드의 카메라 준비 대기 유지
        yield return null;
        yield return new WaitForEndOfFrame();

        if (currentParcel == null) yield break;

        parcelPrepared = true;

        if (!manualTestMode)
        {
            RequestPreparedDecision();
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
            Quaternion.Euler(
                0f,
                Random.Range(-yawJitter, yawJitter),
                0f
            );

        currentParcel = Instantiate(
            selectedPrefab,
            spawnPosition,
            spawnRotation
        );

        currentParcel.gameObject.SetActive(true);
        actualCategory = currentParcel.category;

        Rigidbody[] bodies =
            currentParcel.GetComponentsInChildren<Rigidbody>();

        foreach (Rigidbody body in bodies)
        {
            body.isKinematic = true;
        }
    }

    private ParcelInfo GetPrefab(int index)
    {
        switch (index)
        {
            case 0:
                return generalPrefab;

            case 1:
                return foodPrefab;

            default:
                return fragilePrefab;
        }
    }

    [ContextMenu("Request Prepared Decision")]
    public void RequestPreparedDecision()
    {
        if (!Application.isPlaying) return;
        if (!ready || !parcelPrepared) return;
        if (currentParcel == null) return;
        if (waitingForDecision || handlingRobot) return;

        if (useRobotSorting)
        {
            if (sortingRobot == null)
            {
                Debug.LogError(
                    "[ParcelInspectAgent] Sorting Robot을 연결하세요.",
                    this
                );
                return;
            }

            if (sortingRobot.IsBusy) return;
        }

        waitingForDecision = true;
        RequestDecision();
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!waitingForDecision || currentParcel == null) return;

        if (actions.DiscreteActions.Length < 1)
        {
            waitingForDecision = false;

            Debug.LogError(
                "[ParcelInspectAgent] Discrete Branch 1개, " +
                "Branch Size 3으로 설정하세요.",
                this
            );
            return;
        }

        int predictedIndex = actions.DiscreteActions[0];
        int actualIndex = (int)actualCategory;

        if (predictedIndex < 0 || predictedIndex >= 3 ||
            actualIndex < 0 || actualIndex >= 3)
        {
            waitingForDecision = false;

            Debug.LogError(
                $"[ParcelInspectAgent] 분류 번호 오류: " +
                $"정답={actualIndex}, 예측={predictedIndex}",
                this
            );
            return;
        }

        // 동일 상자를 여러 번 판정하지 않도록 차단
        waitingForDecision = false;
        parcelPrepared = false;

        bool correct = predictedIndex == actualIndex;

        LastPrediction = (ParcelCategory)predictedIndex;
        LastCorrect = correct;

        SetReward(correct ? correctReward : wrongReward);
        RecordResult(actualIndex, predictedIndex, correct);

        Academy.Instance.StatsRecorder.Add(
            "Parcel/Accuracy",
            correct ? 1f : 0f
        );

        if (logEachDecision)
        {
            Debug.Log(
                $"[Parcel] 정답: {actualCategory} / " +
                $"예측: {LastPrediction} / " +
                $"{(correct ? "정답" : "오답")}",
                this
            );
        }

        if (totalCount % Mathf.Max(1, logEvery) == 0)
        {
            float accuracy = 100f * correctCount / totalCount;

            Debug.Log(
                $"[Parcel] 누적 {totalCount}회 / " +
                $"정답 {correctCount}회 / " +
                $"정확도 {accuracy:F1}%",
                this
            );
        }

        if (useRobotSorting)
        {
            handlingRobot = true;

            StartCoroutine(
                HandleRobotSorting(
                    currentParcel.gameObject,
                    LastPrediction
                )
            );
        }
        else
        {
            // 학습 시 기존처럼 판정 후 바로 종료
            EndEpisode();
        }
    }

    private IEnumerator HandleRobotSorting(
        GameObject targetParcel,
        ParcelCategory predictedCategory
    )
    {
        if (sortingRobot == null)
        {
            handlingRobot = false;

            Debug.LogError(
                "[ParcelInspectAgent] Sorting Robot 연결이 없습니다.",
                this
            );
            yield break;
        }

        // 실제 정답이 아니라 AI의 예측 결과에 따라 이동
        yield return sortingRobot.SortParcel(
            targetParcel,
            predictedCategory
        );

        handlingRobot = false;

        if (!sortingRobot.LastSortSucceeded)
        {
            Debug.LogError(
                "[ParcelInspectAgent] 분류 이동에 실패했습니다. " +
                "수거함 연결과 Console 오류를 확인하세요. " +
                "자동 진행을 중지합니다.",
                this
            );
            yield break;
        }

        // 로봇 작업 완료 후 상자 정리
        ClearParcel();

        // 다음 상자 검사 시작
        EndEpisode();
    }

    private void ClearParcel()
    {
        if (currentParcel == null) return;

        currentParcel.gameObject.SetActive(false);
        Destroy(currentParcel.gameObject);
        currentParcel = null;
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
            if (keyboard.digit3Key.isPressed)
            {
                selected = 2;
            }
            else if (keyboard.digit2Key.isPressed)
            {
                selected = 1;
            }
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.Alpha3))
        {
            selected = 2;
        }
        else if (Input.GetKey(KeyCode.Alpha2))
        {
            selected = 1;
        }
#endif

        discreteActions[0] = selected;
    }

    private void RecordResult(
        int actualIndex,
        int predictedIndex,
        bool correct
    )
    {
        totalCount++;

        if (correct)
        {
            correctCount++;
        }

        confusionTable[actualIndex, predictedIndex]++;
    }

    [ContextMenu("Print Confusion Table")]
    private void PrintConfusionTable()
    {
        Debug.Log(
            "[Parcel] 혼동표: 행=정답, 열=예측\n" +
            "열 순서: General, Food, Fragile\n" +
            $"General: {confusionTable[0, 0]}, " +
            $"{confusionTable[0, 1]}, {confusionTable[0, 2]}\n" +
            $"Food: {confusionTable[1, 0]}, " +
            $"{confusionTable[1, 1]}, {confusionTable[1, 2]}\n" +
            $"Fragile: {confusionTable[2, 0]}, " +
            $"{confusionTable[2, 1]}, {confusionTable[2, 2]}",
            this
        );
    }
}