using UnityEngine;
using UnityEngine.EventSystems;

public struct RootAgent
{
    public Vector2 position;
    public float angle;
    public int rootIndex;
}

public class TextureGenerator : MonoBehaviour 
{
    public GameObject display;

    public ComputeShader computeShader;
    private RenderTexture dataTexture;
    private RenderTexture displayTexture;

    ComputeBuffer rootBuffer;

    private void Start() {
        // Initialize all textures
        dataTexture = new RenderTexture(Screen.width, Screen.height, 3, RenderTextureFormat.ARGB32);
        dataTexture.enableRandomWrite = true;
        dataTexture.Create();

        displayTexture = new RenderTexture( Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        displayTexture.enableRandomWrite = true;
        displayTexture.Create();
        
        display.GetComponent<MeshRenderer>().material.mainTexture = displayTexture;   

        int updateKernel = computeShader.FindKernel("Update");
        computeShader.SetTexture(updateKernel, "DataTexture", dataTexture);
        RootAgent root = new RootAgent();
        root.position = new Vector2(1, 1);
        root.angle = 0f;
        RootAgent[] roots = new RootAgent[1];
        roots[0] = root;

        CreateAndSetBuffer<RootAgent>(ref rootBuffer, roots, computeShader, "roots", updateKernel);
        computeShader.SetBuffer(0, updateKernel, rootBuffer);

        int displayKernel = computeShader.FindKernel("Display");

        computeShader.SetTexture(displayKernel, "Source", dataTexture);
        computeShader.SetTexture(displayKernel, "Result", displayTexture);

    }

    private void FixedUpdate() {
        UpdateTexture();
    }

    private void LateUpdate() {
        DisplayTexture();
    }

    private void DisplayTexture() {
        // Use data texture to calculate display texture
        int displayKernel = computeShader.FindKernel("Display");

        int workgroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int workgroupsY = Mathf.CeilToInt(Screen.height / 8.0f);

        computeShader.Dispatch(displayKernel, workgroupsX, workgroupsY, 1);
    }

    private void UpdateTexture() {        
        int updateKernel = computeShader.FindKernel("Update");        
        computeShader.Dispatch(updateKernel, 1, 1, 1);
    }

    public static void CreateAndSetBuffer<T>(ref ComputeBuffer buffer, T[] data, ComputeShader cs, string nameID, int kernelIndex = 0)
    {
        int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
        CreateStructuredBuffer<T>(ref buffer, data.Length);
        buffer.SetData(data);
        cs.SetBuffer(kernelIndex, nameID, buffer);
    }

    public static void CreateStructuredBuffer<T>(ref ComputeBuffer buffer, int count)
    {
        int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
        bool createNewBuffer = buffer == null || !buffer.IsValid() || buffer.count != count || buffer.stride != stride;
        if (createNewBuffer)
        {
            Release(buffer);
            buffer = new ComputeBuffer(count, stride);
        }
    }

    public static void Release(params ComputeBuffer[] buffers)
    {
        for (int i = 0; i < buffers.Length; i++)
        {
            if (buffers[i] != null)
            {
                buffers[i].Release();
            }
        }
    }

    private void OnDestroy() {
        rootBuffer.Release();
    }

} 