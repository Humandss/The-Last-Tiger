#ifndef IMPACT_DECAL_INCLUDED
#define IMPACT_DECAL_INCLUDED

// =========================================================
// ImpactDecal.hlsl
//
// 피격 타입(관통/도탄/무관통)에 따라 다른 비주얼을 출력하는
// Shader Graph Custom Function용 HLSL 파일.
//
// 사용법:
// - Shader Graph에 Custom Function 노드 추가
// - Type: File
// - Source: 이 파일 드래그
// - Name: ImpactDecal  (함수명에서 _float 뺀 이름)
//
// 참고:
// - Shader Graph Custom Function은 함수명 뒤에 _float 또는 _half 붙여야 함
// - UnityTexture2D 타입은 Shader Graph에서 자동으로 sampler까지 묶어서 넘겨줌
// - SAMPLE_TEXTURE2D 매크로 사용 (URP Core.hlsl에 정의됨)
// =========================================================

void ImpactDecal_float(
    float2 UV,
    float ImpactType,              // 0 = Penetration, 1 = Ricochet, 2 = NoPenetration
    UnityTexture2D PenMap,         // 관통: 구멍 + 방사형 크랙
    UnityTexture2D RicochetMap, // 도탄: 사선 긁힘
    UnityTexture2D NonPenMap, // 무관통: 움푹 패임
    out float3 Albedo,
    out float Alpha,
    out float Smoothness,
    out float Metallic, 
    out float AmbientOcclusion
)
{
    // -----------------------------------------------------
    // TODO: 여기에 분기 로직 작성
    //
    // 힌트:
    //   float4 penSample = SAMPLE_TEXTURE2D(PenMap.tex, PenMap.samplerstate, UV);
    //   float4 ricSample = SAMPLE_TEXTURE2D(RicochetMap.tex, RicochetMap.samplerstate, UV);
    //   float4 dentSample = SAMPLE_TEXTURE2D(NonPenMap.tex, NonPenMap.samplerstate, UV);
    //
    //   분기 방식 1: if/else
    //     if (ImpactType < 0.5)       { ... Penetration ... }
    //     else if (ImpactType < 1.5)  { ... Ricochet ... }
    //     else                         { ... NoPenetration ... }
    //
    //   분기 방식 2: step() + lerp() (브랜치 없이 GPU 친화적)
    //     float isPen = step(ImpactType, 0.5);
    //     float isRic = step(0.5, ImpactType) * step(ImpactType, 1.5);
    //     float isDent = step(1.5, ImpactType);
    //     float4 result = penSample * isPen + ricSample * isRic + dentSample * isDent;
    //
    // 타입별 Smoothness 차이도 주면 더 그럴싸함:
    //   관통(Pen):  거친 구멍이라 Smoothness 낮게 (예: 0.1)
    //   도탄(Ric):  금속 긁힌 자국이라 Smoothness 높게 (예: 0.6)
    //   무관통(Dent): 중간 정도 (예: 0.3)
    // -----------------------------------------------------

    // 임시 동작 (Custom Function 연결 테스트용)
    // → 실제 분기 로직 구현하면서 이 부분을 교체하세요
    Albedo = float3(1, 0, 1);  // 핑크 — "아직 구현 안 됨" 표시
    Alpha = 1.0;
    Smoothness = 0.5;
    Metallic = 0.1;
    AmbientOcclusion = 0.1;
}

#endif // IMPACT_DECAL_INCLUDED
