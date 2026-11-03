using System.Collections;
using UnityEngine;

/// <summary>
/// 흡착 로봇 자동 분류
/// 박스 접근 → 흡착 → 이동 → 내려놓기 → 홈 복귀
/// 최대 3회 흡착 재시도
/// 3회 모두 실패하면 대시보드에 최종 실패 기록
/// </summary>
public class VacuumPickPlaceTestVol2 : MonoBehaviour
{
    [Header("연결 컴포넌트")]
    public Industrial6AxisIK ik;
    public VacuumGripper vacuum;
    public Rigidbody parcel;
    public Transform placePoint;
    public bool runOnStart = true;

    [Header("대시보드 연결")]
    public DashboardStatsController dashboardStats;

    [Header("동작 설정")]
    public float speed = 0.5f;
    public float approachHeight = 0.25f;
    public float pressDepth = 0.005f;

    [Header("테스트 실패 시뮬레이션")]
    [Tooltip("0 = 항상 정상 판정, 1 = 물리적으로 흡착해도 항상 실패 처리")]
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
        // 매 작업 시작 시 결과 초기화
        lastRunSucceeded = false;

        if (!ik || !vacuum || !parcel || !ik.target)
        {
            Debug.LogError(
                "[VacuumTest] ik / vacuum / parcel / ik.target 연결이 필요합니다!"
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

        Collider parcelCollider = parcel.GetComponent<Collider>();

        if (parcelCollider == null)
        {
            Debug.LogError(
                "[VacuumTest] 상자에 Collider가 없습니다!"
            );

            yield break;
        }

        Bounds b = parcelCollider.bounds;

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

        bool grabSuccess = false;

        int attempts = Mathf.Max(1, maxRetry);

        // 흡착 최대 3회 시도
        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            Debug.Log(
                $"[VacuumTest] 흡착 시도 {attempt}/{attempts} - 상단 접근 시작"
            );

            yield return Move(
                t,
                top + Vector3.up * approachHeight,
                alignedTopDownRot
            );

            Debug.Log("[VacuumTest] 하강 시작");

            yield return Move(
                t,
                top - Vector3.up * pressDepth,
                alignedTopDownRot,
                0.3f
            );

            Debug.Log("[VacuumTest] 흡착(Vacuum) ON");

            vacuum.vacuumOn = true;

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            bool isActuallyHolding = vacuum.holding;

            // 실패 확률 시뮬레이션
            if (
                isActuallyHolding &&
                failureProbability > 0f &&
                Random.value < failureProbability
            )
            {
                Debug.LogWarning(
                    $"[VacuumTest] 시뮬레이션 흡착 실패! " +
                    $"설정 확률: {failureProbability * 100f:F0}%"
                );

                isActuallyHolding = false;
            }

            Debug.Log(
                "[VacuumTest] 흡착 결과: " +
                (isActuallyHolding ? "성공" : "실패")
            );

            if (isActuallyHolding)
            {
                Debug.Log("[VacuumTest] 흡착 성공!");

                grabSuccess = true;
                break;
            }

            Debug.LogWarning(
                $"[VacuumTest] 흡착 실패 ({attempt}/{attempts}). 재시도합니다."
            );

            vacuum.vacuumOn = false;

            yield return Move(
                t,
                top + Vector3.up * approachHeight,
                alignedTopDownRot
            );

            yield return new WaitForSeconds(0.3f);
        }

        // 모든 흡착 시도 실패
        if (!grabSuccess)
        {
            Debug.LogWarning(
                $"[VacuumTest] {attempts}회 모두 흡착 실패! 최종 실패 처리합니다."
            );

            vacuum.vacuumOn = false;

            // 대시보드 최종 실패 기록
            if (dashboardStats != null)
            {
                dashboardStats.RegisterFailure(parcel);
            }
            else
            {
                Debug.LogWarning(
                    "[VacuumTest] Dashboard Stats가 연결되지 않아 실패를 기록하지 못했습니다."
                );
            }

            yield return Move(
                t,
                homePosition,
                homeRotation
            );

            yield break;
        }

        // 흡착 성공 후 기존 이송 동작
        Debug.Log(
            "[VacuumTest] 4. 상단으로 들어올림 -> 이송 위치로 이동"
        );

        yield return Move(
            t,
            top + Vector3.up * approachHeight,
            alignedTopDownRot,
            0.5f
        );

        yield return Move(
            t,
            place + Vector3.up * approachHeight,
            alignedTopDownRot
        );

        Debug.Log(
            "[VacuumTest] 5. 내려놓을 위치로 하강"
        );

        yield return Move(
            t,
            place + Vector3.up * 0.01f,
            alignedTopDownRot,
            0.3f
        );

        Debug.Log(
            "[VacuumTest] 6. 흡착(Vacuum) OFF (놓기)"
        );

        vacuum.vacuumOn = false;

        yield return new WaitForSeconds(0.3f);

        Debug.Log(
            "[VacuumTest] 7. 지정된 홈 복귀."
        );

        yield return Move(
            t,
            place + Vector3.up * approachHeight,
            alignedTopDownRot
        );

        yield return Move(
            t,
            homePosition,
            homeRotation
        );

        // 로봇의 이송 동작은 완료됨.
        // 실제 분류 성공 카운트는 벨트 센서에서 처리함.
        lastRunSucceeded = true;

        Debug.Log(
            "[VacuumTest] 전체 흡착 공정 완료!"
        );
    }

    private IEnumerator Move(
        Transform t,
        Vector3 to,
        Quaternion rot,
        float speedScale = 1f
    )
    {
        Vector3 from = t.position;
        Quaternion r0 = t.rotation;

        float safeSpeed =
            Mathf.Max(0.001f, speed * speedScale);

        float dur = Mathf.Max(
            0.3f,
            Vector3.Distance(from, to) / safeSpeed
        );

        for (
            float s = 0f;
            s < 1f;
            s += Time.fixedDeltaTime / dur
        )
        {
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

        t.position = to;
        t.rotation = rot;

        yield return new WaitForSeconds(0.2f);

        if (!ik.reachable)
        {
            Debug.LogWarning(
                "[VacuumTest] 도달 불가 위치: " + to
            );
        }
    }
}