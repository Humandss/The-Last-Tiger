using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DummySttRunner : MonoBehaviour
{
    [SerializeField] private string fakeResult = "운전수 출발";
    [SerializeField] private float delaySec = 0.1f;

    public IEnumerator TranscribeWav(byte[] wavBytes, Action<string> onSuccess, Action<string> onError)
    {
        yield return new WaitForSeconds(delaySec);
        onSuccess?.Invoke(fakeResult);
    }
}
