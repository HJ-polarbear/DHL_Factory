using UnityEngine;

public class ParcelSpawner : MonoBehaviour
{
    [Header("생성할 택배 프리팹")]
    public GameObject[] parcelPrefabs;

    private void Start()
    {
        // 시작할 때 첫 번째 택배 생성
        SpawnNextParcel();
    }

    public void SpawnNextParcel()
    {
        if (parcelPrefabs == null || parcelPrefabs.Length == 0)
        {
            Debug.LogWarning("택배 프리팹을 연결해주세요.");
            return;
        }

        // 연결된 프리팹 중 하나를 무작위로 선택
        int randomIndex = Random.Range(0, parcelPrefabs.Length);
        GameObject prefab = parcelPrefabs[randomIndex];

        if (prefab == null)
        {
            Debug.LogWarning("택배 프리팹이 비어 있습니다.");
            return;
        }

        Instantiate(prefab, transform.position, transform.rotation);
        Debug.Log($"택배 생성: {prefab.name}");
    }
}