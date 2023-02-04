using UnityEngine;
using UnityEngine.EventSystems;

public class TextureGenerator : MonoBehaviour 
{

    public ComputeShader TextureShader;
    public ComputeShader DisplayShader;
    private RenderTexture dataTexture;
    private RenderTexture realDisplayTexture;
    private RenderTexture _rTexture;
    public GameObject display;



    private void Start() {
        dataTexture = new RenderTexture(Screen.width, Screen.height, 3, RenderTextureFormat.ARGB32);
        dataTexture.enableRandomWrite = true;
        dataTexture.Create();

        realDisplayTexture = new RenderTexture( Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        realDisplayTexture.enableRandomWrite = true;
        realDisplayTexture.Create();
        
        display.GetComponent<MeshRenderer>().material.mainTexture = realDisplayTexture;


        int kernel = TextureShader.FindKernel("CSMain");
        TextureShader.SetTexture(kernel, "Result", dataTexture);

        DisplayShader.SetTexture(kernel, "Source", dataTexture);
        DisplayShader.SetTexture(kernel, "Result", realDisplayTexture);

        int workgroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int workgroupsY = Mathf.CeilToInt(Screen.height / 8.0f);

        TextureShader.Dispatch(kernel, workgroupsX, workgroupsY, 1);
        DisplayShader.Dispatch(kernel, workgroupsX, workgroupsY, 1);
    }




} 