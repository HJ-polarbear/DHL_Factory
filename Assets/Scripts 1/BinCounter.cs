using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BinCounter : MonoBehaviour
{
    [Header("적재함 설정")]
    public int capacity = 6;
    public float emptyDelay = 1f;

    private readonly HashSet<Rigidbody> parcels = new HashSet<Rigidbody>();
    private bool isEmptying;

    public int Count => parcels.Count;
    public bool IsFull => Count >= capacity;

    private void OnTriggerEnter(Collider other)
    {
        Rigidbody parcel = other.attachedRigidbody;

        // Rigidbody가 있는 박스만 감지
        if (parcel == null) return;

        // 같은 박스를 중복으로 세지 않음
        if (!parcels.Add(parcel)) return;

        Debug.Log($"적재함: {Count}/{capacity}");

        if (IsFull && !isEmptying)
        {
            StartCoroutine(EmptyBin());
        }
    }

    private IEnumerator EmptyBin()
    {
        isEmptying = true;

        Debug.Log($"적재함이 가득 찼습니다. ({Count}/{capacity}) 회수를 시작합니다.");

        // 6개가 쌓인 모습을 잠시 보여주기
        yield return new WaitForSeconds(emptyDelay);

        foreach (Rigidbody parcel in parcels)
        {
            if (parcel != null)
            {
                Destroy(parcel.gameObject);
            }
        }

        parcels.Clear();
        isEmptying = false;

        Debug.Log($"적재함 회수 완료: 0/{capacity}");
    }
}