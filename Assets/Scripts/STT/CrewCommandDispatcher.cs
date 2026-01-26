using System.Collections.Generic;
using UnityEngine;

public class CrewCommandDispatcher : MonoBehaviour
{
    private readonly Queue<ParsedCmd> driverQ = new();
    private readonly Queue<ParsedCmd> gunnerQ = new();
    private readonly Queue<ParsedCmd> loaderQ = new();


    [SerializeField] private GunnerController gunner;
    [SerializeField] private LoaderController loader;


    
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
        // 프레임마다 하나씩 처리
        if (driverQ.Count > 0) ExecuteDriver(driverQ.Dequeue());
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

    void ExecuteDriver(ParsedCmd c)
    {
        Debug.Log($"[EXEC][조종수] {c},{c.ToString()}");
        // TODO: 나중에 motor.MoveForward() 같은 실제 호출로 바꾸면 됨
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

            default:
                Debug.Log($"[Gunner] 처리 안 함: {c.GetCmd}");
                break;
        }
    }
}