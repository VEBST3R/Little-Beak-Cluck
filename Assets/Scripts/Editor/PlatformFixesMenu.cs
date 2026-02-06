#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using LittleBeakCluck.UI;

namespace LittleBeakCluck.Editor
{
    /// <summary>
    /// Editor утиліти для швидкого додавання Platform Fixes
    /// </summary>
    public static class PlatformFixesMenu
    {
        [MenuItem("Little Beak Cluck/Platform Fixes/Add Canvas Fix to Selected", false, 100)]
        private static void AddCanvasFixToSelected()
        {
            if (Selection.activeGameObject == null)
            {
                EditorUtility.DisplayDialog("Помилка", "Виберіть GameObject з Canvas компонентом", "OK");
                return;
            }

            var canvas = Selection.activeGameObject.GetComponent<Canvas>();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("Помилка", "Виберіть GameObject з Canvas компонентом", "OK");
                return;
            }

            if (Selection.activeGameObject.GetComponent<PlatformCanvasFix>() != null)
            {
                EditorUtility.DisplayDialog("Інфо", "PlatformCanvasFix вже доданий до цього Canvas", "OK");
                return;
            }

            Undo.AddComponent<PlatformCanvasFix>(Selection.activeGameObject);
            EditorUtility.DisplayDialog("Успіх", $"PlatformCanvasFix доданий до {Selection.activeGameObject.name}", "OK");
        }

        [MenuItem("Little Beak Cluck/Platform Fixes/Add Canvas Fix to All Canvas in Scene", false, 101)]
        private static void AddCanvasFixToAllInScene()
        {
            var allCanvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            int added = 0;
            int skipped = 0;

            foreach (var canvas in allCanvas)
            {
                if (canvas.GetComponent<PlatformCanvasFix>() == null)
                {
                    Undo.AddComponent<PlatformCanvasFix>(canvas.gameObject);
                    added++;
                }
                else
                {
                    skipped++;
                }
            }

            EditorUtility.DisplayDialog("Результат", 
                $"Додано PlatformCanvasFix до {added} Canvas\nПропущено (вже є): {skipped}", "OK");
        }

        [MenuItem("Little Beak Cluck/Platform Fixes/Setup Rendering Fix in Scene", false, 102)]
        private static void SetupRenderingFixInScene()
        {
            var existing = Object.FindFirstObjectByType<PlatformRenderingFix>();
            if (existing != null)
            {
                bool replace = EditorUtility.DisplayDialog("PlatformRenderingFix вже існує", 
                    "PlatformRenderingFix вже є в сцені. Видалити і створити новий?", 
                    "Так", "Ні");
                
                if (replace)
                {
                    Undo.DestroyObjectImmediate(existing.gameObject);
                }
                else
                {
                    return;
                }
            }

            var go = new GameObject("PlatformRenderingFix");
            Undo.RegisterCreatedObjectUndo(go, "Create PlatformRenderingFix");
            Undo.AddComponent<PlatformRenderingFix>(go);
            
            EditorUtility.DisplayDialog("Успіх", 
                "PlatformRenderingFix створено!\n\nНалаштуйте параметри в Inspector якщо потрібно.", "OK");
        }

        [MenuItem("Little Beak Cluck/Platform Fixes/Validate All Fixes", false, 103)]
        private static void ValidateAllFixes()
        {
            string report = "=== Platform Fixes Status ===\n\n";

            // Перевірка GameBootstrapper
            var bootstrapper = Object.FindFirstObjectByType<GameBootstrapper>();
            if (bootstrapper != null)
            {
                report += "✓ GameBootstrapper знайдено\n";
                // Тут можна перевірити serialized fields через SerializedObject якщо потрібно
            }
            else
            {
                report += "✗ GameBootstrapper не знайдено в сцені!\n";
            }

            // Перевірка PlatformRenderingFix
            var renderingFix = Object.FindFirstObjectByType<PlatformRenderingFix>();
            if (renderingFix != null)
            {
                report += "✓ PlatformRenderingFix знайдено\n";
            }
            else
            {
                report += "⚠ PlatformRenderingFix не знайдено (додається автоматично при запуску)\n";
            }

            // Перевірка Canvas
            var allCanvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            int canvasWithFix = 0;
            int canvasWithoutFix = 0;

            foreach (var canvas in allCanvas)
            {
                if (canvas.GetComponent<PlatformCanvasFix>() != null)
                {
                    canvasWithFix++;
                }
                else
                {
                    canvasWithoutFix++;
                }
            }

            report += $"\n--- Canvas Status ---\n";
            report += $"Всього Canvas: {allCanvas.Length}\n";
            report += $"З PlatformCanvasFix: {canvasWithFix}\n";
            report += $"Без PlatformCanvasFix: {canvasWithoutFix}\n";

            if (canvasWithoutFix > 0)
            {
                report += "\n⚠ Рекомендовано додати PlatformCanvasFix до всіх Canvas\n";
            }

            // Перевірка EnemyHudController
            var enemyHud = Object.FindFirstObjectByType<UI.EnemyHudController>();
            if (enemyHud != null)
            {
                report += "\n✓ EnemyHudController знайдено\n";
            }
            else
            {
                report += "\n✗ EnemyHudController не знайдено в сцені\n";
            }

            // Перевірка Animator culling
            var animators = Object.FindObjectsByType<Animator>(FindObjectsSortMode.None);
            int alwaysAnimate = 0;
            int withCulling = 0;

            foreach (var animator in animators)
            {
                if (animator.cullingMode == AnimatorCullingMode.AlwaysAnimate)
                {
                    alwaysAnimate++;
                }
                else
                {
                    withCulling++;
                }
            }

            report += $"\n--- Animator Culling ---\n";
            report += $"Всього Animators: {animators.Length}\n";
            report += $"AlwaysAnimate: {alwaysAnimate}\n";
            report += $"З Culling: {withCulling}\n";

            if (withCulling > 0)
            {
                report += "\n⚠ Деякі Animators мають culling (буде виправлено при запуску)\n";
            }

            EditorUtility.DisplayDialog("Platform Fixes Validation", report, "OK");
            Debug.Log(report);
        }

        [MenuItem("Little Beak Cluck/Platform Fixes/Open Documentation", false, 150)]
        private static void OpenDocumentation()
        {
            string path = "Assets/../PLATFORM_FIXES_README.md";
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            
            if (asset != null)
            {
                AssetDatabase.OpenAsset(asset);
            }
            else
            {
                EditorUtility.DisplayDialog("Документація", 
                    "Відкрийте файл PLATFORM_FIXES_README.md в корені проєкту для детальних інструкцій", "OK");
            }
        }

        [MenuItem("Little Beak Cluck/Platform Fixes/Quick Fix All", false, 200)]
        private static void QuickFixAll()
        {
            bool proceed = EditorUtility.DisplayDialog("Quick Fix All", 
                "Це додасть всі необхідні компоненти:\n\n" +
                "- PlatformCanvasFix до всіх Canvas\n" +
                "- PlatformRenderingFix в сцену\n\n" +
                "Продовжити?", 
                "Так", "Ні");

            if (!proceed) return;

            // Додаємо Canvas Fix
            var allCanvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            int canvasFixed = 0;

            foreach (var canvas in allCanvas)
            {
                if (canvas.GetComponent<PlatformCanvasFix>() == null)
                {
                    Undo.AddComponent<PlatformCanvasFix>(canvas.gameObject);
                    canvasFixed++;
                }
            }

            // Додаємо Rendering Fix
            var renderingFix = Object.FindFirstObjectByType<PlatformRenderingFix>();
            bool renderingFixAdded = false;

            if (renderingFix == null)
            {
                var go = new GameObject("PlatformRenderingFix");
                Undo.RegisterCreatedObjectUndo(go, "Create PlatformRenderingFix");
                Undo.AddComponent<PlatformRenderingFix>(go);
                renderingFixAdded = true;
            }

            string result = "=== Quick Fix Complete ===\n\n";
            result += $"Canvas fixed: {canvasFixed}\n";
            result += renderingFixAdded ? "PlatformRenderingFix: Додано\n" : "PlatformRenderingFix: Вже існує\n";
            result += "\nПеревірте GameBootstrapper:\nEnable Platform Fixes = true";

            EditorUtility.DisplayDialog("Quick Fix Complete", result, "OK");
        }
    }
}
#endif
