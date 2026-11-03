using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DashboardRecentHistoryController : MonoBehaviour
{
    [Serializable]
    public class HistoryRow
    {
        public TMP_Text timeText;
        public TMP_Text categoryText;
        public TMP_Text resultText;
        public Image categoryIcon;
    }

    private class HistoryRecord
    {
        public string time;
        public DashboardStatsController.ParcelType type;
        public bool success;
    }

    [Header("대시보드 연결")]
    public DashboardStatsController dashboardStats;

    [Header("최근 처리 내역 UI")]
    public HistoryRow historyRow1;
    public HistoryRow historyRow2;
    public HistoryRow historyRow3;

    [Header("택배 종류별 아이콘")]
    public Sprite generalIcon;
    public Sprite fragileIcon;
    public Sprite foodIcon;

    [Header("카테고리 색상")]
    public Color generalColor = new Color32(96, 165, 250, 255);
    public Color fragileColor = new Color32(239, 68, 68, 255);
    public Color foodColor = new Color32(34, 211, 238, 255);

    [Header("결과 색상")]
    public Color successColor = new Color32(34, 197, 94, 255);
    public Color failureColor = new Color32(239, 68, 68, 255);

    private readonly List<HistoryRecord> records =
        new List<HistoryRecord>();

    private void OnEnable()
    {
        if (dashboardStats != null)
        {
            dashboardStats.OnParcelProcessed += AddHistory;
        }

        RefreshHistory();
    }

    private void OnDisable()
    {
        if (dashboardStats != null)
        {
            dashboardStats.OnParcelProcessed -= AddHistory;
        }
    }

    // 새로운 처리 내역
    private void AddHistory(
        DashboardStatsController.ParcelType type,
        bool success,
        DateTime processedTime
    )
    {
        HistoryRecord newRecord = new HistoryRecord
        {
            time = processedTime.ToString("HH:mm:ss"),
            type = type,
            success = success
        };

        records.Insert(0, newRecord);

        // 최근 3건만 유지
        if (records.Count > 3)
        {
            records.RemoveAt(records.Count - 1);
        }

        RefreshHistory();
    }

    // 화면 갱신
    private void RefreshHistory()
    {
        UpdateRow(historyRow1, 0);
        UpdateRow(historyRow2, 1);
        UpdateRow(historyRow3, 2);
    }

    private void UpdateRow(HistoryRow row, int index)
    {
        if (row == null)
            return;

        if (index >= records.Count)
        {
            if (row.timeText != null)
                row.timeText.text = "--:--:--";

            if (row.categoryText != null)
                row.categoryText.text = "-";

            if (row.resultText != null)
                row.resultText.text = "-";

            if (row.categoryIcon != null)
                row.categoryIcon.enabled = false;

            return;
        }

        HistoryRecord record = records[index];

        string categoryName = "";
        Color categoryColor = Color.white;
        Sprite categorySprite = null;

        switch (record.type)
        {
            case DashboardStatsController.ParcelType.General:
                categoryName = "일반";
                categoryColor = generalColor;
                categorySprite = generalIcon;
                break;

            case DashboardStatsController.ParcelType.Fragile:
                categoryName = "파손주의";
                categoryColor = fragileColor;
                categorySprite = fragileIcon;
                break;

            case DashboardStatsController.ParcelType.Food:
                categoryName = "식품";
                categoryColor = foodColor;
                categorySprite = foodIcon;
                break;
        }

        // 처리 시간
        if (row.timeText != null)
        {
            row.timeText.text = record.time;
        }

        // 택배 종류
        if (row.categoryText != null)
        {
            row.categoryText.text = categoryName;
            row.categoryText.color = categoryColor;
        }

        // 성공 / 실패
        if (row.resultText != null)
        {
            // 폰트 호환을 위해 특수 X 대신 일반 X 사용
            row.resultText.text =
                record.success ? "✓ 성공" : "X 실패";

            row.resultText.color =
                record.success ? successColor : failureColor;
        }

        // 종류별 아이콘 변경
        if (row.categoryIcon != null)
        {
            if (categorySprite != null)
            {
                row.categoryIcon.sprite = categorySprite;
                row.categoryIcon.enabled = true;
                row.categoryIcon.color = categoryColor;
            }
            else
            {
                // 아이콘이 연결되지 않았으면 숨김
                row.categoryIcon.enabled = false;
            }
        }
    }
}