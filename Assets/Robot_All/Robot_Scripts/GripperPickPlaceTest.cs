using System.Collections;
using UnityEngine;

public class GripperPickPlaceTest : MonoBehaviour
{
    [Header("연결 컴포넌트")]
    public Industrial6AxisIK ik;
    public IndustrialGripperController gripper; // GripperController 타입으로 연결
    public Rigidbody parcel;
    public Transform placePoint;
    public bool runOnStart = true;

    [Header("동작 설정")]
    public float speed = 0.5f;
    public float approachHeight = 0.25f;

    [Header("파지 깊이 조절")]
    public float gripHeightOffset = -0.1f;

    [Header("테스트 실패 시뮬레이션 설정")]
    [Tooltip("임의로 파지를 실패하게 만들 확률 (0.0 = 항상 성공, 1.0 = 항상 실패)")]
    [Range(0f, 1f)] public float failureProbability = 0.0f;

    private Vector3 homePosition;
    private Quaternion homeRotation;
    private bool homeInitialized = false;

    IEnumerator Start()
    {
        yield return new WaitForSeconds(1.0f);

        if (ik != null && ik.target != null)
        {
            homePosition = ik.target.position;
            homeRotation = ik.target.rotation;
            homeInitialized = true;
        }

        if (gripper != null) yield return StartCoroutine(gripper.Open());

        if (runOnStart) yield return StartCoroutine(Run());
    }

    [ContextMenu("Run Test")]
    public void RunTest() => StartCoroutine(Run());

    public IEnumerator Run()
    {
        if (!ik || !gripper || !parcel || !ik.target)
        {
            Debug.LogError("[GripperTest] ik / gripper / parcel / ik.target 중 연결되지 않은 항목이 있습니다!");
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
        approachForward.y = 0;
        if (approachForward == Vector3.zero) approachForward = Vector3.forward;

        Quaternion alignedTopDownRot = Quaternion.LookRotation(Vector3.down, approachForward);

        Collider parcelCol = parcel.GetComponent<Collider>();
        if (!parcelCol)
        {
            Debug.LogError("[GripperTest] 상자에 Collider가 없습니다!");
            yield break;
        }

        Bounds b = parcelCol.bounds;
        Vector3 top = new Vector3(b.center.x, b.max.y, b.center.z);
        Vector3 place = placePoint ? placePoint.position : top + new Vector3(0.5f, 0, 0);
        place.y = (placePoint ? placePoint.position.y : b.min.y) + b.size.y;

        bool graspSuccess = false;
        int maxRetry = 3;

        //Debug.Log("[GripperTest] 1. 상단 접근 시작");
        //yield return StartCoroutine(gripper.Open());
        //yield return StartCoroutine(Move(t, top + Vector3.up * approachHeight, alignedTopDownRot));

        //Debug.Log("[GripperTest] 2. 파지 위치로 하강");
        //yield return StartCoroutine(Move(t, new Vector3(top.x, top.y + gripHeightOffset, top.z), alignedTopDownRot, 0.3f));

        //Debug.Log("[GripperTest] 3. 그리퍼 닫기 (Close 코루틴 실행)");
        //yield return StartCoroutine(gripper.Close()); // 직접 대입 대신 Close() 코루틴 호출

        //// [추가된 부분] 파지 성공 여부 검사 (LastGraspOk 활용)
        //Debug.Log("[GripperTest] 파지 결과: " + gripper.LastGraspInfo);
        //if (!gripper.LastGraspOk)
        //{
        //    Debug.LogWarning("[GripperTest] 파지(잡기) 실패로 인해 안전하게 홈(Home)으로 복귀합니다.");
        //    yield return StartCoroutine(gripper.Open()); // 손가락 열기
        //    yield return StartCoroutine(Move(t, top + Vector3.up * approachHeight, alignedTopDownRot)); // 상단으로 빠져나오기
        //    yield return StartCoroutine(Move(t, homePosition, homeRotation)); // 홈 복귀
        //    yield break; // 공정 중단
        //}
        // [변경] 최대 3회 파지 시도 루프 (실패 시 상단 복귀 후 재시도)
        for (int attempt = 1; attempt <= maxRetry; attempt++)
        {
            Debug.Log($"[GripperTest] 파지 시도 {attempt}/{maxRetry} - 상단 접근 시작");
            yield return StartCoroutine(gripper.Open());
            yield return StartCoroutine(Move(t, top + Vector3.up * approachHeight, alignedTopDownRot));

            Debug.Log("[GripperTest] 파지 위치로 하강");
            yield return StartCoroutine(Move(t, new Vector3(top.x, top.y + gripHeightOffset, top.z), alignedTopDownRot, 0.3f));

            Debug.Log("[GripperTest] 그리퍼 닫기 (Close 코루틴 실행)");
            yield return StartCoroutine(gripper.Close());

            // 기본 물리 판정 결과 확인
            bool isActuallyGrabbed = gripper.LastGraspOk;

            // [추가] 임의 실패 확률 시뮬레이션 적용
            if (isActuallyGrabbed && failureProbability > 0f && Random.value < failureProbability)
            {
                Debug.LogWarning($"[GripperTest] (시뮬레이션) 설정된 확률({failureProbability * 100}%)에 의해 의도적인 파지 실패가 발생했습니다!");
                isActuallyGrabbed = false;
            }

            Debug.Log("[GripperTest] 파지 결과: " + (isActuallyGrabbed ? "성공" : "실패"));

            if (isActuallyGrabbed)
            {
                Debug.Log("[GripperTest] 파지 성공!");
                graspSuccess = true;
                break; // 성공 시 반복문 탈출 후 정상 이송 공정으로 진행
            }
            else
            {
                Debug.LogWarning($"[GripperTest] 파지 실패 (시도 {attempt}/{maxRetry}). 그리퍼를 열고 상단으로 복귀 후 재시도합니다.");
                yield return StartCoroutine(gripper.Open());
                yield return StartCoroutine(Move(t, top + Vector3.up * approachHeight, alignedTopDownRot));
                yield return new WaitForSeconds(0.3f);
            }
        }

        // [변경] 3번 모두 실패했을 때 처리 (컨베이어 작동 및 홈 복귀)
        if (!graspSuccess)
        {
            Debug.LogWarning("[GripperTest] 3회 시도 모두 파지 실패. 컨베이어를 작동시키고 홈으로 복귀합니다.");
            yield return StartCoroutine(gripper.Open());

            // TODO: 컨베이어 작동 코드가 있다면 이곳에 추가하세요.
            // 예: ConveyorController.Instance.StartConveyor();

            yield return StartCoroutine(Move(t, homePosition, homeRotation));
            yield break; // 공정 중단
        }

        Debug.Log("[GripperTest] 4. 상단으로 이동 후 이송");
        yield return StartCoroutine(Move(t, top + Vector3.up * approachHeight, alignedTopDownRot, 0.5f));
        yield return StartCoroutine(Move(t, place + Vector3.up * approachHeight, alignedTopDownRot));

        Debug.Log("[GripperTest] 5. 놓을 위치로 하강");
        yield return StartCoroutine(Move(t, new Vector3(place.x, place.y + gripHeightOffset, place.z), alignedTopDownRot, 0.3f));

        Debug.Log("[GripperTest] 6. 그리퍼 열기 (Open 코루틴 실행)");
        yield return StartCoroutine(gripper.Open()); // 직접 대입 대신 Open() 코루틴 호출

        Debug.Log("[GripperTest] 7. 홈 복귀");
        yield return StartCoroutine(Move(t, place + Vector3.up * approachHeight, alignedTopDownRot));
        yield return StartCoroutine(Move(t, homePosition, homeRotation));

        Debug.Log("[GripperTest] 공정 완료");
    }

    IEnumerator Move(Transform t, Vector3 to, Quaternion rot, float speedScale = 1f)
    {
        if (t == null) yield break;

        Vector3 from = t.position;
        Quaternion r0 = t.rotation;
        float dist = Vector3.Distance(from, to);
        float dur = Mathf.Max(0.3f, dist / (speed * speedScale));

        for (float s = 0; s < 1f; s += Time.fixedDeltaTime / dur)
        {
            if (t == null) yield break;
            float k = Mathf.SmoothStep(0, 1, s);
            t.position = Vector3.Lerp(from, to, k);
            t.rotation = Quaternion.Slerp(r0, rot, k);
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