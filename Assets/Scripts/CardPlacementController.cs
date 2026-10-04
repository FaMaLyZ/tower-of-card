using UnityEngine;
using UnityEngine.InputSystem;

public class CardPlacementController : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private GameObject ghostCardPrefab;

    [Header("Raycast & Layers")]
    [SerializeField] private Camera cam;
    [SerializeField] private LayerMask tableLayer;
    [SerializeField] private LayerMask snapLayer;
    [SerializeField] private float maxPlacementDistance = 15f;
    [SerializeField] private float snapSearchRadius = 0.2f;

    private GameObject ghostInstance;
    private SnapPoint activeSnapPoint;

    private void Start()
    {
        if (cam == null) cam = Camera.main;

        if (ghostCardPrefab != null)
        {
            ghostInstance = Instantiate(ghostCardPrefab);
            ghostInstance.SetActive(false);
        }
    }

    private void Update()
    {
        HandleGhostCard();

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            PlaceCard();
        }
    }

    private void HandleGhostCard()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = cam.ScreenPointToRay(mousePos);

        // 1. ตรวจจับการเล็งไปที่โต๊ะหรือระนาบวาง
        if (Physics.Raycast(ray, out RaycastHit hit, maxPlacementDistance, tableLayer))
        {
            if (!ghostInstance.activeSelf) ghostInstance.SetActive(true);

            // 2. ตรวจหา SnapPoint บริเวณรอบจุดที่เมาส์ชี้
            activeSnapPoint = GetNearestSnapPoint(hit.point);

            if (activeSnapPoint != null)
            {
                // Snap เข้าหาจุด Anchor
                ghostInstance.transform.position = activeSnapPoint.transform.position;
                ghostInstance.transform.rotation = activeSnapPoint.transform.rotation;
            }
            else
            {
                // ลอยตามตำแหน่งเมาส์กรณีอยู่นอกระยะ Snap
                ghostInstance.transform.position = hit.point + Vector3.up * 0.02f;
                ghostInstance.transform.rotation = Quaternion.identity;
            }
        }
        else
        {
            if (ghostInstance.activeSelf) ghostInstance.SetActive(false);
            activeSnapPoint = null;
        }
    }

    private SnapPoint GetNearestSnapPoint(Vector3 worldPos)
    {
        Collider[] colliders = Physics.OverlapSphere(worldPos, snapSearchRadius, snapLayer);
        SnapPoint nearest = null;
        float minDistanceSqr = Mathf.Infinity;

        foreach (var c in colliders)
        {
            SnapPoint point = c.GetComponent<SnapPoint>();
            if (point != null && !point.isOccupied)
            {
                float dSqr = (point.transform.position - worldPos).sqrMagnitude;
                if (dSqr < minDistanceSqr)
                {
                    minDistanceSqr = dSqr;
                    nearest = point;
                }
            }
        }

        return nearest;
    }

    private void PlaceCard()
    {
        // บังคับให้วางได้เฉพาะจุดที่ Snap สำเร็จ (หรือเอาเงื่อนไขออกถ้าต้องการให้ปล่อยตกอิสระได้)
        if (activeSnapPoint == null || !ghostInstance.activeSelf) return;

        // วางไพ่จริงที่ตำแหน่งของ Ghost
        GameObject placedCard = Instantiate(
            cardPrefab, 
            ghostInstance.transform.position, 
            ghostInstance.transform.rotation
        );

        CardPhysics cardPhys = placedCard.GetComponent<CardPhysics>();
        if (cardPhys != null)
        {
            cardPhys.ActivatePhysics();
        }

        // มาร์ก SnapPoint ว่าถูกใช้งานแล้วเพื่อไม่ให้วางซ้ำ
        activeSnapPoint.isOccupied = true;
        activeSnapPoint = null;
    }
}