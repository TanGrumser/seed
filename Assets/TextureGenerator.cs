using UnityEngine;
using System.Collections.Generic;

public struct RootAgent
{
    public Vector2 position;
    public float angle;
    public int rootIndex;
    public int age;
    public int alive;
    public float speed;
}

public class TextureGenerator : MonoBehaviour 
{
    public GameObject display;

    public ComputeShader computeShader;
    private RenderTexture dataTexture;
    private RenderTexture displayTexture;

    ComputeBuffer rootBuffer;
    RootAgent[] roots = new RootAgent[1];

    private uint agentCount;

    private void Start() {
        Debug.Log(sizeof(float) * 4 + sizeof(int) * 3);
        // Initialize all textures
        dataTexture = new RenderTexture(Screen.width, Screen.height, 3, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        dataTexture.enableRandomWrite = true;
        dataTexture.Create();

        displayTexture = new RenderTexture( Screen.width, Screen.height, 3, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        displayTexture.enableRandomWrite = true;
        displayTexture.Create();
        
        display.GetComponent<MeshRenderer>().material.mainTexture = displayTexture;   

        int updateKernel = computeShader.FindKernel("Update");
        int growKernel = computeShader.FindKernel("Grow");
        computeShader.SetTexture(updateKernel, "DataTexture", dataTexture);
        computeShader.SetTexture(growKernel, "Texture", dataTexture);
        computeShader.SetFloat("width", Screen.width);
        computeShader.SetFloat("height", Screen.height);

        RootAgent root = new RootAgent();
        root.position = new Vector2(Screen.width / 2f, Screen.height);
        root.angle = Mathf.PI * 1.5f;
        root.age = 0;
        root.alive = 1;
        RootAgent[] roots = new RootAgent[1];
        root.speed = 1f;
        roots[0] = root;

        agentCount = 1;

        //CreateAndSetBuffer<RootAgent>(ref rootBuffer, roots, computeShader, "roots", updateKernel);
        rootBuffer = new ComputeBuffer((int)agentCount, sizeof(float) * 4 + sizeof(int) * 3);
        rootBuffer.SetData(roots);
        computeShader.SetBuffer(updateKernel, "roots", rootBuffer);

        int displayKernel = computeShader.FindKernel("Display");

        computeShader.SetTexture(displayKernel, "Source", dataTexture);
        computeShader.SetTexture(displayKernel, "Result", displayTexture);

        computeShader.SetInt("width", Screen.width);
		computeShader.SetInt("height", Screen.height);
    }

    private void FixedUpdate() {
        try {
            UpdateTexture();
        } catch (System.Exception e){
            Debug.Break();
            Debug.Log(e.ToString());
        }
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
        Debug.Log(agentCount);

        for (int i = 0; i < agents.Length; i++) {
            RootAgent currentRoot = agents[i];

            if (currentRoot.alive == 0) {
                dirtyAgents = true;
                if (currentRoot.age > 8) {
                    break;
                }

                RootAgent left = new RootAgent();
                left.position = currentRoot.position;
                left.angle = currentRoot.angle + 0.7f;
                left.age = ++currentRoot.age;
                left.speed = 1;
                left.alive = 1;

                RootAgent right = new RootAgent();
                right.position = currentRoot.position;
                right.angle = currentRoot.angle - 0.7f;
                right.age = ++currentRoot.age;
                right.speed = 1;
                right.alive = 1; 

                newAgents.Add(left);
                newAgents.Add(right);
            } else {
                newAgents.Add(currentRoot);
            }
        }

        agentCount = (uint)newAgents.Count;

        if (dirtyAgents && agentCount != 0) {
            RootAgent[] roots = newAgents.ToArray();
            rootBuffer.Release();
            rootBuffer = new ComputeBuffer((int)agentCount, sizeof(float) * 4 + sizeof(int) * 3);
            rootBuffer.SetData(roots);
            computeShader.SetBuffer(updateKernel, "roots", rootBuffer);
        }

        computeShader.SetFloat("time", System.DateTime.Now.ToUniversalTime().Millisecond);
        computeShader.SetFloat("deltaTime", Time.deltaTime);
        computeShader.SetFloat("numRoots", agentCount);
        
        if (agentCount > 0) {
            Debug.Log("Dispatching compute shader");
            computeShader.Dispatch(updateKernel, (int)agentCount, 1, 1);
        }

        int growKernel = computeShader.FindKernel("Grow");        
        int workgroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int workgroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        computeShader.Dispatch(growKernel, workgroupsX, workgroupsY, 1);
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