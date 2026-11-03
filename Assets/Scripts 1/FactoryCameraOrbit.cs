using System.Collections;
using UnityEngine;

public class FactoryCameraOrbit : MonoBehaviour
{
    [Header("공장 회전 중심")]
    public Transform target;

    [Header("시작 화면")]
    public float initialWait = 5f;

    [Header("반 바퀴 회전 시간")]
    public float firstHalfDuration = 20f;
    public float secondHalfDuration = 20f;

    [Header("반대편 촬영")]
    public float inspectionWait = 3f;

    [Header("로봇 작업 촬영")]
    public float robotWorkWait = 20f;

    [Header("줌인 / 줌아웃 시간")]
    public float zoomInDuration = 5f;
    public float zoomOutDuration = 5f;

    [Header("로봇 줌인 위치")]
    public Vector3 robotZoomPosition;
    public Vector3 robotZoomRotation;

    [Header("카메라 회전 반경")]
    [Range(0.3f, 1f)]
    [Tooltip("1 = 기존 거리, 0.65 = 기존보다 35% 가까운 거리")]
    public float orbitRadiusScale = 0.65f;

    [Header("회전 경로 진입 / 복귀 시간")]
    [Tooltip("시작 화면에서 가까운 회전 경로로 이동하는 시간")]
    public float orbitEntryDuration = 3f;

    [Tooltip("회전 완료 후 원래 시작 화면으로 돌아오는 시간")]
    public float orbitExitDuration = 3f;

    [Header("회전 중 높이 변화")]
    public float heightAmount = 0f;

    private Vector3 startPosition;
    private Quaternion startRotation;

    private Vector3 center;
    private Vector3 startOffset;

    private Vector3 orbitStartPosition;
    private Vector3 oppositePosition;
    private Quaternion oppositeRotation;

    private void Start()
    {
        if (target == null)
        {
            Debug.LogWarning(
                "FactoryCameraOrbit: CameraTarget을 연결해주세요."
            );

            enabled = false;
            return;
        }

        // 현재 Main Camera의 위치와 각도를 시작 화면으로 저장
        startPosition = transform.position;
        startRotation = transform.rotation;

        center = target.position;

        // 현재 카메라와 회전 중심 사이의 거리
        Vector3 originalOffset = startPosition - center;

        if (originalOffset.magnitude < 0.01f)
        {
            Debug.LogWarning(
                "FactoryCameraOrbit: 카메라와 Target이 너무 가깝습니다."
            );

            enabled = false;
            return;
        }

        // 회전 반경을 줄여서 공장에 가까운 경로 생성
        startOffset = originalOffset * orbitRadiusScale;

        // 가까워진 회전 시작 위치
        orbitStartPosition = center + startOffset;

        // 가까워진 회전 경로의 정확한 반대편
        oppositePosition = GetOrbitPosition(180f);

        oppositeRotation = Quaternion.LookRotation(
            center - oppositePosition
        );

        StartCoroutine(CameraRoutine());
    }

    private IEnumerator CameraRoutine()
    {
        // 1. 현재 Main Camera 시작 화면 유지
        yield return new WaitForSeconds(initialWait);

        // 2. 공장에 더 가까운 회전 경로로 이동
        yield return MoveCamera(
            startPosition,
            startRotation,
            orbitStartPosition,
            startRotation,
            orbitEntryDuration
        );

        // 3. 가까운 거리에서 반 바퀴 회전
        yield return Orbit(
            0f,
            180f,
            firstHalfDuration,
            false
        );

        // 4. 반대편에서 잠시 정지
        transform.SetPositionAndRotation(
            oppositePosition,
            oppositeRotation
        );

        yield return new WaitForSeconds(inspectionWait);

        // 5. 로봇팔 두 대 줌인
        yield return MoveCamera(
            oppositePosition,
            oppositeRotation,
            robotZoomPosition,
            Quaternion.Euler(robotZoomRotation),
            zoomInDuration
        );

        // 6. 로봇 분류 작업 확대 촬영
        yield return new WaitForSeconds(robotWorkWait);

        // 7. 반대편 회전 위치로 줌아웃
        yield return MoveCamera(
            transform.position,
            transform.rotation,
            oppositePosition,
            oppositeRotation,
            zoomOutDuration
        );

        // 8. 가까운 거리에서 나머지 반 바퀴 회전
        yield return Orbit(
            180f,
            360f,
            secondHalfDuration,
            true
        );

        // 9. 원래 Main Camera 시작 화면으로 복귀
        yield return MoveCamera(
            transform.position,
            transform.rotation,
            startPosition,
            startRotation,
            orbitExitDuration
        );

        // 10. 처음 위치에서 완전히 정지
        transform.SetPositionAndRotation(
            startPosition,
            startRotation
        );
    }

    // 회전 각도에 따른 카메라 위치 계산
    private Vector3 GetOrbitPosition(float angle)
    {
        Vector3 rotatedOffset =
            Quaternion.Euler(0f, angle, 0f) * startOffset;

        return center + rotatedOffset;
    }

    // 공장 중심을 기준으로 반 바퀴 회전
    private IEnumerator Orbit(
        float fromAngle,
        float toAngle,
        float duration,
        bool returnToOriginalRotation
    )
    {
        if (duration <= 0f)
        {
            Vector3 instantPosition = GetOrbitPosition(toAngle);

            Quaternion instantRotation = returnToOriginalRotation
                ? startRotation
                : Quaternion.LookRotation(center - instantPosition);

            transform.SetPositionAndRotation(
                instantPosition,
                instantRotation
            );

            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsed / duration
            );

            // 일정한 회전 속도
            float angle = Mathf.Lerp(
                fromAngle,
                toAngle,
                progress
            );

            Vector3 orbitPosition = GetOrbitPosition(angle);

            float heightOffset =
                Mathf.Sin(progress * Mathf.PI) * heightAmount;

            transform.position =
                orbitPosition + Vector3.up * heightOffset;

            Quaternion lookRotation = Quaternion.LookRotation(
                center - transform.position
            );

            if (returnToOriginalRotation)
            {
                // 마지막 회전에서 원래 카메라 각도로 복귀
                float blend = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(0.8f, 1f, progress)
                );

                transform.rotation = Quaternion.Slerp(
                    lookRotation,
                    startRotation,
                    blend
                );
            }
            else
            {
                // 첫 번째 회전에서 자연스럽게 공장 중심 바라보기
                float blend = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(0f, 0.2f, progress)
                );

                transform.rotation = Quaternion.Slerp(
                    startRotation,
                    lookRotation,
                    blend
                );
            }

            yield return null;
        }

        Vector3 finalPosition = GetOrbitPosition(toAngle);

        Quaternion finalRotation = returnToOriginalRotation
            ? startRotation
            : Quaternion.LookRotation(center - finalPosition);

        transform.SetPositionAndRotation(
            finalPosition,
            finalRotation
        );
    }

    // 카메라를 부드럽게 이동
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

            float progress = Mathf.Clamp01(
                elapsed / duration
            );

            float smooth = Mathf.SmoothStep(
                0f,
                1f,
                progress
            );

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