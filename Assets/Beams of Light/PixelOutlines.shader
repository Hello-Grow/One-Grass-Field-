

Shader "Hidden/PixelOutlines"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white"
    	_depthEdgeStrength("_depthEdgeStrength", Float) = 0.3
    	_normalEdgeStrength("_normalEdgeStrength", Float) = 0.4
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
        
        TEXTURE2D(_NormalsPassTexture);
        TEXTURE2D(_DepthTexture);
        float4 _NormalsPassTexture_TexelSize;
        float4 _DepthTexture_TexelSize;

        SamplerState sampler_point_clamp;
        
        uniform float _depthEdgeStrength;
        uniform float _normalEdgeStrength;

        float getDepth(int x, int y, float2 vUv) {
        	#if UNITY_REVERSED_Z
            return 1.0 - SAMPLE_TEXTURE2D(_DepthTexture, sampler_point_clamp, vUv + float2(x, y) * _MainTex_TexelSize.xy).r;
			#else
        	return SAMPLE_TEXTURE2D(_DepthTexture, sampler_point_clamp, vUv + float2(x, y) * _MainTex_TexelSize.xy).r;
        	#endif
        }

        float3 getNormal(int x, int y, float2 vUv) {
            return SAMPLE_TEXTURE2D(_NormalsPassTexture, sampler_point_clamp, vUv + float2(x, y) * _MainTex_TexelSize.xy).rgb * 2.0 - 1.0;
        }

		float depthEdgeIndicator(float depth, float2 vUv) {
			float d1 = getDepth(1, 0, vUv);
			float d2 = getDepth(-1, 0, vUv);
			float d3 = getDepth(0, 1, vUv);
			float d4 = getDepth(0, -1, vUv);
			
			float laplacianX = d1 + d2 - 2.0 * depth;
			float laplacianY = d3 + d4 - 2.0 * depth;
			
			// Raised threshold strictly to ignore z-fighting offsets and microscopic planar rasterization cracks!
			float diff = abs(laplacianX) + abs(laplacianY);
			return floor(smoothstep(0.02, 0.08, diff) * 2.) / 2.;
		}
        
        float neighborNormalEdgeIndicator(int x, int y, float depth, float3 normal, float2 vUv)
        {
			float depthDiff = getDepth(x, y, vUv) - depth;
			float3 neighborNormal = getNormal(x, y, vUv);
			
			float normalSimilarity = dot(normal, neighborNormal);
			// Relaxed dot-product threshold to ~30-45 degrees, which completely ignores 
			// model import "smoothed normals" that sometimes bevel the boundary edges of modular flush planes!
			float normalIndicator = smoothstep(0.85, 0.60, normalSimilarity); 
			
			float depthIndicator = clamp(sign(depthDiff * .25 + .0025), 0.0, 1.0);
            return normalIndicator * depthIndicator;
		}

		float normalEdgeIndicator(float depth, float3 normal, float2 vUv)
        {
			float indicator = 0.0;
			indicator += neighborNormalEdgeIndicator(0, -1, depth, normal, vUv);
			indicator += neighborNormalEdgeIndicator(0, 1, depth, normal, vUv);
			indicator += neighborNormalEdgeIndicator(-1, 0, depth, normal, vUv);
			indicator += neighborNormalEdgeIndicator(1, 0, depth, normal, vUv);
			return step(0.1, indicator);
		}

        Varyings vert(Attributes IN)
        {
            Varyings OUT;
            OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
            OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
            // OUT.uv = IN.uv; 
            return OUT;
        }
        ENDHLSL

        Pass
        {
            Name "Pixelation"

            HLSLPROGRAM
            float4 frag(Varyings IN) : SV_TARGET
            {
                float4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_point_clamp, IN.uv);

				float depth = 0.0;
				float3 normal = float3(0., 0., 0.);

				if (_depthEdgeStrength > 0.0 || _normalEdgeStrength > 0.0) {
					depth = getDepth(0, 0, IN.uv);
					normal = getNormal(0, 0,IN.uv);
				}

				float dei = 0.0;
				if (_depthEdgeStrength > 0.0) 
					dei = depthEdgeIndicator(depth, IN.uv);

				float nei = 0.0; 
				if (_normalEdgeStrength > 0.0) 
					nei = normalEdgeIndicator(depth, normal, IN.uv);

            	float strength = dei > 0.0 ? (1.0 - _depthEdgeStrength * dei) : (1.0 + _normalEdgeStrength * nei);
            	
            	// Camera's FAR and NEAR properties directlly correlates to depth outlines since they define the range
            	// of the camera values. Smaller Camera FAR value results in more depth outlines
				// float d = getDepth(0, 0, IN.uv);
				// float4 depthRender = float4(d, d, d, 1);
            	// float4 normalRender = float4(getNormal(0, 0, IN.uv), 1.);

            	// return depthRender;
            	return texel * strength;
            	
                //return float4(normal, 1);
                //return float4(dei, dei, dei, 1);
                return float4(nei, nei, nei, 1);
            }
            ENDHLSL
        }

        
    }
}