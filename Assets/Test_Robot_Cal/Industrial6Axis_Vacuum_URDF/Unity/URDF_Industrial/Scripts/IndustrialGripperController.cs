using UnityEngine;

/// <summary>
/// industrial_6axis_gripper.urdf 의 평행 그리퍼 개폐.
/// 로봇 루트(industrial_6axis_gripper)에 붙이면 손가락 ArticulationBody를 이름으로 자동 탐색.
/// opening = 두 손가락 안쪽 면 사이 간격 [m], 0 ~ 0.314 (모델 기준 최대)
/// </summary>
public class IndustrialGripperController : MonoBehaviour
{
    public const float MaxOpening = 0.3147f;

    [Range(0f, MaxOpening)] public float opening = 0.30f;
    public float stiffness = 20000f;
    public float damping = 2000f;
    [Tooltip("손가락 하나가 낼 수 있는 최대 힘 [N]. 물체를 잡으면 이 힘으로 누르고 멈춤")]
    public float forceLimit = 200f;

    ArticulationBody left, right;

    void Start()
    {
        foreach (var ab in GetComponentsInChildren<ArticulationBody>())
        {
            if (ab.name == "gripper_left_finger_link") left = ab;
            if (ab.name == "gripper_right_finger_link") right = ab;
        }
        if (!left || !right)
            Debug.LogError("[Gripper] 손가락 링크 없음 - industrial_6axis_gripper_unity.urdf로 임포트했는지 확인");
    }

    void FixedUpdate()
    {
        float q = Mathf.Clamp(opening, 0f, MaxOpening) * 0.5f;   // 손가락 하나 이동량 (URDF 조인트값)
        Drive(left, q);
        Drive(right, q);   // Unity Importer는 mimic을 무시하므로 두 손가락을 직접 같은 값으로 구동
    }

    void Drive(ArticulationBody ab, float q)
    {
        if (!ab) return;
        var d = ab.xDrive;
        d.stiffness = stiffness; d.damping = damping; d.forceLimit = forceLimit;
        d.target = q;          // prismatic: 단위 m
        ab.xDrive = d;
    }

    public void Open() => opening = MaxOpening;
    public void Close() => opening = 0f;
    public void SetOpening(float meters) => opening = meters;
}
