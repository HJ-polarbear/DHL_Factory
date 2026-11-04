using UnityEngine;

public class ParcelSpawner : MonoBehaviour
{
    [Header("생성할 택배 프리팹")]
    [Tooltip("랜덤으로 생성될 택배 박스 프리팹들의 배열입니다.")]
    public GameObject[] parcelPrefabs;

    [Header("생성 설정")]
    [Tooltip("체크하면 지정된 시간 간격마다 박스를 계속해서 생성합니다.")]
    public bool continuousSpawn = true;

    [Tooltip("박스가 생성되는 주기 (초 단위)")]
    [Min(0.1f)]
    public float spawnInterval = 3f;

    [Header("박스 크기 설정")]
    [Tooltip("체크하면 프리팹이 가진 원래 크기(Scale)를 그대로 반영합니다. 해제하면 아래 spawnScale 크기로 강제 조절됩니다.")]
    public bool useOriginalPrefabScale = false;

    [Tooltip("박스가 생성될 때 적용할 크기 (useOriginalPrefabScale이 꺼져 있을 때 적용)")]
    public Vector3 spawnScale = new Vector3(0.5f, 0.5f, 0.5f);

    [Tooltip("체크하면 게임이 시작되자마자 첫 번째 박스를 즉시 생성합니다.")]
    public bool spawnImmediately = false;

    [Header("겹침 방지 (스폰 위치 검사) 설정")]
    [Tooltip("스폰 위치에 다른 박스가 있는지 감지할 체크 박스의 크기입니다. 이 값을 조절하여 겹침 방지 범위를 변경할 수 있습니다.")]
    public Vector3 checkBoxSize = new Vector3(0.5f, 0.5f, 0.5f);

    [Tooltip("겹침을 감지할 대상 레이어 (Parcel 레이어 지정)")]
    public LayerMask parcelLayer;

    private float timer;
    private bool paused;
    private bool singleSpawnCompleted;

    private void Start()
    {
        timer = 0f;
        singleSpawnCompleted = false;

        // 1단계: 등록된 프리팹이 유효한지 검사
        if (!HasValidPrefab())
        {
            Debug.LogError("[ParcelSpawner] Parcel Prefabs에 박스 원본을 연결하세요.", this);
            enabled = false;
            return;
        }

        // 2단계: 시작하자마자 생성 옵션이 켜져 있는 경우 첫 박스 생성 시도
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
        // 일시정지 상태이거나, 단발 생성 완료 후 연속 생성이 꺼져 있으면 중지
        if (paused) return;
        if (!continuousSpawn && singleSpawnCompleted) return;

        float interval = GetSpawnInterval();
        timer += Time.deltaTime;

        // 설정된 주기가 될 때까지 대기
        if (timer < interval) return;

        // 3단계: 주기가 되면 박스 생성 시도
        if (TrySpawnParcel())
        {
            timer = 0f;
            singleSpawnCompleted = true;
        }
    }

    // 외부에서 스폰을 일시정지하거나 재개할 때 호출하는 메서드
    public void SetPaused(bool value)
    {
        paused = value;
    }

    // 박스 생성을 시도하는 핵심 메서드
    private bool TrySpawnParcel()
    {
        if (paused) return false;

        // 4단계 [겹침 방지]: Physics.CheckBox를 이용해 스폰 위치에 이미 박스가 있는지 확인
        bool occupied = Physics.CheckBox(
            transform.position,
            GetHalfExtents(),
            transform.rotation,
            parcelLayer,
            QueryTriggerInteraction.Ignore
        );

        // 다른 박스가 감지되어 공간이 차 있다면 생성을 취소하고 대기
        if (occupied) return false;

        // 5단계: 등록된 프리팹 중 하나를 무작위로 선택
        GameObject prefab = GetRandomPrefab();
        if (prefab == null) return false;

        // 6단계: 선택된 프리팹을 스폰 위치와 회전값에 맞춰 생성 (인스턴스화)
        GameObject parcel = Instantiate(
            prefab,
            transform.position,
            transform.rotation
        );

        // 7단계: 박스 크기 반영 설정 (원래 크기 유지 혹은 지정된 크기로 변경)
        if (useOriginalPrefabScale)
        {
            // 프리팹이 가진 원래 로컬 스케일을 그대로 유지
            Debug.Log($"[ParcelSpawner] 택배 생성 (원래 크기 유지): {parcel.name}", parcel);
        }
        else
        {
            // 사용자가 입력한 spawnScale 크기로 강제 조절
            parcel.transform.localScale = spawnScale;
            Debug.Log($"[ParcelSpawner] 택배 생성 (지정된 크기): {parcel.name} (크기: {spawnScale})", parcel);
        }

        parcel.SetActive(true);
        return true;
    }

    // 무작위 프리팹을 골라내는 보조 메서드
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

    // 유효한 프리팹이 있는지 검사하는 메서드
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

    // 겹침 방지 체크 박스의 절반 크기(HalfExtents) 계산
    private Vector3 GetHalfExtents()
    {
        return new Vector3(
            Mathf.Max(0.01f, Mathf.Abs(checkBoxSize.x)) * 0.5f,
            Mathf.Max(0.01f, Mathf.Abs(checkBoxSize.y)) * 0.5f,
            Mathf.Max(0.01f, Mathf.Abs(checkBoxSize.z)) * 0.5f
        );
    }

    // 유니티 에디터 화면에서 겹침 방지 감지 범위를 초록색 상자로 시각화
    private void OnDrawGizmosSelected()
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(Vector3.zero, GetHalfExtents() * 2f);
        Gizmos.matrix = previousMatrix;
    }
}