using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class FactoryProcessController : MonoBehaviour
{
    [Header("공정 시작 / 일시정지")]
    public Button startPauseButton;
    public TMP_Text startPauseButtonText;

    [Header("초기화")]
    public Button resetButton;
    public TMP_Text resetButtonText;

    [Header("오류 메시지")]
    public TMP_Text errorMessageText;

    [Header("대시보드 연결")]
    public DashboardStatsController dashboardStats;

    [Header("OEE 연결")]
    public FactoryOEEController factoryOEE;

    [Header("시작 설정")]
    public bool startPaused = true;

    [Header("버튼 색상")]
    public Color startColor = new Color32(22, 163, 74, 255);
    public Color pauseColor = new Color32(245, 158, 11, 255);
    public Color resetColor = new Color32(71, 85, 105, 255);

    [Header("오류 색상")]
    public Color normalColor = new Color32(34, 197, 94, 255);
    public Color errorColor = new Color32(239, 68, 68, 255);

    private bool isRunning;

    private void Awake()
    {
        isRunning = !startPaused;
        Time.timeScale = isRunning ? 1f : 0f;
    }

    private void Start()
    {
        if (startPauseButton != null)
            startPauseButton.onClick.AddListener(ToggleProcess);

        if (resetButton != null)
            resetButton.onClick.AddListener(ResetProcess);

        UpdateButtonUI();
        SetErrorMessage("✓ 오류 없음", normalColor);
        UpdateOEEState();

        if (dashboardStats != null)
            dashboardStats.OnParcelProcessed += HandleParcelProcessed;
    }

    private void OnDestroy()
    {
        if (startPauseButton != null)
            startPauseButton.onClick.RemoveListener(ToggleProcess);

        if (resetButton != null)
            resetButton.onClick.RemoveListener(ResetProcess);

        if (dashboardStats != null)
            dashboardStats.OnParcelProcessed -= HandleParcelProcessed;

        Time.timeScale = 1f;
    }

    // ======================================
    // 공정 시작 / 일시정지
    // ======================================

    public void ToggleProcess()
    {
        if (isRunning)
            PauseProcess();
        else
            StartProcess();
    }

    public void StartProcess()
    {
        isRunning = true;
        Time.timeScale = 1f;

        UpdateButtonUI();
        UpdateOEEState();

        Debug.Log("[공정 제어] 공정 시작");
    }

    public void PauseProcess()
    {
        isRunning = false;
        Time.timeScale = 0f;

        UpdateButtonUI();
        UpdateOEEState();

        Debug.Log("[공정 제어] 공정 일시정지");
    }

    // ======================================
    // 초기화
    // ======================================

    public void ResetProcess()
    {
        Debug.Log("[공정 제어] 공정 초기화");

        Time.timeScale = 1f;

        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }

    // ======================================
    // 버튼 글자 및 색상
    // ======================================

    private void UpdateButtonUI()
    {
        if (startPauseButtonText != null)
        {
            startPauseButtonText.text =
                isRunning ? "Ⅱ 일시정지" : "▶ 공정 시작";
        }

        if (startPauseButton != null)
        {
            Image image = startPauseButton.GetComponent<Image>();

            if (image != null)
                image.color = isRunning ? pauseColor : startColor;
        }

        if (resetButtonText != null)
        {
            resetButtonText.text = "↻ 초기화";
        }

        if (resetButton != null)
        {
            Image image = resetButton.GetComponent<Image>();

            if (image != null)
                image.color = resetColor;
        }
    }

    // ======================================
    // OEE 연동
    // ======================================

    private void UpdateOEEState()
    {
        if (factoryOEE != null)
            factoryOEE.SetDowntime(!isRunning);
    }

    // ======================================
    // 분류 실패 감지
    // ======================================

    private void HandleParcelProcessed(
        DashboardStatsController.ParcelType type,
        bool success,
        DateTime processedTime
    )
    {
        if (success)
            return;

        string parcelName = "";

        switch (type)
        {
            case DashboardStatsController.ParcelType.General:
                parcelName = "일반";
                break;

            case DashboardStatsController.ParcelType.Fragile:
                parcelName = "파손주의";
                break;

            case DashboardStatsController.ParcelType.Food:
                parcelName = "식품";
                break;
        }

        string message =
            $"! 분류 실패: {parcelName} 택배";

        SetErrorMessage(message, errorColor);

        Debug.LogWarning(
            $"[공정 오류] {parcelName} 택배 분류 실패 " +
            $"({processedTime:HH:mm:ss})"
        );
    }

    // ======================================
    // 오류 메시지
    // ======================================

    private void SetErrorMessage(string message, Color color)
    {
        if (errorMessageText == null)
            return;

        errorMessageText.text = message;
        errorMessageText.color = color;
    }
}