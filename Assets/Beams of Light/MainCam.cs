using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]
public class MainCam : MonoBehaviour
{
    [SerializeField] private Camera _rt_cam;
    [SerializeField] private Camera _this;
    
    [SerializeField] private Transform[] _snapGroup;
    private Vector3[] _originalPositions;
    
    private void OnEnable()
    {
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }
    
    private void OnDisable()
    {
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
    }

    private void OnValidate()
    {
        if (_rt_cam != null && _this != null)
            _rt_cam.orthographicSize = _this.orthographicSize;
    }

    void Update()
    {
        _rt_cam.orthographicSize = _this.orthographicSize;

        if (Input.GetKeyDown(KeyCode.Mouse0))
        {
            Ray ray = Camera.main.ScreenPointToRay (Input.mousePosition);
            RaycastHit[] hit = Physics.RaycastAll(ray, Mathf.Infinity);
            if (hit.Length == 0)
                return;

            float closest = 99999;
            int closest_index = -1;
            for(int i = 0; i  < hit.Length; i++)
            {
                float distance = Vector3.Distance(hit[i].collider.transform.position, transform.position);
                if (distance < closest)
                {
                    closest = distance;
                    closest_index = i;
                }
            }
            // Debug.Log(hit[closest_index].collider.gameObject.name);
        }
    }

    void LateUpdate()
    {
        if (_rt_cam == null || _this == null) return;

        float pixelHeight = _rt_cam.pixelHeight;
        float orthoSize = _rt_cam.orthographicSize;
        float aspect = _rt_cam.aspect;
        
        float S = pixelHeight / (2f * orthoSize);
        
        Vector3 right = _this.transform.right;
        Vector3 up = _this.transform.up;

        Vector3 worldPos = _this.transform.position;
        float x = Vector3.Dot(worldPos, right);
        float y = Vector3.Dot(worldPos, up);

        float snappedX = Mathf.Floor(x * S + 0.5f) / S;
        float snappedY = Mathf.Floor(y * S + 0.5f) / S;
        
        float offsetX = snappedX - x;
        float offsetY = snappedY - y;

        _rt_cam.transform.position = worldPos + right * offsetX + up * offsetY;
        _rt_cam.transform.rotation = _this.transform.rotation;
        
        float uvOffsetX = -offsetX / (2f * orthoSize * aspect);
        float uvOffsetY = -offsetY / (2f * orthoSize);
        
        Shader.SetGlobalVector("_PixelUVOffset", new Vector4(uvOffsetX, uvOffsetY, 0, 0));

        if (_snapGroup != null && _snapGroup.Length > 0)
        {
            if (_originalPositions == null || _originalPositions.Length != _snapGroup.Length)
            {
                _originalPositions = new Vector3[_snapGroup.Length];
            }
            
            for (int i = 0; i < _snapGroup.Length; i++)
            {
                if (_snapGroup[i] == null) continue;
                Vector3 objPos = _snapGroup[i].position;
                _originalPositions[i] = objPos;
                
                // Object snapping is DISABLED because snapping modular environment pieces 
                // causes their relative float coordinates to drift apart on the grid, 
                // which actively tears open invisible seams between flush geometry.
                
                /*
                float objX = Vector3.Dot(objPos, right);
                float objY = Vector3.Dot(objPos, up);
                
                float objSnappedX = Mathf.Floor(objX * S + 0.5f) / S;
                float objSnappedY = Mathf.Floor(objY * S + 0.5f) / S;
                
                float objOffsetX = objSnappedX - objX;
                float objOffsetY = objSnappedY - objY;
                
                _snapGroup[i].position = objPos + right * objOffsetX + up * objOffsetY;
                */
            }
        }
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == _rt_cam)
        {
            if (_snapGroup != null && _originalPositions != null)
            {
                for (int i = 0; i < _snapGroup.Length; i++)
                {
                    if (_snapGroup[i] != null)
                    {
                        _snapGroup[i].position = _originalPositions[i];
                    }
                }
            }
        }
    }
}
