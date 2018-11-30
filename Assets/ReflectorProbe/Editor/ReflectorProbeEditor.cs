using UnityEditor;
using UnityEngine;

namespace Reflector
{
    [CustomEditor(typeof(ReflectorProbe)), CanEditMultipleObjects]
    public class ReflectorProbeEditor : Editor
    {
        private SerializedObject reflectionProbes;

        private SerializedProperty importance;
        private SerializedProperty size;
        private SerializedProperty offset;
        private SerializedProperty projection;
        private SerializedProperty intensity;
        private SerializedProperty blend;

        private SerializedProperty resolution;

        private SerializedProperty customCamera;

        private SerializedProperty nearClip;
        private SerializedProperty farClip;
        private SerializedProperty culling;
        private SerializedProperty occlusion;
        private SerializedProperty clear;
        private SerializedProperty background;
        private SerializedProperty hdr;

        private SerializedProperty baked;
        private SerializedProperty bakeable;

        private GUIContent[] resolutionNames = new GUIContent[] { new GUIContent("16"), new GUIContent("32"), new GUIContent("64"), new GUIContent("128"),
                                                                  new GUIContent("256"), new GUIContent("512"), new GUIContent("1024"), new GUIContent("2048") };
        private int[] resolutionValues = new int[] { 16, 32, 64, 128, 256, 512, 1024, 2048 };

        private void OnEnable()
        {
            ReflectionProbe[] probes = new ReflectionProbe[serializedObject.targetObjects.Length];
            for (int i = 0; i < serializedObject.targetObjects.Length; i++)
                probes[i] = ((ReflectorProbe)serializedObject.targetObjects[i]).GetComponent<ReflectionProbe>();

            reflectionProbes = new SerializedObject(probes);

            importance = reflectionProbes.FindProperty("m_Importance");
            size = reflectionProbes.FindProperty("m_BoxSize");
            offset = reflectionProbes.FindProperty("m_BoxOffset");
            projection = reflectionProbes.FindProperty("m_BoxProjection");
            intensity = reflectionProbes.FindProperty("m_IntensityMultiplier");
            blend = reflectionProbes.FindProperty("m_BlendDistance");

            resolution = reflectionProbes.FindProperty("m_Resolution");

            customCamera = serializedObject.FindProperty("customCamera");

            nearClip = reflectionProbes.FindProperty("m_NearClip");
            farClip = reflectionProbes.FindProperty("m_FarClip");
            culling = reflectionProbes.FindProperty("m_CullingMask");
            clear = reflectionProbes.FindProperty("m_ClearFlags");
            background = reflectionProbes.FindProperty("m_BackGroundColor");
            hdr = reflectionProbes.FindProperty("m_HDR");
            occlusion = reflectionProbes.FindProperty("m_UseOcclusionCulling");

            baked = serializedObject.FindProperty("baked");
            bakeable = serializedObject.FindProperty("bakeable");
        }

        public override void OnInspectorGUI()
        {
            reflectionProbes.Update();
            serializedObject.Update();

            EditorGUILayout.LabelField("Runtime");
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(importance, new GUIContent("Importance"));
            EditorGUILayout.PropertyField(intensity, new GUIContent("Intensity"));
            EditorGUILayout.PropertyField(projection, new GUIContent("Box Projection"));
            EditorGUILayout.PropertyField(blend, new GUIContent("Blend Distance"));
            EditorGUILayout.PropertyField(size, new GUIContent("Size"));
            EditorGUILayout.PropertyField(offset, new GUIContent("Offset"));

            EditorGUI.indentLevel--;

            EditorGUILayout.LabelField("Render");
            EditorGUI.indentLevel++;

            EditorGUILayout.IntPopup(resolution, resolutionNames, resolutionValues);
            EditorGUILayout.PropertyField(customCamera, new GUIContent("Camera Override"));
            if (customCamera.objectReferenceValue == null && !customCamera.hasMultipleDifferentValues)
            {
                EditorGUILayout.PropertyField(clear, new GUIContent("Clear Flags"));
                if (clear.intValue == 2)
                    EditorGUILayout.PropertyField(background, new GUIContent("Background Color"));

                //EditorGUILayout.PropertyField(culling, new GUIContent("Culling Mask"));
                EditorGUILayout.PropertyField(nearClip, new GUIContent("Near Clip"));
                EditorGUILayout.PropertyField(farClip, new GUIContent("Far Clip"));
                EditorGUILayout.PropertyField(occlusion, new GUIContent("Occlusion Culling"));
                EditorGUILayout.PropertyField(hdr, new GUIContent("HDR"));
            }

            EditorGUI.indentLevel--;

            EditorGUILayout.LabelField("Bake");
            EditorGUI.indentLevel++;

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(bakeable, new GUIContent("Bakeable"));
            Object objectReferenceValue = EditorGUILayout.ObjectField(new GUIContent("Baked Cubemap"), baked.objectReferenceValue, typeof(Cubemap), false);
            if (EditorGUI.EndChangeCheck())
                baked.objectReferenceValue = objectReferenceValue;

            EditorGUI.indentLevel--;

            GUILayout.BeginHorizontal(new GUILayoutOption[0]);
            GUILayout.Space(EditorGUIUtility.labelWidth);

            if (GUILayout.Button("Bake"))
            {
                ReflectorProbe[] probes = new ReflectorProbe[serializedObject.targetObjects.Length];
                for (int i = 0; i < probes.Length; i++)
                    probes[i] = serializedObject.targetObjects[i] as ReflectorProbe;

                ReflectorProbeBaker.Bake(probes);
            }

            if (GUILayout.Button("Bake All"))
            {
                ReflectorProbeBaker.BakeReflector();
            }

            if (GUILayout.Button("Clear"))
            {
                ReflectorProbe[] probes = new ReflectorProbe[serializedObject.targetObjects.Length];
                for (int i = 0; i < probes.Length; i++)
                    probes[i] = serializedObject.targetObjects[i] as ReflectorProbe;

                ReflectorProbeBaker.Clear(probes);
            }

            GUILayout.EndHorizontal();

            reflectionProbes.ApplyModifiedProperties();
            serializedObject.ApplyModifiedProperties();
        }

        protected virtual void OnSceneGUI()
        {
        }
    }
}
