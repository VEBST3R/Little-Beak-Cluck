#if UNITY_EDITOR
using LittleBeakCluck.Enemies;
using UnityEditor;
using UnityEngine;

namespace LittleBeakCluck.Enemies.Editor
{
    [CustomEditor(typeof(EnemyBehaviour))]
    public class EnemyBehaviourEditor : UnityEditor.Editor
    {
        private static readonly Color FillColor = new Color(1f, 0f, 0f, 0.1f);
        private static readonly Color OutlineColor = new Color(1f, 0f, 0f, 0.7f);

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var behaviour = (EnemyBehaviour)target;
            if (behaviour.Config == null)
            {
                EditorGUILayout.HelpBox("Assign an Enemy Config to enable scene handles for the attack zone.", MessageType.Info);
            }
        }

        private void OnSceneGUI()
        {
            var behaviour = (EnemyBehaviour)target;
            var config = behaviour.Config;
            if (config == null)
                return;

            Transform anchor = behaviour.AttackAnchor;
            if (anchor == null)
                return;

            Vector3 basePos = anchor.position;
            Vector3 right = anchor.right;
            Vector3 up = anchor.up;

            Vector2 offset = config.attackBoxOffset;
            Vector2 size = config.attackBoxSize;

            Vector3 center = basePos + right * offset.x + up * offset.y;

            DrawRectangle(center, right, up, size);

            float handleSize = HandleUtility.GetHandleSize(center) * 0.5f;

            EditorGUI.BeginChangeCheck();
            Vector3 movedCenter = Handles.Slider2D(center, anchor.forward, right, up, handleSize, Handles.RectangleHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(config, "Move Attack Box");
                Vector3 localCenter = anchor.InverseTransformPoint(movedCenter);
                Vector3 localBase = anchor.InverseTransformPoint(basePos);
                Vector3 delta = localCenter - localBase;
                config.attackBoxOffset = new Vector2(delta.x, delta.y);
                EditorUtility.SetDirty(config);
            }

            handleSize = HandleUtility.GetHandleSize(center) * 0.7f;

            EditorGUI.BeginChangeCheck();
            Vector3 sizeVector = new Vector3(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y), 1f);
            Vector3 scaledSize = Handles.ScaleHandle(sizeVector, center, anchor.rotation, handleSize);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(config, "Resize Attack Box");
                config.attackBoxSize = new Vector2(Mathf.Max(0.05f, scaledSize.x), Mathf.Max(0.05f, scaledSize.y));
                EditorUtility.SetDirty(config);
            }
        }

        private static void DrawRectangle(Vector3 center, Vector3 right, Vector3 up, Vector2 size)
        {
            Vector3 halfRight = right * (size.x * 0.5f);
            Vector3 halfUp = up * (size.y * 0.5f);

            Vector3 v0 = center - halfRight - halfUp;
            Vector3 v1 = center + halfRight - halfUp;
            Vector3 v2 = center + halfRight + halfUp;
            Vector3 v3 = center - halfRight + halfUp;

            Handles.DrawSolidRectangleWithOutline(new[] { v0, v1, v2, v3 }, FillColor, OutlineColor);
        }
    }
}
#endif
