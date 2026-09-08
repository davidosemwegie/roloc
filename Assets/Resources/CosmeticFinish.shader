Shader "ROLOC/Cosmetic Finish"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Finish ("Finish", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float4 uv : TEXCOORD0; float4 settings : TEXCOORD1; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 p : TEXCOORD0; float4 settings : TEXCOORD1; float4 mask : TEXCOORD2; };
            float _Finish;
            float4 _ClipRect;
            float _UIMaskSoftnessX, _UIMaskSoftnessY;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.p = v.uv.xy; o.color = v.color; o.settings = v.settings;
                float2 pixelSize = o.vertex.w / abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 rect = clamp(_ClipRect, -2e10, 2e10);
                o.mask = float4(v.vertex.xy * 2 - rect.xy - rect.zw,
                    .25 / (.25 * float2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize)));
                return o;
            }
            float shape(float2 p, float inner, float sweep)
            {
                float r = length(p);
                float d = r - 1;
                if (inner > 0) d = max(d, inner - r);
                if (sweep < .999)
                {
                    float angle = atan2(p.x, p.y);
                    if (angle < 0) angle += 6.283185;
                    d = max(d, (angle - sweep * 6.283185) * max(r, .01));
                }
                return d;
            }
            float coverage(float d, float aa) { return 1 - smoothstep(-aa, aa, d); }
            float bell(float x, float width) { return exp(-x * x / width); }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.p;
                float inner = i.settings.x;
                float aa = max(fwidth(length(p)), .001);
                float shaded = fmod(floor(i.settings.w / 2), 2);
                float animate = fmod(i.settings.w, 2) * shaded;
                float shadows = floor(i.settings.w / 4);
                float time = _Time.y * animate;
                float d = shape(p, inner, i.settings.y);
                float face = coverage(d, aa);
                float side = coverage(shape(p + float2(0, i.settings.z), inner, i.settings.y), aa) * shaded;
                float shadow = coverage(shape(p + float2(0, i.settings.z + .09), inner, i.settings.y), .09) * .16 * shadows;
                float radius = length(p);
                float2 direction = p / max(radius, .001);
                float band = inner > 0 ? saturate((radius - inner) / max(1 - inner, .01)) : radius;
                float z = sqrt(saturate(1 - radius * radius));
                float3 normal = inner > 0 ? normalize(float3(direction * (band * 2 - 1), .55)) : normalize(float3(p * .65, max(z, .12)));
                float3 light = normalize(float3(-.45, .65, 1));
                float diffuse = saturate(dot(normal, light));
                float spec = pow(saturate(dot(normal, normalize(light + float3(0,0,1)))), 48);
                float3 baseColor = i.color.rgb;
                float3 rgb = baseColor;
                if (_Finish < .5) // Glass: a clear center, broad window reflections, and a thick polished edge.
                {
                    float rim = smoothstep(.70, .98, radius);
                    rgb = baseColor * (.72 + .20 * diffuse);
                    rgb = lerp(rgb, baseColor * .48, rim * .60);
                    rgb += baseColor * bell(radius - .86, .0007) * .30;
                    float window = bell(p.x + .34 + p.y * .24, .009) * smoothstep(-.2, .38, p.y);
                    float window2 = bell(p.x + .12 + p.y * .24, .0015) * smoothstep(.0, .48, p.y);
                    rgb = lerp(rgb, float3(.94,.98,1), saturate(window * .65 + window2 * .65) * (1 - smoothstep(.78, .98, radius)));
                    rgb = lerp(rgb, float3(.93,.98,1), bell(radius - .956, .00028) * (.22 + .48 * saturate(dot(direction,float2(-.6,.8)))));
                    rgb += float3(.6,.8,1) * bell(p.x - .22, .065) * bell(p.y + .64, .008) * .28;
                }
                else if (_Finish < 1.5) // Pearl: broad nacre sheen rather than nested flat circles.
                {
                    float wave = p.x * 3 + p.y * 4 + z * 5 + sin(time * .45) * .16;
                    float3 nacre = .5 + .5 * cos(wave + float3(0,2.1,4.2));
                    rgb = baseColor * (.76 + .24 * diffuse);
                    rgb = lerp(rgb, lerp(baseColor, float3(1,.96,.93), .62), pow(diffuse, 5) * .6);
                    rgb += (nacre - .4) * .14 * smoothstep(.2,.95,radius);
                    rgb = lerp(rgb, float3(1,.97,.94), spec * .22);
                    rgb += bell(radius - .94,.001) * .055;
                }
                else if (_Finish < 2.5) // Porcelain: an opaque rounded glaze, deep inner wall and soft broad shine.
                {
                    rgb = baseColor * (.62 + .38 * diffuse);
                    rgb = lerp(rgb, float3(1,.99,.94), pow(diffuse, 10) * .48);
                    rgb = lerp(rgb, float3(1,1,.98), spec * .42);
                    rgb *= .84 + .16 * smoothstep(0,.2,band);
                    rgb += bell(band - .88,.003) * .09;
                }
                else // Orbit: concentric machined bands and small orbiting glints.
                {
                    float groove = bell(band - .33,.0016) + bell(band - .69,.0016);
                    rgb = baseColor * (.65 + .35 * diffuse) * (1 - groove * .58);
                    rgb += (bell(band - .41,.001) + bell(band - .77,.001)) * .23;
                    float angle = atan2(p.y,p.x);
                    float glint = pow(saturate(cos(angle - time * .38 - 2.2)), 42);
                    rgb = lerp(rgb, float3(.9,.95,1), glint * .7 * bell(band - .52,.075));
                    rgb += bell(band - .96,.001) * .22;
                }
                rgb = lerp(baseColor, saturate(rgb), shaded);
                float body = max(face, side);
                float3 sideColor = baseColor * float3(.47,.48,.55);
                float3 bodyColor = lerp(sideColor, rgb, face);
                float alpha = body + shadow * (1 - body);
                float3 result = (bodyColor * body + float3(.14,.17,.22) * shadow * (1 - body)) / max(alpha,.0001);
                alpha *= i.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                float2 mask = saturate((_ClipRect.zw - _ClipRect.xy - abs(i.mask.xy)) * i.mask.zw);
                alpha *= mask.x * mask.y;
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - .001);
                #endif
                return fixed4(result, alpha);
            }
            ENDCG
        }
    }
}
