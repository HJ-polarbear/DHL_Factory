using UnityEngine;

/// <summary>
/// 컨베이어 시작 위치에 택배를 일정 간격으로 생성한다.
///
/// [변경]
/// - 박스 크기를 덮어쓰지 않는다. 프리팹에 저장된 크기를 그대로 사용한다.
///   (AI 학습 때 에이전트가 프리팹 크기 그대로 박스를 만들었기 때문에, 공정에서도 같아야 함)
///   → Parcel Prefabs에는 에이전트 학습용과 같은 Assets/Prefabs 프리팹을 연결할 것
/// - 생성 순서 번호를 이름에 붙인다. (예: FoodBox_007)
///   생성 로그와 검사·AI 판정 로그에서 같은 박스인지 바로 확인할 수 있다.
/// </summary>
public class ParcelSpawner : MonoBehaviour
{
    [Header("생성할 택배 프리팹 (에이전트 학습용과 같은 Assets/Prefabs 프리팹)")]
    public GameObject[] parcelPrefabs;

    [Header("생성 설정")]
    [Tooltip("켜면 지정한 간격마다 계속 생성합니다.")]
    public bool continuousSpawn = true;

    [Tooltip("택배 생성 간격 (초)")]
    [Min(0.1f)]
    public float spawnInterval = 3f;

    [Tooltip("켜면 시작하자마자 첫 박스를 생성합니다.")]
    public bool spawnImmediately = false;

    [Header("생성 위치 확인")]
    [Tooltip("생성 위치에 이미 박스가 있으면 생성하지 않습니다.")]
    public Vector3 checkBoxSize = new Vector3(0.5f, 0.5f, 0.5f);
    public LayerMask parcelLayer;

    private float timer;
    private bool paused;
    private bool singleSpawnCompleted;
    private int spawnCount;

    private void Start()
    {
        timer = 0f;
        singleSpawnCompleted = false;
        spawnCount = 0;

        if (!HasValidPrefab())
        {
            Debug.LogError("[ParcelSpawner] Parcel Prefabs에 박스 원본을 연결하세요.", this);
            enabled = false;
            return;
        }

        if (spawnImmediately)
        {
            if (TrySpawnParcel())
            {
                singleSpawnCompleted = true;
            }
            else
            {
                timer = GetSpawnInterval();
            }
        }
    }

    private void Update()
    {
        if (paused) return;
        if (!continuousSpawn && singleSpawnCompleted) return;

        float interval = GetSpawnInterval();
        timer += Time.deltaTime;

        if (timer < interval) return;

        if (TrySpawnParcel())
        {
            timer = 0f;
            singleSpawnCompleted = true;
        }
    }

    public void SetPaused(bool value)
    {
        paused = value;
    }

    private bool TrySpawnParcel()
    {
        if (paused) return false;

        // 생성 위치에 이미 박스가 있으면 생성하지 않음
        bool occupied = Physics.CheckBox(
            transform.position,
            GetHalfExtents(),
            transform.rotation,
            parcelLayer,
            QueryTriggerInteraction.Ignore
        );

        if (occupied) return false;

        GameObject prefab = GetRandomPrefab();
        if (prefab == null) return false;

        GameObject parcel = Instantiate(
            prefab,
            transform.position,
            transform.rotation
        );

        // 생성 순서 번호를 붙여 로그 추적을 쉽게 함 (예: FoodBox_007)
        spawnCount++;
        parcel.name = $"{prefab.name}_{spawnCount:D3}";

        // 컨베이어에 처음 올라올 때는 Untagged로 초기화
        // (태그는 AI 판정 후 SortingCoordinator가 붙임)
        parcel.tag = "Untagged";
        foreach (Transform child in parcel.transform)
        {
            child.gameObject.tag = "Untagged";
        }

        // 박스 크기는 프리팹 그대로 사용 (덮어쓰지 않음)
        parcel.SetActive(true);

        Debug.Log(
            $"[ParcelSpawner] 택배 생성: {parcel.name} (크기: {parcel.transform.localScale})",
            parcel
        );
        return true;
    }

    private GameObject GetRandomPrefab()
    {
        if (parcelPrefabs == null) return null;

        int validCount = 0;
        foreach (GameObject prefab in parcelPrefabs)
        {
            if (prefab != null) validCount++;
        }

        if (validCount == 0) return null;

        int selectedIndex = Random.Range(0, validCount);
        foreach (GameObject prefab in parcelPrefabs)
        {
            if (prefab == null) continue;
            if (selectedIndex == 0) return prefab;
            selectedIndex--;
        }

        return null;
    }

    private bool HasValidPrefab()
    {
        if (parcelPrefabs == null) return false;
        foreach (GameObject prefab in parcelPrefabs)
        {
            if (prefab != null) return true;
        }
        return false;
    }

    private float GetSpawnInterval()
    {
        return Mathf.Max(0.1f, spawnInterval);
    }

    private Vector3 GetHalfExtents()
    {
        return new Vector3(
            Mathf.Max(0.01f, Mathf.Abs(checkBoxSize.x)) * 0.5f,
            Mathf.Max(0.01f, Mathf.Abs(checkBoxSize.y)) * 0.5f,
            Mathf.Max(0.01f, Mathf.Abs(checkBoxSize.z)) * 0.5f
        );
    }

    private void OnDrawGizmosSelected()
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(Vector3.zero, GetHalfExtents() * 2f);
        Gizmos.matrix = previousMatrix;
    }
}