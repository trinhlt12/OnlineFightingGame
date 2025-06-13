using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public static CameraFollow Singleton
    {
        get => _singleton;
        set
        {
            if (value == null)
            {
                _singleton = null;
            }else if (_singleton == null)
            {
                _singleton = value;
            }else if (_singleton != value)
            {
                Destroy(value);
                Debug.LogError("CameraFollow singleton already exists. Destroying the new instance.");
            }
        }
    }

    private static CameraFollow _singleton;
    private        Transform    target;

    private void Awake()
    {
        Singleton = this;
    }

    private void OnDestroy()
    {
        if(Singleton == this)
        {
            Singleton = null;
        }
    }

    private void LateUpdate()
    {
        if (this.target != null)
        {
            transform.SetPositionAndRotation(this.target.position, this.target.rotation);
        }
    }

    public void SetTarget(Transform newTarget)
    {
        this.target = newTarget;
    }
}