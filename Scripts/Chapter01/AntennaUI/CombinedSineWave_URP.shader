Shader "UI/CombinedSineWave (URP)"
{
    Properties
    {
        // UGUI 표준
        [PerRendererData]_MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,1)
        [PerRendererData]_StencilComp ("Stencil Comparison", Float) = 8
        [PerRendererData]_Stencil ("Stencil ID", Float) = 0
        [PerRendererData]_StencilOp ("Stencil Operation", Float) = 0
        [PerRendererData]_StencilWriteMask ("Stencil Write Mask", Float) = 255
        [PerRendererData]_StencilReadMask ("Stencil Read Mask", Float) = 255
        [PerRendererData]_ColorMask ("Color Mask", Float) = 15
        [PerRendererData]_UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        // 파형 파라미터
        _AmpScale   ("Amplitude Scale", Float) = 1
        _LineWidthPx("Line Width (px)", Float) = 1
        _Phase      ("Phase", Float) = 0
        _WaveCount  ("Wave Count", Float) = 0

        // 안티에일리어싱 튜닝
        _AaMinPx    ("AA Min Width (px)", Float) = 0.9
        _CurvSoft   ("Curvature Softness", Float) = 200.0  // 크면 영향↓
    }

    SubShader
    {
        Tags{
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "CanUseSpriteAtlas"="True"
            "PreviewType"="Plane"
            "RenderPipeline"="UniversalPipeline"
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
            Name "UI Combined Sine (A+B)"
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _TextureSampleAdd;
            fixed4 _Color;

            float  _AmpScale;
            float  _LineWidthPx;
            float  _Phase;
            float  _WaveCount;
            float4 _WaveParams[8];    // x=F, y=A, z=phase
            float4 _ClipRect;

            // C#에서 GetPixelAdjustedRect()로 세팅한 '진짜 픽셀' 크기
            float2 _RectSize;

            // AA/곡률 보정 파라미터
            float  _AaMinPx;
            float  _CurvSoft;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float2 uv       : TEXCOORD0;
                float4 color    : COLOR;
                float4 worldPos : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                o.worldPos = v.vertex;
                return o;
            }

            // y(x), y'(x), y''(x) 계산
            void EvalCurve(float x, out float y, out float dydx, out float ddy)
            {
                const float TAU = 6.28318530718;
                float sum = 0.0;
                float dsum = 0.0;
                float d2sum = 0.0;

                [unroll]
                for (int k = 0; k < 8; k++)
                {
                    if (k >= (int)_WaveCount) break;
                    float F = _WaveParams[k].x;
                    float A = _WaveParams[k].y;
                    float P = _WaveParams[k].z;

                    float arg = TAU * F * x + P + _Phase;
                    float s = sin(arg);
                    float c = cos(arg);

                    sum   += A * s;
                    dsum  += A * (TAU * F) * c;
                    d2sum -= A * (TAU * F) * (TAU * F) * s;
                }

                y    = 0.5 + _AmpScale * sum;
                dydx = _AmpScale * dsum;
                ddy  = _AmpScale * d2sum;
            }

            // 곡선과의 근사 수직거리(1차)
            float DistanceToCurve(float2 uv, out float metric, out float curvature)
            {
                float y, dydx, ddy;
                EvalCurve(uv.x, y, dydx, ddy);

                metric = sqrt(1.0 + dydx * dydx);

                // 곡률 k = |y''| / (1 + y'^2)^(3/2)
                curvature = abs(ddy) / pow(metric, 3.0);

                return abs(uv.y - y) / metric;    // divide가 핵심
            }

            fixed4 frag (v2f i) : SV_Target
            {
                #ifdef UNITY_UI_CLIP_RECT
                if (!UnityRectContains(_ClipRect, i.worldPos.xy)) clip(-1);
                #endif

                // px 기준 라인 반폭을 UV로 변환(세로 기준)
                float halfW = (_LineWidthPx * 0.5) / max(_RectSize.y, 1.0);

                // 5-탭 로컬 평균(중앙 + 4축)
                float2 px = 1.0 / max(_RectSize, 1.0); // 한 픽셀 UV
                float metric0, curv0;
                float d0 = DistanceToCurve(i.uv,                 metric0, curv0);

                float metric1, curv1; float d1 = DistanceToCurve(i.uv + float2( px.x, 0), metric1, curv1);
                float metric2, curv2; float d2 = DistanceToCurve(i.uv + float2(-px.x, 0), metric2, curv2);
                float metric3, curv3; float d3 = DistanceToCurve(i.uv + float2( 0,  px.y), metric3, curv3);
                float metric4, curv4; float d4 = DistanceToCurve(i.uv + float2( 0, -px.y), metric4, curv4);

                // 가우시안풍 가중 평균 (중앙 가중 4, 주변 1): 총 8
                float dAvg = (d0 * 4.0 + d1 + d2 + d3 + d4) * (1.0 / 8.0);

                // 곡률도 함께 평균(대략)
                float curvAvg = (curv0 * 4.0 + curv1 + curv2 + curv3 + curv4) * (1.0 / 8.0);

                // 곡률 기반 feather 안정화 (곡률 커질수록 feather를 살짝 줄여 경계 일관성)
                float curvatureFactor = saturate(1.0 / (1.0 + curvAvg * _CurvSoft));

                // fwidth(dAvg)에 곡률 팩터 적용 + 하한(픽셀) 보정
                float pxY = 1.0 / max(_RectSize.y, 1.0);
                float w   = max(fwidth(dAvg) * curvatureFactor, _AaMinPx * pxY);

                // 안정적인 커버리지
                float alpha = 1.0 - smoothstep(halfW, halfW + w, dAvg);

                fixed4 col = i.color;
                col.a *= alpha;

                #if UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif
                return col;
            }
            ENDHLSL
        }
    }
}