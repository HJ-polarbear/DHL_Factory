using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CNN 비전 인식 결과로 구분할 물품 분류 유형
/// </summary>
public enum ParcelCategory
{
    Unknown,      // 미인식 / 해당 없음 (스토퍼를 열어서 통과시킴)
    SuctionType,  // 흡착형 그리퍼(Suction) 전용 물품 (예: 상자, 납작한 물품)
    GripperType   // 집게형 그리퍼(Finger Gripper) 전용 물품 (예: 원통, 불규칙 부품)
}

public class ParcelSortingSequence : MonoBehaviour
{
    #region [1. 시스템 컴포넌트 연결]
    [Header("=== 로봇 및 그리퍼 제어 ===")]
    [Tooltip("로봇 관절 역기구학 제어용 (ArmIK 스크립트)")]
    public ArmIK armIK;

    [Tooltip("집게형 그리퍼 제어용 (GripperController 스크립트)")]
    public GripperController fingerGripper;

    [Tooltip("흡착형 그리퍼 제어용 (실제 구현 방식에 따라 연결)")]
    public Transform suctionGripper;

    [Header("=== 컨베이어 및 스토퍼 제어 ===")]
    [Tooltip("물품 공급용 컨베이어 스크립트 또는 애니메이터")]
    public MonoBehaviour inputConveyor;

    [Tooltip("분류 목적지 컨베이어 위치 (흡착형 전용)")]
    public Transform destinationConveyorA;

    [Tooltip("분류 목적지 컨베이어 위치 (집게형 전용)")]
    public Transform destinationConveyorB;

    [Tooltip("스토퍼 오브젝트 (센서 위치에서 물품을 막아주는 물리 벽/메커니즘)")]
    public Transform stopper;
    public Vector3 stopperClosedPos; // 스토퍼 차단 위치
    public Vector3 stopperOpenPos;   // 스토퍼 열림 위치

    [Header("=== 위치 좌표 설정 (월드 Y, m) ===")]
    [Tooltip("센서 및 스토퍼 정지 위치 (CNN 판정이 이루어지는 장소)")]
    public Transform stopperStopPoint;

    [Tooltip("로봇 대기(Home) 위치")]
    public Vector3 homePosition = new Vector3(0.2f, 0.3f, 0f);

    [Tooltip("이동 및 접근 높이 여유값 (m)")]
    public float hoverHeight = 0.15f;
    public float approachSpeed = 0.1f;
    public float travelSpeed = 0.2f;
    #endregion

    #region [2. 상태 및 내부 변수]
    public bool IsBusy { get; private set; } = false;
    public string CurrentStatus { get; private set; } = "대기 중";

    private Rigidbody currentTargetParcel; // 현재 센서에 감지되어 작업 중인 물품
    #endregion

    #region [3. 초기화 및 대기]
    void Start()
    {
        if (armIK == null) armIK = GetComponent<ArmIK>();

        // 초기 대기 상태로 이송
        StartCoroutine(GoHome());
    }

    /// <summary>
    /// 로봇을 안전한 기본 대기(Home) 위치로 복귀시키는 코루틴
    /// </summary>
    public IEnumerator GoHome()
    {
        CurrentStatus = "대기 위치로 복귀 중";
        yield return armIK.MoveTo(homePosition, 0f);
        CurrentStatus = "대기 중";
    }
    #endregion

    #region [4. 시퀀스 메인 프로세스]
    /// <summary>
    /// 센서에 물품이 도착했을 때 외부(센서 스크립트 등)에서 호출하는 시퀀스 시작 함수
    /// </summary>
    public void OnParcelArrivedAtStopper(GameObject parcelObj)
    {
        if (IsBusy) return;

        currentTargetParcel = parcelObj.GetComponent<Rigidbody>();
        StartCoroutine(ProcessSortingSequence());
    }

    /// <summary>
    /// 분류 시퀀스 전체를 총괄하는 메인 코루틴
    /// </summary>
    private IEnumerator ProcessSortingSequence()
    {
        IsBusy = true;

        // Step 1. 컨베이어 일시 정지 (물품 정지)
        CurrentStatus = "1. 물품 정지 및 CNN 비전 검사 중";
        SetConveyorState(false);
        yield return new WaitForSeconds(0.3f); // 물품 안착 대기

        // Step 2. CNN 비전 인식 수행 (임시 Mockup - 추후 실제 CNN 연동 스크립트로 대체)
        ParcelCategory category = SimulateCNNInference(currentTargetParcel);
        Debug.Log($"[Sorting] CNN 인식 결과: {category}");

        // Step 3. 분류 조건에 따른 분기 처리
        switch (category)
        {
            case ParcelCategory.SuctionType:
                yield return ProcessSuctionTransfer(destinationConveyorA);
                break;

            case ParcelCategory.GripperType:
                yield return ProcessGripperTransfer(destinationConveyorB);
                break;

            case ParcelCategory.Unknown:
            default:
                // 어느 분류에도 해당하지 않는 경우: 스토퍼를 열어 통과시킴
                yield return ProcessPassThrough();
                break;
        }

        // Step 4. 로봇 복귀 및 컨베이어 재가동
        CurrentStatus = "4. 대기 위치 복귀 및 시스템 리셋";
        yield return GoHome();

        SetConveyorState(true); // 컨베이어 다시 작동
        IsBusy = false;
    }
    #endregion

    #region [5. 툴별 이송 및 통과 세부 로직]

    /// <summary>
    /// [흡착형 로봇 처리] 물품을 흡착하여 목적지 컨베이어 A로 이동
    /// </summary>
    private IEnumerator ProcessSuctionTransfer(Transform targetDest)
    {
        CurrentStatus = "2-A. 흡착형 이송 시작";
        Vector3 parcelPos = currentTargetParcel.position;

        // 1. 물품 상공 대기 위치로 이동
        Vector3 hoverPos = parcelPos + Vector3.up * hoverHeight;
        yield return armIK.MoveTo(hoverPos, 0f);

        // 2. 물품으로 접근 및 흡착 ON
        yield return armIK.MoveLinear(parcelPos, approachSpeed, 0f);
        EnableSuction(true, currentTargetParcel);

        // 3. 들어올려 목적지 컨베이어 A 상공으로 이동
        yield return armIK.MoveLinear(hoverPos, approachSpeed, 0f);
        Vector3 destHover = targetDest.position + Vector3.up * hoverHeight;
        yield return armIK.MoveLinear(destHover, travelSpeed, 0f);

        // 4. 목적지에 놓기 및 흡착 OFF
        yield return armIK.MoveLinear(targetDest.position, approachSpeed, 0f);
        EnableSuction(false, currentTargetParcel);
        yield return new WaitForSeconds(0.2f);

        // 5. 상공으로 이탈
        yield return armIK.MoveLinear(destHover, approachSpeed, 0f);
    }

    /// <summary>
    /// [집게형 로봇 처리] 물품을 그리퍼로 집어서 목적지 컨베이어 B로 이동
    /// </summary>
    private IEnumerator ProcessGripperTransfer(Transform targetDest)
    {
        CurrentStatus = "2-B. 집게형 이송 시작";
        Vector3 parcelPos = currentTargetParcel.position;

        // 1. 물품 상공 대기 위치로 이동
        Vector3 hoverPos = parcelPos + Vector3.up * hoverHeight;
        yield return armIK.MoveTo(hoverPos, 0f);

        // 2. 물품 접근 및 닫기(Grasp)
        yield return armIK.MoveLinear(parcelPos, approachSpeed, 0f);
        if (fingerGripper != null)
        {
            yield return fingerGripper.Close();
        }

        // 3. 들어올려 목적지 컨베이어 B 상공으로 이동
        yield return armIK.MoveLinear(hoverPos, approachSpeed, 0f);
        Vector3 destHover = targetDest.position + Vector3.up * hoverHeight;
        yield return armIK.MoveLinear(destHover, travelSpeed, 0f);

        // 4. 목적지에 놓기 및 그리퍼 열기(Release)
        yield return armIK.MoveLinear(targetDest.position, approachSpeed, 0f);
        if (fingerGripper != null)
        {
            yield return fingerGripper.Open();
        }
        yield return new WaitForSeconds(0.2f);

        // 5. 상공으로 이탈
        yield return armIK.MoveLinear(destHover, approachSpeed, 0f);
    }

    /// <summary>
    /// [예외/미해당 물품] 스토퍼를 열어 다음 컨베이어로 통과시킴
    /// </summary>
    private IEnumerator ProcessPassThrough()
    {
        CurrentStatus = "2-C. 해당 없음 -> 스토퍼 개방 (통과)";

        // 1. 스토퍼 열기
        SetStopperOpen(true);

        // 2. 물품이 지나갈 때까지 컨베이어 잠깐 작동 대기
        SetConveyorState(true);
        yield return new WaitForSeconds(2.0f); // 물품 통과 필요 시간

        // 3. 스토퍼 다시 닫기
        SetStopperOpen(false);
    }
    #endregion

    #region [6. 하드웨어 제어 가상 래퍼 (수정 포인트)]

    /// <summary>
    /// CNN 추론 결과 모의 반환 (추후 PyTorch/TensorFlow 연동 스크립트로 교체)
    /// </summary>
    private ParcelCategory SimulateCNNInference(Rigidbody parcel)
    {
        if (parcel == null) return ParcelCategory.Unknown;

        // 예시: 태그나 스크립트 이름을 읽어 구별 (테스트용)
        if (parcel.CompareTag("SuctionTarget")) return ParcelCategory.SuctionType;
        if (parcel.CompareTag("GripperTarget")) return ParcelCategory.GripperType;

        return ParcelCategory.Unknown;
    }

    /// <summary>
    /// 컨베이어 멈춤 / 재가동
    /// </summary>
    private void SetConveyorState(bool isRunning)
    {
        // TODO: 사용 중인 컨베이어 스크립트의 On/Off 함수나 속도 조절 파라미터 연결
        if (inputConveyor != null)
        {
            inputConveyor.enabled = isRunning;
        }
    }

    /// <summary>
    /// 스토퍼 열기 / 닫기
    /// </summary>
    private void SetStopperOpen(bool open)
    {
        if (stopper != null)
        {
            stopper.localPosition = open ? stopperOpenPos : stopperClosedPos;
        }
    }

    /// <summary>
    /// 흡착 그리퍼 ON/OFF (물리적 Parent 고정 방식예시)
    /// </summary>
    private void EnableSuction(bool enable, Rigidbody target)
    {
        if (target == null) return;

        if (enable)
        {
            target.isKinematic = true;
            target.transform.SetParent(suctionGripper, true);
        }
        else
        {
            target.transform.SetParent(null, true);
            target.isKinematic = false;
        }
    }
    #endregion
}