using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Ballistics/Shell Data")]
public class ShellData : ScriptableObject
{
    [Header("Ballistics")]
    public string shellName ="PzGr 39";
    [Range(0.0f, 200.0f)] public float caliber = 88.00f; // 구경 
    [Range(1.0f, 100.0f)] public float projectileMass = 0.004f;       // kg
    [Range(100.0f, 1500.0f)] public float muzzleVelocity = 920.0f; // 속도 m/s
    [Range(0.0f, 1.0f)] public float dragCoeff = 0.3f;    // 탄두 형상에 따른 공지 저항 계수
    [Range(0.0f, 15.0f)] public float refAreaScale = 1.0f;   // 단면적 스케일 mm단위
    [Range(5.0f, 500.0f)] public float penetrationPower = 165.0f; // 관통력

    [Header("Fuze")]
    [SerializeField] public float fuzeDealy = 0.0f;
    [SerializeField] public float fuzeSensitivity = 0.0f;


    [Header("LifeTime")]
    [Range(0.0f, 50.0f)] public float lifeTime = 0.0f;

}
