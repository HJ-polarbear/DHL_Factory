using UnityEngine;

public enum ParcelCategorycopy
{
    General = 0,
    Food = 1,
    Fragile = 2
}

public class ParcelItem : MonoBehaviour
{
    [Header("택배 카테고리 정보")]
    public ParcelCategory category;
}