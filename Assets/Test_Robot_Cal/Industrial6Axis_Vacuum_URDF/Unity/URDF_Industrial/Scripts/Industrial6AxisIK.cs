using UnityEngine;

/// <summary>
/// industrial_6axis.urdf 전용 해석적(Analytic) IK.
/// - 구형 손목(4·5·6축 교차) 6축 로봇의 닫힌 해 → 반복 계산 없음, 수렴 실패/떨림 없음
/// - 최대 8개 해 중 "현재 관절각에 가장 가까운 해"를 선택 → 팔이 갑자기 뒤집히지 않음
/// - 손목 특이점(J5≈0)에서는 J4를 현재 값으로 고정
///
/// 사용법: URDF로 임포트한 로봇 루트(industrial_6axis)에 붙이고 target에 빈 오브젝트 지정.
///   target 위치 = 툴 끝(TCP) 위치, target의 파란 화살표(+Z) = 툴 접근 방향, 초록(+Y) = 툴 윗방향
/// </summary>
[DefaultExecutionOrder(-50)]
public class Industrial6AxisIK : MonoBehaviour
{
    public Transform target;
    [Tooltip("플랜지(tool0) 기준 TCP 오프셋 [m]. x = 접근방향 길이, y/z = 옆 오프셋 (ROS 좌표)")]
    public Vector3 toolOffsetRos = Vector3.zero;
    [Tooltip("Play 시작 시 target을 현재 TCP 위치로 옮김")]
    public bool snapTargetOnStart = true;

    [Header("Drive (ArticulationBody)")]
    public float stiffness = 100000f;
    public float damping = 10000f;
    public bool disableGravity = true;

    [Header("상태 (읽기 전용)")]
    public bool reachable = true;
    public float[] jointDeg = new float[6];

    // ---- 로봇 치수 [m] (URDF와 동일) ----
    const double D1 = 0.46224, A1 = 0.16342, A2 = 0.84044, A3 = 0.16342, D4 = 0.89647, D6 = 0.12607;
    static readonly double L = System.Math.Sqrt(D4 * D4 + A3 * A3);
    static readonly double BETA = System.Math.Atan2(A3, D4);
    static readonly double[,] LIM = {
        {-170,170},{-90,150},{-160,180},{-200,200},{-140,140},{-360,360} }; // deg
    static readonly double[] W = { 3, 3, 2, 1, 1, 0.5 };

    ArticulationBody[] joints = new ArticulationBody[6];
    Transform baseLink;
    double[] q = new double[6];

    void Start()
    {
        foreach (var ab in GetComponentsInChildren<ArticulationBody>())
        {
            if (ab.name == "base_link") { baseLink = ab.transform; ab.immovable = true; }
            for (int i = 0; i < 6; i++) if (ab.name == "link_" + (i + 1)) joints[i] = ab;
            if (disableGravity) ab.useGravity = false;
        }
        if (baseLink == null) baseLink = transform;
        // 툴(그리퍼/흡착) URDF로 임포트했고 오프셋이 비어 있으면 tool0 -> tcp 링크 위치로 TCP 자동 설정
        if (toolOffsetRos == Vector3.zero)
        {
            Transform tool0 = null, tcp = null;
            foreach (var t in GetComponentsInChildren<Transform>())
            {
                if (t.name == "tool0") tool0 = t;
                if (t.name == "tcp") tcp = t;
            }
            if (tool0 != null && tcp != null)
            {
                Vector3 local = tool0.InverseTransformPoint(tcp.position);   // tool0 로컬(Unity) = Ros2Unity(ROS 로컬)
                toolOffsetRos = Unity2Ros(local);
                Debug.Log($"[IK] TCP 오프셋 자동 설정 (ROS, m): {toolOffsetRos}");
            }
        }
        for (int i = 0; i < 6; i++)
        {
            if (joints[i] == null) { Debug.LogError("[IK] link_" + (i + 1) + " 없음 - industrial_6axis_unity.urdf로 임포트했는지 확인"); enabled = false; return; }
            var d = joints[i].xDrive;
            d.stiffness = stiffness; d.damping = damping; d.forceLimit = float.MaxValue;
            joints[i].xDrive = d;
            q[i] = d.target * Mathf.Deg2Rad;
        }
        //로봇의 시작 팔오루 타겟 위치
        if (snapTargetOnStart && target != null)
        {
            //FK(q, out var R, out var p);
            //Vector3 tcp = ToVec(Add(p, Mul(R, new[] { (double)toolOffsetRos.x, toolOffsetRos.y, toolOffsetRos.z })));
            //target.position = baseLink.TransformPoint(Ros2Unity(tcp));
            ////target.rotation = baseLink.rotation * RosRotToUnity(R);
            FK(q, out var R, out var p);
            Vector3 tcp = ToVec(Add(p, Mul(R, new[] { (double)toolOffsetRos.x, toolOffsetRos.y, toolOffsetRos.z })));

            Vector3 targetPos = baseLink.TransformPoint(Ros2Unity(tcp));
            targetPos.y -= 0.5f; // 시작할 때 높이를 살짝(15cm) 위로 띄워 팔 꺾임 방지

            target.position = targetPos;
            //target.rotation = baseLink.rotation * RosRotToUnity(R);
        }
    }

    void FixedUpdate()
    {
        if (target == null) return;
        // 타깃 → 로봇 베이스 좌표계 → ROS 좌표계
        Vector3 pu = baseLink.InverseTransformPoint(target.position);
        Quaternion ru = Quaternion.Inverse(baseLink.rotation) * target.rotation;
        double[] p = ToD(Unity2Ros(pu));
        double[,] R = UnityRotToRos(ru);

        var sol = SolveNearest(R, p, q, new[] { (double)toolOffsetRos.x, toolOffsetRos.y, toolOffsetRos.z });
        reachable = sol != null;
        if (!reachable) return;
        q = sol;
        for (int i = 0; i < 6; i++)
        {
            var d = joints[i].xDrive;
            d.stiffness = stiffness; d.damping = damping; d.forceLimit = float.MaxValue; // Importer Controller가 덮어써도 복구
            d.target = (float)(q[i] * Mathf.Rad2Deg);
            joints[i].xDrive = d;
            jointDeg[i] = d.target;
        }
    }

    // ================= IK =================
    public static double[] SolveNearest(double[,] R, double[] p, double[] qref, double[] tool)
    {
        double[] wc = Sub(p, Mul(R, new[] { D6 + tool[0], tool[1], tool[2] }));
        double[] best = null; double bestD = double.MaxValue;
        double base1 = System.Math.Atan2(wc[1], wc[0]);
        foreach (double q1 in new[] { base1, base1 + System.Math.PI })
        {
            double r = wc[0] * System.Math.Cos(q1) + wc[1] * System.Math.Sin(q1) - A1, s = wc[2] - D1;
            double k = (r * r + s * s - A2 * A2 - L * L) / (2 * A2 * L);
            if (System.Math.Abs(k) > 1) continue;
            double asn = System.Math.Asin(k);
            foreach (double dl in new[] { asn, System.Math.PI - asn })
            {
                double A = A2 + L * System.Math.Sin(dl), B = L * System.Math.Cos(dl);
                double q2 = System.Math.Atan2(r, s) - System.Math.Atan2(B, A);
                double th = q2 - dl + BETA, q3 = q2 - th;
                double[,] M = MulT(MulM(Rz(q1), Ry(th)), R);   // R03^T * R
                double b = System.Math.Atan2(System.Math.Sqrt(M[1, 0] * M[1, 0] + M[2, 0] * M[2, 0]), M[0, 0]);
                double a, c;
                if (System.Math.Abs(System.Math.Sin(b)) < 1e-6) { a = -qref[3]; c = System.Math.Atan2(M[2, 1], M[1, 1]) - a; }
                else { a = System.Math.Atan2(M[1, 0], -M[2, 0]); c = System.Math.Atan2(M[0, 1], M[0, 2]); }
                for (int f = 0; f < 2; f++)
                {
                    double aa = f == 0 ? a : a + System.Math.PI, bb = f == 0 ? b : -b, cc = f == 0 ? c : c + System.Math.PI;
                    double[] s6 = WrapNear(new[] { q1, q2, q3, -aa, -bb, -cc }, qref);
                    if (s6 == null) continue;
                    double dsum = 0; for (int i = 0; i < 6; i++) dsum += System.Math.Pow((s6[i] - qref[i]) * W[i], 2);
                    if (dsum < bestD) { bestD = dsum; best = s6; }
                }
            }
        }
        return best;
    }

    static double[] WrapNear(double[] s, double[] refq)
    {
        for (int i = 0; i < 6; i++)
        {
            double lo = LIM[i, 0] * System.Math.PI / 180, hi = LIM[i, 1] * System.Math.PI / 180;
            double v = refq[i] + Mod(s[i] - refq[i] + System.Math.PI, 2 * System.Math.PI) - System.Math.PI;
            while (v > hi) v -= 2 * System.Math.PI;
            while (v < lo) v += 2 * System.Math.PI;
            if (v > hi + 1e-9) return null;
            s[i] = v;
        }
        return s;
    }
    static double Mod(double x, double m) { double r = x % m; return r < 0 ? r + m : r; }

    public static void FK(double[] q, out double[,] R, out double[] p)
    {
        double th = q[1] - q[2];
        double r = A1 + A2 * System.Math.Sin(q[1]) + D4 * System.Math.Cos(th) + A3 * System.Math.Sin(th);
        double z = D1 + A2 * System.Math.Cos(q[1]) - D4 * System.Math.Sin(th) + A3 * System.Math.Cos(th);
        R = MulM(MulM(MulM(MulM(Rz(q[0]), Ry(th)), Rx(-q[3])), Ry(-q[4])), Rx(-q[5]));
        double[] wc = Mul(Rz(q[0]), new[] { r, 0, z });
        p = Add(wc, Mul(R, new[] { D6, 0, 0 }));
    }

    // ================= 좌표 변환 (ROS: x앞 y왼 z위 / Unity: x오른 y위 z앞) =================
    static Vector3 Unity2Ros(Vector3 u) => new Vector3(u.z, -u.x, u.y);
    static Vector3 Ros2Unity(Vector3 r) => new Vector3(-r.y, r.z, r.x);
    static readonly Vector3[] RosAxesInUnity = { new Vector3(0, 0, 1), new Vector3(-1, 0, 0), new Vector3(0, 1, 0) };
    static double[,] UnityRotToRos(Quaternion q)
    {
        var R = new double[3, 3];
        for (int j = 0; j < 3; j++) { Vector3 c = Unity2Ros(q * RosAxesInUnity[j]); R[0, j] = c.x; R[1, j] = c.y; R[2, j] = c.z; }
        return R;
    }
    static Quaternion RosRotToUnity(double[,] R)
    {
        Vector3 fwd = Ros2Unity(new Vector3((float)R[0, 0], (float)R[1, 0], (float)R[2, 0]));  // tool x -> Unity +Z
        Vector3 up = Ros2Unity(new Vector3((float)R[0, 2], (float)R[1, 2], (float)R[2, 2]));   // tool z -> Unity +Y
        return Quaternion.LookRotation(fwd, up);
    }

    // ================= 작은 행렬 유틸 =================
    static double[,] Rz(double t) { double c = System.Math.Cos(t), s = System.Math.Sin(t); return new[,] { { c, -s, 0 }, { s, c, 0 }, { 0, 0, 1 } }; }
    static double[,] Ry(double t) { double c = System.Math.Cos(t), s = System.Math.Sin(t); return new[,] { { c, 0, s }, { 0, 1, 0 }, { -s, 0, c } }; }
    static double[,] Rx(double t) { double c = System.Math.Cos(t), s = System.Math.Sin(t); return new[,] { { 1, 0, 0 }, { 0, c, -s }, { 0, s, c } }; }
    static double[,] MulM(double[,] a, double[,] b) { var r = new double[3, 3]; for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) for (int k = 0; k < 3; k++) r[i, j] += a[i, k] * b[k, j]; return r; }
    static double[,] MulT(double[,] a, double[,] b) { var r = new double[3, 3]; for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) for (int k = 0; k < 3; k++) r[i, j] += a[k, i] * b[k, j]; return r; }
    static double[] Mul(double[,] a, double[] v) { var r = new double[3]; for (int i = 0; i < 3; i++) for (int k = 0; k < 3; k++) r[i] += a[i, k] * v[k]; return r; }
    static double[] Add(double[] a, double[] b) => new[] { a[0] + b[0], a[1] + b[1], a[2] + b[2] };
    static double[] Sub(double[] a, double[] b) => new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] };
    static double[] ToD(Vector3 v) => new double[] { v.x, v.y, v.z };
    static Vector3 ToVec(double[] v) => new Vector3((float)v[0], (float)v[1], (float)v[2]);

    void OnDrawGizmos()
    {
        if (target == null) return;
        Gizmos.color = reachable ? Color.green : Color.red;
        Gizmos.DrawWireSphere(target.position, 0.04f);
        Gizmos.color = Color.blue; Gizmos.DrawLine(target.position, target.position + target.forward * 0.2f);
    }

}
