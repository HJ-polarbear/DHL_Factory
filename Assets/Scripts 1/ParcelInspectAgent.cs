using System;
using System.Collections;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ParcelInspectAgent : Agent
{
    [Header("공정 모드: 외부 상자만 검사, 생성/삭제 안 함")]
    public bool processMode = true;
    [Header("학습용 프리팹")]
    public ParcelInfo generalPrefab;
    public ParcelInfo foodPrefab;
    public ParcelInfo fragilePrefab;
    public Transform inspectionPoint;
    public bool manualTestMode = false;
    [Range(0f, 0.1f)] public float lateralJitter = 0.03f;
    [Range(0f, 0.1f)] public float forwardJitter = 0.01f;
    [Range(0f, 180f)] public float yawJitter = 15f;
    public float correctReward = 1f;
    public float wrongReward = -1f;
    public bool logEachDecision = false;
    [Min(1)] public int logEvery = 100;

    public event Action<Rigidbody, ParcelCategory> Inspected;
    public ParcelCategory LastPrediction { get; private set; }
    public bool LastCorrect { get; private set; }
    public bool IsDeciding => preparing || waiting;

    private ParcelInfo currentParcel;
    private ParcelCategory actualCategory;
    private Rigidbody processParcel;
    private bool ready;
    private bool preparing;
    private bool waiting;
    private int totalCount;
    private int correctCount;
    private readonly int[,] confusionTable = new int[3, 3];

    public override void Initialize()
    {
        if (processMode) { ready = true; return; }
        ready = inspectionPoint != null && generalPrefab != null &&
            foodPrefab != null && fragilePrefab != null;
        if (ready)
            ready = generalPrefab.category == ParcelCategory.General &&
                foodPrefab.category == ParcelCategory.Food &&
                fragilePrefab.category == ParcelCategory.Fragile;
        if (!ready) Debug.LogError("[Agent] 검사 위치, 프리팹 3개와 Category 설정을 확인하세요.", this);
    }

    public override void OnEpisodeBegin()
    {
        StopAllCoroutines();
        preparing = false;
        waiting = false;
        processParcel = null;
        if (processMode) return;
        ClearTrainingParcel();
        if (!ready) return;
        SpawnParcel();
        StartCoroutine(PrepareTrainingDecision());
    }

    public bool Inspect(Rigidbody parcel)
    {
        if (!processMode || !isActiveAndEnabled || parcel == null || IsDeciding)
            return false;
        processParcel = parcel;
        preparing = true;
        StartCoroutine(PrepareProcessDecision());
        return true;
    }

    private IEnumerator PrepareProcessDecision()
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        preparing = false;
        if (processParcel == null) yield break;
        waiting = true;
        RequestDecision();
    }

    private IEnumerator PrepareTrainingDecision()
    {
        preparing = true;
        yield return null;
        yield return new WaitForEndOfFrame();
        preparing = false;
        if (currentParcel == null || manualTestMode) yield break;
        waiting = true;
        RequestDecision();
    }

    private void SpawnParcel()
    {
        int index = UnityEngine.Random.Range(0, 3);
        ParcelInfo prefab = index == 0 ? generalPrefab : index == 1 ? foodPrefab : fragilePrefab;
        Vector3 offset = new Vector3(UnityEngine.Random.Range(-lateralJitter, lateralJitter),
            0f, UnityEngine.Random.Range(-forwardJitter, forwardJitter));
        Quaternion rotation = inspectionPoint.rotation * Quaternion.Euler(0f,
            UnityEngine.Random.Range(-yawJitter, yawJitter), 0f);
        currentParcel = Instantiate(prefab,
            inspectionPoint.position + inspectionPoint.rotation * offset, rotation);
        currentParcel.gameObject.SetActive(true);
        actualCategory = currentParcel.category;
        foreach (Rigidbody body in currentParcel.GetComponentsInChildren<Rigidbody>())
            body.isKinematic = true;
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!waiting) return;
        if (actions.DiscreteActions.Length < 1)
        {
            Debug.LogError("[Agent] Discrete Branch 1개, 크기 3이 필요합니다.", this);
            return;
        }
        int index = actions.DiscreteActions[0];
        if (index < 0 || index > 2)
        {
            Debug.LogError("[Agent] 행동 번호는 0~2여야 합니다.", this);
            return;
        }
        waiting = false;
        LastPrediction = (ParcelCategory)index;
        if (processMode)
        {
            Rigidbody target = processParcel;
            processParcel = null;
            // 공정 상자는 생성하거나 삭제하지 않습니다.
            EndEpisode();
            if (target != null) Inspected?.Invoke(target, LastPrediction);
            return;
        }
        if (currentParcel == null) return;
        int actual = (int)actualCategory;
        if (actual < 0 || actual > 2) return;
        LastCorrect = index == actual;
        SetReward(LastCorrect ? correctReward : wrongReward);
        totalCount++;
        if (LastCorrect) correctCount++;
        confusionTable[actual, index]++;
        Academy.Instance.StatsRecorder.Add("Parcel/Accuracy", LastCorrect ? 1f : 0f);
        if (logEachDecision)
            Debug.Log($"[Parcel] 정답={actualCategory}, 예측={LastPrediction}, 정답여부={LastCorrect}", this);
        if (totalCount % Mathf.Max(1, logEvery) == 0)
            Debug.Log($"[Parcel] {totalCount}회, 정확도={100f * correctCount / totalCount:F1}%", this);
        EndEpisode();
    }

    private void ClearTrainingParcel()
    {
        if (currentParcel == null) return;
        currentParcel.gameObject.SetActive(false);
        Destroy(currentParcel.gameObject);
        currentParcel = null;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        if (actionsOut.DiscreteActions.Length < 1) return;
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
        var output = actionsOut.DiscreteActions;
        output[0] = selected;
    }

    [ContextMenu("Print Confusion Table")]
    private void PrintConfusionTable()
    {
        Debug.Log("[Parcel] 행=정답, 열=예측 (General, Food, Fragile)\n" +
            $"General: {confusionTable[0, 0]}, {confusionTable[0, 1]}, {confusionTable[0, 2]}\n" +
            $"Food: {confusionTable[1, 0]}, {confusionTable[1, 1]}, {confusionTable[1, 2]}\n" +
            $"Fragile: {confusionTable[2, 0]}, {confusionTable[2, 1]}, {confusionTable[2, 2]}", this);
    }
}
