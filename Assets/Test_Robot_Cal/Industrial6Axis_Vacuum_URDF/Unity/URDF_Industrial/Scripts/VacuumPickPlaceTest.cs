using System.Collections;
using UnityEngine;

/// <summary>
/// 흡착 자동 테스트: 박스 위로 접근 -> 내려가서 흡착 -> 들어올림 -> 놓을 위치로 이동 -> 내려놓고 해제 -> 복귀
/// 아무 오브젝트(빈 GameObject 등)에 붙이고 Inspector에서 연결 후 Play, 또는 Play 중 우클릭 메뉴 "Run Test".
/// </summary>
public class VacuumPickPlaceTest : MonoBehaviour
{
    public Industrial6AxisIK ik;
    public VacuumGripper vacuum;
    public Rigidbody parcel;               // 집을 택배 (Rigidbody + BoxCollider)
    public Transform placePoint;           // 내려놓을 위치 (바닥/선반 위 지점). 비우면 박스 옆 0.5 m
    public bool runOnStart = true;

    [Header("동작")]
    public float speed = 0.5f;             // m/s
    public float approachHeight = 0.25f;   // 박스 윗면 위 접근 높이
    public float pressDepth = 0.005f;      // 윗면보다 살짝 더 눌러 접촉 확보

    IEnumerator Start()
    {
        yield return new WaitForSeconds(0.5f);   // 물리 안정화
        if (runOnStart) yield return Run();
    }

    [ContextMenu("Run Test")]
    public void RunTest() => StartCoroutine(Run());

    IEnumerator Run()
    {
        if (!ik || !vacuum || !parcel || !ik.target) { Debug.LogError("[Test] ik / vacuum / parcel / ik.target 연결 필요"); yield break; }
        Transform t = ik.target;
        Quaternion down = Quaternion.LookRotation(Vector3.down, Vector3.forward); // 흡착 방향(파란 화살표) = 아래
        Vector3 home = t.position;

        Bounds b = parcel.GetComponent<Collider>().bounds;
        Vector3 top = new Vector3(b.center.x, b.max.y, b.center.z);
        Vector3 place = placePoint ? placePoint.position : top + new Vector3(0.5f, 0, 0);
        place.y = (placePoint ? placePoint.position.y : b.min.y) + b.size.y;   // 놓을 때 윗면 높이

        Debug.Log("[Test] 1. 박스 위 접근");
        yield return Move(t, top + Vector3.up * approachHeight, down);
        Debug.Log("[Test] 2. 하강");
        yield return Move(t, top - Vector3.up * pressDepth, down, 0.3f);
        Debug.Log("[Test] 3. 흡착 ON");
        vacuum.vacuumOn = true;
        yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
        Debug.Log("[Test] 결과: " + vacuum.lastResult);
        if (!vacuum.holding) { vacuum.vacuumOn = false; yield return Move(t, top + Vector3.up * approachHeight, down); yield break; }

        Debug.Log("[Test] 4. 들어올림 -> 이동");
        yield return Move(t, top + Vector3.up * approachHeight, down, 0.5f);
        yield return Move(t, place + Vector3.up * approachHeight, down);
        Debug.Log("[Test] 5. 내려놓기");
        yield return Move(t, place + Vector3.up * 0.01f, down, 0.3f);
        vacuum.vacuumOn = false;
        yield return new WaitForSeconds(0.3f);
        Debug.Log("[Test] 6. 복귀. 박스 최종 위치: " + parcel.position);
        yield return Move(t, place + Vector3.up * approachHeight, down);
        yield return Move(t, home, down);
        Debug.Log("[Test] 완료");
    }

    IEnumerator Move(Transform t, Vector3 to, Quaternion rot, float speedScale = 1f)
    {
        Vector3 from = t.position; Quaternion r0 = t.rotation;
        float dur = Mathf.Max(0.3f, Vector3.Distance(from, to) / (speed * speedScale));
        for (float s = 0; s < 1f; s += Time.fixedDeltaTime / dur)
        {
            float k = Mathf.SmoothStep(0, 1, s);
            t.position = Vector3.Lerp(from, to, k);
            t.rotation = Quaternion.Slerp(r0, rot, k);
            yield return new WaitForFixedUpdate();
        }
        t.position = to; t.rotation = rot;
        yield return new WaitForSeconds(0.2f);   // 로봇이 따라올 시간
        if (!ik.reachable) Debug.LogWarning("[Test] 도달 불가 위치: " + to);
    }
}
