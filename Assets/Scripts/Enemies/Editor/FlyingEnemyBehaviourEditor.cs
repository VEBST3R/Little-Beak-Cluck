#if UNITY_EDITOR
using LittleBeakCluck.Enemies;
using UnityEditor;
using UnityEngine;

namespace LittleBeakCluck.Enemies.Editor
{
    [CustomEditor(typeof(FlyingEnemyBehaviour))]
    public class FlyingEnemyBehaviourEditor : UnityEditor.Editor
    {
        private static readonly Color PatrolZoneFill = new Color(0f, 1f, 1f, 0.1f);
        private static readonly Color PatrolZoneOutline = new Color(0f, 1f, 1f, 0.6f);
        private static readonly Color AttackFill = new Color(1f, 0f, 0f, 0.1f);
        private static readonly Color AttackOutline = new Color(1f, 0f, 0f, 0.7f);

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var behaviour = (FlyingEnemyBehaviour)target;
            var config = behaviour.Config as FlyingEnemyConfig;
            
            if (config == null)
            {
                EditorGUILayout.HelpBox("Assign a Flying Enemy Config to enable scene handles.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("Scene View:\n• Cyan box = Patrol Zone (can be moved and resized)\n• Red box = Attack damage zone", MessageType.Info);
            }
        }

        private void OnSceneGUI()
        {
            var behaviour = (FlyingEnemyBehaviour)target;
            var config = behaviour.Config as FlyingEnemyConfig;
            if (config == null)
                return;

            Vector3 enemyPos = behaviour.transform.position;
            
            // Draw and handle patrol zone
            DrawPatrolZone(behaviour, config, enemyPos);
            
            // Draw attack zone
            DrawAttackZone(behaviour, config);
        }

        private void DrawPatrolZone(FlyingEnemyBehaviour behaviour, FlyingEnemyConfig config, Vector3 enemyPos)
        {
            Vector3 zoneCenter = enemyPos + (Vector3)config.patrolZoneOffset;
            Vector3 size = new Vector3(config.patrolZoneSize.x, config.patrolZoneSize.y, 0.01f);

            // Draw the patrol zone
            DrawBox(zoneCenter, size, PatrolZoneFill, PatrolZoneOutline);

            float handleSize = HandleUtility.GetHandleSize(zoneCenter) * 0.15f;

            // Center handle - move entire zone
            EditorGUI.BeginChangeCheck();
            Vector3 newCenter = Handles.PositionHandle(zoneCenter, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(config, "Move Patrol Zone");
                Vector3 delta = newCenter - zoneCenter;
                config.patrolZoneOffset += new Vector2(delta.x, delta.y);
                EditorUtility.SetDirty(config);
            }

            // Size handles - resize zone
            Vector3 halfSize = size * 0.5f;
            
            // Right handle
            DrawResizeHandle(config, zoneCenter, Vector3.right, halfSize.x, true, false, "Resize Patrol Zone Width");
            
            // Left handle
            DrawResizeHandle(config, zoneCenter, Vector3.left, halfSize.x, true, false, "Resize Patrol Zone Width");
            
            // Up handle
            DrawResizeHandle(config, zoneCenter, Vector3.up, halfSize.y, false, true, "Resize Patrol Zone Height");
            
            // Down handle
            DrawResizeHandle(config, zoneCenter, Vector3.down, halfSize.y, false, true, "Resize Patrol Zone Height");

            // Label
            Handles.Label(zoneCenter + Vector3.up * halfSize.y + Vector3.up * 0.5f, 
                $"Patrol Zone\n{config.patrolZoneSize.x:F1} x {config.patrolZoneSize.y:F1}",
                EditorStyles.whiteBoldLabel);
        }

        private void DrawResizeHandle(FlyingEnemyConfig config, Vector3 center, Vector3 direction, float distance, bool affectsWidth, bool affectsHeight, string undoName)
        {
            Vector3 handlePos = center + direction * distance;
            float handleSize = HandleUtility.GetHandleSize(handlePos) * 0.08f;

            EditorGUI.BeginChangeCheck();
            Vector3 newPos = Handles.Slider(handlePos, direction, handleSize, Handles.DotHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(config, undoName);
                Vector3 delta = newPos - handlePos;
                float change = Vector3.Dot(delta, direction) * 2f;
                
                if (affectsWidth)
                {
                    config.patrolZoneSize.x = Mathf.Max(1f, config.patrolZoneSize.x + change);
                }
                if (affectsHeight)
                {
                    config.patrolZoneSize.y = Mathf.Max(1f, config.patrolZoneSize.y + change);
                }
                
                EditorUtility.SetDirty(config);
            }
        }

        private void DrawAttackZone(FlyingEnemyBehaviour behaviour, FlyingEnemyConfig config)
        {
            Transform anchor = behaviour.AttackAnchor;
            if (anchor == null)
                return;

            Vector3 basePos = anchor.position;
            Vector3 right = anchor.right;
            Vector3 up = anchor.up;

            Vector2 offset = config.attackBoxOffset;
            Vector2 size = config.attackBoxSize;

            Vector3 center = basePos + right * offset.x + up * offset.y;

            // Draw attack box
            Vector3 halfRight = right * (size.x * 0.5f);
            Vector3 halfUp = up * (size.y * 0.5f);

            Vector3 v0 = center - halfRight - halfUp;
            Vector3 v1 = center + halfRight - halfUp;
            Vector3 v2 = center + halfRight + halfUp;
            Vector3 v3 = center - halfRight + halfUp;

            Handles.DrawSolidRectangleWithOutline(new[] { v0, v1, v2, v3 }, AttackFill, AttackOutline);

            // Attack box handles
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
        }

        private static void DrawBox(Vector3 center, Vector3 size, Color fillColor, Color outlineColor)
        {
            Vector3 halfSize = size * 0.5f;
            
            Vector3 v0 = center + new Vector3(-halfSize.x, -halfSize.y, 0);
            Vector3 v1 = center + new Vector3(halfSize.x, -halfSize.y, 0);
            Vector3 v2 = center + new Vector3(halfSize.x, halfSize.y, 0);
            Vector3 v3 = center + new Vector3(-halfSize.x, halfSize.y, 0);

            Handles.DrawSolidRectangleWithOutline(new[] { v0, v1, v2, v3 }, fillColor, outlineColor);
        }
    }
}
#endif
