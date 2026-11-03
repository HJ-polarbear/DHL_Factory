using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DashboardPanelController : MonoBehaviour
{
    [Header("상세 현황 패널")]
    public GameObject detailPanel;

    [Header("상세 현황 버튼")]
    public Button detailButton;

    [Header("버튼 글자")]
    public TMP_Text buttonText;

    private void Start()
    {
        // 게임 시작 시 상세 패널 숨기기
        detailPanel.SetActive(false);

        // 버튼 클릭 이벤트 연결
        detailButton.onClick.AddListener(ToggleDetailPanel);

        UpdateButtonText();
    }

    public void ToggleDetailPanel()
    {
        // 현재 상태를 반대로 변경
        bool isOpen = !detailPanel.activeSelf;

        detailPanel.SetActive(isOpen);

        UpdateButtonText();
    }

    private void UpdateButtonText()
    {
        if (buttonText == null) return;

        if (detailPanel.activeSelf)
        {
            buttonText.text = "상세 현황 ↑";
        }
        else
        {
            buttonText.text = "상세 현황 ↓";
        }
    }
}