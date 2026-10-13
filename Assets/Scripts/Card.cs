using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class Card : MonoBehaviour
{
    public CardData Data { get; private set; }

    Renderer rend;
    Collider col;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        col = GetComponent<Collider>();
    }

    public void Setup(CardData data)
    {
        Data = data;
        rend.material.color = data.tint;   // works with URP Lit (_BaseColor)
    }

    // Ghost = preview card following the mouse. Green tint = snapped, red = no slot nearby.
    public void SetGhost(bool snapped)
    {
        if (col != null) col.enabled = false;
        rend.material.color = snapped
            ? Color.Lerp(Data.tint, Color.green, 0.35f)
            : Color.Lerp(Data.tint, Color.red, 0.6f);
    }

    public void SetPlaced()
    {
        if (col != null) col.enabled = true;
        rend.material.color = Data.tint;
    }
}