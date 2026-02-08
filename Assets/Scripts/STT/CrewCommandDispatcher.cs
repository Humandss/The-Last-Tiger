using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct DriverDesired
{
    public bool stop;
    public float throttle; // -1..1
    public float steer;    // -1..1
    public float pivot;    // -1..1

    public void Clear()
    {
        stop = false;
        throttle = 0f;
        steer = 0f;
        pivot = 0f;
    }
}

public class CrewCommandDispatcher : MonoBehaviour
{
    private readonly Queue<ParsedCmd> driverQ = new();
    private readonly Queue<ParsedCmd> gunnerQ = new();
    private readonly Queue<ParsedCmd> loaderQ = new();


    [SerializeField] private GunnerController gunner;
    [SerializeField] private LoaderController loader;
    [SerializeField] private DriverController driver;

    [Header("Driver Cmd Processing")]
    [SerializeField] private int maxDriverCmdsPerFrame = 16; // 무한루프 방지
    [SerializeField] private bool driverConsumeAllEachFrame = true;

    public void EnqueueFromStt(string stt)
    {
        var map = CrewParser.Parse(stt);

        foreach (var kv in map)
        {
            var q = GetQueue(kv.Key);

            foreach (var pc in kv.Value)
                q.Enqueue(pc);

            Debug.Log($"[Parse] {kv.Key} => {string.Join(", ", kv.Value)}");
        }

    }

    void Update()
    {
        DriverDesired d = default;
        d.Clear();

        while (driverQ.Count > 0)
            ExecuteDriver(ref d, driverQ.Dequeue());

        if (d.stop)
            driver.StopAll();
        else
            driver.SetDesired(d.throttle, d.steer, d.pivot);

        if (loaderQ.Count > 0) ExecuteLoader(loaderQ.Dequeue());
        if (gunnerQ.Count > 0) ExecuteGunner(gunnerQ.Dequeue());
    }

    Queue<ParsedCmd> GetQueue(CrewRole role) => role switch
    {
        CrewRole.Driver => driverQ,
        CrewRole.Loader => loaderQ,
        CrewRole.Gunner => gunnerQ,
        _ => driverQ
    };

    private float IntensityMul(Intensity? i)
    {
        return i switch
        {
            Intensity.Small => 0.35f,
            Intensity.Normal => 0.65f,
            Intensity.Large => 1.0f,
            _ => 0.65f
        };
    }
    void ExecuteDriver(ref DriverDesired d, ParsedCmd c)
    {
        float mul = IntensityMul(c.GetIntensity);

        switch (c.GetCmd)
        {
            case Cmd.Stop:
                d.stop = true;
                return;

            case Cmd.MoveForward:
                d.throttle = Mathf.Clamp(+1f * mul, -1f, 1f);
                return;

            case Cmd.MoveBackward:
                d.throttle = Mathf.Clamp(-1f * mul, -1f, 1f);
                return;

            case Cmd.TurnRight:
                d.steer = Mathf.Clamp(+1f * mul, -1f, 1f);
                d.pivot = 0f;
                return;

            case Cmd.TurnLeft:
                d.steer = Mathf.Clamp(-1f * mul, -1f, 1f);
                d.pivot = 0f;
                return;

            case Cmd.PivotRight:
                d.pivot = Mathf.Clamp(+1f * mul, -1f, 1f);
                d.steer = 0f;
                d.throttle = 0f;
                return;

            case Cmd.PivotLeft:
                d.pivot = Mathf.Clamp(-1f * mul, -1f, 1f);
                d.steer = 0f;
                d.throttle = 0f;
                return;
        }
    }

    void ExecuteLoader(ParsedCmd c)
    {
        Debug.Log($"[EXEC][장전수] {c}");

        switch (c.GetCmd)
        {
            case Cmd.LoadDefault:
                loader.LoadDefault();
                break;

            case Cmd.LoadAP:
                loader.Load(AmmoType.AP);
                break;

            case Cmd.LoadHE:
                loader.Load(AmmoType.HE);
                break;

            default:
                Debug.Log($"[Loader] 처리 안 함: {c.GetCmd}");
                break;
        }
    }

    void ExecuteGunner(ParsedCmd c)
    {
        Debug.Log($"[EXEC][포수] {c}");

        switch (c.GetCmd)
        {
            case Cmd.CeaseAction:
                gunner.CeaseAction();
                break;

            case Cmd.AimAt:
                gunner.Aim();
                break;

            case Cmd.SetRange:
                var meters = c.GetRangeMeters;
                if (meters.HasValue) gunner.SetRange(meters.Value);
                else Debug.LogWarning("[Cmd.SetRange] rangeMeters is null");
                break;

            case Cmd.AlignHull:
                gunner.AlignHull();
                break;

            case Cmd.Fire:
                gunner.Fire();
                break;

            case Cmd.TrackTarget:
                gunner.StartTracking();
                break;

            default:
                Debug.Log($"[Gunner] 처리 안 함: {c.GetCmd}");
                break;
        }
    }
}