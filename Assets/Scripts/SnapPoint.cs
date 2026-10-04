using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class SnapPoint : MonoBehaviour
{
    public enum CardOrientation { LeanLeft, LeanRight, FlatRoof }
    public CardOrientation orientation;

    [HideInInspector] public bool isOccupied = false;
    public float detectionRadius = 0.25f;

    private SphereCollider col;

    private void Awake()
    {
        col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = detectionRadius;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = isOccupied ? Color.red : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.DrawRay(transform.position, transform.forward * 0.15f);
    }
}