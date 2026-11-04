using UnityEngine;

public class ParcelBeltCounter : MonoBehaviour
{
    public enum ParcelType
    {
        General,
        Fragile,
        Food
    }

    [Header("감지할 택배 종류")]
    public ParcelType parcelType;

    [Header("대시보드 연결")]
    public DashboardStatsController dashboardStats;

    [Header("택배 태그")]
    public string generalTag = "Parcel_Ignore";
    public string fragileTag = "Parcel_Vacuum";
    public string foodTag = "Parcel_Gripper";

    [Header("디버그 설정")]
    public bool showDebugLogs = true;

    private void Start()
    {
        if (dashboardStats == null)
        {
            Debug.LogError(
                $"[벨트 감지] {gameObject.name}: Dashboard Stats가 연결되지 않았습니다!"
            );
        }

        Collider sensorCollider = GetComponent<Collider>();

        if (sensorCollider == null)
        {
            Debug.LogError(
                $"[벨트 감지] {gameObject.name}: Collider가 없습니다!"
            );
        }
        else if (!sensorCollider.isTrigger)
        {
            Debug.LogError(
                $"[벨트 감지] {gameObject.name}: Is Trigger가 꺼져 있습니다!"
            );
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1. 컨베이어 벨트(Banda)나 Untagged 배경 물체는 무시
        if (other.name.Contains("Banda") || other.CompareTag("Untagged"))
        {
            // Rigidbody가 있더라도 이름이나 태그가 벨트인 경우 제외
            if (other.attachedRigidbody == null ||
                other.attachedRigidbody.name.Contains("Banda") ||
                other.attachedRigidbody.CompareTag("Untagged"))
            {
                return;
            }
        }

        Rigidbody parcel = other.attachedRigidbody;

        // 2. 물리 상자가 아니면 무시
        if (parcel == null)
        {
            return;
        }

        // 3. 대상이 지정된 택배 태그 중 하나라도 가지고 있는지 확인 (택배가 아니면 무시)
        if (!IsAnyParcelTag(other, parcel))
        {
            return;
        }

        if (showDebugLogs)
        {
            Debug.Log(
                $"[벨트 감지] 센서 진입! " +
                $"센서: {gameObject.name}, " +
                $"감지 오브젝트: {other.gameObject.name}, " +
                $"Tag: {other.tag}"
            );
            Debug.Log(
                $"[벨트 감지] Rigidbody 확인: " +
                $"{parcel.name}, Tag: {parcel.tag}"
            );
        }

        if (dashboardStats == null)
        {
            Debug.LogError(
                "[벨트 감지] Dashboard Stats 연결이 없습니다!"
            );
            return;
        }

        // 4. 이 센서가 담당하는 종류의 택배가 맞는지 검사
        if (!IsCorrectParcel(other, parcel))
        {
            if (showDebugLogs)
            {
                Debug.LogWarning(
                    $"[벨트 감지] 태그 불일치! " +
                    $"센서 종류: {parcelType}, " +
                    $"오브젝트 태그: {other.tag}, " +
                    $"Rigidbody 태그: {parcel.tag}"
                );
            }
            return;
        }

        // 5. 대시보드 통계 등록
        DashboardStatsController.ParcelType dashboardType;

        switch (parcelType)
        {
            case ParcelType.General:
                dashboardType = DashboardStatsController.ParcelType.General;
                break;

            case ParcelType.Fragile:
                dashboardType = DashboardStatsController.ParcelType.Fragile;
                break;

            case ParcelType.Food:
                dashboardType = DashboardStatsController.ParcelType.Food;
                break;

            default:
                return;
        }

        bool counted = dashboardStats.RegisterParcel(
            parcel,
            dashboardType
        );

        if (counted)
        {
            Debug.Log(
                $"[벨트 감지] {parcelType} 택배 분류 완료: {parcel.name}"
            );
        }
        else if (showDebugLogs)
        {
            Debug.LogWarning(
                $"[벨트 감지] 카운트 실패 또는 중복 집계: {parcel.name}"
            );
        }
    }

    private bool IsAnyParcelTag(Collider other, Rigidbody parcel)
    {
        return HasTag(other, parcel, generalTag) ||
               HasTag(other, parcel, fragileTag) ||
               HasTag(other, parcel, foodTag);
    }

    private bool IsCorrectParcel(Collider other, Rigidbody parcel)
    {
        string expectedTag = "";

        switch (parcelType)
        {
            case ParcelType.General:
                expectedTag = generalTag;
                break;

            case ParcelType.Fragile:
                expectedTag = fragileTag;
                break;

            case ParcelType.Food:
                expectedTag = foodTag;
                break;
        }

        if (string.IsNullOrEmpty(expectedTag))
            return false;

        return HasTag(other, parcel, expectedTag);
    }

    private bool HasTag(Collider other, Rigidbody parcel, string tag)
    {
        if (string.IsNullOrEmpty(tag)) return false;
        return (parcel != null && parcel.CompareTag(tag)) || other.CompareTag(tag);
    }
}