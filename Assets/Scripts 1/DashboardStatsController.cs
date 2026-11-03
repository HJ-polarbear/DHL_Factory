using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class DashboardStatsController : MonoBehaviour
{
    public enum ParcelType
    {
        General,
        Fragile,
        Food
    }

    [Header("대시보드 상단 수량")]
    public TMP_Text totalCountText;
    public TMP_Text generalCountText;
    public TMP_Text fragileCountText;
    public TMP_Text foodCountText;

    [Header("대시보드 성공률")]
    public TMP_Text successText;

    [Header("현재 누적 수량")]
    [SerializeField] private int totalCount = 0;
    [SerializeField] private int generalCount = 0;
    [SerializeField] private int fragileCount = 0;
    [SerializeField] private int foodCount = 0;

    [Header("분류 성공 및 실패")]
    [SerializeField] private int successCount = 0;
    [SerializeField] private int failureCount = 0;

    private readonly HashSet<Rigidbody> processedParcels =
        new HashSet<Rigidbody>();

    // 최근 처리 내역 이벤트
    // 택배 종류, 성공 여부, 처리 시각
    public event Action<ParcelType, bool, DateTime> OnParcelProcessed;

    public int TotalCount => totalCount;
    public int GeneralCount => generalCount;
    public int FragileCount => fragileCount;
    public int FoodCount => foodCount;

    public int SuccessCount => successCount;
    public int FailureCount => failureCount;

    public float SuccessRate
    {
        get
        {
            int completedCount = successCount + failureCount;

            if (completedCount == 0)
                return 0f;

            return (float)successCount / completedCount * 100f;
        }
    }

    private void Start()
    {
        UpdateDashboard();
    }

    // ======================================
    // 분류 성공
    // ======================================

    public bool RegisterParcel(Rigidbody parcel, ParcelType type)
    {
        if (parcel == null)
            return false;

        if (!processedParcels.Add(parcel))
            return false;

        switch (type)
        {
            case ParcelType.General:
                generalCount++;
                break;

            case ParcelType.Fragile:
                fragileCount++;
                break;

            case ParcelType.Food:
                foodCount++;
                break;
        }

        totalCount++;
        successCount++;

        UpdateDashboard();

        // 최근 처리 내역에 전달
        OnParcelProcessed?.Invoke(
            type,
            true,
            DateTime.Now
        );

        Debug.Log(
            $"[대시보드] 분류 성공: {type} | " +
            $"전체 {totalCount}, " +
            $"일반 {generalCount}, " +
            $"파손주의 {fragileCount}, " +
            $"식품 {foodCount}, " +
            $"성공 {successCount}, " +
            $"실패 {failureCount}, " +
            $"성공률 {SuccessRate:F1}%"
        );

        return true;
    }

    // ======================================
    // 분류 실패
    // ======================================

    public bool RegisterFailure(Rigidbody parcel)
    {
        if (parcel == null)
            return false;

        if (!processedParcels.Add(parcel))
            return false;

        totalCount++;
        failureCount++;

        UpdateDashboard();

        ParcelType type = GetParcelType(parcel);

        // 최근 처리 내역에 전달
        OnParcelProcessed?.Invoke(
            type,
            false,
            DateTime.Now
        );

        Debug.Log(
            $"[대시보드] 최종 분류 실패: {parcel.name} | " +
            $"전체 {totalCount}, " +
            $"성공 {successCount}, " +
            $"실패 {failureCount}, " +
            $"성공률 {SuccessRate:F1}%"
        );

        return true;
    }

    // 실패한 택배의 종류 확인
    private ParcelType GetParcelType(Rigidbody parcel)
    {
        if (parcel.CompareTag("Parcel_Vacuum"))
            return ParcelType.Fragile;

        if (parcel.CompareTag("Parcel_Gripper"))
            return ParcelType.Food;

        return ParcelType.General;
    }

    // ======================================
    // 기존 대시보드 업데이트
    // ======================================

    private void UpdateDashboard()
    {
        if (totalCountText != null)
        {
            totalCountText.text =
                $"전체\n {totalCount:D2}";
        }

        if (generalCountText != null)
        {
            generalCountText.text =
                $"일반\n {generalCount:D2}";
        }

        if (fragileCountText != null)
        {
            fragileCountText.text =
                $"파손주의\n   {fragileCount:D2}";
        }

        if (foodCountText != null)
        {
            foodCountText.text =
                $"식품\n {foodCount:D2}";
        }

        if (successText != null)
        {
            int completedCount = successCount + failureCount;

            if (completedCount == 0)
            {
                successText.text = "성공률\n--%";
            }
            else
            {
                successText.text =
                    $"성공률\n{SuccessRate:F1}%";
            }
        }
    }
}