using UnityEngine;
using System.Collections.Generic;
using System;

public struct RootAgent
{
    public Vector2 position;
    public float angle;
    public int rootIndex;
    public int age;
    public int alive;
    public float speed;
    public float plantTime;
}

public class TextureGenerator : MonoBehaviour 
{
    public GameObject display;

    public ComputeShader computeShader;
    private RenderTexture trialTexture;
    private RenderTexture dataTexture;
    private RenderTexture displayTexture;

    ComputeBuffer rootBuffer;
    ComputeBuffer countBuffer;
    List<RootAgent> roots = new List<RootAgent>();
    int agentStide = System.Runtime.InteropServices.Marshal.SizeOf(typeof(RootAgent));

    private uint agentCount;

    private void Start() {
        // Initialize all textures
        dataTexture = new RenderTexture(Screen.width, Screen.height, 3, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        dataTexture.enableRandomWrite = true;
        dataTexture.Create();
        Debug.Log(agentStide);
        trialTexture = new RenderTexture(Screen.width, Screen.height, 3, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        trialTexture.enableRandomWrite = true;
        trialTexture.Create();

        displayTexture = new RenderTexture( Screen.width, Screen.height, 3, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        displayTexture.enableRandomWrite = true;
        displayTexture.Create();
        
        display.GetComponent<MeshRenderer>().material.mainTexture = displayTexture;   

        int updateKernel = computeShader.FindKernel("Update");
        int growKernel = computeShader.FindKernel("Grow");
        computeShader.SetTexture(updateKernel, "TrialTexture", dataTexture);
        computeShader.SetTexture(growKernel, "DataTexture", dataTexture);
        // computeShader.SetFloat("width", Screen.width);
        // computeShader.SetFloat("height", Screen.height);

        RootAgent root = new RootAgent();
        root.position = new Vector2(Screen.width / 2f, Screen.height);
        root.angle = Mathf.PI * 1.5f;
        root.age = 0;
        root.alive = 1;
        root.speed = 1f;
        root.plantTime = Time.realtimeSinceStartup;
        roots.Add(root);

        agentCount = 1;

        //CreateAndSetBuffer<RootAgent>(ref rootBuffer, roots, computeShader, "roots", updateKernel);
        rootBuffer = new ComputeBuffer((int)agentCount, agentStide);
        rootBuffer.SetData(roots.ToArray());
        computeShader.SetBuffer(updateKernel, "roots", rootBuffer);

        countBuffer = new ComputeBuffer(1, sizeof(int));
        int[] agentCountArray = {(int)agentCount};
        countBuffer.SetData(agentCountArray);
        computeShader.SetBuffer(updateKernel, "rootCount", countBuffer);

        int displayKernel = computeShader.FindKernel("Display");

        computeShader.SetTexture(displayKernel, "Source", dataTexture);
        computeShader.SetTexture(displayKernel, "Result", displayTexture);

        computeShader.SetInt("width", Screen.width);
		computeShader.SetInt("height", Screen.height);
		computeShader.SetInt("numRoots", (int)agentCount);

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

        

        System.Random rnd = new System.Random();
        for (int i = 0; i < roots.Count; i++) {
            RootAgent root = roots[i];
            
            float aliveTime = Time.realtimeSinceStartup - root.plantTime;

            if (aliveTime >= 1.2) {
                float rndValue = (float)rnd.NextDouble();
                if (aliveTime >= 2.2 || rndValue < 0.5) {
                    root.alive = 0;
                    roots[i] = root;
                    dirtyAgents = true;
                }
            }
        };

        if (dirtyAgents) {
            List<RootAgent> newAgents = new List<RootAgent>();
            RootAgent[] agents = new RootAgent[agentCount];

            rootBuffer.GetData(agents);

            for (int i = 0; i < agentCount; i++) {
                RootAgent currentRoot = agents[i];
                if (roots[i].alive == 0) {
                    if (roots[i].age > 5) {
                        continue;
                    }

                    RootAgent left = new RootAgent();
                    left.position = currentRoot.position;
                    left.angle = currentRoot.angle + 0.7f;
                    left.age = ++currentRoot.age;
                    left.speed = 1;
                    left.alive = 1;
                    left.plantTime = Time.realtimeSinceStartup;

                    newAgents.Add(left);


                    RootAgent right = new RootAgent();
                    right.position = currentRoot.position;
                    right.angle = currentRoot.angle - 0.7f;
                    right.age = ++currentRoot.age;
                    right.speed = 1;
                    right.alive = 1; 
                    right.plantTime = Time.realtimeSinceStartup;

                    newAgents.Add(right);

                } else {
                    newAgents.Add(currentRoot);
                }
            }

            agentCount = (uint)newAgents.Count;

            if (agentCount != 0) {
                roots = newAgents;
                rootBuffer.Release();
                rootBuffer = new ComputeBuffer((int)agentCount, agentStide);
                rootBuffer.SetData(roots.ToArray());
                computeShader.SetBuffer(updateKernel, "roots", rootBuffer);
            }
        }


        computeShader.SetFloat("time", System.DateTime.Now.ToUniversalTime().Millisecond);
        computeShader.SetFloat("deltaTime", Time.deltaTime);
        computeShader.SetFloat("numRoots", agentCount);

        int[] agentCountArray = {(int)agentCount};
        countBuffer.SetData(agentCountArray);
        computeShader.SetBuffer(updateKernel, "rootCount", countBuffer);
        
        if (agentCount > 0) {
            // Debug.Log("Dispatching compute shader");
            computeShader.Dispatch(updateKernel, (int)agentCount, 1, 1);
        }

        // int growKernel = computeShader.FindKernel("Grow");        
        // int workgroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        // int workgroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        // computeShader.Dispatch(growKernel, workgroupsX, workgroupsY, 1);
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

    private static void PrintRoots (RootAgent[] roots) {
        string res = "";
        foreach (RootAgent root in roots) {
            res += "pos: " + root.position.ToString()
            + ", " + root.age;
        }

        Debug.Log(res);
    }

    private void OnDestroy() {
        rootBuffer.Release();
    }

} 