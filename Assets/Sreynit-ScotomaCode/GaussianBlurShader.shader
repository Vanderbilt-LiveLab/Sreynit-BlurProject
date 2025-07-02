Shader "Unlit/GaussianBlurShader"
{
    Properties
    {
        _MainTex ("Base Texture", 2D) = "white" {}
        _OriginalTex ("Original Texture", 2D) = "white" {}
        _GazePos ("Gaze Position", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Blur Radius", Float) = 0.3
        _Sigma ("Gaussian Sigma", Float) = 20.0
        _NeighborhoodSize ("Neighborhood Size", Float) = 0.03
        _Iterations ("Iterations", Float) = 10
        _BlurDirection ("Blur Direction", Vector) = (1, 0, 0, 0)
        //_IsFinalPass ("Is Final Pass", Int) = 0
        _IsPeripheral ("Peripheral Blur", Int) = 0
        _ShowDebug ("Debug Circle", Int) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"


            sampler2D _MainTex;
            sampler2D _OriginalTex;

            float4 _GazePos;
            float _Radius;
            float _Sigma;
            float _NeighborhoodSize;
            float _Iterations;
            float4 _BlurDirection;
            int _IsPeripheral;
            int _ShowDebug;

            float GaussianWeight(float x, float sigma)
            {
                float variance = sigma * sigma;
                return exp(-((x * x) / (2.0 * variance))) / sqrt(6.2831853 * variance);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;

                #if defined(UNITY_SINGLE_PASS_STEREO)
                if (unity_StereoEyeIndex != 0) return tex2D(_OriginalTex, uv);
                #endif

                float dist = distance(uv, _GazePos.xy);
                fixed4 original = tex2D(_OriginalTex, uv);
                fixed4 blurred = tex2D(_MainTex, uv);

                // Gaussian blur logic
                float4 col = 0;
                float sum = 0;
                for (int idx = 0; idx < 50; idx++)
                {
                    if (idx >= _Iterations) break;

                    float normIndex = (idx / (_Iterations - 1.0)) - 0.5;
                    float offset = normIndex * _NeighborhoodSize;
                    //float2 offsetUV = clamp(uv + _BlurDirection.xy * offset, 0.0, 1.0);
                    float2 offsetUV = uv + _BlurDirection.xy * offset;

                    float weight = GaussianWeight(offset, _Sigma);
                    col += tex2D(_MainTex, offsetUV) * weight;
                    sum += weight;
                }
                blurred = col / sum;

                // Debug ring: red outline at radius
                // float ringWidth = 0.002;
                // float edge = smoothstep(_Radius - ringWidth, _Radius, dist) *
                //              (1.0 - smoothstep(_Radius, _Radius + ringWidth, dist));
                // if (edge > 0.01)
                //     return float4(1, 0, 0, 1);

                // Apply full blur  inside radius
                //if (dist <= _Radius)
                //    return blurred;
                //else
                //    return original;

                
                // Apply based on blur type
                fixed4 result;
                if (_IsPeripheral != 0)
                {
                    // Peripheral: blur outside
                    result = dist > _Radius ? blurred : original;
                }
                else
                {
                    // Central: blur inside
                    result = dist <= _Radius ? blurred : original;
                }

                if (_ShowDebug != 0)
                {
                    float ringWidth = 0.002;
                    float edge = smoothstep(_Radius - ringWidth, _Radius, dist) * (1.0 - smoothstep(_Radius, _Radius + ringWidth, dist));
                    if (edge > 0.01)
                    {
                       return float4(1, 0, 0, 1);

                    }
                }
                return result;
            }


            ENDCG
        }
    }
}
