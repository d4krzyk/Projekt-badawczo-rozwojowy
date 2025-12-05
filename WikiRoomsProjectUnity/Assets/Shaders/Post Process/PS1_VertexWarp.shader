Shader "Custom/PS1_PS1Style"
{
    Properties
    {
        _MainTex("Albedo", 2D) = "white" {}
        _GeoRes("Pixel Grid Size (px)", Float) = 8
        _Warp("Warp Strength (0..1)", Range(0,1)) = 0.1
        _Dither("Dither Strength (0..1)", Range(0,1)) = 0.4
        _Jitter("Jitter Amount (px)", Float) = 0.25
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _GeoRes;
            float _Warp;
            float _Dither;
            float _Jitter;

            // UWAGA: _ScreenParams jest deklarowane przez Unity — nie deklaruj ponownie tutaj.

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                noperspective float2 uv : TEXCOORD0; // noperspective -> affine interpolation (PS1 look)
                UNITY_FOG_COORDS(1)
                float4 screenPos : TEXCOORD2; // ComputeScreenPos -> bezpieczna pozycja ekranu
            };

            // 4x4 Bayer matrix (values 0..15)
            int bayer4[16] = {
                 0,  8,  2, 10,
                12,  4, 14,  6,
                 3, 11,  1,  9,
                15,  7, 13,  5
            };

            // cheap 2D hash -> two random floats in [0,1)
            float2 rand2(float2 p)
            {
                float2 a = float2(127.1, 311.7);
                float2 b = float2(269.5, 183.3);
                return frac(sin(float2(dot(p, a), dot(p, b))) * 43758.5453);
            }

            v2f vert(appdata v)
            {
                v2f o;

                // Projektuj do clip-space
                float4 clip = UnityObjectToClipPos(v.vertex);

                // screen pixel coords [0..width, 0..height]
                float2 ndc = clip.xy / clip.w;
                float2 screen = (ndc * 0.5 + 0.5) * _ScreenParams.xy;

                // minimalny jitter — seedujemy pozycją piksela (floor -> stabilne per-pixel)
                float2 seed = floor(screen);
                float2 r = rand2(seed);
                float2 jitterPixels = (r - 0.5) * _Jitter; // w pikselach

                // zastosuj jitter przed snapowaniem
                float2 screenJ = screen + jitterPixels;

                // snap to a screen grid (pixelated vertices)
                float grid = max(1.0, _GeoRes);
                float2 snapped = floor(screenJ / grid + 0.5) * grid;

                // back to NDC
                float2 ndcSnapped = (snapped / _ScreenParams.xy - 0.5) * 2.0;
                float4 clipSnapped = float4(ndcSnapped * clip.w, clip.z, clip.w);

                // mix original and snapped (kontrolowane przez _Warp)
                float4 finalClip = lerp(clip, clipSnapped, saturate(_Warp));

                o.pos = finalClip;
                o.screenPos = ComputeScreenPos(finalClip);

                // pass original UV but with no perspective correction -> affine mapping
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                UNITY_TRANSFER_FOG(o, o.pos);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // sample texture (affine-interpolated UVs produce PS1-style distortion)
                float2 uv = i.uv;
                fixed4 col = tex2D(_MainTex, uv);

                // apply built-in Unity fog (supports ExponentialSquared when enabled in Lighting)
                UNITY_APPLY_FOG(i.fogCoord, col);

                // compute pixel coords reliably using UNITY_PROJ_COORD + _ScreenParams
                float4 scr = UNITY_PROJ_COORD(i.screenPos); // xy = 0..1
                float2 pixel = scr.xy * _ScreenParams.xy;

                // subtle Bayer dithering only as tiny noise (no palette quantization)
                if (_Dither > 0.0001)
                {
                    // compute 4x4 index safely
                    int mx = (int)fmod(floor(pixel.x), 4.0);
                    int my = (int)fmod(floor(pixel.y), 4.0);
                    if (mx < 0) mx += 4;
                    if (my < 0) my += 4;
                    int idx = mx + my * 4;
                    float b = bayer4[idx] / 15.0; // 0..1

                    // very small bias to avoid visible patterning; scale by _Dither
                    float ditherBias = (b - 0.5) * _Dither * 0.02;
                    float3 c = col.rgb + ditherBias;
                    col.rgb = saturate(c);
                }

                return col;
            }

            ENDCG
        }
    }

    FallBack "Diffuse"
}
