using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ISttRunner
{
    /// <summary>
    /// WAV 바이트를 받아 비동기 전사 후 callback 호출
    /// </summary>
    IEnumerator TranscribeWav(byte[] wavBytes, Action<string> onSuccess, Action<string> onError);
}

