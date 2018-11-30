using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Reflector
{
    /// <summary>
    /// Bake deferred reflection probe.
    /// </summary>
    public class ReflectorProbeBaker
    {
        [MenuItem("Tools/Bake All Reflector")]
        public static void BakeReflector()
        {
            Bake(UnityEngine.Object.FindObjectsOfType<ReflectorProbe>());
        }

        public static void Bake(ReflectorProbe[] probes)
        {
            try
            {
                string path;
                if (!CreateDirectory(out path))
                    return;

                int count = 0;
                for (int i = 0; i < probes.Length; i++)
                {
                    if (probes[i].Baked)
                        count++;
                }

                int current = 0;
                for (int i = 0; i < probes.Length; i++)
                {
                    ReflectorProbe probe = probes[i];

                    current++;
                    EditorUtility.DisplayProgressBar("Baking Deferred Probe", "Baking: " + probe.name, current / (float)count);

                    Texture previous = probe.GetComponent<ReflectionProbe>().customBakedTexture;
                    string existing = AssetDatabase.GetAssetPath(previous);

                    Cubemap cubemap = probe.BakeReflection();
                    if (cubemap == null)
                        continue;

                    if (string.IsNullOrEmpty(existing))
                    {
                        string asset = "Assets" + path + '/' + probe.name + Guid.NewGuid().ToString() + ".asset";
                        AssetDatabase.CreateAsset(cubemap, asset);
                    }
                    else
                    {
                        AssetDatabase.CreateAsset(cubemap, existing);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            EditorSceneManager.MarkAllScenesDirty();
        }

        public static void Clear(ReflectorProbe[] probes)
        {
            for (int i = 0; i < probes.Length; i++)
            {
                if (probes[i].Baked == null)
                    continue;

                string path = AssetDatabase.GetAssetPath(probes[i].Baked);
                if (string.IsNullOrEmpty(path))
                    continue;

                AssetDatabase.DeleteAsset(path);
                probes[i].ClearBaking();
            }

            EditorSceneManager.MarkAllScenesDirty();
        }

        private static bool CreateDirectory(out string path)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                Debug.LogWarning("Tring to bake reflections from a scene not saved.");
                path = "";
                return false;
            }

            path = scene.path.Split('.')[0];
            string[] subpath = path.Split('/');
            path = "";
            for (int i = 1; i < subpath.Length; i++)
                path += '/' + subpath[i];

            DirectoryInfo dir = new DirectoryInfo(Application.dataPath + path);
            if (!dir.Exists)
            {
                dir.Create();
                AssetDatabase.Refresh();
            }

            return true;
        }
    }
}