using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 진공(흡착) 면 그리퍼 시뮬레이션 - industrial_6axis_vacuum_unity.urdf 용
/// 로봇 루트에 붙이면 vacuum_gripper_link / tcp 를 이름으로 자동 탐색.
///
/// 흡착 판정 (현실 조건을 단순화):
///  1) 흡착면(297 x 123 mm)에 격자로 레이를 쏴서, 같은 물체 면에 닿는 비율(coverage)이 minCoverage 이상
///  2) 닿은 면의 법선과 흡착 방향의 각도가 maxTiltDeg 이하 (모서리·경사면은 실패)
///  3) 물체 질량 x 9.81 이 liftForceN 이하 (FXCB-SW150 데이터시트 수직 80 N)
/// 조건을 만족하면 FixedJoint 로 물체를 흡착면에 고정, Release 하면 해제.
/// 대상 물체: Rigidbody + Collider 필요 (택배 박스 등)
/// </summary>
public class VacuumGripper : MonoBehaviour
{
    [Header("명령")]
    public bool vacuumOn = false;
    public KeyCode toggleKey = KeyCode.V;

    [Header("흡착면 (FXCB-SW150 297, m)")]
    public float padLength = 0.297f;   // tool y 방향
    public float padWidth = 0.1232f;   // tool z 방향
    public int raysAlongLength = 7;
    public int raysAlongWidth = 3;
    public float maxGap = 0.02f;       // 흡착면에서 이 거리 안에 면이 있어야 함 [m]

    [Header("판정 기준")]
    [Range(0.1f, 1f)] public float minCoverage = 0.5f;
    public float maxTiltDeg = 15f;
    public float liftForceN = 80f;

    [Header("상태 (읽기 전용)")]
    public bool holding;
    public string lastResult = "";
    public float lastCoverage;

    Transform tcp;
    ArticulationBody gripperBody;
    FixedJoint joint;
    Rigidbody held;
    bool prevCmd;

    void Start()
    {
        foreach (var t in GetComponentsInChildren<Transform>())
        {
            if (t.name == "tcp") tcp = t;
            if (t.name == "vacuum_gripper_link") gripperBody = t.GetComponent<ArticulationBody>();
        }
        if (gripperBody == null) // 고정 조인트 링크가 합쳐졌을 경우 가장 가까운 상위 ArticulationBody 사용
            gripperBody = tcp ? tcp.GetComponentInParent<ArticulationBody>() : null;
        if (!tcp || !gripperBody)
            Debug.LogError("[Vacuum] tcp / vacuum_gripper_link 없음 - industrial_6axis_vacuum_unity.urdf 로 임포트했는지 확인");
    }

    void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER   // 새 Input System 전용 프로젝트에서는 키 토글 비활성 (Inspector 체크박스 사용)
        if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey)) vacuumOn = !vacuumOn;
#endif
    }

    void FixedUpdate()
    {
        if (!tcp || !gripperBody) return;
        if (vacuumOn && !prevCmd) TryAttach();
        if (!vacuumOn && prevCmd) Release();
        if (holding && joint == null) { holding = false; held = null; lastResult = "joint broken"; }
        prevCmd = vacuumOn;
    }

    public void Suck() => vacuumOn = true;
    public void StopSuck() => vacuumOn = false;

    void TryAttach()
    {
        // tcp 로컬: Unity +Z = 흡착 방향(ROS x), Unity -X = ROS y(길이 방향), Unity +Y = ROS z(폭 방향)
        Vector3 dir = tcp.forward;
        Vector3 lenAxis = -tcp.right, widAxis = tcp.up;
        var hitsPerBody = new Dictionary<Rigidbody, int>();
        var normalSum = new Dictionary<Rigidbody, Vector3>();
        int total = raysAlongLength * raysAlongWidth;

        for (int i = 0; i < raysAlongLength; i++)
            for (int j = 0; j < raysAlongWidth; j++)
            {
                float u = raysAlongLength == 1 ? 0 : (i / (float)(raysAlongLength - 1) - 0.5f) * padLength;
                float v = raysAlongWidth == 1 ? 0 : (j / (float)(raysAlongWidth - 1) - 0.5f) * padWidth;
                Vector3 origin = tcp.position + lenAxis * u + widAxis * v - dir * 0.01f;
                if (Physics.Raycast(origin, dir, out var hit, maxGap + 0.01f, ~0, QueryTriggerInteraction.Ignore))
                {
                    var rb = hit.rigidbody;
                    if (rb == null) continue;                                                     // 고정 물체(바닥, 컨베이어 프레임) 제외
                    if (hit.collider.GetComponentInParent<ArticulationBody>() != null) continue; // 로봇 자신 제외
                    hitsPerBody.TryGetValue(rb, out int c); hitsPerBody[rb] = c + 1;
                    normalSum.TryGetValue(rb, out var n); normalSum[rb] = n + hit.normal;
                }
            }

        Rigidbody best = null; int bestHits = 0;
        foreach (var kv in hitsPerBody) if (kv.Value > bestHits) { best = kv.Key; bestHits = kv.Value; }
        lastCoverage = bestHits / (float)total;

        if (best == null) { lastResult = "FAIL: 흡착면 아래에 물체 없음"; return; }
        if (lastCoverage < minCoverage) { lastResult = $"FAIL: 접촉 부족 {lastCoverage:P0} (모서리/작은 면)"; return; }
        float tilt = Vector3.Angle(-dir, normalSum[best].normalized);
        if (tilt > maxTiltDeg) { lastResult = $"FAIL: 면 기울기 {tilt:F1}도 > {maxTiltDeg}도"; return; }
        float weightN = best.mass * Physics.gravity.magnitude;
        if (weightN > liftForceN) { lastResult = $"FAIL: 무게 {weightN:F0}N > 흡착력 {liftForceN}N"; return; }

        joint = best.gameObject.AddComponent<FixedJoint>();
        joint.connectedArticulationBody = gripperBody;
        joint.breakForce = liftForceN * 3f;      // 급가속 시 과부하로 떨어짐 (현실의 진공 이탈 흉내)
        joint.enableCollision = false;
        held = best; holding = true;
        lastResult = $"OK: {best.name} 흡착 (접촉 {lastCoverage:P0}, 기울기 {tilt:F1}도)";
    }

    public void Release()
    {
        if (joint) Destroy(joint);
        joint = null; held = null;
        if (holding) lastResult = "released";
        holding = false;
    }

    void OnDrawGizmosSelected()
    {
        if (!tcp) return;
        Gizmos.color = holding ? Color.green : (vacuumOn ? Color.yellow : Color.gray);
        Gizmos.matrix = Matrix4x4.TRS(tcp.position, tcp.rotation, Vector3.one);
        Gizmos.DrawWireCube(new Vector3(0, 0, maxGap * 0.5f), new Vector3(padLength, padWidth, maxGap));
    }
}
