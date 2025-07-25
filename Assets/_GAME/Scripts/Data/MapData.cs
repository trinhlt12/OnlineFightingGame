// File: Assets/_GAME/Scripts/Data/MapData.cs
using UnityEngine;

[CreateAssetMenu(fileName = "New Map Data", menuName = "ValorArena/Map Data")]
public class MapData : ScriptableObject
{
    [Header("Map Information")]
    public string mapName;
    public Sprite     mapPreviewImage;
    public GameObject mapPrefab;

    [Header("Map Settings")]
    public Vector3 spawnOffset = Vector3.zero;
    public bool enableDebugLogs = true;
}