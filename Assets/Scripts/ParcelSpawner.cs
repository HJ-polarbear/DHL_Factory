using System.Collections.Generic;
using UnityEngine;

public class ParcelSpawner : MonoBehaviour
{
    [Header("생성할 택배 프리팹")]
    public GameObject[] parcelPrefabs;

    [Header("생성 간격")]
    public float spawnInterval = 3f;

    [Header("생성 위치 확인")]
    public Vector3 checkBoxSize = new Vector3(0.7f, 0.7f, 0.7f);
    public LayerMask parcelLayer;

    private readonly List<GameObject> shuffledPrefabs =
        new List<GameObject>();

    private int nextIndex;
    private float timer;
    private bool paused;

    private void Start()
    {
        RefillAndShuffle();

        // 시작하자마자 첫 번째 박스 생성 시도
        TrySpawnParcel();
    }

    private void Update()
    {
        if (paused || shuffledPrefabs.Count == 0) return;

        timer += Time.deltaTime;

        if (timer < spawnInterval) return;

        // 생성에 성공했을 때만 다음 간격을 다시 셈
        if (TrySpawnParcel())
        {
            timer = 0f;
        }
    }

    public void SetPaused(bool value)
    {
        paused = value;
    }

    private bool TrySpawnParcel()
    {
        if (shuffledPrefabs.Count == 0) return false;

        // 생성 위치에 다른 박스가 있으면 생성하지 않음
        bool occupied = Physics.CheckBox(
            transform.position,
            checkBoxSize * 0.5f,
            transform.rotation,
            parcelLayer,
            QueryTriggerInteraction.Ignore
        );

        if (occupied) return false;

        if (nextIndex >= shuffledPrefabs.Count)
        {
            RefillAndShuffle();
        }

        GameObject prefab = shuffledPrefabs[nextIndex];

        Instantiate(prefab, transform.position, transform.rotation);
        nextIndex++;

        Debug.Log($"택배 생성: {prefab.name}");
        return true;
    }

    private void RefillAndShuffle()
    {
        shuffledPrefabs.Clear();

        if (parcelPrefabs == null) return;

        foreach (GameObject prefab in parcelPrefabs)
        {
            if (prefab != null)
            {
                shuffledPrefabs.Add(prefab);
            }
        }

        // 세 종류를 무작위 순서로 섞음
        for (int i = shuffledPrefabs.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);

            GameObject temp = shuffledPrefabs[i];
            shuffledPrefabs[i] = shuffledPrefabs[randomIndex];
            shuffledPrefabs[randomIndex] = temp;
        }

        nextIndex = 0;
    }
}