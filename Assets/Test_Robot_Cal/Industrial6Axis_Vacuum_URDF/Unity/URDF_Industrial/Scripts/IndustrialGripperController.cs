using System.Collections;
using UnityEngine;

/// <summary>
/// 2지 그리퍼 제어 (ArticulationBody 손가락).
///
/// [변경 1] 박스 고정 방식을 흡착 그리퍼(VacuumGripper)와 같은 FixedJoint 방식으로 바꿨다.
///   - 박스를 로봇 계층 아래로 SetParent하지 않는다.
///   - 박스를 Kinematic으로 바꾸지 않는다 (동적 Rigidbody + FixedJoint).
///   - 손가락이 닿는 순간 더 조이지 않도록 손가락 목표를 현재 위치에 고정한다.
///
/// [변경 2] Physics.IgnoreCollision을 사용하지 않는다.
///   크래시 로그(PxsSolverStartTask::setupDescTask)는 박스를 잡은 직후 물리 계산 단계에서 발생했다.
///   기존 코드는 잡는 순간 박스와 로봇 관절(ArticulationBody) 콜라이더 전체에 IgnoreCollision을 걸었는데,
///   정상 동작하는 흡착 코드에는 이 처리가 없다. 흡착과 똑같이 맞추기 위해 제거했다.
///   (손가락과 박스는 FixedJoint로 함께 움직이므로 충돌을 무시하지 않아도 된다.)
/// </summary>
public class IndustrialGripperController : MonoBehaviour
{
    public const float MaxOpening = 0.3147f;

    [Header("손가락 ArticulationBody (자동 탐색됨)")]
    public ArticulationBody left;
    public ArticulationBody right;

    [Header("박스를 고정할 그리퍼 본체 링크 (비워두면 손가락의 부모 링크 자동 사용)")]
    public ArticulationBody gripperBody;

    [Header("접촉 센서 (선택 사항)")]
    public FingerContact rightContact;
    public FingerContact leftContact;

    [Header("부품을 붙일 위치 (손가락 사이 정중앙) - 현재는 참고용")]
    public Transform attachPoint;

    [Header("개폐 설정 (m)")]
    [Range(0f, MaxOpening)] public float opening = 0.30f;
    public float stiffness = 20000f;
    public float damping = 2000f;
    public float forceLimit = 200f;
    public float moveTime = 0.5f;
    [Tooltip("목표가 닫힘에 도달한 뒤에도 손가락이 닿기를 기다리는 최대 시간(초)")]
    public float extraCloseTime = 0.5f;

    public Rigidbody HeldPart { get; private set; }
    public bool IsBusy { get; private set; }
    public bool LastGraspOk { get; private set; }

    FixedJoint joint;   // 박스와 그리퍼 본체를 잇는 조인트

    void Start()
    {
        foreach (var ab in GetComponentsInChildren<ArticulationBody>())
        {
            if (ab.name == "gripper_left_finger_link" && left == null) left = ab;
            if (ab.name == "gripper_right_finger_link" && right == null) right = ab;
        }
        if (!left || !right)
            Debug.LogError("[Gripper] 손가락 링크 없음 - industrial_6axis_gripper_unity.urdf로 임포트했는지 확인");

        // 손가락의 부모 링크(그리퍼 본체)를 고정 대상으로 사용
        if (gripperBody == null && left != null && left.transform.parent != null)
            gripperBody = left.transform.parent.GetComponentInParent<ArticulationBody>();
        if (gripperBody == null)
            Debug.LogError("[Gripper] 그리퍼 본체 ArticulationBody를 찾지 못했습니다. Inspector에서 gripperBody를 지정하세요.");

        SetOpening(opening);
    }

    void FixedUpdate()
    {
        // opening 값에 따라 좌우 손가락 대칭 드라이브 갱신
        float q = Mathf.Clamp(opening, 0f, MaxOpening) * 0.5f;
        Drive(left, q);
        Drive(right, q);

        // 과부하로 조인트가 끊어졌으면 잡은 상태 해제
        if (HeldPart != null && joint == null)
        {
            Debug.LogWarning("[Gripper] 조인트가 끊어져 박스를 놓쳤습니다.");
            HeldPart = null;
            LastGraspOk = false;
        }
    }

    void Drive(ArticulationBody ab, float q)
    {
        if (!ab) return;
        var d = ab.xDrive;
        d.stiffness = stiffness;
        d.damping = damping;
        d.forceLimit = forceLimit;
        d.target = q;
        ab.xDrive = d;
    }

    // 닫기 코루틴
    public IEnumerator Close()
    {
        if (IsBusy) yield break;
        IsBusy = true;
        LastGraspOk = false;

        if (left == null || right == null)
        {
            Debug.LogError("[Gripper] left 또는 right ArticulationBody가 지정되지 않았습니다!");
            IsBusy = false;
            yield break;
        }

        float startOpening = opening;
        float targetOpening = 0.0f; // 완전히 닫힘 목표
        float t = 0f;
        float extra = 0f;
        bool touched = false;

        while (t < 1f || extra < extraCloseTime)
        {
            if (t < 1f)
            {
                t += Time.fixedDeltaTime / moveTime;
                opening = Mathf.Lerp(startOpening, targetOpening, t);
            }
            else
            {
                extra += Time.fixedDeltaTime;
            }

            // 널 체크가 포함된 접촉 센서 확인
            Rigidbody rR = (rightContact != null) ? rightContact.TouchingPart : null;
            Rigidbody rL = (leftContact != null) ? leftContact.TouchingPart : null;

            // 양쪽 손가락이 모두 동일한 물체를 잡았을 때
            if (rR != null && rR == rL)
            {
                touched = true;
                // 현재 벌어져 있는 포지션으로 opening 고정 (더 조이지 않음)
                opening = Mathf.Abs(left.jointPosition[0]) + Mathf.Abs(right.jointPosition[0]);
                LastGraspOk = Attach(rR);
                if (LastGraspOk)
                    Debug.Log("[IndustrialGripper] 물체 접촉 감지 완료. 파지 위치 고정 및 닫기 중지.");
                break;
            }

            yield return new WaitForFixedUpdate();
        }

        if (!LastGraspOk)
        {
            Debug.Log(touched ? "[IndustrialGripper] 실패: 고정 실패" : "[IndustrialGripper] 실패: 양쪽 손가락이 동시에 닿지 않음");
        }

        IsBusy = false;
    }

    // 열기 코루틴
    public IEnumerator Open()
    {
        if (IsBusy) yield break;
        IsBusy = true;

        Detach();

        float startOpening = opening;
        float targetOpening = MaxOpening;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.fixedDeltaTime / moveTime;
            opening = Mathf.Lerp(startOpening, targetOpening, t);
            yield return new WaitForFixedUpdate();
        }

        yield return new WaitForSeconds(0.3f);
        IsBusy = false;
    }

    // FixedJoint로 박스를 그리퍼 본체에 고정 (흡착 그리퍼와 같은 방식)
    bool Attach(Rigidbody part)
    {
        if (part == null) return false;
        if (gripperBody == null)
        {
            Debug.LogError("[Gripper] gripperBody가 없어 박스를 고정할 수 없습니다.");
            return false;
        }
        if (joint != null) Destroy(joint);   // 혹시 남아 있는 조인트 정리

        HeldPart = part;

        // 박스는 동적 Rigidbody 그대로 두고 속도만 정지
        part.isKinematic = false;
        part.linearVelocity = Vector3.zero;
        part.angularVelocity = Vector3.zero;

        // 그리퍼 본체와 FixedJoint로 연결 (흡착 그리퍼와 동일)
        joint = part.gameObject.AddComponent<FixedJoint>();
        joint.connectedArticulationBody = gripperBody;
        joint.enableCollision = false;

        Debug.Log("[Gripper] 부품 부착 성공");
        return true;
    }

    void Detach()
    {
        // 조인트만 제거하면 박스는 그대로 떨어짐
        if (joint != null) Destroy(joint);
        joint = null;

        if (HeldPart == null) return;
        HeldPart = null;

        if (rightContact != null) rightContact.Clear();
        if (leftContact != null) leftContact.Clear();

        Debug.Log("[Gripper] 부품 분리 완료");
    }

    public void SetOpening(float meters) => opening = meters;
}

