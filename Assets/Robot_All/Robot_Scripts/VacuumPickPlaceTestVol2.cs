using System.Collections;
using UnityEngine;

/// <summary>
/// 흡착 자동 테스트 (상자 회전 각도 연동 Top-Down 방식): 
/// 박스 위로 접근(회전 일치) -> 내려가서 흡착 -> 들어올림 -> 놓을 위치로 이동 -> 내려놓고 해제 -> 복귀
/// </summary>
public class VacuumPickPlaceTestVol2 : MonoBehaviour
{
    [Header("연결 컴포넌트")]
    public Industrial6AxisIK ik;
    public VacuumGripper vacuum;
    public Rigidbody parcel;               // 집을 택배 (Rigidbody + BoxCollider)
    public Transform placePoint;           // 내려놓을 위치 (비우면 박스 옆 0.5m)
    public bool runOnStart = true;

    [Header("동작 설정")]
    public float speed = 0.5f;             // m/s
    public float approachHeight = 0.25f;   // 박스 윗면 위 접근 높이
    public float pressDepth = 0.005f;      // 윗면보다 살짝 더 눌러 접촉 확보

    [Header("테스트 실패 시뮬레이션 설정")]
    [Tooltip("임의로 흡착을 실패하게 만들 확률 (0.0 = 항상 성공, 1.0 = 항상 실패)")]
    [Range(0f, 1f)] public float failureProbability = 0.0f;

    private Vector3 homePosition;
    private Quaternion homeRotation;
    private bool homeInitialized = false;

    IEnumerator Start()
    {
        yield return new WaitForSeconds(1.0f);   // 물리 안정화 및 초기 홈 자세 캡처 대기

        if (ik != null && ik.target != null)
        {
            homePosition = ik.target.position;
            homeRotation = ik.target.rotation;
            homeInitialized = true;
        }

        if (runOnStart) yield return StartCoroutine(Run());
    }

    [ContextMenu("Run Test")]
    public void RunTest() => StartCoroutine(Run());

    public IEnumerator Run()
    {
        if (!ik || !vacuum || !parcel || !ik.target)
        {
            Debug.LogError("[VacuumTest] ik / vacuum / parcel / ik.target 연결이 필요합니다!");
            yield break;
        }

        if (!homeInitialized)
        {
            homePosition = ik.target.position;
            homeRotation = ik.target.rotation;
            homeInitialized = true;
        }

        Transform t = ik.target;

        // [핵심 변경] 상자의 회전(Yaw 등)을 반영한 Top-Down 회전 계산
        Vector3 approachForward = parcel.transform.forward;
        approachForward.y = 0; // 수평 성분만 추출하여 기울어짐 방지
        if (approachForward == Vector3.zero) approachForward = Vector3.forward;

        Quaternion alignedTopDownRot = Quaternion.LookRotation(Vector3.down, approachForward);

        // 택배 상자의 바운즈 계산
        Bounds b = parcel.GetComponent<Collider>().bounds;
        Vector3 top = new Vector3(b.center.x, b.max.y, b.center.z);
        Vector3 place = placePoint ? placePoint.position : top + new Vector3(0.5f, 0, 0);
        place.y = (placePoint ? placePoint.position.y : b.min.y) + b.size.y;

        //Debug.Log("[VacuumTest] 1. 상자 회전 각도 동기화 및 상단 접근 시작");
        //yield return Move(t, top + Vector3.up * approachHeight, alignedTopDownRot);

        //Debug.Log("[VacuumTest] 2. 하강 시작");
        //yield return Move(t, top - Vector3.up * pressDepth, alignedTopDownRot, 0.3f);

        //Debug.Log("[VacuumTest] 3. 흡착(Vacuum) ON");
        //vacuum.vacuumOn = true;
        //yield return new WaitForFixedUpdate();
        //yield return new WaitForFixedUpdate();

        //Debug.Log("[VacuumTest] 흡착 결과: " + vacuum.lastResult);
        //if (!vacuum.holding)
        //{
        //    Debug.LogWarning("[VacuumTest] 흡착 실패로 인해 복귀합니다.");
        //    vacuum.vacuumOn = false;
        //    yield return Move(t, top + Vector3.up * approachHeight, alignedTopDownRot);
        //    yield return Move(t, homePosition, homeRotation);
        //    yield break;
        //}
        // ==========================================
        // [수정] 흡착 최대 3회 시도 로직
        // ==========================================
        //Debug.Log("[VacuumTest] 3. 흡착(Vacuum) ON 시도 시작");

        //bool grabSuccess = false;
        //int maxRetry = 3;

        //for (int attempt = 1; attempt <= maxRetry; attempt++)
        //{
        //    Debug.Log($"[VacuumTest] 흡착 시도 {attempt}/{maxRetry}");
        //    vacuum.vacuumOn = true;
        //    yield return new WaitForFixedUpdate();
        //    yield return new WaitForFixedUpdate();

        //    Debug.Log("[VacuumTest] 흡착 결과: " + vacuum.lastResult);

        //    if (vacuum.holding)
        //    {
        //        Debug.Log("[VacuumTest] 흡착 성공!");
        //        grabSuccess = true;
        //        break; // 성공 시 반복문 탈출 후 정상 이송 공정으로 진행
        //    }
        //    else
        //    {
        //        Debug.LogWarning($"[VacuumTest] 흡착 실패 (시도 {attempt}/{maxRetry}). 잠시 대기 후 재시도합니다.");
        //        vacuum.vacuumOn = false;
        //        yield return new WaitForSeconds(0.3f);
        //    }
        //}

        //// 3번 모두 실패했을 때 처리
        //if (!grabSuccess)
        //{
        //    Debug.LogWarning("[VacuumTest] 3회 시도 모두 흡착 실패. 물품을 스킵하고 컨베이어를 작동시킵니다.");
        //    vacuum.vacuumOn = false;

        //    // TODO: 컨베이어 작동 코드가 있다면 이곳에 추가하세요.
        //    // 예시: ConveyorController.Instance.StartConveyor();

        //    // 상단으로 빠져나와 홈으로 복귀
        //    yield return Move(t, top + Vector3.up * approachHeight, alignedTopDownRot);
        //    yield return Move(t, homePosition, homeRotation);
        //    yield break; // 프로세스 종료 (다음 물품 처리로 연결)
        //}
        // 최대 3회 반복 (실패 시 상단 복귀 후 재시도)
        bool grabSuccess = false;
        int maxRetry = 3;

        // [추가] 최대 3회 흡착 시도 루프 (실패 시 상단 복귀 후 재시도)
        for (int attempt = 1; attempt <= maxRetry; attempt++)
        {
            Debug.Log($"[VacuumTest] 흡착 시도 {attempt}/{maxRetry} - 상단 접근 시작");
            yield return Move(t, top + Vector3.up * approachHeight, alignedTopDownRot);

            Debug.Log("[VacuumTest] 하강 시작");
            yield return Move(t, top - Vector3.up * pressDepth, alignedTopDownRot, 0.3f);

            Debug.Log("[VacuumTest] 흡착(Vacuum) ON");
            vacuum.vacuumOn = true;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            // 기본 물리 판정 결과 확인
            bool isActuallyHolding = vacuum.holding;

            // [추가] 임의 실패 확률 시뮬레이션 적용
            if (isActuallyHolding && failureProbability > 0f && Random.value < failureProbability)
            {
                Debug.LogWarning($"[VacuumTest] (시뮬레이션) 설정된 확률({failureProbability * 100}%)에 의해 의도적인 흡착 실패가 발생했습니다!");
                isActuallyHolding = false;
            }

            Debug.Log("[VacuumTest] 흡착 결과: " + (isActuallyHolding ? "성공" : "실패"));

            if (isActuallyHolding)
            {
                Debug.Log("[VacuumTest] 흡착 성공!");
                grabSuccess = true;
                break; // 성공 시 반복문 탈출 후 정상 이송 공정으로 진행
            }
            else
            {
                Debug.LogWarning($"[VacuumTest] 흡착 실패 (시도 {attempt}/{maxRetry}). 흡착을 끄고 상단으로 복귀 후 재시도합니다.");
                vacuum.vacuumOn = false;

                // 실패 시 상단으로 복귀
                yield return Move(t, top + Vector3.up * approachHeight, alignedTopDownRot);
                yield return new WaitForSeconds(0.3f);
            }
        }

        // 3번 모두 실패했을 때 처리 (컨베이어 작동 및 홈 복귀)
        if (!grabSuccess)
        {
            Debug.LogWarning("[VacuumTest] 3회 시도 모두 흡착 실패. 컨베이어를 작동시키고 홈으로 복귀합니다.");
            vacuum.vacuumOn = false;

            // TODO: 컨베이어 작동 코드가 있다면 아래에 추가하세요.
            // 예: ConveyorController.Instance.StartConveyor();

            yield return Move(t, homePosition, homeRotation);
            yield break; // 프로세스 종료
        }

        Debug.Log("[VacuumTest] 4. 상단으로 들어올림 -> 이송 위치로 이동");
        yield return Move(t, top + Vector3.up * approachHeight, alignedTopDownRot, 0.5f);
        yield return Move(t, place + Vector3.up * approachHeight, alignedTopDownRot);

        Debug.Log("[VacuumTest] 5. 내려놓을 위치로 하강");
        yield return Move(t, place + Vector3.up * 0.01f, alignedTopDownRot, 0.3f);

        Debug.Log("[VacuumTest] 6. 흡착(Vacuum) OFF (놓기)");
        vacuum.vacuumOn = false;
        yield return new WaitForSeconds(0.3f);

        Debug.Log("[VacuumTest] 7. 지정된 홈 복귀.");
        yield return Move(t, place + Vector3.up * approachHeight, alignedTopDownRot);
        yield return Move(t, homePosition, homeRotation);

        Debug.Log("[VacuumTest] 전체 흡착 공정 완료!");
    }

    IEnumerator Move(Transform t, Vector3 to, Quaternion rot, float speedScale = 1f)
    {
        Vector3 from = t.position;
        Quaternion r0 = t.rotation;
        float dur = Mathf.Max(0.3f, Vector3.Distance(from, to) / (speed * speedScale));

        for (float s = 0; s < 1f; s += Time.fixedDeltaTime / dur)
        {
            float k = Mathf.SmoothStep(0, 1, s);
            t.position = Vector3.Lerp(from, to, k);
            t.rotation = Quaternion.Slerp(r0, rot, k);
            yield return new WaitForFixedUpdate();
        }

        t.position = to;
        t.rotation = rot;
        yield return new WaitForSeconds(0.2f);

        if (!ik.reachable)
            Debug.LogWarning("[VacuumTest] 경고: 도달 불가(Unreachable) 위치입니다 -> " + to);
    }
}