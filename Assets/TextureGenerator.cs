using UnityEngine;
using System.Collections.Generic;

public struct RootAgent
{
    public Vector2 position;
    public float angle;
    public int rootIndex;
    public int age;
    public int alive;
}

public class TextureGenerator : MonoBehaviour 
{
    public GameObject display;

    public ComputeShader computeShader;
    private RenderTexture dataTexture;
    private RenderTexture displayTexture;

    ComputeBuffer rootBuffer;

    private int agentCount;

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
        root.position = new Vector2(200, 200);
        root.angle = 90f;
        root.age = 0;
        root.alive = 1;
        RootAgent[] roots = new RootAgent[1];
        roots[0] = root;

        agentCount = 1;

        CreateAndSetBuffer<RootAgent>(ref rootBuffer, roots, computeShader, "roots", updateKernel);
        computeShader.SetBuffer(0, updateKernel, rootBuffer);

        int displayKernel = computeShader.FindKernel("Display");

        computeShader.SetTexture(displayKernel, "Source", dataTexture);
        computeShader.SetTexture(displayKernel, "Result", displayTexture);

        computeShader.SetInt("width", Screen.width);
		computeShader.SetInt("height", Screen.height);

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

        bool dirtyAgents = false;
        List<RootAgent> newAgents = new List<RootAgent>();
        RootAgent[] agents = new RootAgent[agentCount];
        rootBuffer.GetData(agents);
        if (agents.Length > 1000) {
            Debug.Log("Too many agents!");
            Debug.Break();
        }
        
        for (int i = 0; i < agents.Length; i++) {
            RootAgent currentRoot = agents[i];
            if (currentRoot.alive == 0) {
                dirtyAgents = true;
                if (currentRoot.age > 5) {
                    return;
                }

                RootAgent left = new RootAgent();
                left.position = currentRoot.position;
                left.angle = currentRoot.angle + 15;
                left.age = currentRoot.age++;
                left.alive = 1; 

                RootAgent right = new RootAgent();
                right.position = currentRoot.position;
                right.angle = currentRoot.angle - 15;
                right.age = currentRoot.age++;
                right.alive = 1; 

                newAgents.Add(left);
                newAgents.Add(right);
            } else {
                newAgents.Add(currentRoot);
            }
        }

        if (dirtyAgents) {
            agentCount = newAgents.Count;
            RootAgent[] roots = newAgents.ToArray();
            CreateAndSetBuffer<RootAgent>(ref rootBuffer, roots, computeShader, "roots", updateKernel);
            computeShader.SetBuffer(0, updateKernel, rootBuffer);
        }

        computeShader.SetFloat("time", Time.time);
        computeShader.Dispatch(updateKernel, agentCount, 1, 1);
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