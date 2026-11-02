using System.Collections;
using UnityEngine;

public class FactoryCameraOrbit : MonoBehaviour
{
    [Header("검사대 중심")]
    public Transform target;

    [Header("대기 시간")]
    public float initialWait = 5f;
    public float inspectionWait = 5f;
    public float roundWait = 3f;

    [Header("회전 각도")]
    [Tooltip("첫 번째 검사대 촬영 각도")]
    public float firstInspectionAngle = 90f;

    [Header("회전 시간")]
    [Tooltip("시작 위치에서 첫 번째 촬영 위치까지 걸리는 시간")]
    public float firstOrbitDuration = 15f;

    [Tooltip("첫 번째 촬영 위치에서 반대쪽까지 걸리는 시간")]
    public float halfOrbitDuration = 30f;

    [Tooltip("두 번째 촬영 위치에서 시작 위치까지 걸리는 시간")]
    public float returnOrbitDuration = 15f;

    [Header("줌 설정")]
    [Tooltip("카메라가 검사대 쪽으로 가까워지는 거리")]
    public float zoomDistance = 10f;

    [Tooltip("줌인할 때 카메라를 추가로 올리는 높이")]
    public float zoomHeight = 4f;

    public float zoomInDuration = 8f;
    public float zoomOutDuration = 8f;

    [Header("회전 중 위아래 움직임")]
    public float heightAmount = 2f;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private Vector3 startOffset;
    private Vector3 center;

    private void Start()
    {
        if (target == null)
        {
            Debug.LogWarning(
                "FactoryCameraOrbit: Target에 CameraTarget을 연결해주세요."
            );

            enabled = false;
            return;
        }

        startPosition = transform.position;
        startRotation = transform.rotation;
        center = target.position;
        startOffset = startPosition - center;

        if (startOffset.magnitude < 0.01f)
        {
            Debug.LogWarning(
                "FactoryCameraOrbit: 카메라와 Target 위치가 너무 가깝습니다."
            );

            enabled = false;
            return;
        }

        StartCoroutine(CameraRoutine());
    }

    private IEnumerator CameraRoutine()
    {
        // 시작 화면을 5초간 그대로 유지
        yield return new WaitForSeconds(initialWait);

        while (true)
        {
            float secondInspectionAngle = firstInspectionAngle + 180f;

            // 시작 위치에서 첫 번째 촬영 위치까지 회전
            yield return Orbit(
                0f,
                firstInspectionAngle,
                firstOrbitDuration,
                true,
                false
            );

            // 첫 번째 위치에서 줌인 후 대기
            yield return Zoom(firstInspectionAngle, true);
            yield return new WaitForSeconds(inspectionWait);
            yield return Zoom(firstInspectionAngle, false);

            // 반대편까지 반 바퀴 회전
            yield return Orbit(
                firstInspectionAngle,
                secondInspectionAngle,
                halfOrbitDuration,
                false,
                false
            );

            // 반대편에서도 줌인 후 대기
            yield return Zoom(secondInspectionAngle, true);
            yield return new WaitForSeconds(inspectionWait);
            yield return Zoom(secondInspectionAngle, false);

            // 처음 위치로 돌아옴
            yield return Orbit(
                secondInspectionAngle,
                360f,
                returnOrbitDuration,
                false,
                true
            );

            transform.SetPositionAndRotation(
                startPosition,
                startRotation
            );

            yield return new WaitForSeconds(roundWait);
        }
    }

    private IEnumerator Orbit(
        float fromAngle,
        float toAngle,
        float duration,
        bool startFromOriginalRotation,
        bool returnToOriginalRotation
    )
    {
        float elapsed = 0f;
        float moveDuration = Mathf.Max(duration, 0.01f);

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsed / moveDuration);
            float smooth = Mathf.SmoothStep(0f, 1f, progress);

            float angle = Mathf.Lerp(fromAngle, toAngle, smooth);

            Vector3 offset =
                Quaternion.Euler(0f, angle, 0f) * startOffset;

            // 각 회전 구간의 시작과 끝에서는 높이 변화가 0
            float heightOffset =
                Mathf.Sin(progress * Mathf.PI) * heightAmount;

            transform.position =
                center + offset + Vector3.up * heightOffset;

            Quaternion lookRotation = Quaternion.LookRotation(
                center - transform.position
            );

            if (startFromOriginalRotation)
            {
                transform.rotation = Quaternion.Slerp(
                    startRotation,
                    lookRotation,
                    smooth
                );
            }
            else if (returnToOriginalRotation)
            {
                float returnBlend = Mathf.InverseLerp(
                    0.8f,
                    1f,
                    progress
                );

                returnBlend = Mathf.SmoothStep(
                    0f,
                    1f,
                    returnBlend
                );

                transform.rotation = Quaternion.Slerp(
                    lookRotation,
                    startRotation,
                    returnBlend
                );
            }
            else
            {
                transform.rotation = lookRotation;
            }

            yield return null;
        }

        Vector3 finalOffset =
            Quaternion.Euler(0f, toAngle, 0f) * startOffset;

        Vector3 finalPosition = center + finalOffset;

        Quaternion finalRotation = returnToOriginalRotation
            ? startRotation
            : Quaternion.LookRotation(center - finalPosition);

        transform.SetPositionAndRotation(
            finalPosition,
            finalRotation
        );
    }

    private IEnumerator Zoom(float angle, bool zoomIn)
    {
        Vector3 wideOffset =
            Quaternion.Euler(0f, angle, 0f) * startOffset;

        float wideDistance = wideOffset.magnitude;

        float closeDistance = Mathf.Clamp(
            wideDistance - zoomDistance,
            0.5f,
            wideDistance
        );

        Vector3 widePosition = center + wideOffset;

        Vector3 closePosition =
            center
            + wideOffset.normalized * closeDistance
            + Vector3.up * zoomHeight;

        Quaternion wideRotation = Quaternion.LookRotation(
            center - widePosition
        );

        Quaternion closeRotation = Quaternion.LookRotation(
            center - closePosition
        );

        Vector3 fromPosition = zoomIn
            ? widePosition
            : closePosition;

        Vector3 toPosition = zoomIn
            ? closePosition
            : widePosition;

        Quaternion fromRotation = zoomIn
            ? wideRotation
            : closeRotation;

        Quaternion toRotation = zoomIn
            ? closeRotation
            : wideRotation;

        float duration = zoomIn
            ? zoomInDuration
            : zoomOutDuration;

        yield return MoveCamera(
            fromPosition,
            fromRotation,
            toPosition,
            toRotation,
            duration
        );
    }

    private IEnumerator MoveCamera(
        Vector3 fromPosition,
        Quaternion fromRotation,
        Vector3 toPosition,
        Quaternion toRotation,
        float duration
    )
    {
        if (duration <= 0f)
        {
            transform.SetPositionAndRotation(
                toPosition,
                toRotation
            );

            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsed / duration);
            float smooth = Mathf.SmoothStep(0f, 1f, progress);

            transform.position = Vector3.Lerp(
                fromPosition,
                toPosition,
                smooth
            );

            transform.rotation = Quaternion.Slerp(
                fromRotation,
                toRotation,
                smooth
            );

            yield return null;
        }

        transform.SetPositionAndRotation(
            toPosition,
            toRotation
        );
    }
}