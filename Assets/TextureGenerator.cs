using UnityEngine;

public class TextureGenerator : MonoBehaviour 
{

    public ComputeShader TextureShader;
    private RenderTexture displayedTexture;
    private RenderTexture _rTexture;
    public GameObject display;

    public int width = 1280;
    public int height = 720;


    private void Start() {
        displayedTexture = new RenderTexture(width, height, 3, RenderTextureFormat.ARGB32);
        display.GetComponent<MeshRenderer>().material.mainTexture = displayedTexture;
    }

    private void Update() {
        Debug.Log("OnRenderImage");
        if (_rTexture == null) { 
            
            _rTexture = new RenderTexture( Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _rTexture.enableRandomWrite = true;
            _rTexture.Create();
        }

        TextureShader.SetTexture(0, "Result", _rTexture);

        int kernel = TextureShader.FindKernel("CSMain");
        TextureShader.SetTexture(kernel, "Result", _rTexture);

        int workgroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int workgroupsY = Mathf.CeilToInt(Screen.height / 8.0f);


        TextureShader.Dispatch(kernel, workgroupsX, workgroupsY, 1);
        Graphics.Blit(_rTexture, displayedTexture);
    }    

} 