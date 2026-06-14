#ifndef IMPACT_DECAL_INCLUDED
#define IMPACT_DECAL_INCLUDED

// =========================================================
// ImpactDecal.hlsl
//
// 피격 타입(관통/도탄/무관통)에 따라 다른 비주얼을 출력하는
// Shader Graph Custom Function용 HLSL 파일.
// =========================================================

void ImpactDecal_float(
    float2 UV,
    float ImpactType,              // 0 = Penetration, 1 = Ricochet, 2 = NoPenetration
    UnityTexture2D PenMap,         // 관통
    UnityTexture2D RicochetMap, // 도탄
    UnityTexture2D NonPenMap, // 무관통
    out float3 Albedo,
    out float Alpha,
    out float Smoothness,
    out float Metallic,
    out float AmbientOcclusion
)
{
    
    float4 penSample = SAMPLE_TEXTURE2D(PenMap.tex, PenMap.samplerstate, UV);
    float4 noPenSample = SAMPLE_TEXTURE2D(NonPenMap.tex, NonPenMap.samplerstate, UV);
    float4 ricSample = SAMPLE_TEXTURE2D(RicochetMap.tex, RicochetMap.samplerstate, UV);
    
    // 알파 컷오프 임계값
    const float ALPHA_CUTOFF = 0.85;

    if (ImpactType < 0.5)         // Penetration
    {
        float a          = step(ALPHA_CUTOFF, penSample.a);
        Albedo           = penSample.rgb;
        Alpha            = a;
        Smoothness       = lerp(0.5, 0.1, a);
        Metallic         = lerp(0.0, 0.7, a);
        AmbientOcclusion = lerp(1.0, 0.4, a);
    }
    else if (ImpactType < 1.5)    // Ricochet
    {
        float a          = step(ALPHA_CUTOFF, ricSample.a);
        Albedo           = ricSample.rgb;
        Alpha            = a;
        Smoothness       = lerp(0.5, 0.7, a);
        Metallic         = lerp(0.0, 0.95, a);
        AmbientOcclusion = lerp(1.0, 0.85, a);
    }
    else                           // NoPenetration
    {
        float a          = step(ALPHA_CUTOFF, noPenSample.a);
        Albedo           = noPenSample.rgb;
        Alpha            = a;
        Smoothness       = lerp(0.5, 0.3, a);
        Metallic         = lerp(0.0, 0.6, a);
        AmbientOcclusion = lerp(1.0, 0.6, a);
    }
}

#endif // IMPACT_DECAL_INCLUDED
