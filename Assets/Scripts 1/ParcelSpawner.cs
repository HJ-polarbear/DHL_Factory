using UnityEngine;

public class ParcelSpawner : MonoBehaviour
{
    [Header("생성할 택배 프리팹")]
    public GameObject[] parcelPrefabs;

    [Header("생성 설정")]
    [Tooltip("켜면 지정한 간격마다 계속 생성합니다.")]
    public bool continuousSpawn = true;

    [Tooltip("택배 생성 간격 (초)")]
    [Min(0.1f)]
    public float spawnInterval = 3f;

    [Tooltip("생성되는 박스의 실제 크기 (기본값 0.5)")]
    public Vector3 spawnScale = new Vector3(0.5f, 0.5f, 0.5f);

    [Tooltip("켜면 시작하자마자 첫 박스를 생성합니다.")]
    public bool spawnImmediately = false;

    [Header("생성 위치 확인")]
    public Vector3 checkBoxSize = new Vector3(0.5f, 0.5f, 0.5f);
    public LayerMask parcelLayer;

    private float timer;
    private bool paused;
    private bool singleSpawnCompleted;

    private void Start()
    {
        timer = 0f;
        singleSpawnCompleted = false;

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

        // 실제 박스 크기(Scale)를 0.5로 설정
        parcel.transform.localScale = spawnScale;
        parcel.SetActive(true);

        Debug.Log($"[ParcelSpawner] 택배 생성: {parcel.name} (크기: {spawnScale})", parcel);
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