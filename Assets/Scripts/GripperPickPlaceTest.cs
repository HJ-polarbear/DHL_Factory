using System.Collections;
using UnityEngine;

/// <summary>
/// 집게형(Gripper) 자동 Pick and Place 테스트: 
/// 박스 위로 접근 -> 내려가서 그리퍼 닫기 -> 들어올림 -> 놓을 위치로 이동 -> 내려놓고 그리퍼 열기 -> 복귀
/// </summary>
public class GripperPickPlaceTest : MonoBehaviour
{
    [Header("연결 컴포넌트")]
    public Industrial6AxisIK ik;
    public IndustrialGripperController gripper; // 앞서 만든 집게 제어 스크립트 연결
    public Rigidbody parcel;               // 집을 택배 (Rigidbody + BoxCollider)
    public Transform placePoint;           // 내려놓을 위치 (비우면 박스 옆 0.5m)
    public bool runOnStart = true;

    [Header("동작 설정")]
    public float speed = 0.5f;             // m/s
    public float approachHeight = 0.25f;   // 박스 윗면 위 접근 높이
    public float gripHeightOffset = 0.02f; // 박스 측면/상단 파지 높이 조절

    [Header("그리퍼 개폐 설정 값")]
    public float gripOpenWidth = 0.3f;     // 열었을 때의 opening 값
    public float gripCloseWidth = 0.05f;   // 닫았을 때(물체를 잡았을 때)의 opening 값

    IEnumerator Start()
    {
        yield return new WaitForSeconds(0.5f);   // 물리 안정화 대기

        // 초기화: 시작할 때 그리퍼 열어두기
        if (gripper != null) gripper.opening = gripOpenWidth;

        if (runOnStart) yield return Run();
    }

    [ContextMenu("Run Test")]
    public void RunTest() => StartCoroutine(Run());

    IEnumerator Run()
    {
        if (!ik || !gripper || !parcel || !ik.target)
        {
            Debug.LogError("[Test] ik / gripper / parcel / ik.target 연결이 필요합니다!");
            yield break;
        }

        Transform t = ik.target;

        // Top-Down 고정 회전값 설정
        Quaternion topDownRot = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        Vector3 home = t.position;

        // 택배 상자의 바운즈 계산
        Bounds b = parcel.GetComponent<Collider>().bounds;
        Vector3 top = new Vector3(b.center.x, b.max.y, b.center.z);
        Vector3 place = placePoint ? placePoint.position : top + new Vector3(0.5f, 0, 0);
        place.y = (placePoint ? placePoint.position.y : b.min.y) + b.size.y;

        Debug.Log("[Test] 1. 박스 상단 접근 시작 (그리퍼 오픈 상태)");
        gripper.opening = gripOpenWidth;
        yield return Move(t, top + Vector3.up * approachHeight, topDownRot);

        Debug.Log("[Test] 2. 하강 시작 (파지 위치)");
        yield return Move(t, top + Vector3.up * gripHeightOffset, topDownRot, 0.3f);

        Debug.Log("[Test] 3. 그리퍼 닫기 (물체 파지)");
        gripper.opening = gripCloseWidth;
        yield return new WaitForSeconds(0.8f); // 물체를 단단히 잡을 때까지 물리 대기 시간 부여

        Debug.Log("[Test] 4. 상단으로 들어올림 -> 이송 위치로 이동");
        yield return Move(t, top + Vector3.up * approachHeight, topDownRot, 0.5f);
        yield return Move(t, place + Vector3.up * approachHeight, topDownRot);

        Debug.Log("[Test] 5. 내려놓을 위치로 하강");
        yield return Move(t, place + Vector3.up * gripHeightOffset, topDownRot, 0.3f);

        Debug.Log("[Test] 6. 그리퍼 열기 (물체 놓기)");
        gripper.opening = gripOpenWidth;
        yield return new WaitForSeconds(0.5f); // 물체가 놓이는 안정화 대기 시간

        Debug.Log("[Test] 7. 홈(Home) 복귀. 박스 최종 위치: " + parcel.position);
        yield return Move(t, place + Vector3.up * approachHeight, topDownRot);
        yield return Move(t, home, topDownRot);

        Debug.Log("[Test] 전체 집게 공정 완료!");
    }

    /// <summary>
    /// 타겟의 위치와 회전을 부드럽게 보간하며 이동시키는 함수
    /// </summary>
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
        yield return new WaitForSeconds(0.2f);   // 로봇이 따라올 안정화 시간

        if (!ik.reachable)
            Debug.LogWarning("[Test] 경고: 도달 불가(Unreachable) 위치입니다 -> " + to);
    }
}