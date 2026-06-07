Shader "Mukseon/DirectionOutlineGlow"
{
    // 방향 속성 색상 외곽선 글로우(#82, combat_system.md §3).
    // _GlowColor는 EnemyDirectionColorView에서 MaterialPropertyBlock으로 주입한다.
    // 파이프라인 비의존(UnityCG) 언릿 스프라이트 셰이더 — URP 2D에서도 렌더된다.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _GlowColor ("Glow Color", Color) = (1,1,1,1)
        _GlowThickness ("Glow Thickness (texels)", Range(0,8)) = 2.5
        _GlowIntensity ("Glow Intensity", Range(0,4)) = 1.6
        // 아틀라스 내 스프라이트 UV 바운드(min.xy, max.xy). EnemyDirectionColorView가 주입한다.
        // 미설정 시 전체 텍스처(0..1)로 클램핑 → 비아틀라스 단일 텍스처와 동일하게 동작.
        _SpriteRect ("Sprite UV Rect", Vector) = (0,0,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float4 color : COLOR;
                float2 uv    : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _GlowColor;
            float _GlowThickness;
            float _GlowIntensity;
            float4 _SpriteRect;

            v2f vert(appdata IN)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(IN.vertex);
                o.uv = IN.uv;
                o.color = IN.color;
                return o;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, IN.uv) * _Color * IN.color;

                // 8방향 주변 알파를 샘플링해 스프라이트 바깥 외곽 글로우 강도를 계산한다.
                // 샘플 UV를 스프라이트 자신의 UV 바운드(_SpriteRect)로 클램핑해, 아틀라스 패킹 시
                // 오프셋이 이웃 스프라이트의 알파를 침범(bleeding)하지 않도록 한다.
                float2 o = _MainTex_TexelSize.xy * _GlowThickness;
                float2 uvMin = _SpriteRect.xy;
                float2 uvMax = _SpriteRect.zw;
                float a = 0;
                a += tex2D(_MainTex, clamp(IN.uv + float2( o.x, 0), uvMin, uvMax)).a;
                a += tex2D(_MainTex, clamp(IN.uv + float2(-o.x, 0), uvMin, uvMax)).a;
                a += tex2D(_MainTex, clamp(IN.uv + float2( 0,  o.y), uvMin, uvMax)).a;
                a += tex2D(_MainTex, clamp(IN.uv + float2( 0, -o.y), uvMin, uvMax)).a;
                a += tex2D(_MainTex, clamp(IN.uv + float2( o.x,  o.y), uvMin, uvMax)).a;
                a += tex2D(_MainTex, clamp(IN.uv + float2(-o.x, -o.y), uvMin, uvMax)).a;
                a += tex2D(_MainTex, clamp(IN.uv + float2( o.x, -o.y), uvMin, uvMax)).a;
                a += tex2D(_MainTex, clamp(IN.uv + float2(-o.x,  o.y), uvMin, uvMax)).a;

                // 스프라이트 본체 바깥(현재 알파가 낮은 곳)에서만 글로우가 보이도록 한다.
                float outline = saturate(a) * (1.0 - tex.a);
                float glowAlpha = saturate(outline * _GlowIntensity) * _GlowColor.a;

                // 본체 색을 글로우 위에 합성 (스프라이트가 글로우를 가린다).
                fixed3 rgb = tex.rgb * tex.a + _GlowColor.rgb * glowAlpha * (1.0 - tex.a);
                fixed outA = saturate(tex.a + glowAlpha);

                // Blend가 SrcAlpha 기준이므로 스트레이트 컬러로 되돌린다.
                fixed3 outRGB = outA > 1e-4 ? rgb / outA : fixed3(0, 0, 0);
                return fixed4(outRGB, outA);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
