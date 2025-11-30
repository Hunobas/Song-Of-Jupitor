Shader "Hidden/Custom/CameraBlur" {
    Properties { }
    SubShader {
        ZTest Always ZWrite Off Cull Off
        Pass {
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _TexelSize;   // x=1/w, y=1/h, z=w, w=h (직접 쓰고 싶으면 유지)
            float  _Intensity;
            float  _ClampPx;
            float  _AngleRad;
            float2 _Center;      // 0..1
            float  _RadiusPx;

            #pragma multi_compile __ TYPE_LINEAR TYPE_RADIAL
            #pragma multi_compile __ METHOD_GAUSS METHOD_FIXED METHOD_PROP

            float gaussianWeight(float x, float sigma) {
                return exp(-0.5 * (x*x) / (sigma*sigma));
            }

            float3 BlurLinear(float2 uv) {
                float2 dir = float2(cos(_AngleRad), sin(_AngleRad));
                float2 stepUV = dir * _RadiusPx * _TexelSize.xy / max(_RadiusPx, 1.0);
                const int TAPS = 13;
                float sigma = max(_RadiusPx * 0.5, 1.0);

                float3 acc = 0; float wsum = 0;
                [unroll] for (int i = -(TAPS/2); i <= (TAPS/2); ++i) {
                    float k = (float)i;
                    float2 uvk = uv + stepUV * k;
                #if defined(METHOD_GAUSS)
                    float w = gaussianWeight(k, sigma);
                #elif defined(METHOD_FIXED)
                    float w = 1.0;
                #else
                    float w = abs(k) + 1.0;
                #endif
                    acc += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvk).rgb * w;
                    wsum += w;
                }
                return acc / max(wsum, 1e-4);
            }

            float3 BlurRadial(float2 uv) {
                float2 dir = normalize(uv - _Center);
                float steps = max(_RadiusPx, 1.0);
                float2 stepUV = dir * (_RadiusPx * _TexelSize.xy) / steps;
                const int TAPS = 13;
                float sigma = max(_RadiusPx * 0.5, 1.0);

                float3 acc = 0; float wsum = 0;
                [unroll] for (int i=0; i<TAPS; ++i) {
                    float t = ((i/(TAPS-1.0)) - 0.5) * 2.0;
                    float2 uvk = uv + stepUV * t * steps;
                #if defined(METHOD_GAUSS)
                    float w = gaussianWeight(t * _RadiusPx, sigma);
                #elif defined(METHOD_FIXED)
                    float w = 1.0;
                #else
                    float w = abs(t) * _RadiusPx + 1.0;
                #endif
                    acc += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvk).rgb * w;
                    wsum += w;
                }
                return acc / max(wsum, 1e-4);
            }

            half4 Frag(Varyings input) : SV_Target {
                float2 uv = input.texcoord;
                float3 col = 0;
            #if defined(TYPE_LINEAR)
                col = BlurLinear(uv);
            #else
                col = BlurRadial(uv);
            #endif
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
