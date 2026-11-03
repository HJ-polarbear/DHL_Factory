using System;
using System.Collections;
using System.IO;
using UnityEngine;

// [추가 1] 새 Input System을 사용할 때만 포함
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ParcelImageCapture : MonoBehaviour
{
    public enum CaptureLabel
    {
        General,
        Food,
        Fragile
    }

    [Header("검사 카메라에 연결된 RenderTexture")]
    public RenderTexture inspectionTexture;

    [Header("현재 촬영할 택배의 정답 종류")]
    public CaptureLabel captureLabel = CaptureLabel.General;

    private bool isSaving;

    // [추가 2] F9를 누르면 사진 저장
    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            Keyboard.current.f9Key.wasPressedThisFrame)
        {
            SaveCapture();
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.F9))
        {
            SaveCapture();
        }
#endif
    }

    [ContextMenu("Save Capture")]
    public void SaveCapture()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Play 모드에서 실행하세요.");
            return;
        }

        if (inspectionTexture == null)
        {
            Debug.LogError("Inspection Texture를 연결하세요.");
            return;
        }

        if (isSaving)
        {
            return;
        }

        StartCoroutine(SaveCaptureRoutine());
    }

    private IEnumerator SaveCaptureRoutine()
    {
        isSaving = true;

        // 이번 프레임의 렌더링이 끝난 뒤 읽습니다.
        yield return new WaitForEndOfFrame();

        RenderTexture previous = RenderTexture.active;
        Texture2D image = null;

        try
        {
            if (!inspectionTexture.IsCreated())
            {
                Debug.LogError(
                    "RenderTexture가 아직 생성되지 않았습니다."
                );
                yield break;
            }

            RenderTexture.active = inspectionTexture;

            image = new Texture2D(
                inspectionTexture.width,
                inspectionTexture.height,
                TextureFormat.RGB24,
                false
            );

            image.ReadPixels(
                new Rect(
                    0,
                    0,
                    inspectionTexture.width,
                    inspectionTexture.height
                ),
                0,
                0
            );

            image.Apply();

            string folder = Path.Combine(
                Application.persistentDataPath,
                "ParcelDataset",
                captureLabel.ToString()
            );

            Directory.CreateDirectory(folder);

            string fileName =
                $"{captureLabel}_{DateTime.Now:yyyyMMdd_HHmmss_fff}_" +
                $"{Guid.NewGuid():N}.png";

            string filePath = Path.Combine(folder, fileName);

            File.WriteAllBytes(
                filePath,
                image.EncodeToPNG()
            );

            Debug.Log($"이미지 저장 완료:\n{filePath}");
        }
        finally
        {
            RenderTexture.active = previous;

            if (image != null)
            {
                Destroy(image);
            }

            isSaving = false;
        }
    }
}