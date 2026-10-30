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

        Debug.Log("[GripperTest] 1. 상단 접근 시작");
        yield return StartCoroutine(gripper.Open());
        yield return StartCoroutine(Move(t, top + Vector3.up * approachHeight, alignedTopDownRot));

        Debug.Log("[GripperTest] 2. 파지 위치로 하강");
        yield return StartCoroutine(Move(t, new Vector3(top.x, top.y + gripHeightOffset, top.z), alignedTopDownRot, 0.3f));

        Debug.Log("[GripperTest] 3. 그리퍼 닫기 (Close 코루틴 실행)");
        yield return StartCoroutine(gripper.Close()); // 직접 대입 대신 Close() 코루틴 호출

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