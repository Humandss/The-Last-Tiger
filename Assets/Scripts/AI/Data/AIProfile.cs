using UnityEngine;

[CreateAssetMenu(fileName = "AIProfile", menuName = "TankAI/AIProfile")]
public class AIProfile : ScriptableObject
{
    [Header("Detection")]
    public float detectionRange = 40f;      // 감지 거리
    public float fieldOfView = 90f;         // 시야각

    [Header("Combat")]
    public float preferredCombatRange = 25f; // 유지하려는 교전 거리
    public float aimAccuracy = 5f;           // 조준 오차각
    public bool useLeadTarget = false;       // 선행 조준 여부
    public float reactionTime = 1.0f;        // 감지 후 반응 딜레이

    [Header("Movement")]
    public float flankingChance = 0f;        // 측면 우회 확률 
    public float retreatHpThreshold = 0.2f;  // 후퇴 시작 체력 비율

    [Header("Fire")]
    public AmmoType preferredAmmo = AmmoType.AP;
}