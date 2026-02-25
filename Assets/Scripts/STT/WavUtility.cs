using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class WavUtility : MonoBehaviour
{
    public static byte[] FromMonoFloat(float[] samples, int sampleRate)
    {
        if (samples == null) return Array.Empty<byte>();

        const short channels = 1;
        const short bitsPerSample = 16;
        int byteRate = sampleRate * channels * (bitsPerSample / 8);
        short blockAlign = (short)(channels * (bitsPerSample / 8));

        using MemoryStream ms = new MemoryStream();
        using BinaryWriter bw = new BinaryWriter(ms);

        int dataSize = samples.Length * 2;

        // RIFF header
        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + dataSize);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

        // fmt chunk
        bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);                 // PCM chunk size
        bw.Write((short)1);           // PCM format
        bw.Write(channels);
        bw.Write(sampleRate);
        bw.Write(byteRate);
        bw.Write(blockAlign);
        bw.Write(bitsPerSample);

        // data chunk
        bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        bw.Write(dataSize);

        for (int i = 0; i < samples.Length; i++)
        {
            float clamped = Mathf.Clamp(samples[i], -1f, 1f);
            short s = (short)Mathf.RoundToInt(clamped * short.MaxValue);
            bw.Write(s);
        }

        bw.Flush();
        return ms.ToArray();
    }
}
