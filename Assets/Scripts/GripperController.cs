using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GripperController : MonoBehaviour
{
    [Header("손가락 (Prismatic ArticulationBody)")]
    public ArticulationBody rightFinger;
    public ArticulationBody leftFinger;
    public FingerContact rightContact;
    public FingerContact leftContact;

    [Header("열림/닫힘 값 (m)")]
    public float rightOpen = 0.0f;
    public float rightClose = 0.02f;
    public float leftOpen = 0.0f;
    public float leftClose = -0.02f;

    [Header("동작 시간 설정")]
    public float moveTime = 0.5f;
    public float extraCloseTime = 0.5f;

    [Header("그리퍼 모터 힘 설정 (중요)")]
    [Tooltip("모터가 낼 수 있는 최대 힘 (부품을 꽉 쥐지 못하고 밀리면 이 값을 키우세요)")]
    public float forceLimit = 5000f;
    [Tooltip("목표 위치를 유지하려는 강성 (Stiffness)")]
    public float stiffness = 100000f;
    [Tooltip("떨림을 방지하는 감쇠 (Damping)")]
    public float damping = 10000f;

    [Header("부품을 붙일 위치 (손가락 사이 정중앙 Attchpoint)")]
    public Transform attachPoint;

    [Header("파지 판정 (마찰 원뿔)")]
    public bool useGraspCheck = true;
    public float frictionCoefficient = 0.36f;
    public float padHalfHeight = 0.006f;
    public float padHalfWidth = 0.006f;
    public float slipBackoff = 0.001f;

    [Header("놓은 뒤 충돌을 다시 켜기까지 대기(초)")]
    public float restoreCollisionDelay = 0.3f;

    [Header("테스트 UI")]
    public bool showTestButtons = true;

    public Rigidbody HeldPart { get; private set; }
    public bool IsBusy { get; private set; }

    public bool LastGraspOk { get; private set; }
    public float LastAngleRight { get; private set; }
    public float LastAngleLeft { get; private set; }
    public string LastGraspInfo { get; private set; } = "-";
    public float MaxAngle => Mathf.Atan(frictionCoefficient) * Mathf.Rad2Deg;

    Collider[] robotColliders;
    readonly List<Collider> ignoredPartColliders = new List<Collider>();

    Vector3 dbgHitA, dbgHitB, dbgNormA, dbgNormB;
    bool dbgValid;

    void Start()
    {
        robotColliders = transform.root.GetComponentsInChildren<Collider>(true);
        SetTargets(rightOpen, leftOpen);
    }
    public IEnumerator Close()
    {
        if (IsBusy) yield break;
        IsBusy = true;
        LastGraspOk = false;

        bool predictedOk = PredictGrasp(out Rigidbody predictedPart);
        string pred = LastGraspInfo;

        Vector3 partPos0 = predictedPart != null ? predictedPart.position : Vector3.zero;
        Quaternion partRot0 = predictedPart != null ? predictedPart.rotation : Quaternion.identity;
        float elapsed = 0f;
        float rightFirst = -1f;
        float leftFirst = -1f;

        float startR = rightFinger.jointPosition[0];
        float startL = leftFinger.jointPosition[0];
        float t = 0f;
        bool touched = false;
        float extra = 0f;

        while (t < 1f || extra < extraCloseTime)
        {
            if (t < 1f)
            {
                t += Time.fixedDeltaTime / moveTime;
                SetTargets(Mathf.Lerp(startR, rightClose, t),
                           Mathf.Lerp(startL, leftClose, t));
            }
            else
            {
                extra += Time.fixedDeltaTime;
            }

            elapsed += Time.fixedDeltaTime;

            if (rightFirst < 0f && rightContact.TouchingPart != null) rightFirst = elapsed;
            if (leftFirst < 0f && leftContact.TouchingPart != null) leftFirst = elapsed;

            Rigidbody r = rightContact.TouchingPart;
            if (r != null && r == leftContact.TouchingPart)
            {
                touched = true;
                float posR = rightFinger.jointPosition[0];
                float posL = leftFinger.jointPosition[0];

                bool ok = !useGraspCheck || (predictedOk && r == predictedPart);
                if (ok)
                {
                    SetTargets(posR, posL);
                    Attach(r);
                    LastGraspOk = true;
                }
                else
                {
                    SetTargets(posR + Mathf.Sign(rightOpen - rightClose) * slipBackoff,
                               posL + Mathf.Sign(leftOpen - leftClose) * slipBackoff);
                }
                break;
            }
            yield return new WaitForFixedUpdate();
        }

        string result;
        if (LastGraspOk) result = "성공";
        else if (touched) result = "실패: 미끄러짐 (판정 각도 초과)";
        else result = "실패: 양쪽 손가락이 동시에 닿지 않음";
        LastGraspInfo = $"{result}\n  닫기 전 판정: {pred}";

        string rightTime = rightFirst >= 0f ? $"{rightFirst:F2}초" : "안 닿음";
        string leftTime = leftFirst >= 0f ? $"{leftFirst:F2}초" : "안 닿음";

        float movedMm = 0f;
        float turnedDeg = 0f;
        if (predictedPart != null)
        {
            movedMm = Vector3.Distance(predictedPart.position, partPos0) * 1000f;
            turnedDeg = Quaternion.Angle(predictedPart.rotation, partRot0);
        }

        Debug.Log($"[Grasp] {result} | 판정: {pred} | 처음 닿은 시간 R: {rightTime}, L: {leftTime} | 부품 이동 {movedMm:F1}mm, 회전 {turnedDeg:F1}도");
        IsBusy = false;
    }

    public IEnumerator Open()
    {
        if (IsBusy) yield break;
        IsBusy = true;

        Detach();
        float startR = rightFinger.jointPosition[0];
        float startL = leftFinger.jointPosition[0];
        float t = 0f;

        while (t < 1f)
        {
            t += Time.fixedDeltaTime / moveTime;
            SetTargets(Mathf.Lerp(startR, rightOpen, t),
                       Mathf.Lerp(startL, leftOpen, t));
            yield return new WaitForFixedUpdate();
        }

        yield return new WaitForSeconds(restoreCollisionDelay);
        SetIgnoreCollision(false);
        IsBusy = false;
    }

    bool PredictGrasp(out Rigidbody part)
    {
        part = null;
        dbgValid = false;

        if (attachPoint == null) return false;

        Vector3 axis = (rightFinger.transform.rotation * (rightFinger.anchorRotation * Vector3.right)).normalized;
        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, axis).normalized;
        Vector3 side = Vector3.Cross(axis, up).normalized;
        Vector3 center = attachPoint.position;

        bool okA = FirstContact(center, axis, up, side, +1f, out RaycastHit a, out Rigidbody partA);
        bool okB = FirstContact(center, axis, up, side, -1f, out RaycastHit b, out Rigidbody partB);

        if (!okA || !okB || partA != partB)
        {
            LastGraspInfo = "실패: 손가락 사이에 부품이 없음";
            return false;
        }
        part = partA;

        LastAngleRight = Vector3.Angle(a.normal, axis);
        LastAngleLeft = Vector3.Angle(b.normal, -axis);

        dbgHitA = a.point;
        dbgNormA = a.normal;
        dbgHitB = b.point;
        dbgNormB = b.normal;
        dbgValid = true;

        bool ok = LastAngleRight <= MaxAngle && LastAngleLeft <= MaxAngle;
        LastGraspInfo = $"{(ok ? "성공" : "실패: 미끄러짐")} ({LastAngleRight:F1}°, {LastAngleLeft:F1}° / 한계 {MaxAngle:F1}°)";
        return ok;
    }

    bool FirstContact(Vector3 center, Vector3 axis, Vector3 up, Vector3 side, float sideSign,
                      out RaycastHit best, out Rigidbody bestPart)
    {
        best = default;
        bestPart = null;
        float bestDist = float.MaxValue;

        Vector3 dir = -sideSign * axis;

        for (int iu = -1; iu <= 1; iu++)
        {
            for (int iv = -1; iv <= 1; iv++)
            {
                Vector3 origin = center + up * (iu * padHalfHeight) + side * (iv * padHalfWidth)
                                 + sideSign * axis * 0.1f;

                foreach (var h in Physics.RaycastAll(origin, dir, 0.2f, ~0, QueryTriggerInteraction.Ignore))
                {
                    Rigidbody rb = h.collider.attachedRigidbody;
                    if (rb == null || !rb.CompareTag("Part") || rb == HeldPart) continue;

                    if (h.distance < bestDist)
                    {
                        bestDist = h.distance;
                        best = h;
                        bestPart = rb;
                    }
                }
            }
        }
        return bestPart != null;
    }

    void Attach(Rigidbody part)
    {
        HeldPart = part;
        part.isKinematic = true;
        part.transform.SetParent(attachPoint, true);

        ignoredPartColliders.Clear();
        ignoredPartColliders.AddRange(part.GetComponentsInChildren<Collider>());
        SetIgnoreCollision(true);
    }

    void Detach()
    {
        if (HeldPart == null) return;
        HeldPart.transform.SetParent(null, true);
        HeldPart.isKinematic = false;
        HeldPart = null;
        rightContact.Clear();
        leftContact.Clear();
    }

    void SetIgnoreCollision(bool ignore)
    {
        foreach (var partCollider in ignoredPartColliders)
        {
            if (partCollider == null) continue;
            foreach (var robotCollider in robotColliders)
            {
                if (robotCollider != null && robotCollider != partCollider)
                    Physics.IgnoreCollision(partCollider, robotCollider, ignore);
            }
        }
        if (!ignore) ignoredPartColliders.Clear();
    }

    void SetTargets(float right, float left)
    {
        if (rightFinger != null)
        {
            var d = rightFinger.xDrive;
            // 유니티 ArticulationBody에서 위치 제어를 하려면 드라이브 타입이 Force 상태에서 
            // Stiffness와 Damping이 들어가야 하지만, 일부 버전에서는 드라이브 설정 방식이 다릅니다.
            d.target = right;
            d.forceLimit = forceLimit;
            d.stiffness = stiffness;
            d.damping = damping;
            rightFinger.xDrive = d;
        }

        if (leftFinger != null)
        {
            var d = leftFinger.xDrive;
            d.target = left;
            d.forceLimit = forceLimit;
            d.stiffness = stiffness;
            d.damping = damping;
            leftFinger.xDrive = d;
        }
    }

    void OnDrawGizmos()
    {
        if (!dbgValid) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(dbgHitA, dbgHitA + dbgNormA * 0.03f);
        Gizmos.DrawLine(dbgHitB, dbgHitB + dbgNormB * 0.03f);
        Gizmos.color = Color.white;
        Gizmos.DrawLine(dbgHitA, dbgHitB);
        Gizmos.DrawSphere(dbgHitA, 0.0015f);
        Gizmos.DrawSphere(dbgHitB, 0.0015f);
    }

    void OnGUI()
    {
        if (!showTestButtons) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 220), GUI.skin.box);
        if (GUILayout.Button("Open Gripper")) StartCoroutine(Open());
        if (GUILayout.Button("Close Gripper")) StartCoroutine(Close());

        float rPos = rightFinger != null ? rightFinger.jointPosition[0] : 0f;
        float lPos = leftFinger != null ? leftFinger.jointPosition[0] : 0f;
        GUILayout.Label($"R / L 위치: {rPos:F4} / {lPos:F4}");
        GUILayout.Label($"접촉 상태 R: {(rightContact != null && rightContact.TouchingPart != null ? rightContact.TouchingPart.name : "-")}");
        GUILayout.Label($"접촉 상태 L: {(leftContact != null && leftContact.TouchingPart != null ? leftContact.TouchingPart.name : "-")}");
        GUILayout.Label($"잡은 부품: {(HeldPart != null ? HeldPart.name : "-")}");
        GUILayout.EndArea();
    }
}