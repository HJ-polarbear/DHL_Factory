using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IndustrialGripperController : MonoBehaviour
{
    public const float MaxOpening = 0.3147f;

    [Header("손가락 ArticulationBody (자동 탐색됨)")]
    public ArticulationBody left;
    public ArticulationBody right;

    [Header("접촉 센서 (선택 사항)")]
    public FingerContact rightContact;
    public FingerContact leftContact;

    [Header("부품을 붙일 위치 (손가락 사이 정중앙)")]
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

    Collider[] robotColliders;
    readonly List<Collider> ignoredPartColliders = new List<Collider>();

    void Start()
    {
        robotColliders = transform.root.GetComponentsInChildren<Collider>(true);

        foreach (var ab in GetComponentsInChildren<ArticulationBody>())
        {
            if (ab.name == "gripper_left_finger_link" && left == null) left = ab;
            if (ab.name == "gripper_right_finger_link" && right == null) right = ab;
        }
        if (!left || !right)
            Debug.LogError("[Gripper] 손가락 링크 없음 - industrial_6axis_gripper_unity.urdf로 임포트했는지 확인");

        SetOpening(opening);
    }

    void FixedUpdate()
    {
        // opening 값에 따라 좌우 손가락 대칭 드라이브 갱신
        float q = Mathf.Clamp(opening, 0f, MaxOpening) * 0.5f;
        Drive(left, q);
        Drive(right, q);
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

    // 닫기 코루틴 (안전하게 정리된 버전)
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
        float elapsed = 0f;
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

            elapsed += Time.fixedDeltaTime;

            // 널 체크가 포함된 접촉 센서 확인
            Rigidbody rR = (rightContact != null) ? rightContact.TouchingPart : null;
            Rigidbody rL = (leftContact != null) ? leftContact.TouchingPart : null;

            // 양쪽 손가락이 모두 동일한 물체를 잡았을 때
            if (rR != null && rR == rL)
            {
                touched = true;
                // 현재 벌어져 있는 포지션으로 opening 고정
                opening = (Mathf.Abs(left.jointPosition[0]) + Mathf.Abs(right.jointPosition[0]));
                Attach(rR);
                LastGraspOk = true;
                Debug.Log("[IndustrialGripper] 물체 접촉 감지 완료. 파지 위치 고정 및 닫기 중지.");
                break;
            }

            yield return new WaitForFixedUpdate();
        }

        if (!LastGraspOk)
        {
            Debug.Log(touched ? "[IndustrialGripper] 실패: 미끄러짐" : "[IndustrialGripper] 실패: 양쪽 손가락이 동시에 닿지 않음");
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
        SetIgnoreCollision(false);
        IsBusy = false;
    }

    void Attach(Rigidbody part)
    {
        if (part == null) return;
        if (attachPoint == null)
        {
            Debug.LogError("[Gripper] attachPoint가 지정되지 않았습니다!");
            return;
        }

        HeldPart = part;

        // 1. 충돌 무시 처리
        ignoredPartColliders.Clear();
        ignoredPartColliders.AddRange(part.GetComponentsInChildren<Collider>());
        SetIgnoreCollision(true);

        // 2. 물리 안정화 및 부모 설정
        part.linearVelocity = Vector3.zero;
        part.angularVelocity = Vector3.zero;
        part.isKinematic = true;
        part.useGravity = false;
        part.transform.SetParent(attachPoint, true);

        Debug.Log("[Gripper] 부품 부착 성공");
    }

    void Detach()
    {
        if (HeldPart == null) return;

        HeldPart.transform.SetParent(null, true);
        HeldPart.isKinematic = false;
        HeldPart.useGravity = true;

        SetIgnoreCollision(false);
        HeldPart = null;

        if (rightContact != null) rightContact.Clear();
        if (leftContact != null) leftContact.Clear();

        Debug.Log("[Gripper] 부품 분리 완료");
    }

    void SetIgnoreCollision(bool ignore)
    {
        foreach (var partCollider in ignoredPartColliders)
        {
            if (partCollider == null) continue;
            foreach (var robotCollider in robotColliders)
            {
                if (robotCollider != null && robotCollider != partCollider)
                {
                    Physics.IgnoreCollision(partCollider, robotCollider, ignore);
                }
            }
        }
        if (!ignore) ignoredPartColliders.Clear();
    }

    public void SetOpening(float meters) => opening = meters;
}//using System.Collections;
 //using System.Collections.Generic;
 //using UnityEngine;

//public class IndustrialGripperController : MonoBehaviour
//{
//    public const float MaxOpening = 0.3147f;

//    [Header("손가락 ArticulationBody (자동 탐색됨)")]
//    public ArticulationBody left;
//    public ArticulationBody right;

//    [Header("접촉 센서 (선택 사항)")]
//    public FingerContact rightContact;
//    public FingerContact leftContact;

//    [Header("부품을 붙일 위치 (손가락 사이 정중앙)")]
//    public Transform attachPoint;

//    [Header("개폐 설정 (m)")]
//    [Range(0f, MaxOpening)] public float opening = 0.30f;
//    public float stiffness = 20000f;
//    public float damping = 2000f;
//    public float forceLimit = 200f;
//    public float moveTime = 0.5f;
//    [Tooltip("목표가 닫힘에 도달한 뒤에도 손가락이 닿기를 기다리는 최대 시간(초)")]
//    public float extraCloseTime = 0.5f;

//    public Rigidbody HeldPart { get; private set; }
//    public bool IsBusy { get; private set; }
//    public bool LastGraspOk { get; private set; }

//    Collider[] robotColliders;
//    readonly List<Collider> ignoredPartColliders = new List<Collider>();

//    void Start()
//    {
//        robotColliders = transform.root.GetComponentsInChildren<Collider>(true);

//        foreach (var ab in GetComponentsInChildren<ArticulationBody>())
//        {
//            if (ab.name == "gripper_left_finger_link" && left == null) left = ab;
//            if (ab.name == "gripper_right_finger_link" && right == null) right = ab;
//        }
//        if (!left || !right)
//            Debug.LogError("[Gripper] 손가락 링크 없음 - industrial_6axis_gripper_unity.urdf로 임포트했는지 확인");

//        SetOpening(opening);
//    }

//    void FixedUpdate()
//    {
//        // 평소에는 opening 값에 따라 손가락 드라이브 갱신
//        float q = Mathf.Clamp(opening, 0f, MaxOpening) * 0.5f;
//        Drive(left, q);
//        Drive(right, q);
//    }

//    void Drive(ArticulationBody ab, float q)
//    {
//        if (!ab) return;
//        var d = ab.xDrive;
//        d.stiffness = stiffness;
//        d.damping = damping;
//        d.forceLimit = forceLimit;
//        d.target = q;
//        ab.xDrive = d;
//    }

//    // 코루틴 방식의 닫기
//    public IEnumerator Close()
//    {
//        if (IsBusy) yield break;
//        IsBusy = true;
//        LastGraspOk = false;

//        // 손가락 ArticulationBody나 센서가 누락되었는지 방어 코드 추가
//        if (rightFinger == null || leftFinger == null)
//        {
//            Debug.LogError("[Gripper] rightFinger 또는 leftFinger가 할당되지 않았습니다!");
//            IsBusy = false;
//            yield break;
//        }

//        bool predictedOk = false;
//        Rigidbody predictedPart = null;

//        // PredictGrasp 실패 시 크래시 방지용 try-catch 또는 조건문 감싸기
//        try
//        {
//            predictedOk = PredictGrasp(out predictedPart);
//        }
//        catch (System.Exception e)
//        {
//            Debug.LogWarning($"[Gripper] PredictGrasp 중 예외 발생: {e.Message}");
//        }

//        string pred = LastGraspInfo;

//        Vector3 partPos0 = predictedPart != null ? predictedPart.position : Vector3.zero;
//        Quaternion partRot0 = predictedPart != null ? predictedPart.rotation : Quaternion.identity;
//        float elapsed = 0f;
//        float rightFirst = -1f;
//        float leftFirst = -1f;

//        float startR = rightFinger.jointPosition.Length > 0 ? rightFinger.jointPosition[0] : 0f;
//        float startL = leftFinger.jointPosition.Length > 0 ? leftFinger.jointPosition[0] : 0f;
//        float t = 0f;
//        bool touched = false;

//        float extra = 0f;

//        while (t < 1f || extra < extraCloseTime)
//        {
//            if (t < 1f)
//            {
//                t += Time.fixedDeltaTime / moveTime;
//                SetTargets(Mathf.Lerp(startR, rightClose, t),
//                           Mathf.Lerp(startL, leftClose, t));
//            }
//            else
//            {
//                extra += Time.fixedDeltaTime;
//            }

//            elapsed += Time.fixedDeltaTime;

//            // [안전 장치] contact 컴포넌트 널 체크 추가
//            Rigidbody rR = (rightContact != null) ? rightContact.TouchingPart : null;
//            Rigidbody rL = (leftContact != null) ? leftContact.TouchingPart : null;

//            if (rightFirst < 0f && rR != null) rightFirst = elapsed;
//            if (leftFirst < 0f && rL != null) leftFirst = elapsed;

//            if (rR != null && rR == rL)
//            {
//                touched = true;
//                float posR = rightFinger.jointPosition.Length > 0 ? rightFinger.jointPosition[0] : 0f;
//                float posL = leftFinger.jointPosition.Length > 0 ? leftFinger.jointPosition[0] : 0f;

//                bool ok = !useGraspCheck || (predictedOk && rR == predictedPart);
//                if (ok)
//                {
//                    SetTargets(posR, posL);
//                    Attach(rR);
//                    LastGraspOk = true;
//                }
//                else
//                {
//                    SetTargets(posR + Mathf.Sign(rightOpen - rightClose) * slipBackoff,
//                               posL + Mathf.Sign(leftOpen - leftClose) * slipBackoff);
//                }
//                break;
//            }
//            yield return new WaitForFixedUpdate();
//        }

//        string result;
//        if (LastGraspOk) result = "성공";
//        else if (touched) result = "실패: 미끄러짐 (판정 각도 초과)";
//        else result = "실패: 양쪽 손가락이 동시에 닿지 않음";
//        LastGraspInfo = $"{result}\n  닫기 전 판정: {pred}";

//        IsBusy = false;
//    }

//    // 코루틴 방식의 열기
//    public IEnumerator Open()
//    {
//        if (IsBusy) yield break;
//        IsBusy = true;

//        Detach();

//        float startOpening = opening;
//        float targetOpening = MaxOpening;
//        float t = 0f;

//        while (t < 1f)
//        {
//            t += Time.fixedDeltaTime / moveTime;
//            opening = Mathf.Lerp(startOpening, targetOpening, t);
//            yield return new WaitForFixedUpdate();
//        }

//        yield return new WaitForSeconds(0.3f);
//        SetIgnoreCollision(false);
//        IsBusy = false;
//    }

//    void Attach(Rigidbody part)
//    {
//        if (part == null) return;
//        HeldPart = part;

//        // 1. 충돌 무시 리스트 미리 수집
//        ignoredPartColliders.Clear();
//        ignoredPartColliders.AddRange(part.GetComponentsInChildren<Collider>());
//        SetIgnoreCollision(true); // 로봇과 상자 간의 물리 충돌 즉시 차단 (크래시 방지)

//        // 2. 부모 자식 연결 및  кинематика 설정
//        part.isKinematic = true;
//        part.transform.SetParent(attachPoint, true);
//    }

//    void Detach()
//    {
//        if (HeldPart == null) return;

//        HeldPart.transform.SetParent(null, true);
//        HeldPart.isKinematic = false;

//        SetIgnoreCollision(false); // 놓을 때 충돌 복원
//        HeldPart = null;

//        if (rightContact != null) rightContact.Clear();
//        if (leftContact != null) leftContact.Clear();
//    }

//    void SetIgnoreCollision(bool ignore)
//    {
//        foreach (var partCollider in ignoredPartColliders)
//        {
//            if (partCollider == null) continue;
//            foreach (var robotCollider in robotColliders)
//            {
//                if (robotCollider != null && robotCollider != partCollider)
//                {
//                    Physics.IgnoreCollision(partCollider, robotCollider, ignore);
//                }
//            }
//        }
//        if (!ignore) ignoredPartColliders.Clear();
//    }

//    public void SetOpening(float meters) => opening = meters;
//}