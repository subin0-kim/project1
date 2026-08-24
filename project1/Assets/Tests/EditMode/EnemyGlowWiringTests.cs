using Mukseon.Gameplay.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    /// <summary>
    /// 적 프리팹의 글로우 배선 검증(#83).
    ///
    /// 표시 방식이 '외곽선 글로우'일 때 HUD는 "색 오브는 글로우와 중복이니 숨긴다"고 판단한다.
    /// 그 판단은 <see cref="EnemyDirectionColorView"/>가 붙은 적에게 글로우가 <b>실제로</b>
    /// 그려질 때만 성립한다. 컴포넌트만 있고 머티리얼이 Sprites-Default면 글로우도 오브도 없는
    /// 적이 되는데, 화면에는 그냥 "안 죽는 적"으로 보여서 원인을 프리팹까지 되짚기 어렵다.
    /// 런타임 가드(<c>GlowSupported</c>)가 증상을 막아 주지만, 배선 자체가 빠진 것은 여기서 잡는다.
    /// </summary>
    public class EnemyGlowWiringTests
    {
        private const string EnemyPrefabFolder = "Assets/Prefabs/Enemies";
        private const string GlowColorProperty = "_GlowColor";

        [Test]
        public void EveryEnemyWithColorView_HasGlowCapableMaterial()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { EnemyPrefabFolder });
            Assert.That(guids.Length, Is.GreaterThan(0), $"{EnemyPrefabFolder}에서 적 프리팹을 찾지 못했습니다.");

            int checkedCount = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                var view = prefab.GetComponentInChildren<EnemyDirectionColorView>(true);
                if (view == null)
                {
                    // 색상 뷰가 없는 적(예: Boss_CorruptedMountainKing)은 애초에 글로우를 쓰지 않으므로
                    // HUD가 오브를 숨기지 않는다. 그 배선은 #82 범위라 여기서 요구하지 않는다.
                    continue;
                }

                SpriteRenderer renderer = ResolveRenderer(view, prefab);
                Assert.That(renderer, Is.Not.Null, $"{path}: EnemyDirectionColorView가 있는데 SpriteRenderer가 없습니다.");

                Material material = renderer.sharedMaterial;
                Assert.That(material, Is.Not.Null, $"{path}: SpriteRenderer에 머티리얼이 없습니다.");
                Assert.That(material.HasProperty(GlowColorProperty), Is.True,
                    $"{path}: 머티리얼 '{material.name}'에 {GlowColorProperty}가 없어 글로우가 그려지지 않습니다. " +
                    "DirectionGlow.mat을 배선하세요.");

                checkedCount++;
            }

            Assert.That(checkedCount, Is.GreaterThan(0), "글로우를 쓰는 적 프리팹이 하나도 검사되지 않았습니다.");
        }

        // EnemyDirectionColorView.Awake와 같은 순서로 해석한다: 직렬화된 참조 우선, 없으면 자식에서 탐색.
        private static SpriteRenderer ResolveRenderer(EnemyDirectionColorView view, GameObject prefab)
        {
            var serialized = new SerializedObject(view);
            SerializedProperty property = serialized.FindProperty("_spriteRenderer");
            var assigned = property != null ? property.objectReferenceValue as SpriteRenderer : null;
            return assigned != null ? assigned : prefab.GetComponentInChildren<SpriteRenderer>(true);
        }
    }
}
