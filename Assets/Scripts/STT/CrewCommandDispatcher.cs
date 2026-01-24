using System.Collections.Generic;
using UnityEngine;

public class CrewCommandDispatcher : MonoBehaviour
{
    private readonly Queue<ParsedCmd> driverQ = new();
    private readonly Queue<ParsedCmd> gunnerQ = new();
    private readonly Queue<ParsedCmd> loaderQ = new();

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
        // TODO: loader.LoadAP(), loader.LoadHE()
    }

    void ExecuteGunner(ParsedCmd c)
    {
        Debug.Log($"[EXEC][포수] {c}");
        // TODO: weapon.Fire()
    }
}