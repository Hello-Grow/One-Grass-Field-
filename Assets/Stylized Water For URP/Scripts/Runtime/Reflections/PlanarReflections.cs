// Copyright 2020, Alexander Ameye, All rights reserved.
// https://alexander-ameye.gitbook.io/stylized-water/

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;

namespace StylizedWater
{
    [ExecuteAlways, DisallowMultipleComponent, AddComponentMenu("Effects/Planar Reflections")]
    [HelpURL("https://alexander-ameye.gitbook.io/stylized-water/")]
    public class PlanarReflections : MonoBehaviour
    {
        [Header("Settings")]
        public Camera targetCamera;
        public GameObject reflectionTarget;
        [Space]
        [Range(0.01f, 1.0f)]
        public float renderScale = 1.0f;
        public float reflectionPlaneOffset = 0.0f;
        public float farClipPlane = 1000f;
        public bool reflectSkybox = true;
        public LayerMask reflectionLayer = -1;
        public bool hideReflectionCamera = true;

        [Header("Debug")]
        [SerializeField] private Camera _reflectionCamera;
        private RenderTexture _reflectionTexture;
        private readonly int _planarReflectionTextureId = Shader.PropertyToID("_PlanarReflectionTexture");

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += DoPlanarReflections;
            reflectionLayer = ~(1 << 4);
        }

        private void OnDisable()
        {
            Cleanup();
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            RenderPipelineManager.beginCameraRendering -= DoPlanarReflections;

            if (_reflectionCamera)
            {
                _reflectionCamera.targetTexture = null;
                SafeDestroyObject(_reflectionCamera.gameObject);
            }

            if (_reflectionTexture)
            {
                RenderTexture.ReleaseTemporary(_reflectionTexture);
                _reflectionTexture = null;
            }
        }

        private void UpdateReflectionCamera(Camera realCamera)
        {
            if (_reflectionCamera == null) _reflectionCamera = InitializeReflectionCamera();

            Vector3 pos = Vector3.zero;
            Vector3 normal = Vector3.up;

            if (reflectionTarget != null)
            {
                pos = reflectionTarget.transform.position + Vector3.up * reflectionPlaneOffset;
                normal = reflectionTarget.transform.up;
            }

            UpdateCamera(realCamera, _reflectionCamera);
            _reflectionCamera.gameObject.hideFlags = (hideReflectionCamera) ? HideFlags.HideAndDontSave : HideFlags.DontSave;
#if UNITY_EDITOR
            EditorApplication.DirtyHierarchyWindowSorting();
#endif
            
            var d = -Vector3.Dot(normal, pos);
            var reflectionPlane = new Vector4(normal.x, normal.y, normal.z, d);

            // Calculate exact reflection matrix for mirror symmetry
            var reflection = Matrix4x4.identity;
            PlanarReflections.CalculateReflectionMatrix(ref reflection, reflectionPlane);

            // Reflected transform for culling and initial state
            Vector3 worldPos = realCamera.transform.position;
            float distToPlane = Vector3.Dot(normal, worldPos) + d;
            Vector3 reflectedPos = worldPos - 2.0f * distToPlane * normal;
            Vector3 reflectedFwd = Vector3.Reflect(realCamera.transform.forward, normal);
            Vector3 reflectedUp = Vector3.Reflect(realCamera.transform.up, normal);
            
            _reflectionCamera.transform.position = reflectedPos;
            _reflectionCamera.transform.rotation = Quaternion.LookRotation(reflectedFwd, reflectedUp);

            if (realCamera.orthographic)
            {
                _reflectionCamera.orthographic = true;
                _reflectionCamera.orthographicSize = realCamera.orthographicSize;
                _reflectionCamera.aspect = realCamera.aspect;
                _reflectionCamera.nearClipPlane = 0.01f;
                _reflectionCamera.farClipPlane = farClipPlane;
                
                // For orthographic, we use the reflected transform directly and flip the projection matrix.
                // This avoids the issues with manual view matrix overrides in URP orthographic mode.
                _reflectionCamera.ResetWorldToCameraMatrix();
                _reflectionCamera.ResetProjectionMatrix();

                var projectionMatrix = realCamera.projectionMatrix;
                projectionMatrix.m00 *= -1; // Flip X-axis for mirroring
                _reflectionCamera.projectionMatrix = projectionMatrix;
            }
            else
            {
                // Apply the mirrored view matrix for perspective
                _reflectionCamera.worldToCameraMatrix = realCamera.worldToCameraMatrix * reflection;

                // For perspective, we use the oblique near clip plane to prevent seeing under the water.
                var clipPlane = CameraSpacePlane(_reflectionCamera, pos - Vector3.up * 0.01f, normal, 1.0f);
                _reflectionCamera.projectionMatrix = realCamera.CalculateObliqueMatrix(clipPlane);
            }

            _reflectionCamera.cullingMask = reflectionLayer;
        }

        private void UpdateCamera(Camera src, Camera dest)
        {
            if (dest == null) return;

            dest.CopyFrom(src);
            dest.useOcclusionCulling = false;

            if (dest.gameObject.TryGetComponent(out UnityEngine.Rendering.Universal.UniversalAdditionalCameraData camData))
            {
                camData.renderShadows = false;
                if (reflectSkybox) dest.clearFlags = CameraClearFlags.Skybox;
                else
                {
                    dest.clearFlags = CameraClearFlags.SolidColor;
                    dest.backgroundColor = Color.black;
                }
            }
        }

        private Camera InitializeReflectionCamera()
        {
            var go = new GameObject("Planar Reflection Camera", typeof(Camera));
            var reflectionCamera = go.GetComponent<Camera>();

            reflectionCamera.transform.SetPositionAndRotation(transform.position, transform.rotation);
            reflectionCamera.depth = -10;
            reflectionCamera.enabled = false;
            reflectionCamera.allowMSAA = false;

            // Ensure URP settings
            var camData = reflectionCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            camData.requiresColorOption = CameraOverrideOption.Off;
            camData.requiresDepthOption = CameraOverrideOption.Off;
            camData.renderShadows = false;

            return reflectionCamera;
        }

        private Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float sideSign)
        {
            var m = cam.worldToCameraMatrix;
            var cameraPosition = m.MultiplyPoint(pos);
            var cameraNormal = m.MultiplyVector(normal).normalized * sideSign;
            return new Vector4(cameraNormal.x, cameraNormal.y, cameraNormal.z, -Vector3.Dot(cameraPosition, cameraNormal));
        }

        private void CreateReflectionTexture(Camera camera)
        {
            var urpAsset = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            float pipelineRenderScale = urpAsset != null ? urpAsset.renderScale : 1.0f;
            var descriptor = GetDescriptor(camera, pipelineRenderScale);

            if (_reflectionTexture)
            {
                if (_reflectionTexture.width != descriptor.width || _reflectionTexture.height != descriptor.height)
                {
                    RenderTexture.ReleaseTemporary(_reflectionTexture);
                    _reflectionTexture = RenderTexture.GetTemporary(descriptor);
                }
            }
            else
            {
                _reflectionTexture = RenderTexture.GetTemporary(descriptor);
            }

            _reflectionCamera.targetTexture = _reflectionTexture;
        }

        RenderTextureDescriptor GetDescriptor(Camera camera, float pipelineRenderScale)
        {
            var width = (int)Mathf.Max(camera.pixelWidth * pipelineRenderScale * renderScale, 1f);
            var height = (int)Mathf.Max(camera.pixelHeight * pipelineRenderScale * renderScale, 1f);

            var descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.DefaultHDR, 16);
            descriptor.msaaSamples = 1;
            descriptor.sRGB = true;

            return descriptor;
        }

        private static bool _isRendering;

        private void DoPlanarReflections(ScriptableRenderContext context, Camera camera)
        {
            if (_isRendering) return;
            if (camera.cameraType == CameraType.Reflection || camera.cameraType == CameraType.Preview) return;
            
            // If a target camera is set, only run for it. 
            if (targetCamera != null)
            {
                if (camera != targetCamera && camera.cameraType != CameraType.SceneView) return;
            }
            else
            {
                // No target camera set — skip the MainCamera (it's just the blit driver
                // in this pixel art setup). Reflections should trigger for _rt_cam.
                if (camera.CompareTag("MainCamera")) return;
            }

            if (!reflectionTarget || camera.pixelWidth < 2) return;

            _isRendering = true;

            UpdateReflectionCamera(camera);
            CreateReflectionTexture(camera);

            var data = new PlanarReflectionSettingData();
            data.Set();
            
            UniversalRenderPipeline.RenderSingleCamera(context, _reflectionCamera);
            
            data.Restore();

            // Apply directly to material
            if (reflectionTarget.TryGetComponent(out MeshRenderer mr))
            {
                if (mr.sharedMaterial) mr.sharedMaterial.SetTexture(_planarReflectionTextureId, _reflectionTexture);
            }
            
            Shader.SetGlobalTexture(_planarReflectionTextureId, _reflectionTexture);

            _isRendering = false;
        }

        class PlanarReflectionSettingData
        {
            private readonly bool fog;
            private readonly int maximumLODLevel;
            private readonly float lodBias;

            public PlanarReflectionSettingData()
            {
                fog = RenderSettings.fog;
                maximumLODLevel = QualitySettings.maximumLODLevel;
                lodBias = QualitySettings.lodBias;
            }

            public void Set()
            {
                GL.invertCulling = true;
                RenderSettings.fog = false;
                QualitySettings.maximumLODLevel = 1;
                QualitySettings.lodBias = lodBias * 0.5f;
            }

            public void Restore()
            {
                GL.invertCulling = false;
                RenderSettings.fog = fog;
                QualitySettings.maximumLODLevel = maximumLODLevel;
                QualitySettings.lodBias = lodBias;
            }
        }

        public static void CalculateReflectionMatrix(ref Matrix4x4 reflectionMatrix, Vector4 plane)
        {
            reflectionMatrix.m00 = (1F - 2F * plane[0] * plane[0]);
            reflectionMatrix.m01 = (-2F * plane[0] * plane[1]);
            reflectionMatrix.m02 = (-2F * plane[0] * plane[2]);
            reflectionMatrix.m03 = (-2F * plane[3] * plane[0]);

            reflectionMatrix.m10 = (-2F * plane[1] * plane[0]);
            reflectionMatrix.m11 = (1F - 2F * plane[1] * plane[1]);
            reflectionMatrix.m12 = (-2F * plane[1] * plane[2]);
            reflectionMatrix.m13 = (-2F * plane[3] * plane[1]);

            reflectionMatrix.m20 = (-2F * plane[2] * plane[0]);
            reflectionMatrix.m21 = (-2F * plane[2] * plane[1]);
            reflectionMatrix.m22 = (1F - 2F * plane[2] * plane[2]);
            reflectionMatrix.m23 = (-2F * plane[3] * plane[2]);

            reflectionMatrix.m30 = 0F;
            reflectionMatrix.m31 = 0F;
            reflectionMatrix.m32 = 0F;
            reflectionMatrix.m33 = 1F;
        }

        void SafeDestroyObject(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isEditor) DestroyImmediate(obj);
            else Destroy(obj);
        }
    }
}
