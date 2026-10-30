using System.Collections;
using UnityEngine;

/// <summary>
/// 흡착 자동 테스트 (Top-Down 인형뽑기 방식 고정): 
/// 박스 위로 접근 -> 내려가서 흡착 -> 들어올림 -> 놓을 위치로 이동 -> 내려놓고 해제 -> 복귀
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

    IEnumerator Start()
    {
        yield return new WaitForSeconds(0.5f);   // 물리 안정화 대기
        if (runOnStart) yield return Run();
    }

    [ContextMenu("Run Test")]
    public void RunTest() => StartCoroutine(Run());

    IEnumerator Run()
    {
        if (!ik || !vacuum || !parcel || !ik.target)
        {
            Debug.LogError("[Test] ik / vacuum / parcel / ik.target 연결이 필요합니다!");
            yield break;
        }

        Transform t = ik.target;

        // [보완 포인트] 위에서 아래를 내려다보는(Top-Down) 고정 회전값 설정
        // 파란색 화살표(+Z, 접근 방향)는 아래(Vector3.down), 초록색 화살표(+Y, 윗방향)는 전방(Vector3.forward)으로 고정하여 손목 뒤집힘 방지
        Quaternion topDownRot = Quaternion.LookRotation(Vector3.down, Vector3.forward);

        Vector3 home = t.position;

        // 택배 상자의 바운즈 계산
        Bounds b = parcel.GetComponent<Collider>().bounds;
        Vector3 top = new Vector3(b.center.x, b.max.y, b.center.z);
        Vector3 place = placePoint ? placePoint.position : top + new Vector3(0.5f, 0, 0);
        place.y = (placePoint ? placePoint.position.y : b.min.y) + b.size.y;

        Debug.Log("[Test] 1. 박스 상단 접근 시작");
        yield return Move(t, top + Vector3.up * approachHeight, topDownRot);

        Debug.Log("[Test] 2. 하강 시작");
        yield return Move(t, top - Vector3.up * pressDepth, topDownRot, 0.3f);

        Debug.Log("[Test] 3. 흡착(Vacuum) ON");
        vacuum.vacuumOn = true;
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Debug.Log("[Test] 흡착 결과: " + vacuum.lastResult);
        if (!vacuum.holding)
        {
            Debug.LogWarning("[Test] 흡착 실패로 인해 복귀합니다.");
            vacuum.vacuumOn = false;
            yield return Move(t, top + Vector3.up * approachHeight, topDownRot);
            yield break;
        }

        Debug.Log("[Test] 4. 상단으로 들어올림 -> 이송 위치로 이동");
        yield return Move(t, top + Vector3.up * approachHeight, topDownRot, 0.5f);
        yield return Move(t, place + Vector3.up * approachHeight, topDownRot);

        Debug.Log("[Test] 5. 내려놓을 위치로 하강");
        yield return Move(t, place + Vector3.up * 0.01f, topDownRot, 0.3f);

        Debug.Log("[Test] 6. 흡착(Vacuum) OFF (놓기)");
        vacuum.vacuumOn = false;
        yield return new WaitForSeconds(0.3f);

        Debug.Log("[Test] 7. 홈(Home) 복귀. 박스 최종 위치: " + parcel.position);
        yield return Move(t, place + Vector3.up * approachHeight, topDownRot);
        yield return Move(t, home, topDownRot);

        Debug.Log("[Test] 전체 공정 완료!");
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