Shader "Hidden/URP/SliceGlitch"
{
  SubShader
  {
    Tags { "RenderType"="Opaque" "Queue"="Overlay" }
    Pass
    {
      ZTest Always Cull Off ZWrite Off

      HLSLPROGRAM
      #pragma vertex Vert
      #pragma fragment Frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

      // Blitter source
      TEXTURE2D_X(_BlitTexture);
      SAMPLER(sampler_LinearClamp);

      // Common
      float  _Intensity;          // 0..1
      int    _Seed;
      float4 _SourceTexelSize;    // (1/w,1/h,w,h)

      // Block params
      float  _RectAmount;         // 0..1 : probability per cell
      float  _RectMinSizePx;      // min side (px)
      float  _RectMaxSizePx;      // max side (px)
      float  _RectMaxShiftPx;     // max displacement (px)
      int    _Iterations;         // 1..4
      float  _Aniso;              // 0..1 (width/height anisotropy)

      struct Attributes { uint vertexID : SV_VertexID; };
      struct Varyings  { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

      Varyings Vert (Attributes v)
      {
        Varyings o;
        o.positionCS = GetFullScreenTriangleVertexPosition(v.vertexID);
        o.uv         = GetFullScreenTriangleTexCoord(v.vertexID);
        return o;
      }

      // --- Hash helpers ---
      float  Hash11(float x)
      {
        x = frac(x * 0.1031);
        x *= x + 33.33;
        x *= x + x;
        return frac(x);
      }

      float2 Hash21(float2 p)
      {
        // nb: fast, correlation is fine for glitch
        float3 p3 = frac(float3(p.x, p.y, p.x) * 0.1031);
        p3 += dot(p3, p3.yzx + 33.33);
        return frac((p3.xx + p3.yz) * p3.zy);
      }

      float3 Hash31(float3 p)
      {
        p = frac(p * 0.1031);
        p += dot(p, p.yzx + 31.32);
        return frac((p.xxy + p.yzz) * p.zyx);
      }

      // Rectangle test: inside if |delta| <= half extent
      bool InsideRect(float2 p, float2 center, float2 half)
      {
        float2 d = abs(p - center);
        return all(d <= half);
      }

      float4 Frag (Varyings i) : SV_Target
      {
          float2 uv = i.uv;
          float  w  = _SourceTexelSize.z;
          float  h  = _SourceTexelSize.w;
          float2 p  = float2(uv.x * w, uv.y * h); // pixel space

          // 기본: 원본 픽셀
          float4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

          if (_Intensity <= 0.0 || _RectAmount <= 0.0) 
              return col;

          // 블록 여러 개 테스트
          [unroll(4)]
          for (int k = 0; k < _Iterations; k++)
          {
              float cellSize = lerp(_RectMinSizePx, _RectMaxSizePx, (k+0.5)/max(1,_Iterations));
              float2 cell = floor(p / cellSize);

              float2 rA = Hash21(cell + float2(_Seed, k*17));
              float2 rB = Hash21(cell + float2(_Seed*1.37+13, k*29));
              float2 rC = Hash21(cell + float2(_Seed*2.11+7,  k*41));

              if (rA.x < (_RectAmount * _Intensity))
              {
                  // 원본 블록의 크기와 위치
                  float sx = lerp(_RectMinSizePx, _RectMaxSizePx, rB.x);
                  float sy = lerp(_RectMinSizePx, _RectMaxSizePx, rC.x);
                  float2 half = 0.5 * float2(sx, sy);
                  float2 srcCenter = (cell + 0.5) * cellSize;

                  // "붙여넣기"할 대상 위치 (랜덤 오프셋)
                  float2 dstOffset = (rC.yx - 0.5) * _RectMaxShiftPx * 2.0;
                  float2 dstCenter = srcCenter + dstOffset;

                  // 현재 픽셀이 dstRect 안에 들어가면
                  if (InsideRect(p, dstCenter, half))
                  {
                      // 원본 블록 좌표로 되돌림
                      float2 local = (p - dstCenter) + srcCenter;
                      float2 uvSrc = local / float2(w,h);

                      float4 piece = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvSrc);

                      // 원본 위에 덮어씌우기 (완전 대체 or 섞기)
                      col = piece; 
                      // 또는 col = lerp(col, piece, _Intensity);

                      break;
                  }
              }
          }

          return col;
      }
      ENDHLSL
    }
  }
  Fallback Off
}
