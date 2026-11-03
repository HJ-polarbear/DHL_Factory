using UnityEngine;

public enum ParcelCategory
{
    General = 0,
    Food = 1,
    Fragile = 2
}

public class ParcelInfo : MonoBehaviour
{
    [Header("채점에만 사용하는 정답")]
    public ParcelCategory category;
}