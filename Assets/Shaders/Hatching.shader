Shader "Unlit/Hatching"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _LightDir ("LightDirection", Vector) = (40,50,0,0)
        _ObjectPos("ObjectPosition", Vector) = (0,0,0,0)
        _BrightTAM("BrightTAM", 2D) = "white" {}
        _DarkTAM("DarkTAM", 2D) = "white" {}
        _Noise("Noise", 2D) = "white" {}
        _TilingDistanceLevel("TilingDistanceLevel", float) = 1
        _mipBias("Mip Bias", float) = 0
        _TilingNoise("TilingNoise", float) = 0
        _Rotation("Rotation", float) = 0
        _RotationSteps("RotationSteps", float) = 8
    }
        SubShader
        {

            Tags {"RenderType" = "Opaque" "Queue" = "Geometry" "UniversalMaterialType" = "Lit"}
            ZWrite On
            ZTest LEqual
            LOD 100

            // This pass is used when drawing to a _CameraNormalsTexture texture, and is needed if we want objects with the hatching
            // material to get picked up by the sobel outline post-processing effects (this also allows them to cast shadows, if we want)
            
            //NOTE: the code for this rendering pass is from the Unity git page
            Pass
            {
                Name "DepthNormals"
                Tags
                {
                    "LightMode" = "DepthNormals"
                }

                // -------------------------------------
                // Render State Commands
                ZWrite On
                Cull[_Cull]

                HLSLPROGRAM
                #pragma target 2.0

                // -------------------------------------
                // Shader Stages
                #pragma vertex DepthNormalsVertex
                #pragma fragment DepthNormalsFragment

                // -------------------------------------
                // Material Keywords
                #pragma shader_feature_local _NORMALMAP
                #pragma shader_feature_local _PARALLAXMAP
                #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
                #pragma shader_feature_local_fragment _ALPHATEST_ON
                #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

                // -------------------------------------
                // Unity defined keywords
                #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

                // -------------------------------------
                // Universal Pipeline keywords
                #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

                //--------------------------------------
                // GPU Instancing
                #pragma multi_compile_instancing
                #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

                // -------------------------------------
                // Includes
                #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
                #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
                ENDHLSL
            }

            // Main hatching pass
            Pass
            {

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 nrm : NORMAL;
                float3 tg : TANGENT;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 worldNormal : NORMAL;
                float4 vertex : SV_POSITION; 
                float3 worldPos  : TEXCOORD2;
            };


            sampler2D _MainTex;
            float4 _MainTex_ST;
            half4 _MainTex_TexelSize;
            float3 _LightDir;
            sampler2D _BrightTAM;
            sampler2D _DarkTAM;
            sampler2D _Noise;
            float _mipBias;
            float _TilingDistanceLevel;
            float _ObjectPos;
            float _Rotation;
            float _RotationSteps;

            // Some built-in Unity functions that I'm just defining here
            float4 Unity_Remap_float4(float4 In, float2 InMinMax, float2 OutMinMax)
            {
                return OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
            }
            float4 Unity_Posterize_float4(float4 In, float4 Steps)
            {
                return floor(In / (1 / Steps)) * (1 / Steps);
            }
            float3 PosterizeBlended(float3 lightLevel, float steps)
            {
                float stepSize = 1.0 / steps;
                float set = floor(lightLevel / stepSize) * stepSize;
                float remainder = (lightLevel - set) * 0.9;
                return set + stepSize * saturate(remainder / stepSize);
            }
            float Posterize(float4 In, float4 Steps)
            {
                return floor(In / (1 / Steps)) * (1 / Steps);
            }

            // Vertex shader is where the uv will get rotated depending on light direction
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.nrm);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);

                // We want the "x" portion of the light direction vector to determine hatching orientation
                // (in other words, we want to rotate hatching textures around the z axis)
                // Get angle by taking arctan of y/x of normalized light dir vector
                float3 n = normalize(_LightDir);
                float angle = atan(n.y / n.x) % 2*3.14;

                // Step the angle so that it "stutters" similar to how TAM textures are changed every 20 frames
                float stepAngle = Posterize(Unity_Remap_float4(angle, float2(-3.14/2, 3.14/2), float2(0, 1)), _RotationSteps);

                // Create basic 2D rotation matrix
                float c = cos(stepAngle);
                float s = sin(stepAngle);
                float2x2 mat = { c, -s, s, c };

                // Make sure we're rotating UV about the center
                o.uv -= 0.5;
                o.uv.xy = mul(mat, o.uv.xy);
                o.uv += 0.5;

                return o;
            }

            // Manually setting the mip level this way let's us apply a bias so that we can tweak when mip level changes happen
            // Note, code for this function is mostly from unity's built in mipmap function, just with the inclusion of the bias
            float GetMipLevel(float2 tex_coord, float mipBias)
            {
                float2 dx = ddx(tex_coord);
                float2 dy = ddy(tex_coord);
                // dmax essentially tells us how many texels are being squished for the texture
                float dmax = max(dot(dx, dx), dot(dy, dy));
                return max(0.0, 0.5 * log2(dmax) + mipBias);
            }

            // The most important function! Determines blending of TAM textures based on light level
            float3 GetHatching(float lightLevel, float mipLevel, float2 uv)
            {               
                //We only need two texture lookups since we packed our six TAMs into two RGB textures
                float3 lightHatch = tex2Dlod(_BrightTAM, float4(uv.x, uv.y, 0, mipLevel));
                float3 darkHatch = tex2Dlod(_DarkTAM, float4(uv.x, uv.y, 0, mipLevel));
                
                // We have six tone levels for six different light levels. We want the final output
                // to be either one tone level, or a blend of two tone levels that it falls between.
                // So, multiply our light level by the number of tones we have, and get which "tone
                // section" it belongs to.

                float lightLevelStep = lightLevel * 6;

                float t1 = saturate(lightLevelStep - 0.1);
                float t2 = saturate(lightLevelStep - 1);
                float t3 = saturate(lightLevelStep - 2);
                float t4 = saturate(lightLevelStep - 3);
                float t5 = saturate(lightLevelStep - 4);
                float t6 = saturate(lightLevelStep - 5);

                // We only actually want the two "darkest" non-zero values

                t1 -= t2;
                t2 -= t3;
                t3 -= t4;
                t4 -= t5;
                t5 -= t6;

                float3 finalCol = float3(0, 0, 0);

                // Apply weights
                lightHatch = float3(lightHatch.r * t1, lightHatch.g * t2, lightHatch.b * t3);
                darkHatch = float3(darkHatch.r * t4, darkHatch.g * t5, darkHatch.b * t6);

                finalCol = lightHatch.r + lightHatch.g + lightHatch.b + darkHatch.r + darkHatch.g + darkHatch.b;

                return finalCol;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {                
                float4 clipPos = UnityWorldToClipPos(i.worldPos);

                float4 color = (1, 1, 1, 1);

                // Standard diffuse shading. The "1-" can be removed here to make hatching strokes white
                // Note, you can also use posterize to make an easy ramp shading model here, which creates very distinct
                // hatching texture boundaries. I found that this doesn't look great, but it might be worth exploring.
                float lightLevel = 1-Unity_Remap_float4(dot(i.worldNormal, normalize(_LightDir)), float2(-1,1), float2(0,1));

                // Adjust tiling based on distance (not being used right now)
                float distToCamera = 25-clamp(distance(_WorldSpaceCameraPos, _ObjectPos),0,25);
                float tiling = _TilingDistanceLevel+distToCamera;
                float2 uv = i.uv;

                // Get mip level applying the mip bias as well
                float mipLevel = clamp(round(GetMipLevel(uv * _MainTex_TexelSize.zw, _mipBias)),0,3);

                color.rgb =  1-  GetHatching(lightLevel, mipLevel, uv);

                return color;

            }
            ENDCG
        }
    }
}
