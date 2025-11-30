Shader "UI/SliceGlitch"
{
  Properties
  {
    [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
    _Color ("Tint", Color) = (1,1,1,1)

    // --- Stencil for Mask/RectMask2D ---
    _StencilComp ("Stencil Comparison", Float) = 8
    _Stencil ("Stencil ID", Float) = 0
    _StencilOp ("Stencil Operation", Float) = 0
    _StencilWriteMask ("Stencil Write Mask", Float) = 255
    _StencilReadMask ("Stencil Read Mask", Float) = 255
    _ColorMask ("Color Mask", Float) = 15

    // --- Glitch params ---
    _Intensity ("Intensity", Range(0,1)) = 0
    _RectAmount ("Rect Amount", Range(0,1)) = 0.25
    _RectMinSizePx ("Rect Min Size (px)", Float) = 12
    _RectMaxSizePx ("Rect Max Size (px)", Float) = 96
    _RectMaxShiftPx ("Rect Max Shift (px)", Float) = 32
    _Iterations ("Iterations (1..4)", Range(1,4)) = 3
    _Aniso ("Anisotropy", Range(0,1)) = 1
    _Seed ("Seed", Int) = 12345
  }

  SubShader
  {
    Tags
    {
      "Queue"="Transparent"
      "IgnoreProjector"="True"
      "RenderType"="Transparent"
      "PreviewType"="Plane"
      "CanUseSpriteAtlas"="True"
    }

    Stencil
    {
      Ref [_Stencil]
      Comp [_StencilComp]
      Pass [_StencilOp]
      ReadMask [_StencilReadMask]
      WriteMask [_StencilWriteMask]
    }

    Cull Off
    ZWrite Off
    ZTest Always
    Blend SrcAlpha OneMinusSrcAlpha
    ColorMask [_ColorMask]

    Pass
    {
      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

      struct appdata
      {
        float4 vertex : POSITION;
        float4 color  : COLOR;
        float2 uv     : TEXCOORD0;
      };

      struct v2f
      {
        float4 pos : SV_POSITION;
        float4 col : COLOR;
        float2 uv  : TEXCOORD0;
      };

      TEXTURE2D(_MainTex);
      SAMPLER(sampler_MainTex);
      float4 _MainTex_ST;
      float4 _MainTex_TexelSize; // (1/w,1/h,w,h)
      float4 _Color;

      // glitch params
      float  _Intensity;
      float  _RectAmount;
      float  _RectMinSizePx;
      float  _RectMaxSizePx;
      float  _RectMaxShiftPx;
      float  _Iterations;
      float  _Aniso;
      int    _Seed;

      v2f vert(appdata v)
      {
        v2f o;
        o.pos = TransformObjectToHClip(v.vertex.xyz);
        o.uv  = TRANSFORM_TEX(v.uv, _MainTex);
        o.col = v.color * _Color;
        return o;
      }

      // fast hashes
      float2 Hash21(float2 p)
      {
        float3 p3 = frac(float3(p.x, p.y, p.x) * 0.1031);
        p3 += dot(p3, p3.yzx + 33.33);
        return frac((p3.xx + p3.yz) * p3.zy);
      }

      bool InsideRect(float2 p, float2 center, float2 half)
      {
        float2 d = abs(p - center);
        return all(d <= half);
      }

      float4 frag(v2f i) : SV_Target
      {
        // base
        float4 baseCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.col;

        if (_Intensity <= 0.0 || _RectAmount <= 0.0)
          return baseCol;

        // pixel space in texture resolution
        float w = _MainTex_TexelSize.z;
        float h = _MainTex_TexelSize.w;
        float2 p = float2(i.uv.x * w, i.uv.y * h);

        float4 col = baseCol;

        int iter = (int)clamp(_Iterations, 1.0, 4.0);

        [unroll(4)]
        for (int k = 0; k < iter; k++)
        {
          float tScale = (k + 0.5) / max(1, iter);
          float cellSize = lerp(_RectMinSizePx, _RectMaxSizePx, tScale);

          float2 cell = floor(p / cellSize);
          float2 rA = Hash21(cell + float2(_Seed, k*17));
          float2 rB = Hash21(cell + float2(_Seed*1.37 + 13.0, k*29));
          float2 rC = Hash21(cell + float2(_Seed*2.11 + 7.0,  k*41));

          if (rA.x < (_RectAmount * _Intensity))
          {
            // source rect in texture space
            float sx = lerp(_RectMinSizePx, _RectMaxSizePx, rB.x);
            float sy = lerp(_RectMinSizePx, _RectMaxSizePx, rC.x);
            float sAvg = (sx + sy) * 0.5;
            sx = lerp(sAvg, sx, _Aniso);
            sy = lerp(sAvg, sy, _Aniso);
            float2 half = 0.5 * float2(sx, sy);

            float2 srcCenter = (cell + 0.5) * cellSize;        // 중심
            float2 dstOffset = (rC.yx - 0.5) * _RectMaxShiftPx * 2.0;
            float2 dstCenter = srcCenter + dstOffset;          // 붙여넣기 위치

            if (InsideRect(p, dstCenter, half))
            {
              // 현재 픽셀 p를 소스 직사각형 좌표로 매핑하여 샘플
              float2 local = (p - dstCenter) + srcCenter;
              float2 uvSrc = local / float2(w, h);
              float4 piece = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvSrc) * i.col;

              // 완전 덮어쓰기(원하면 lerp로 섞기 가능)
              col = piece;
              // col = lerp(col, piece, _Intensity); // blend 대체 시
            }
          }
        }

        return col;
      }
      ENDHLSL
    }
  }
  FallBack "UI/Default"
}
