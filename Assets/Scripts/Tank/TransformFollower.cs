using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TransformFollower : MonoBehaviour
{
    [SerializeField] private Transform source;      // 따라갈 대상(모델)
    [SerializeField] private bool followPosition = true;
    [SerializeField] private bool followRotation = true;
    [SerializeField] private bool followScale = false;
    [SerializeField] private bool followLocal = true;

    private void LateUpdate()
    {
        if (!source) return;

        if (followLocal)
        {
            if (followPosition) transform.localPosition = source.localPosition;
            if (followRotation) transform.localRotation = source.localRotation;
            if (followScale) transform.localScale = source.localScale;
        }
        else
        {
            if (followPosition) transform.position = source.position;
            if (followRotation) transform.rotation = source.rotation;
            if (followScale) transform.localScale = source.lossyScale; 
        }
    }
}
