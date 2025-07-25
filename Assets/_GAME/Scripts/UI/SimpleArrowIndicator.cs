// File: Assets/_GAME/Scripts/UI/SimpleArrowIndicator.cs
using UnityEngine;
using Fusion;

public class SimpleArrowIndicator : NetworkBehaviour
{
    [Header("Arrow Settings")]
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private float heightAbovePlayer = 3f;
    [SerializeField] private bool enableDebugLogs = false;

    private GameObject _myArrow;

    public override void Spawned()
    {
        if (Object.HasInputAuthority)
        {
            CreateArrow();

            if (enableDebugLogs)
                Debug.Log("[SimpleArrowIndicator] Created arrow for my character");
        }
    }

    private void CreateArrow()
    {
        if (arrowPrefab == null)
        {
            Debug.LogError("[SimpleArrowIndicator] Arrow prefab not assigned!");
            return;
        }

        Vector3 arrowPosition = transform.position + Vector3.up * heightAbovePlayer;
        _myArrow = Instantiate(arrowPrefab, arrowPosition, Quaternion.identity);

        _myArrow.transform.SetParent(transform);
    }

    private void Update()
    {
        if (_myArrow != null && Object.HasInputAuthority)
        {
            Vector3 targetPosition = transform.position + Vector3.up * heightAbovePlayer;
            _myArrow.transform.position = targetPosition;

            if (Camera.main != null)
            {
                _myArrow.transform.LookAt(Camera.main.transform);
                _myArrow.transform.Rotate(0, 180, 0);
            }
        }
    }

    private void OnDestroy()
    {
        if (_myArrow != null)
        {
            Destroy(_myArrow);
        }
    }
}