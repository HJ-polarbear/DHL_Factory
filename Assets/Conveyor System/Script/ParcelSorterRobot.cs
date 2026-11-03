using System.Collections;
using UnityEngine;

public class ParcelSorterRobot : MonoBehaviour
{
    [Header("수거함 위 투하 기준점")]
    public Transform binGeneral;
    public Transform binFood;
    public Transform binFragile;

    [Header("이동 설정")]
    [Min(0.01f)]
    public float moveSpeed = 1f;

    [Min(0f)]
    public float arcHeight = 0.5f;

    [Tooltip("목적지 기준점에서 추가로 높일 거리")]
    [Min(0f)]
    public float dropHeight = 0.2f;

    [Tooltip("상자를 놓은 뒤 다음 상자로 넘어가기까지 대기")]
    [Min(0f)]
    public float releaseWait = 1f;

    public bool IsBusy { get; private set; }
    public bool LastSortSucceeded { get; private set; }

    public IEnumerator SortParcel(
        GameObject parcel,
        ParcelCategory category
    )
    {
        // 작업이 겹치면 기존 작업이 끝날 때까지 대기
        while (IsBusy)
        {
            yield return null;
        }

        LastSortSucceeded = false;

        if (parcel == null)
        {
            Debug.LogError(
                "[ParcelSorterRobot] 이동할 상자가 없습니다.",
                this
            );
            yield break;
        }

        Transform targetBin;

        switch (category)
        {
            case ParcelCategory.General:
                targetBin = binGeneral;
                break;

            case ParcelCategory.Food:
                targetBin = binFood;
                break;

            case ParcelCategory.Fragile:
                targetBin = binFragile;
                break;

            default:
                Debug.LogError(
                    $"[ParcelSorterRobot] 지원하지 않는 분류: {category}",
                    this
                );
                yield break;
        }

        if (targetBin == null)
        {
            Debug.LogError(
                $"[ParcelSorterRobot] {category} 수거함을 연결하세요.",
                this
            );
            yield break;
        }

        IsBusy = true;

        try
        {
            Rigidbody[] bodies =
                parcel.GetComponentsInChildren<Rigidbody>();

            foreach (Rigidbody body in bodies)
            {
                body.isKinematic = true;
            }

            Vector3 startPosition = parcel.transform.position;

            Vector3 dropPosition =
                targetBin.position + Vector3.up * dropHeight;

            float duration = Mathf.Max(
                0.05f,
                Vector3.Distance(startPosition, dropPosition) /
                Mathf.Max(0.01f, moveSpeed)
            );

            float elapsed = 0f;

            Debug.Log(
                $"[Robot] 예측={category}, 목적지={targetBin.name}",
                this
            );

            while (elapsed < duration)
            {
                if (parcel == null) yield break;

                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / duration);

                Vector3 position = Vector3.Lerp(
                    startPosition,
                    dropPosition,
                    t
                );

                position.y += Mathf.Sin(t * Mathf.PI) * arcHeight;

                parcel.transform.position = position;

                yield return null;
            }

            if (parcel == null) yield break;

            parcel.transform.position = dropPosition;

            foreach (Rigidbody body in bodies)
            {
                if (body != null)
                {
                    body.isKinematic = false;
                }
            }

            yield return new WaitForSeconds(
                Mathf.Max(0f, releaseWait)
            );

            LastSortSucceeded = true;

            Debug.Log(
                $"[Robot] {category} 분류 이동 완료",
                this
            );
        }
        finally
        {
            IsBusy = false;
        }
    }
}