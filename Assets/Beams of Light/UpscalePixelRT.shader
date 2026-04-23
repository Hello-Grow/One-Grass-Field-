Shader "Hidden/UpscalePixelRT"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white"
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE
        #pragma vertex vert
        #pragma fragment frag

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        
        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionHCS : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        TEXTURE2D(_MainTex);
        float4 _MainTex_TexelSize;
		float4 _MainTex_ST;
        SamplerState sampler_point_clamp;
        SamplerState sampler_bilinear_clamp;
        
        float2 _PixelUVOffset;
        
        Varyings vert(Attributes IN)
        {
            Varyings OUT;
            OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
            OUT.uv = IN.uv + _PixelUVOffset; 
            return OUT;
        }
        ENDHLSL

        Pass
        {
            Name "Pixelation"

            HLSLPROGRAM
            float4 frag(Varyings IN) : SV_TARGET
            {
                // Pure point sampling guarantees perfectly sharp pixel art.
                // The smoothing of motion is already naturally handled by the 
                // _PixelUVOffset addition applied to IN.uv in the vertex shader!
                float4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_point_clamp, IN.uv);
                return texel;
            }
            ENDHLSL
        }

        
    }
}