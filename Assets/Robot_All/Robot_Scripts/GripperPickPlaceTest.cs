using System.Collections;
using UnityEngine;

public class GripperPickPlaceTest : MonoBehaviour
{
    [Header("연결 컴포넌트")]
    public Industrial6AxisIK ik;
    public IndustrialGripperController gripper;
    public Rigidbody parcel;
    public Transform placePoint;
    public bool runOnStart = true;

    [Header("대시보드 연결")]
    public DashboardStatsController dashboardStats;

    [Header("동작 설정")]
    public float speed = 0.5f;
    public float approachHeight = 0.25f;

    [Header("파지 깊이 조절")]
    public float gripHeightOffset = -0.1f;

    [Header("테스트 실패 시뮬레이션")]
    [Tooltip("0 = 인위적인 실패 없음, 1 = 파지에 성공해도 항상 실패 처리")]
    [Range(0f, 1f)]
    public float failureProbability = 0f;

    [Header("재시도 설정")]
    [Min(1)]
    public int maxRetry = 3;

    [Header("최근 작업 결과")]
    [SerializeField]
    private bool lastRunSucceeded = false;

    public bool LastRunSucceeded => lastRunSucceeded;

    private Vector3 homePosition;
    private Quaternion homeRotation;
    private bool homeInitialized = false;

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(1f);

        if (ik != null && ik.target != null)
        {
            homePosition = ik.target.position;
            homeRotation = ik.target.rotation;
            homeInitialized = true;
        }

        if (gripper != null)
        {
            yield return StartCoroutine(gripper.Open());
        }

        if (runOnStart)
        {
            yield return StartCoroutine(Run());
        }
    }

    [ContextMenu("Run Test")]
    public void RunTest()
    {
        StartCoroutine(Run());
    }

    public IEnumerator Run()
    {
        // 새 작업 시작 시 이전 결과 초기화
        lastRunSucceeded = false;

        if (!ik || !gripper || !parcel || !ik.target)
        {
            Debug.LogError(
                "[GripperTest] ik / gripper / parcel / ik.target 연결이 필요합니다!"
            );

            yield break;
        }

        if (!homeInitialized)
        {
            homePosition = ik.target.position;
            homeRotation = ik.target.rotation;
            homeInitialized = true;
        }

        Transform t = ik.target;

        // 박스의 회전 방향에 맞춘 Top-Down 자세
        Vector3 approachForward = parcel.transform.forward;
        approachForward.y = 0f;

        if (approachForward == Vector3.zero)
        {
            approachForward = Vector3.forward;
        }

        Quaternion alignedTopDownRot =
            Quaternion.LookRotation(
                Vector3.down,
                approachForward
            );

        Collider parcelCol = parcel.GetComponent<Collider>();

        if (!parcelCol)
        {
            Debug.LogError(
                "[GripperTest] 상자에 Collider가 없습니다!"
            );

            yield break;
        }

        Bounds b = parcelCol.bounds;

        Vector3 top = new Vector3(
            b.center.x,
            b.max.y,
            b.center.z
        );

        Vector3 place = placePoint
            ? placePoint.position
            : top + new Vector3(0.5f, 0f, 0f);

        place.y = (
            placePoint
                ? placePoint.position.y
                : b.min.y
        ) + b.size.y;

        bool graspSuccess = false;

        int attempts = Mathf.Max(1, maxRetry);

        // 최대 3회 파지 시도
        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            Debug.Log(
                $"[GripperTest] 파지 시도 {attempt}/{attempts} - 상단 접근 시작"
            );

            // 그리퍼 열기
            yield return StartCoroutine(gripper.Open());

            // 박스 상단 접근
            yield return StartCoroutine(
                Move(
                    t,
                    top + Vector3.up * approachHeight,
                    alignedTopDownRot
                )
            );

            Debug.Log("[GripperTest] 파지 위치로 하강");

            // 파지 위치까지 하강
            yield return StartCoroutine(
                Move(
                    t,
                    new Vector3(
                        top.x,
                        top.y + gripHeightOffset,
                        top.z
                    ),
                    alignedTopDownRot,
                    0.3f
                )
            );

            Debug.Log(
                "[GripperTest] 그리퍼 닫기 (Close 코루틴 실행)"
            );

            // 박스 파지 시도
            yield return StartCoroutine(gripper.Close());

            bool isActuallyGrabbed = gripper.LastGraspOk;

            // 테스트용 인위적 실패 확률
            if (
                isActuallyGrabbed &&
                failureProbability > 0f &&
                Random.value < failureProbability
            )
            {
                Debug.LogWarning(
                    $"[GripperTest] 시뮬레이션 파지 실패! " +
                    $"설정 확률: {failureProbability * 100f:F0}%"
                );

                isActuallyGrabbed = false;
            }

            Debug.Log(
                "[GripperTest] 파지 결과: " +
                (isActuallyGrabbed ? "성공" : "실패")
            );

            if (isActuallyGrabbed)
            {
                Debug.Log("[GripperTest] 파지 성공!");

                graspSuccess = true;
                break;
            }

            // 파지 실패 시 재시도
            Debug.LogWarning(
                $"[GripperTest] 파지 실패 ({attempt}/{attempts}). " +
                "그리퍼를 열고 상단으로 복귀합니다."
            );

            yield return StartCoroutine(gripper.Open());

            yield return StartCoroutine(
                Move(
                    t,
                    top + Vector3.up * approachHeight,
                    alignedTopDownRot
                )
            );

            yield return new WaitForSeconds(0.3f);
        }

        // 3회 모두 파지 실패
        if (!graspSuccess)
        {
            Debug.LogWarning(
                $"[GripperTest] {attempts}회 모두 파지 실패! " +
                "최종 실패 처리합니다."
            );

            yield return StartCoroutine(gripper.Open());

            // 대시보드 실패 기록
            if (dashboardStats != null)
            {
                dashboardStats.RegisterFailure(parcel);
            }
            else
            {
                Debug.LogWarning(
                    "[GripperTest] Dashboard Stats가 연결되지 않아 " +
                    "실패를 기록하지 못했습니다."
                );
            }

            // 홈 복귀
            yield return StartCoroutine(
                Move(
                    t,
                    homePosition,
                    homeRotation
                )
            );

            yield break;
        }

        // 파지 성공 후 기존 이송 동작 유지
        Debug.Log(
            "[GripperTest] 4. 상단으로 이동 후 이송"
        );

        yield return StartCoroutine(
            Move(
                t,
                top + Vector3.up * approachHeight,
                alignedTopDownRot,
                0.5f
            )
        );

        yield return StartCoroutine(
            Move(
                t,
                place + Vector3.up * approachHeight,
                alignedTopDownRot
            )
        );

        Debug.Log(
            "[GripperTest] 5. 놓을 위치로 하강"
        );

        yield return StartCoroutine(
            Move(
                t,
                new Vector3(
                    place.x,
                    place.y + gripHeightOffset,
                    place.z
                ),
                alignedTopDownRot,
                0.3f
            )
        );

        Debug.Log(
            "[GripperTest] 6. 그리퍼 열기 (Open 코루틴 실행)"
        );

        // 박스 내려놓기
        yield return StartCoroutine(gripper.Open());

        Debug.Log("[GripperTest] 7. 홈 복귀");

        yield return StartCoroutine(
            Move(
                t,
                place + Vector3.up * approachHeight,
                alignedTopDownRot
            )
        );

        yield return StartCoroutine(
            Move(
                t,
                homePosition,
                homeRotation
            )
        );

        // 이송 동작 완료
        // 실제 성공 카운트는 FoodCountSensor에서 처리
        lastRunSucceeded = true;

        Debug.Log("[GripperTest] 공정 완료");
    }

    private IEnumerator Move(
        Transform t,
        Vector3 to,
        Quaternion rot,
        float speedScale = 1f
    )
    {
        if (t == null)
            yield break;

        Vector3 from = t.position;
        Quaternion r0 = t.rotation;

        float dist = Vector3.Distance(from, to);

        float safeSpeed =
            Mathf.Max(0.001f, speed * speedScale);

        float dur = Mathf.Max(
            0.3f,
            dist / safeSpeed
        );

        for (
            float s = 0f;
            s < 1f;
            s += Time.fixedDeltaTime / dur
        )
        {
            if (t == null)
                yield break;

            float k = Mathf.SmoothStep(
                0f,
                1f,
                s
            );

            t.position = Vector3.Lerp(
                from,
                to,
                k
            );

            t.rotation = Quaternion.Slerp(
                r0,
                rot,
                k
            );

            yield return new WaitForFixedUpdate();
        }

        if (t != null)
        {
            t.position = to;
            t.rotation = rot;
        }

        yield return new WaitForSeconds(0.2f);
    }
}