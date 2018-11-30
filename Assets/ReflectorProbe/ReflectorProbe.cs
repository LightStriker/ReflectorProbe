using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace Reflector
{
    [RequireComponent(typeof(ReflectionProbe)), ExecuteInEditMode]
    public class ReflectorProbe : MonoBehaviour//, ISectorizable
    {
        private static HashSet<ReflectorProbe> instances = new HashSet<ReflectorProbe>();

        public static ReflectorProbe[] Instances
        {
            get { return instances.ToArray(); }
        }

        private static HashSet<ReflectorProbe> dynamics = new HashSet<ReflectorProbe>(); 

        private static Dictionary<int, RenderPair> targets = new Dictionary<int, RenderPair>();

        private static RenderPair[] Renders
        {
            get { return targets.Values.ToArray(); }
        }

        private static Camera renderCamera;

        private static Material mirror = null;

        private static Quaternion[] orientations = new Quaternion[]
        {
            Quaternion.LookRotation(Vector3.right, Vector3.down),
            Quaternion.LookRotation(Vector3.left, Vector3.down),
            Quaternion.LookRotation(Vector3.up, Vector3.forward),
            Quaternion.LookRotation(Vector3.down, Vector3.back),
            Quaternion.LookRotation(Vector3.forward, Vector3.down),
            Quaternion.LookRotation(Vector3.back, Vector3.down)
        };

        private RenderTexture cubemap;

        public RenderTexture Cubemap
        {
            get { return cubemap; }
        }

        private Coroutine refreshing;

        public bool Refreshing
        {
            get { return refreshing != null; }
        }

        private bool visible = false;

        public bool Visible
        {
            get { return visible; }
            set { visible = value; }
        }

        public GameObject GameObject
        {
            get { return gameObject; }
        }

        [SerializeField]
        private bool bakeable = false;

        [SerializeField]
        private Texture baked;

        public Texture Baked
        {
            get { return baked; }
        }

        [SerializeField]
        private Camera customCamera;

        private Camera customCameraInstance;
        private Camera externalCamera;

        private ImageEffectPair[] effects;

        private float cullRenderTimer = 0;
        private bool rendering = false;

        public Camera Camera
        {
            get
            {
                if (externalCamera != null)
                    return externalCamera;

                if (customCameraInstance != null)
                    return customCameraInstance;

                if (customCamera == null)
                {
                    if (renderCamera == null)
                        renderCamera = new GameObject("ReflectionCamera").AddComponent<Camera>();

                    return renderCamera;
                }
                else
                {
                    customCameraInstance = Instantiate(customCamera.gameObject).GetComponent<Camera>();
                    return customCameraInstance;
                }
            }
            set
            {
                externalCamera = value;
            }
        }

        private void OnEnable()
        {
            instances.Add(this);

            ReflectionProbe probe = GetComponent<ReflectionProbe>();
            probe.hideFlags = HideFlags.None;
            probe.mode = ReflectionProbeMode.Custom;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.customBakedTexture = baked;

            //CullingCamera.PostCull += PostCullRender;

#if UNITY_EDITOR
            UnityEditorInternal.InternalEditorUtility.SetIsInspectorExpanded(probe, false);
#endif
        }

        private void OnDisable()
        {
            instances.Remove(this);
            if (customCameraInstance != null)
                Destroy(customCameraInstance.gameObject);

            ResetReflection();

            //CullingCamera.PostCull -= PostCullRender;
        }

        private void OnDrawGizmos()
        {
            ReflectionProbe probe = GetComponent<ReflectionProbe>();
            Gizmos.color = new Color(1, 0.4f, 0, 0.3f);
            Gizmos.matrix = transform.localToWorldMatrix;

            Vector3 center = probe.center;

            Gizmos.DrawWireCube(center, probe.size);
            Gizmos.matrix = Matrix4x4.identity;
        }

        private void OnDrawGizmosSelected()
        {
            ReflectionProbe probe = GetComponent<ReflectionProbe>();
            Gizmos.color = new Color(1, 0.4f, 0, 0.1f);
            Gizmos.matrix = transform.localToWorldMatrix;

            Vector3 center = probe.center;

            Gizmos.DrawCube(center, probe.size);
            Gizmos.matrix = Matrix4x4.identity;
        }

        public Cubemap BakeReflection()
        {
            if (Application.isPlaying || !bakeable)
                return null;

            CreateData();

            int resolution = GetComponent<ReflectionProbe>().resolution;

            RenderPair pair;
            if (!targets.TryGetValue(resolution, out pair))
                return null;

            Cubemap cubemap = new Cubemap(resolution, TextureFormat.RGB24, true);
            Texture2D reader = new Texture2D(resolution, resolution, TextureFormat.RGB24, true, true);

            Camera camera = Camera;
            camera.gameObject.SetActive(true);
            camera.transform.position = transform.position;
            camera.targetTexture = pair.Render;

            SetCameraSettings(camera);

            for (int face = 0; face < 6; face++)
            {
                camera.transform.rotation = orientations[face];

                Shader.EnableKeyword("NO_REFLECTION");
                camera.Render();
                Shader.DisableKeyword("NO_REFLECTION");

                Graphics.Blit(pair.Render, pair.Mirror, mirror);

                RenderTexture source = pair.Mirror;
                RenderTexture destination = pair.Render;
                for (int i = 0; i < effects.Length; i++)
                {
                    if (effects[i].Render(source, destination))
                    {
                        RenderTexture temp = source;
                        source = destination;
                        destination = temp;
                    }
                }

                RenderTexture.active = source;
                reader.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
                cubemap.SetPixels(reader.GetPixels(), (CubemapFace)face);

                Clear(pair);

                RenderTexture.active = null;
            }

            cubemap.Apply();
            GetComponent<ReflectionProbe>().customBakedTexture = cubemap;
            baked = cubemap;

            DestroyImmediate(reader);
            DestroyImmediate(camera.gameObject);

            ResetReflection();

            return cubemap;
        }

        public void ClearBaking()
        {
            baked = null;
            GetComponent<ReflectionProbe>().customBakedTexture = null;
        }

        public void RefreshReflection(RefreshMode refresh = RefreshMode.Default)
        {
            RefreshReflectionAt(refresh, transform.position); 
        }

        public void RefreshReflectionAt(RefreshMode refresh, Vector3 position)
        {
            CreateData();
            CreateCubemap();

            int resolution = GetComponent<ReflectionProbe>().resolution;

            RenderPair pair;
            if (!targets.TryGetValue(resolution, out pair))
            {
                refreshing = null;
                return;
            }

            Camera camera = Camera;
            camera.transform.position = position;
            camera.targetTexture = pair.Render;

            SetCameraSettings(camera);

            switch (refresh)
            {
                case RefreshMode.Default:
                    break;
                case RefreshMode.Instant:
                    RefreshInstant(pair, camera);
                    break;
                case RefreshMode.Face:
                    refreshing = StartCoroutine(RefreshFace(pair, camera));
                    break;
                case RefreshMode.Overtime:
                    refreshing = StartCoroutine(RefreshOvertime(pair, camera));
                    break;
            }

            camera.gameObject.SetActive(false);
        }

        public void ResetReflection()
        {
            dynamics.Remove(this);

            ReflectionProbe probe = GetComponent<ReflectionProbe>();
            probe.customBakedTexture = baked;
            int resolution = probe.resolution;

            RenderPair pair;
            if (targets.TryGetValue(resolution, out pair))
            {
                pair.Reflections.Remove(this);
                if (pair.Reflections.Count == 0)
                {
                    pair.Release();
                    targets.Remove(resolution);
                }
            }

            if (cubemap != null)
            {
                cubemap.Release();
                cubemap = null;
            }
        }

        private void RefreshInstant(RenderPair pair, Camera camera)
        {
            try
            {
                for (int face = 0; face < 6; face++)
                {
                    camera.transform.rotation = orientations[face];

                    Shader.EnableKeyword("NO_REFLECTION");
                    camera.Render();
                    Shader.DisableKeyword("NO_REFLECTION");

                    Graphics.Blit(pair.Render, pair.Mirror, mirror);

                    RenderTexture source = pair.Mirror;
                    RenderTexture destination = pair.Render;
                    for (int i = 0; i < effects.Length; i++)
                    {
                        if (effects[i].Render(source, destination))
                        {
                            RenderTexture temp = source;
                            source = destination;
                            destination = temp;
                        }
                    }

                    Graphics.CopyTexture(source, 0, 0, cubemap, face, 0);

                    Clear(pair);
                }

                cubemap.GenerateMips();
            }
            finally
            {
                rendering = false;
                refreshing = null;
            }
        }

        private IEnumerator RefreshFace(RenderPair pair, Camera camera)
        {
            try
            {
                for (int face = 0; face < 6; face++)
                {
                    camera.transform.rotation = orientations[face];

                    Shader.EnableKeyword("NO_REFLECTION");
                    camera.Render();
                    Shader.DisableKeyword("NO_REFLECTION");

                    Graphics.Blit(pair.Render, pair.Mirror, mirror);
                    Graphics.CopyTexture(pair.Mirror, 0, 0, cubemap, face, 0);

                    Clear(pair);

                    yield return null;
                }

                cubemap.GenerateMips();
            }
            finally
            {
                rendering = false;
                refreshing = null;
            }
        }

        private IEnumerator RefreshOvertime(RenderPair pair, Camera camera)
        {
            try
            {
                for (int face = 0; face < 6; face++)
                {
                    yield return ClearOvertime(pair);

                    camera.transform.rotation = orientations[face];

                    rendering = true;
                    cullRenderTimer = 0.25f;
                    while (cullRenderTimer > 0)
                    {
                        cullRenderTimer -= Time.deltaTime;
                        yield return null;
                    }

                    yield return null;

                    Graphics.Blit(pair.Render, pair.Mirror, mirror);

                    yield return null;

                    Graphics.CopyTexture(pair.Mirror, 0, 0, cubemap, face, 0);

                    yield return null;
                }

                cubemap.GenerateMips();
            }
            finally
            {
                rendering = false;
                refreshing = null;
            }
        }

        /*public void PostCullRender(CullingCamera camera)
        {
            if (!visible || !rendering)
                return;

            Shader.EnableKeyword("NO_REFLECTION");
            Camera.Render();
            Shader.DisableKeyword("NO_REFLECTION");

            cullRenderTimer = -1;
            rendering = false;
        }*/

        private IEnumerator ClearOvertime(RenderPair pair)
        {
            RenderTexture.active = pair.Render;
            GL.Clear(true, true, Color.clear);

            yield return null;

            RenderTexture.active = pair.Mirror;
            GL.Clear(true, true, Color.clear);

            yield return null;
        }

        private void Clear(RenderPair pair)
        {
            RenderTexture rt = RenderTexture.active;

            RenderTexture.active = pair.Render;
            GL.Clear(true, true, Color.red);

            RenderTexture.active = pair.Mirror;
            GL.Clear(true, true, Color.red);

            RenderTexture.active = rt;
        }

        private void CreateData()
        {
            if (Application.isPlaying)
            {
                if (dynamics.Contains(this))
                    return;

                dynamics.Add(this);
            }

            //if (mirror == null)
            //    mirror = new Material(ShadingManager.MirrorShader);

            if (mirror == null)
                mirror = new Material(Shader.Find("Hidden/ReflectorProbe/Mirror"));

            int resolution = GetComponent<ReflectionProbe>().resolution;

            RenderPair pair;
            if (targets.TryGetValue(resolution, out pair))
            {
                pair.Reflections.Add(this);
            }
            else
            {
                RenderTexture render = new RenderTexture(resolution, resolution, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                render.useMipMap = false;
                render.Create();

                RenderTexture mirror = new RenderTexture(resolution, resolution, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                mirror.useMipMap = false;
                mirror.Create();

                pair = new RenderPair(render, mirror);
                pair.Reflections.Add(this);
                targets.Add(resolution, pair);
            }
        }

        private void CreateCubemap()
        {
            if (cubemap != null)
                return;

            int resolution = GetComponent<ReflectionProbe>().resolution;

            cubemap = new RenderTexture(resolution, resolution, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            cubemap.dimension = TextureDimension.Cube;
            cubemap.useMipMap = true;
            cubemap.autoGenerateMips = false;
            cubemap.Create();

            GetComponent<ReflectionProbe>().customBakedTexture = cubemap;
        }

        private void SetCameraSettings(Camera camera)
        {
            ReflectionProbe probe = GetComponent<ReflectionProbe>();

            camera.hideFlags = HideFlags.HideAndDontSave;
            camera.enabled = false;
            camera.gameObject.SetActive(true);
            camera.cameraType = CameraType.Reflection;
            camera.fieldOfView = 90;

            if (customCamera == null)
            {
                camera.farClipPlane = probe.farClipPlane;
                camera.nearClipPlane = probe.nearClipPlane;
                camera.cullingMask = probe.cullingMask;
                camera.clearFlags = (CameraClearFlags)probe.clearFlags;
                camera.backgroundColor = probe.backgroundColor;
                camera.allowHDR = probe.hdr;
            }

            List<ImageEffectPair> pairs = new List<ImageEffectPair>();

            MonoBehaviour[] behaviours = camera.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MethodInfo method = GetRenderImage(behaviours[i].GetType());
                if (method != null)
                    pairs.Add(new ImageEffectPair(behaviours[i], method));
            }

            effects = pairs.ToArray();
        }

        public static MethodInfo GetRenderImage(Type type)
        {
            while (type != null)
            {
                MethodInfo info = type.GetMethod("OnRenderImage", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (info != null)
                    return info;

                if (type.BaseType != null && type.BaseType != typeof(object) && type.BaseType != typeof(UnityEngine.Object))
                    type = type.BaseType;
                else
                    break;
            }

            return null;
        }

        private struct RenderPair
        {
            private RenderTexture render;

            public RenderTexture Render
            {
                get { return render; }
            }

            private RenderTexture mirror;

            public RenderTexture Mirror
            {
                get { return mirror; }
            }

            private HashSet<ReflectorProbe> reflections;

            public HashSet<ReflectorProbe> Reflections
            {
                get { return reflections; }
            }

            public RenderPair(RenderTexture render, RenderTexture mirror)
            {
                this.render = render;
                this.mirror = mirror;
                reflections = new HashSet<ReflectorProbe>();
            }

            public void Release()
            {
                render.Release();
                mirror.Release();
            }
        }

        private struct ImageEffectPair
        {
            private MonoBehaviour behaviour;
            private MethodInfo method;

            public bool Render(RenderTexture source, RenderTexture destination)
            {
                if (!behaviour.enabled)
                    return false;

                method.Invoke(behaviour, new object[] { source, destination });
                return true;
            }

            public ImageEffectPair(MonoBehaviour behaviour, MethodInfo method)
            {
                this.behaviour = behaviour;
                this.method = method;
            }
        }
    }
}
