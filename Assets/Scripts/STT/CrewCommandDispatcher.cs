using System;
using System.Collections.Generic;
using UnityEngine;

// DriverDesired 구조체 완전 제거 - driver는 키보드로만 제어

public class CrewCommandDispatcher : MonoBehaviour
{
    // ✅ driver 큐 완전 제거 - gunner/loader만 처리
    private readonly Queue<ParsedCmd> gunnerQ = new();
    private readonly Queue<ParsedCmd> loaderQ = new();

    [SerializeField] private GunnerController gunner;
    [SerializeField] private LoaderController loader;
    // ✅ DriverController 레퍼런스 제거

    void Start()
    {
        if (gunner == null) Debug.LogError("[Dispatcher] GunnerController 연결 안됨!");
        if (loader == null) Debug.LogError("[Dispatcher] LoaderController 연결 안됨!");
    }

    public void EnqueueFromStt(string stt)
    {
        var map = CrewParser.Parse(stt);
        foreach (var kv in map)
        {
            // ✅ driver 명령은 완전히 무시
            if (kv.Key == CrewRole.Driver)
            {
                Debug.Log($"[Dispatcher] driver 명령 무시 (키보드 전용): {string.Join(", ", kv.Value)}");
                continue;
            }

            var q = GetQueue(kv.Key);
            if (q == null) continue;

            foreach (var pc in kv.Value)
                q.Enqueue(pc);

            Debug.Log($"[Parse] {kv.Key} => {string.Join(", ", kv.Value)}");
        }
    }

    public void EnqueueParsed(CrewRole role, ParsedCmd cmd)
    {
        // ✅ driver로 들어오면 Gunner로 리매핑
        //    (STT 오인식으로 driver가 왔을 때 포수로 전달)
        if (role == CrewRole.Driver)
        {
            Debug.Log($"[Dispatcher] driver → Gunner 리매핑: {cmd}");
            role = CrewRole.Gunner;
        }

        var q = GetQueue(role);
        if (q == null) return;

        q.Enqueue(cmd);
        Debug.Log($"[EnqueueParsed] {role} => {cmd}");
    }

    void Update()
    {
        if (gunnerQ.Count > 0) ExecuteGunner(gunnerQ.Dequeue());
        if (loaderQ.Count > 0) ExecuteLoader(loaderQ.Dequeue());
    }

    private Queue<ParsedCmd> GetQueue(CrewRole role) => role switch
    {
        CrewRole.Gunner => gunnerQ,
        CrewRole.Loader => loaderQ,
        _ => null
    };

    void ExecuteLoader(ParsedCmd c)
    {
        Debug.Log($"[EXEC][장전수] {c}");
        switch (c.GetCmd)
        {
            case Cmd.LoadDefault: loader.LoadDefault(); break;
            case Cmd.LoadAP: loader.Load(AmmoType.AP); break;
            case Cmd.LoadHE: loader.Load(AmmoType.HE); break;
            default: Debug.Log($"[Loader] 처리 안 함: {c.GetCmd}"); break;
        }
    }

    void ExecuteGunner(ParsedCmd c)
    {
        Debug.Log($"[EXEC][포수] {c}");
        switch (c.GetCmd)
        {
            case Cmd.CeaseAction: gunner.CeaseAction(); break;
            case Cmd.AimAt: gunner.Aim(); break;
            case Cmd.AlignHull: gunner.AlignHull(); break;
            case Cmd.Fire: gunner.Fire(); break;
            case Cmd.TrackTarget: gunner.StartTracking(); break;
            case Cmd.SetRange:
                var meters = c.GetRangeMeters;
                if (meters.HasValue) gunner.SetRange(meters.Value);
                else Debug.LogWarning("[Cmd.SetRange] rangeMeters is null");
                break;
            default: Debug.Log($"[Gunner] 처리 안 함: {c.GetCmd}"); break;
        }
    }
}