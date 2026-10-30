using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArmIK : MonoBehaviour
{
    #region Inspector 설정

    [Header("관절 (비워두면 UR20 표준 이름으로 자동 검색)")]
    public ArticulationBody[] joints = new ArticulationBody[0];

    // UR20 및 ROS 범용 표준 관절 이름 매핑
    static readonly string[] JointNames =
        { "shoulder_link", "upper_arm_link", "forearm_link", "wrist_1_link", "wrist_2_link", "hand_link" };

    [Tooltip("엔드이펙터(그리퍼 부착점) 팁 트랜스폼")]
    public Transform tip;

    [Tooltip("그리퍼 동작을 위한 기준 ArticulationBody")]
    public ArticulationBody closingFinger;

    [Header("관절 회전 방향 (처음 한 번 자동 확인 후 저장)")]
    public bool signsCalibrated = false;
    public float[] jointSigns = new float[0];

    [Header("자세 설정")]
    [Tooltip("그리퍼가 항상 수직 아래를 보도록 제어")]
    public bool pointDown = true;
    [Tooltip("자세 오차 가중치")]
    public float orientationWeight = 0.2f;

    [Header("이동 제어 속도 및 오차")]
    public float jointSpeed = 90f;          // 도/초
    public float minMoveTime = 0.3f;
    public float linearStep = 0.01f;        // m 단위 직선 보간 스텝
    public int correctionPasses = 3;
    public float positionTolerance = 0.001f; // 1mm

    [Header("테스트: IKTarget을 연동하여 추적")]
    public Transform testTarget;
    public bool followTestTarget = false;

    [Header("테스트 좌표 설정 (Context Menu용)")]
    public Vector3 sampleGoalPosition = new Vector3(0.3f, 0.4f, 0.2f);
    public float sampleYaw = 0f;

    public bool Ready { get; private set; } = false;
    public bool IsMoving { get; private set; } = false;
    public bool LastSolveOk { get; private set; } = true;
    public float PositionError { get; private set; } = 0f;
    public float AngleError { get; private set; } = 0f;
    public float YawError { get; private set; } = 0f;

    #endregion

    #region 내부 연산 모델 변수

    private int n;
    private Transform baseT;
    private Quaternion[] relRot0;
    private Vector3[] pivotInParent,
                      anchorPos,
                      axisLocal;
    private float[] theta0,
                    lower, upper;
    private bool[] limited;
    private Vector3 tipLocal,
                    dirLocal,
                    closeLocal;

    private float? activeYaw;
    private Vector3 lastFollowPos;
    private float lastFollowYaw;
    private bool firstMove = true;

    #endregion

    #region 1. 로봇 구조 분석 및 계산 모델 구축

    IEnumerator Start()
    {
        AutoAssign();
        BuildModel();

        if (!signsCalibrated || jointSigns == null || jointSigns.Length != n)
            yield return CalibrateSigns();

        Ready = true;
        Debug.Log("[ArmIK] UR20 역운동학(IK) 모델 구축 및 초기화 완료");
    }

    void AutoAssign()
    {
        var bodies = GetComponentsInChildren<ArticulationBody>();
        if (joints == null || joints.Length == 0)
        {
            joints = new ArticulationBody[JointNames.Length];
            for (int i = 0; i < JointNames.Length; i++)
            {
                foreach (var b in bodies)
                {
                    if (b.name.Contains(JointNames[i]) || b.name == JointNames[i])
                    {
                        joints[i] = b;
                        break;
                    }
                }
            }
        }

        if (closingFinger == null)
        {
            foreach (var b in bodies)
            {
                if (b.name.Contains("gripper") || b.name.Contains("finger"))
                {
                    closingFinger = b;
                    break;
                }
            }
        }

        if (tip == null)
        {
            foreach (var t in GetComponentsInChildren<Transform>())
            {
                if (t.name == "AttachPoint" || t.name == "ToolCenterPoint" || t.name.Contains("tip"))
                {
                    tip = t;
                    break;
                }
            }
        }

        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i] == null)
                Debug.LogError($"[ArmIK] {i}번 관절({JointNames[i]})을 찾지 못했습니다. 계층 구조 이름을 확인해주세요.");
        }
        if (tip == null) Debug.LogError("[ArmIK] 팁(AttachPoint) 트랜스폼을 찾지 못했습니다.");
    }

    void BuildModel()
    {
        n = joints.Length;
        if (n == 0 || joints[0] == null) return;

        baseT = joints[0].transform.parent;
        relRot0 = new Quaternion[n];
        pivotInParent = new Vector3[n];
        anchorPos = new Vector3[n];
        axisLocal = new Vector3[n];
        theta0 = new float[n];
        lower = new float[n]; upper = new float[n]; limited = new bool[n];

        for (int j = 0; j < n; j++)
        {
            Transform c = joints[j].transform, p = c.parent;
            Quaternion inv = Quaternion.Inverse(p.rotation);

            relRot0[j] = inv * c.rotation;
            anchorPos[j] = joints[j].anchorPosition;
            axisLocal[j] = joints[j].anchorRotation * Vector3.right;
            pivotInParent[j] = inv * (c.position - p.position) + relRot0[j] * anchorPos[j];
            theta0[j] = joints[j].jointPosition[0] * Mathf.Rad2Deg;

            var d = joints[j].xDrive;
            limited[j] = joints[j].twistLock == ArticulationDofLock.LimitedMotion;
            lower[j] = d.lowerLimit; upper[j] = d.upperLimit;
        }

        Transform hand = joints[n - 1].transform;
        Quaternion handInv = Quaternion.Inverse(hand.rotation);
        tipLocal = handInv * (tip.position - hand.position);
        dirLocal = (tipLocal - anchorPos[n - 1]).normalized;
        closeLocal = closingFinger != null ? handInv * ActualCloseAxis() : Vector3.forward;
    }

    IEnumerator CalibrateSigns()
    {
        jointSigns = new float[n];

        for (int j = 0; j < n; j++)
        {
            float mid = (lower[j] + upper[j]) * 0.5f;
            SetTarget(j, theta0[j] + (theta0[j] <= mid ? 4f : -4f));
        }
        yield return new WaitForSeconds(0.3f);

        for (int j = 0; j < n; j++)
        {
            Transform c = joints[j].transform;
            Quaternion actual = Quaternion.Inverse(c.parent.rotation) * c.rotation;
            float moved = joints[j].jointPosition[0] * Mathf.Rad2Deg - theta0[j];

            float ePlus = Quaternion.Angle(relRot0[j] * Quaternion.AngleAxis(moved, axisLocal[j]), actual);
            float eMinus = Quaternion.Angle(relRot0[j] * Quaternion.AngleAxis(-moved, axisLocal[j]), actual);

            jointSigns[j] = ePlus <= eMinus ? 1f : -1f;
            SetTarget(j, theta0[j]);
        }
        yield return new WaitForSeconds(0.3f);

        signsCalibrated = true;
        Debug.Log($"[ArmIK] UR20 관절 회전 방향 교정 완료: {string.Join(", ", jointSigns)}");
    }

    #endregion

    #region 2. 목표 이동 인터페이스 (MoveTo / MoveLinear)

    void Update()
    {
        if (!Ready || !followTestTarget || testTarget == null || IsMoving) return;

        float yaw = testTarget.eulerAngles.y;
        if ((testTarget.position - lastFollowPos).sqrMagnitude > 0.002f * 0.002f ||
            Mathf.Abs(Mathf.DeltaAngle(yaw, lastFollowYaw)) > 1f)
        {
            lastFollowPos = testTarget.position;
            lastFollowYaw = yaw;
            StartCoroutine(MoveTo(testTarget.position, yaw));
        }
    }

    public IEnumerator MoveTo(Vector3 goal, float? yawDeg = null)
    {
        if (IsMoving) yield break;
        IsMoving = true;
        activeYaw = yawDeg;

        float[] current = CurrentTargets();
        float[] solution = new float[n];

        LastSolveOk = Solve(goal, activeYaw, current, solution);
        if (!LastSolveOk) Debug.LogWarning($"[ArmIK] 목표 위치({goal})에 정확히 도달할 수 없어 가장 가까운 자세로 이동합니다.");

        yield return Drive(solution, MoveTime(current, solution), true);
        yield return Settle();
        yield return Correct(goal);

        IsMoving = false;
    }

    public IEnumerator MoveLinear(Vector3 goal, float speed, float? yawDeg = null)
    {
        if (IsMoving) yield break;
        IsMoving = true;
        activeYaw = yawDeg;

        float[] startAngles = CurrentTargets();
        Forward(startAngles, out Vector3 startPos, out _, out _, null, null);

        float distance = Vector3.Distance(startPos, goal);
        int pointCount = Mathf.Max(1, Mathf.CeilToInt(distance / linearStep));
        float pointTime = Mathf.Max(Time.fixedDeltaTime, distance / Mathf.Max(speed, 0.001f) / pointCount);

        List<float[]> pathAngles = new List<float[]>();
        float[] prevAngles = startAngles;
        bool allOk = true;

        for (int i = 1; i <= pointCount; i++)
        {
            Vector3 point = Vector3.Lerp(startPos, goal, (float)i / pointCount);
            float[] pointAngles = new float[n];
            allOk &= Solve(point, activeYaw, prevAngles, pointAngles);
            pathAngles.Add(pointAngles);
            prevAngles = pointAngles;
        }
        LastSolveOk = allOk;

        foreach (float[] pointAngles in pathAngles)
        {
            yield return Drive(pointAngles, pointTime, false);
        }
        yield return Settle();
        yield return Correct(goal);

        IsMoving = false;
    }

    #endregion

    #region 3. 순/역운동학 (FK & IK Solver) 연산

    void Forward(float[] angles, out Vector3 tipPos, out Vector3 tipDir, out Vector3 closeAxis, Vector3[] jointAxes, Vector3[] jointPivots)
    {
        Vector3 linkPos = baseT.position;
        Quaternion linkRot = baseT.rotation;

        for (int j = 0; j < n; j++)
        {
            Vector3 pivotWorld = linkPos + linkRot * pivotInParent[j];
            float turn = jointSigns[j] * (angles[j] - theta0[j]);
            Quaternion childRot = linkRot * relRot0[j] * Quaternion.AngleAxis(turn, axisLocal[j]);

            if (jointAxes != null)
            {
                jointAxes[j] = jointSigns[j] * (childRot * axisLocal[j]);
                jointPivots[j] = pivotWorld;
            }

            linkPos = pivotWorld - childRot * anchorPos[j];
            linkRot = childRot;
        }

        tipPos = linkPos + linkRot * tipLocal;
        tipDir = linkRot * dirLocal;
        closeAxis = closingFinger != null ? linkRot * closeLocal : Vector3.forward;
    }

    static Vector3 YawErrorVector(Vector3 closeAxis, float yawDeg, out float errorDeg)
    {
        Vector3 targetDir = Quaternion.AngleAxis(yawDeg, Vector3.up) * Vector3.forward;
        Vector3 currentDir = Vector3.ProjectOnPlane(closeAxis, Vector3.up).normalized;
        if (Vector3.Dot(currentDir, targetDir) < 0f) targetDir = -targetDir;
        errorDeg = Vector3.Angle(currentDir, targetDir);
        return Vector3.Cross(currentDir, targetDir);
    }

    bool Solve(Vector3 goal, float? yaw, float[] startAngles, float[] result)
    {
        float[] angles = (float[])startAngles.Clone();
        Vector3[] jointAxes = new Vector3[n];
        Vector3[] jointPivots = new Vector3[n];

        bool useYaw = pointDown && yaw.HasValue;
        int errorCount = 3 + (pointDown ? 2 : 0) + (useYaw ? 1 : 0);
        float rotWeight = orientationWeight;

        float[,] jacobian = new float[errorCount, n];
        float[] errors = new float[errorCount];
        float[,] matrixA = new float[errorCount, errorCount];
        const float damping = 0.02f;
        float yawErrorDeg = 0f;

        for (int iter = 0; iter < 300; iter++)
        {
            Forward(angles, out Vector3 tipPos, out Vector3 tipDir, out Vector3 closeAxis, jointAxes, jointPivots);

            Vector3 posError = goal - tipPos;
            Vector3 downError = Vector3.Cross(tipDir, Vector3.down);
            Vector3 yawError = useYaw ? YawErrorVector(closeAxis, yaw.Value, out yawErrorDeg) : Vector3.zero;

            if (posError.magnitude < 0.0005f &&
                (!pointDown || Vector3.Angle(tipDir, Vector3.down) < 0.5f) &&
                (!useYaw || yawErrorDeg < 0.5f)) break;

            errors[0] = posError.x; errors[1] = posError.y; errors[2] = posError.z;
            if (pointDown) { errors[3] = rotWeight * downError.x; errors[4] = rotWeight * downError.z; }
            if (useYaw) errors[5] = rotWeight * yawError.y;

            for (int j = 0; j < n; j++)
            {
                Vector3 tipMove = Vector3.Cross(jointAxes[j], tipPos - jointPivots[j]);
                jacobian[0, j] = tipMove.x; jacobian[1, j] = tipMove.y; jacobian[2, j] = tipMove.z;
                if (pointDown) { jacobian[3, j] = rotWeight * jointAxes[j].x; jacobian[4, j] = rotWeight * jointAxes[j].z; }
                if (useYaw) jacobian[5, j] = rotWeight * jointAxes[j].y;
            }

            for (int row = 0; row < errorCount; row++)
            {
                for (int col = 0; col < errorCount; col++)
                {
                    float sum = 0f;
                    for (int k = 0; k < n; k++) sum += jacobian[row, k] * jacobian[col, k];
                    matrixA[row, col] = sum + (row == col ? damping * damping : 0f);
                }
            }
            float[] solved = SolveLinear(matrixA, errors, errorCount);
            if (solved == null) break;

            for (int j = 0; j < n; j++)
            {
                float deltaRad = 0f;
                for (int row = 0; row < errorCount; row++) deltaRad += jacobian[row, j] * solved[row];
                angles[j] += Mathf.Clamp(deltaRad * Mathf.Rad2Deg, -10f, 10f);
                if (limited[j]) angles[j] = Mathf.Clamp(angles[j], lower[j], upper[j]);
            }
        }

        System.Array.Copy(angles, result, n);
        Forward(angles, out Vector3 finalPos, out Vector3 finalDir, out Vector3 finalClose, null, null);
        float finalYawDeg = 0f;
        if (useYaw) YawErrorVector(finalClose, yaw.Value, out finalYawDeg);

        return (goal - finalPos).magnitude < 0.002f &&
               (!pointDown || Vector3.Angle(finalDir, Vector3.down) < 3f) &&
               (!useYaw || finalYawDeg < 3f);
    }

    static float[] SolveLinear(float[,] matrix, float[] rhs, int size)
    {
        float[,] mat = (float[,])matrix.Clone();
        float[] vec = (float[])rhs.Clone();

        for (int col = 0; col < size; col++)
        {
            int bestRow = col;
            for (int row = col + 1; row < size; row++)
            {
                if (Mathf.Abs(mat[row, col]) > Mathf.Abs(mat[bestRow, col])) bestRow = row;
            }
            if (Mathf.Abs(mat[bestRow, col]) < 1e-9f) return null;

            if (bestRow != col)
            {
                for (int k = 0; k < size; k++)
                {
                    float temp = mat[col, k]; mat[col, k] = mat[bestRow, k]; mat[bestRow, k] = temp;
                }
                float tempVec = vec[col]; vec[col] = vec[bestRow]; vec[bestRow] = tempVec;
            }

            for (int row = col + 1; row < size; row++)
            {
                float factor = mat[row, col] / mat[col, col];
                for (int k = col; k < size; k++) mat[row, k] -= factor * mat[col, k];
                vec[row] -= factor * vec[col];
            }
        }

        float[] answer = new float[size];
        for (int row = size - 1; row >= 0; row--)
        {
            float sum = vec[row];
            for (int k = row + 1; k < size; k++) sum -= mat[row, k] * answer[k];
            answer[row] = sum / mat[row, row];
        }
        return answer;
    }

    #endregion

    #region 4. 구동 및 오차 보정 루틴

    float MoveTime(float[] fromAngles, float[] toAngles)
    {
        float maxTurn = 0f;
        for (int j = 0; j < n; j++) maxTurn = Mathf.Max(maxTurn, Mathf.Abs(toAngles[j] - fromAngles[j]));
        return Mathf.Max(minMoveTime, maxTurn / jointSpeed);
    }

    IEnumerator Drive(float[] goalAngles, float duration, bool smooth)
    {
        float[] startAngles = CurrentTargets();
        float progress = 0f;
        while (progress < 1f)
        {
            progress += Time.fixedDeltaTime / duration;
            float blend = smooth ? Mathf.SmoothStep(0f, 1f, progress) : Mathf.Clamp01(progress);
            for (int j = 0; j < n; j++) SetTarget(j, Mathf.Lerp(startAngles[j], goalAngles[j], blend));
            yield return new WaitForFixedUpdate();
        }
    }

    IEnumerator Settle()
    {
        float waited = 0f;
        while (waited < 1f)
        {
            bool allArrived = true;
            for (int j = 0; j < n; j++)
            {
                float actualDeg = joints[j].jointPosition[0] * Mathf.Rad2Deg;
                if (Mathf.Abs(actualDeg - joints[j].xDrive.target) > 0.5f) { allArrived = false; break; }
            }
            if (allArrived) break;
            waited += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }

    float[] CurrentTargets()
    {
        float[] targets = new float[n];
        for (int j = 0; j < n; j++) targets[j] = joints[j].xDrive.target;
        return targets;
    }

    void SetTarget(int j, float deg)
    {
        var d = joints[j].xDrive;
        d.target = deg;
        joints[j].xDrive = d;
    }

    IEnumerator Correct(Vector3 goal)
    {
        Vector3 aimPos = goal;
        for (int pass = 0; pass <= correctionPasses; pass++)
        {
            Vector3 posError = goal - tip.position;
            if (posError.magnitude < positionTolerance || pass == correctionPasses) break;
            aimPos += posError;

            float[] currentAngles = CurrentTargets();
            float[] solution = new float[n];
            Solve(aimPos, activeYaw, currentAngles, solution);
            yield return Drive(solution, 0.2f, true);
            yield return Settle();
        }

        PositionError = (goal - tip.position).magnitude;
        AngleError = Vector3.Angle(ActualDir(), Vector3.down);
        YawError = 0f;
        if (activeYaw.HasValue)
        {
            YawErrorVector(ActualCloseAxis(), activeYaw.Value, out float yawErrorDeg);
            YawError = yawErrorDeg;
        }
    }

    Vector3 ActualDir()
    {
        Vector3 wristPivot = joints[n - 1].transform.TransformPoint(anchorPos[n - 1]);
        return (tip.position - wristPivot).normalized;
    }

    Vector3 ActualCloseAxis()
    {
        if (closingFinger == null) return Vector3.forward;
        Vector3 slideAxisLocal = closingFinger.anchorRotation * Vector3.right;
        return (closingFinger.transform.rotation * slideAxisLocal).normalized;
    }

    #endregion

    #region 5. 테스트용 전용 함수 (Context Menu & Debug)

    [ContextMenu("테스트: 지정한 샘플 좌표로 이동 (MoveTo)")]
    public void TestMoveToSample()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[ArmIK Test] 플레이(Play) 모드에서만 실행할 수 있습니다.");
            return;
        }
        StartCoroutine(MoveTo(sampleGoalPosition, sampleYaw));
        Debug.Log($"[ArmIK Test] 샘플 좌표로 이동 시작: {sampleGoalPosition}");
    }

    [ContextMenu("테스트: 지정한 샘플 좌표로 직선 이동 (MoveLinear)")]
    public void TestMoveLinearSample()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[ArmIK Test] 플레이(Play) 모드에서만 실행할 수 있습니다.");
            return;
        }
        StartCoroutine(MoveLinear(sampleGoalPosition, 0.15f, sampleYaw));
        Debug.Log($"[ArmIK Test] 샘플 좌표로 직선 이동 시작: {sampleGoalPosition}");
    }

    #endregion

    #region 디버그 UI

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(300, 10, 290, 115), GUI.skin.box);
        GUILayout.Label(!Ready ? "UR20 IK: 회전 방향 교정 중..." : IsMoving ? "UR20 IK: 이동 중" : "UR20 IK: 대기 중");
        GUILayout.Label($"위치 오차: {PositionError * 100f:F2} cm / 수직 오차: {AngleError:F1}도");
        GUILayout.Label($"경로 계산: {(LastSolveOk ? "성공" : "범위 초과")}");

        if (GUILayout.Button("테스트 이동 실행"))
        {
            TestMoveToSample();
        }

        GUILayout.EndArea();
    }

    #endregion
}