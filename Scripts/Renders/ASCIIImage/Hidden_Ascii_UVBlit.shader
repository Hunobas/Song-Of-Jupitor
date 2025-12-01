Shader "Hidden/Ascii/UVBlit"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _UVRect  ("UVRect (x,y,w,h)", Vector) = (0,0,1,1)
        _FlipY   ("FlipY (0/1)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Opaque"
            "Queue"           = "Overlay"
            "IgnoreProjector" = "True"
        }

        ZTest Always
        ZWrite Off
        Cull Off
        Blend One Zero

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4    _MainTex_TexelSize;
            float4    _UVRect;           
            float     _FlipY;            

            struct appdata
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            // 정점 셰이더:
            // - 클립 공간 위치 계산
            // - 0..1 범위의 기본 UV를 _UVRect 사각형 내부로 매핑
            // - 필요 시 Y 플립을 적용
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);

                float2 baseUV = v.texcoord;

                baseUV.y = (_FlipY > 0.5) ? (1.0 - baseUV.y) : baseUV.y;

                float2 uvMin = _UVRect.xy;
                float2 uvMax = _UVRect.xy + _UVRect.zw;
                o.uv = lerp(uvMin, uvMax, baseUV);

                return o;
            }

            // 프래그먼트 셰이더:
            // - 지정된 UVRect / FlipY를 반영한 위치에서 텍스처를 샘플링
            // - 다운샘플 RT의 Bilinear 필터와 합쳐져 평균 효과를 제공
            half4 frag (v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv);
            }
            ENDHLSL
        }
    }

    Fallback Off
}