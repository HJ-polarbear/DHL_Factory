using UnityEngine;
using UnityEngine.Events;
using RoboLacyTech.Conveyors;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class InspectionController : MonoBehaviour
{
    [Header("컨베이어 연결")]
    public LinearConveyor inspectionConveyor;
    public LinearConveyor inputConveyor;
    public LinearConveyor inputConveyor2;
    public Transform stopper;
    public Transform centerPoint;
    public ParcelSpawner parcelSpawner;
    public float lowerDistance = 0.5f;
    public float stopperSpeed = 2f;
    public float stopTolerance = 0.05f;
    public UnityEvent onParcelReady = new UnityEvent();
    [Header("수동 R키 해제 테스트 - 자동 운전에서는 OFF")]
    public bool allowManualRelease = false;
    public Light inspectionLight;
    public Color idleColor = Color.white;
    public Color inspectionColor = Color.yellow;
    public Color completeColor = Color.green;
    public float idleIntensity = 0.5f;
    public float blinkMinIntensity = 1f;
    public float blinkMaxIntensity = 3f;
    public float blinkSpeed = 2f;
    public float completeIntensity = 3f;
    public float completeLightDuration = 2f;

    private Vector3 stopperUpPosition;
    private Vector3 stopperDownPosition;
    private Rigidbody currentParcel;
    private bool parcelStopped;
    private float completeLightUntil;
    public Rigidbody CurrentParcel => parcelStopped ? currentParcel : null;

    private void Start()
    {
        if (stopper == null || centerPoint == null || inspectionConveyor == null ||
            inputConveyor == null || inputConveyor2 == null)
        {
            Debug.LogError("[Inspection] 스토퍼, 중앙 위치, 컨베이어 3개를 연결하세요.", this);
            enabled = false;
            return;
        }
        stopperUpPosition = stopper.localPosition;
        stopperDownPosition = stopperUpPosition - Vector3.up * lowerDistance;
        stopper.localPosition = stopperDownPosition;
        UpdateInspectionLight();
    }

    private void Update()
    {
        if (allowManualRelease)
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ReleaseParcel();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.R)) ReleaseParcel();
#endif
        }
        UpdateInspectionLight();
    }

    private void FixedUpdate()
    {
        Vector3 target = currentParcel != null ? stopperUpPosition : stopperDownPosition;
        stopper.localPosition = Vector3.MoveTowards(stopper.localPosition, target,
            stopperSpeed * Time.fixedDeltaTime);
        if (currentParcel == null || parcelStopped) return;
        Transform basis = transform.parent != null ? transform.parent : transform;
        Vector3 parcelPosition = basis.InverseTransformPoint(currentParcel.worldCenterOfMass);
        Vector3 centerPosition = basis.InverseTransformPoint(centerPoint.position);
        if (Mathf.Abs(parcelPosition.z - centerPosition.z) > stopTolerance) return;
        inspectionConveyor.Stop();
        parcelStopped = true;
        Debug.Log("[Inspection] 박스 정지, 검사 요청", this);
        onParcelReady?.Invoke();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (currentParcel != null) return;
        Rigidbody body = other.attachedRigidbody;
        if (body == null || body.GetComponent<LinearConveyor>() != null) return;
        currentParcel = body;
        parcelStopped = false;
        inputConveyor.Stop();
        inputConveyor2.Stop();
        if (parcelSpawner != null) parcelSpawner.SetPaused(true);
    }

    public void ReleaseParcel()
    {
        if (currentParcel == null || !parcelStopped) return;
        currentParcel = null;
        parcelStopped = false;
        completeLightUntil = Time.time + Mathf.Max(0f, completeLightDuration);
        inspectionConveyor.Play();
        inputConveyor.Play();
        inputConveyor2.Play();
        if (parcelSpawner != null) parcelSpawner.SetPaused(false);
        else Debug.LogWarning("[Inspection] Parcel Spawner 연결 없음", this);
    }

    private void UpdateInspectionLight()
    {
        if (inspectionLight == null) return;
        if (currentParcel != null && parcelStopped)
        {
            inspectionLight.color = inspectionColor;
            float blink = (Mathf.Sin(Time.time * blinkSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
            inspectionLight.intensity = Mathf.Lerp(blinkMinIntensity, blinkMaxIntensity, blink);
        }
        else if (Time.time < completeLightUntil)
        {
            inspectionLight.color = completeColor;
            inspectionLight.intensity = completeIntensity;
        }
        else
        {
            inspectionLight.color = idleColor;
            inspectionLight.intensity = idleIntensity;
        }
    }
}
