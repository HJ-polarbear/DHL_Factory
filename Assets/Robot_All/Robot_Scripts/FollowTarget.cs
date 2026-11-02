using UnityEngine;

/// <summary>
/// target 오브젝트가 지정한 큐브 오브젝트를 부드럽게 추종하도록 만드는 스크립트.
/// Industrial6AxisIK가 바라보는 target에 부착하거나, target 자체에 붙여서 사용합니다.
/// </summary>
public class FollowTarget : MonoBehaviour
{
    [Tooltip("따라갈 대상 큐브 오브젝트")]
    public Transform targetCube;

    [Header("추종 설정")]
    [Tooltip("위치 따라가는 속도 (값이 클수록 빠르게 따라감)")]
    public float positionSpeed = 10f;

    [Tooltip("회전 따라가는 속도")]
    public float rotationSpeed = 10f;

    [Tooltip("큐브 기준 위치 오프셋 (예: 큐브 위쪽을 바라보게 하고 싶을 때)")]
    public Vector3 positionOffset = Vector3.zero;

    [Tooltip("회전 오프셋 (필요시 각도 조정)")]
    public Vector3 rotationOffset = Vector3.zero;

    void Update()
    {
        if (targetCube == null) return;

        // 목표 위치 계산 (오프셋 포함)
        Vector3 targetPos = targetCube.position + targetCube.TransformDirection(positionOffset);

        // 목표 회전 계산 (큐브 회전 + 오프셋)
        Quaternion targetRot = targetCube.rotation * Quaternion.Euler(rotationOffset);

        // 부드럽게 이동 및 회전 보간 (Lerp / Slerp)
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * positionSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
    }
}