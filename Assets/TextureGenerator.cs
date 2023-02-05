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
    public float width;
    public float speed;
    public float deadTime;
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

    private bool updateTexture = false;

    public void SeedPlant(Vector3 pos) {
        updateTexture = true;
        roots.Add(
            CreateAgent(
                new Vector2(pos.x, Screen.height),
                -Mathf.PI * 0.5f,
                0
            )
        );
        Initialize();
    }

    private void Initialize() {
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
        computeShader.SetTexture(updateKernel, "TrialTexture", trialTexture);
        computeShader.SetTexture(growKernel, "TrailInputTexture", trialTexture);
        computeShader.SetTexture(growKernel, "DataOutputTexture", dataTexture);

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
        if (!updateTexture) {
            return;
        }
        
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
            
            if (root.deadTime <= Time.time) {
                root.alive = 0;
                roots[i] = root;
                dirtyAgents = true;
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

                    newAgents.Add(
                        CreateAgent(
                            currentRoot.position, 
                            currentRoot.angle- 0.9f,
                            currentRoot.age
                        )
                    );

                    newAgents.Add(
                        CreateAgent(
                            currentRoot.position, 
                            currentRoot.angle + 0.9f,
                            currentRoot.age
                        )
                    );
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

        int growKernel = computeShader.FindKernel("Grow");        
        int workgroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int workgroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        computeShader.Dispatch(growKernel, workgroupsX, workgroupsY, 1);
    }

    private static RootAgent CreateAgent(Vector2 pos, float newAngle, int age) {
        RootAgent agent = new RootAgent();
        agent.position = pos;
        agent.angle = newAngle;
        agent.age = ++age;
        agent.speed = 1f;
        agent.width = (5f - age) + 0.2f;
        agent.alive = 1; 
        agent.deadTime = Time.time + UnityEngine.Random.Range(1f, 3f);

        return agent;
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