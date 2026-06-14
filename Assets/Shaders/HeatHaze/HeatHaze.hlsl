#ifndef HEAT_HAZE_INCLUDED
#define HEAT_HAZE_INCLUDED

// =========================================================
// HeatHaze.hlsl
//
// 머즐 플래시 / 폭발 시 공기 일렁임 (Heat Distortion / Refraction)
// =========================================================

// 간단한 의사 난수 (해시 기반)
float HeatHaze_Hash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

// 부드러운 2D Value Noise (보간된 의사 난수)
float HeatHaze_ValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);

    // 4개 격자 모서리 해시
    float a = HeatHaze_Hash(i);
    float b = HeatHaze_Hash(i + float2(1.0, 0.0));
    float c = HeatHaze_Hash(i + float2(0.0, 1.0));
    float d = HeatHaze_Hash(i + float2(1.0, 1.0));

    // smoothstep 보간 (계단 현상 제거)
    float2 u = f * f * (3.0 - 2.0 * f);

    return lerp(a, b, u.x) +
           (c - a) * u.y * (1.0 - u.x) +
           (d - b) * u.x * u.y;
}

void HeatHaze_float(
    float2 ScreenUV,            // 화면 UV (Screen Position.xy / Screen Position.w)
    float2 NoiseUV,             // 메시 UV (노이즈 시드용)
    float DistortionStrength,   // 왜곡 강도 
    float TimeSpeed,            // 노이즈 흐름 속도 
    float NoiseScale,           // 노이즈 패턴 밀도 
    out float2 DistortedUV,
    out float Mask              // 원형 가장자리 페이드
)
{
    // 시간 기반 노이즈 좌표
    float t = _Time.y * TimeSpeed;
    float2 animatedUV = NoiseUV * NoiseScale + float2(t, t * 0.7);

    // X/Y 두 방향 노이즈 (서로 다른 시드)
    float nx = HeatHaze_ValueNoise(animatedUV);
    float ny = HeatHaze_ValueNoise(animatedUV + float2(13.7, 9.3));

    // -1 ~ 1 범위로 변환 후 강도 적용
    float2 noise = float2(nx, ny) * 2.0 - 1.0;

    // 화면 UV에 노이즈 오프셋 적용
    DistortedUV = ScreenUV + noise * DistortionStrength;

    // 원형 페이드 마스크
    // 빌보드 사각형 윤곽 안 보이게 만드는 핵심
    float2 centered = NoiseUV - 0.5;
    float dist = length(centered) * 2.0;          
    Mask = smoothstep(1.0, 0.3, dist);             
}

#endif // HEAT_HAZE_INCLUDED
