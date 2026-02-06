using UnityEngine;
using UnityEngine.U2D.Animation;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LittleBeakCluck
{
    /// <summary>
    /// Фіксить проблеми з зникаючими частинами тіла персонажів на ПК/WebGL.
    /// Проблема виникає через неправильний culling в Unity 2D Animation та різну обробку спрайтів.
    /// </summary>
    [DefaultExecutionOrder(-5000)] // Виконується рано, до початку гри
    public class PlatformRenderingFix : MonoBehaviour
    {
        [Header("Sprite Culling Fix")]
        [Tooltip("Автоматично виправляти SpriteSkin culling для всіх об'єктів")]
        [SerializeField] private bool fixSpriteSkinCulling = true;
        
        [Tooltip("Автоматично знаходити і фіксити всіх персонажів з анімацією")]
        [SerializeField] private bool autoFindAndFixCharacters = true;
        
        [Header("Culling Bounds Multiplier")]
        [Tooltip("Множник для bounds SpriteSkin (більше значення = менше шансів відсікти спрайт)")]
        [SerializeField] private float boundsPadding = 2.0f;
        
        [Header("Z-Position Fix")]
        [Tooltip("Фіксити Z-position для уникнення z-fighting")]
        [SerializeField] private bool fixZPositions = true;
        
        [Tooltip("Базова Z позиція для спрайтів (відносно батька)")]
        [SerializeField] private float baseZPosition = 0f;
        
        [Header("Platform-Specific Settings")]
        [Tooltip("Застосовувати фікси тільки на Desktop/WebGL")]
        [SerializeField] private bool onlyOnDesktop = true;

        private void Awake()
        {
            // Перевіряємо чи потрібно застосовувати фікси
            if (onlyOnDesktop && IsMobilePlatform())
            {
                Debug.Log("[PlatformRenderingFix] Мобільна платформа - фікси не потрібні");
                return;
            }

            ApplyFixes();
        }

        private bool IsMobilePlatform()
        {
#if UNITY_EDITOR
            // В Editor перевіряємо Build Target
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            return buildTarget == BuildTarget.Android || buildTarget == BuildTarget.iOS;
#else
            return Application.isMobilePlatform;
#endif
        }

        private void ApplyFixes()
        {
            if (autoFindAndFixCharacters)
            {
                FixAllCharacters();
            }

            if (fixSpriteSkinCulling)
            {
                FixSpriteSkins();
            }
        }

        private void FixAllCharacters()
        {
            // Знаходимо всі Animator компоненти (ворогів і гравця)
            var animators = FindObjectsByType<Animator>(FindObjectsSortMode.None);
            
            foreach (var animator in animators)
            {
                // Вимикаємо Culling Mode для аніматора на Desktop
                if (animator != null)
                {
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }

                // Фіксимо SpriteSkin компоненти
                if (fixSpriteSkinCulling)
                {
                    var spriteSkins = animator.GetComponentsInChildren<SpriteSkin>(true);
                    foreach (var spriteSkin in spriteSkins)
                    {
                        FixSpriteSkin(spriteSkin);
                    }
                }

                // Фіксимо Z позиції
                if (fixZPositions)
                {
                    FixSpriteRendererZPositions(animator.transform);
                }
            }

            Debug.Log($"[PlatformRenderingFix] Виправлено {animators.Length} персонажів");
        }

        private void FixSpriteSkins()
        {
            var allSpriteSkins = FindObjectsByType<SpriteSkin>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            
            foreach (var spriteSkin in allSpriteSkins)
            {
                FixSpriteSkin(spriteSkin);
            }

            Debug.Log($"[PlatformRenderingFix] Виправлено {allSpriteSkins.Length} SpriteSkin компонентів");
        }

        private void FixSpriteSkin(SpriteSkin spriteSkin)
        {
            if (spriteSkin == null) return;

            // Вимикаємо автоматичний culling - це основний фікс для проблеми зникаючих частин
            spriteSkin.alwaysUpdate = true;
            
            // Додатково перевіряємо SpriteRenderer
            var spriteRenderer = spriteSkin.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                // Для Desktop встановлюємо вищий пріоритет рендерингу
                spriteRenderer.sortingLayerName = spriteRenderer.sortingLayerName; // Refresh
            }
        }

        private void FixSpriteRendererZPositions(Transform root)
        {
            var spriteRenderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                var sr = spriteRenderers[i];
                if (sr == null) continue;

                var pos = sr.transform.localPosition;
                
                // Додаємо невеликий offset на основі sorting order щоб уникнути z-fighting
                float zOffset = -sr.sortingOrder * 0.001f;
                pos.z = baseZPosition + zOffset;
                
                sr.transform.localPosition = pos;
            }
        }

        /// <summary>
        /// Додайте цей компонент до конкретного персонажа для фіксу
        /// </summary>
        public static void FixCharacter(GameObject character)
        {
            if (character == null) return;

            var animator = character.GetComponent<Animator>();
            if (animator != null)
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            var spriteSkins = character.GetComponentsInChildren<SpriteSkin>(true);
            foreach (var spriteSkin in spriteSkins)
            {
                if (spriteSkin != null)
                {
                    spriteSkin.alwaysUpdate = true;
                }
            }

            Debug.Log($"[PlatformRenderingFix] Виправлено персонажа: {character.name}");
        }

#if UNITY_EDITOR
        [ContextMenu("Force Fix All Characters")]
        private void ForceFixInEditor()
        {
            ApplyFixes();
        }
#endif
    }
}
