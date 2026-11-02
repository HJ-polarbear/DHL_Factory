using UnityEngine;

public class FingerContact : MonoBehaviour
{
    [Tooltip("마지막 접촉 후 이 시간(초)까지는 '닿아 있음'으로 인정")]
    public float holdTime = 0.1f;

    [SerializeField] private Rigidbody touching;
    float lastTouchTime = -1f;

    public Rigidbody TouchingPart
    {
        get
        {
            if (touching == null || !touching.gameObject.activeInHierarchy || Time.fixedTime - lastTouchTime > holdTime)
                return null;
            return touching;
        }
    }

    void OnCollisionEnter(Collision collision) => Record(collision);
    void OnCollisionStay(Collision collision) => Record(collision);

    void Record(Collision collision)
    {
        if (collision == null || collision.collider == null) return;

        Rigidbody rb = collision.collider.attachedRigidbody;
        // "Parcel_Gripper" 혹은 "Part" 태그 모두 대응하도록 수정
        if (rb != null && (rb.CompareTag("Parcel_Gripper") || rb.CompareTag("Part")))
        {
            touching = rb;
            lastTouchTime = Time.fixedTime;
        }
    }

    public void Clear()
    {
        touching = null;
        lastTouchTime = -1f;
    }
}