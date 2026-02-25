using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoiceCommandBridge : MonoBehaviour
{
    [SerializeField] private VoiceCommandStateMachine voiceSm;
    [SerializeField] private CrewCommandDispatcher dispatcher;

    private void Awake()
    {
        voiceSm.OnCommandAccepted += HandleAccepted;
    }

    private void OnDestroy()
    {
        if (voiceSm != null)
            voiceSm.OnCommandAccepted -= HandleAccepted;
    }

    private void HandleAccepted(CrewRole role, ParsedCmd cmd)
    {
        dispatcher.EnqueueParsed(role, cmd);
    }
}
