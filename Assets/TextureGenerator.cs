using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEditor;

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
    public float resolution = 1280;
    public ComputeShader computeShader;
    public ComputeShader floorGenerator;
    private RenderTexture trialTexture;
    private RenderTexture dataTexture;
    private RenderTexture displayTexture;
    private RenderTexture floorTexture;
    private bool isResetable = false;
    private Controller controller;
    public GameObject controllerGO;
    private bool isInit = false;
    private int seedIndex = -1;

	private static Vector2 GetRandomDirection()
	{
		return new Vector2(Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f)).normalized;
	}

    private void Awake() {
        controller = controllerGO.GetComponent<Controller>();
        displayTexture = new RenderTexture( Screen.width, Screen.height, 3, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        displayTexture.enableRandomWrite = true;
        displayTexture.Create();

        display.GetComponent<MeshRenderer>().material.mainTexture = displayTexture;   
        ComputeBuffer gradients = new ComputeBuffer(256, sizeof(float) * 2);
		gradients.SetData(Enumerable.Range(0, 256).Select((i) => GetRandomDirection()).ToArray());

        floorTexture = new RenderTexture(Screen.width, Screen.height, 3, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        floorTexture.enableRandomWrite = true;
        floorTexture.Create();

        int updateKernel = floorGenerator.FindKernel("CSMain");
        floorGenerator.SetTexture(updateKernel, "Result", floorTexture);
        floorGenerator.SetTexture(updateKernel, "Output", displayTexture);
        floorGenerator.SetBuffer(updateKernel, "gradients", gradients);
        floorGenerator.SetFloat("res", (float)resolution);
		floorGenerator.SetFloat("t", (float) EditorApplication.timeSinceStartup);

        int workgroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int workgroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        
        floorGenerator.Dispatch(updateKernel, workgroupsX, workgroupsY, 1);
        Initialize();
        updateTexture = true;  
    }

    ComputeBuffer rootBuffer;
    ComputeBuffer countBuffer;
    List<RootAgent> roots = new List<RootAgent>();
    int agentStide = System.Runtime.InteropServices.Marshal.SizeOf(typeof(RootAgent));

    private uint agentCount;

    private bool updateTexture = false;
    private bool updateDisplay = false;

    public void SeedPlant(Vector3 pos) {
        seedIndex++;
        updateTexture = true;
        updateDisplay = true;
        roots.Clear();
        roots.Add(
            CreateAgent(
                new Vector2(pos.x, Screen.height),
                -Mathf.PI * 0.5f,
                0
            )
        );
        isResetable = true;
        Initialize();
    }

    private void Initialize() {
        
        int updateKernel = computeShader.FindKernel("Update");
        int growKernel = computeShader.FindKernel("Grow");
        if (!isInit) {
        // Initialize all textures
        dataTexture = new RenderTexture(Screen.width, Screen.height, 3, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        dataTexture.enableRandomWrite = true;
        dataTexture.Create();

        trialTexture = new RenderTexture(Screen.width, Screen.height, 3, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        trialTexture.enableRandomWrite = true;
        trialTexture.Create();

        computeShader.SetTexture(updateKernel, "TrialTexture", trialTexture);
        computeShader.SetTexture(growKernel, "TrailInputTexture", trialTexture);
        computeShader.SetTexture(growKernel, "DataOutputTexture", dataTexture);
        }
        agentCount = 1;

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
        computeShader.SetTexture(displayKernel, "FloorTexture", floorTexture);

        int workgroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int workgroupsY = Mathf.CeilToInt(Screen.height / 8.0f);

        computeShader.Dispatch(displayKernel, workgroupsX, workgroupsY, 1);

        InitDisplayTexture();
/*
        computeShader.SetInt("width", Screen.width);
		computeShader.SetInt("height", Screen.height);
		computeShader.SetInt("numRoots", (int)agentCount);*/
        isInit = true; 

    }

    private void InitDisplayTexture() {
        int initKernel = computeShader.FindKernel("InitDisplay");
        computeShader.SetTexture(initKernel, "texDisplay", displayTexture);

        int workgroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int workgroupsY = Mathf.CeilToInt(Screen.height / 8.0f);

        computeShader.Dispatch(initKernel, workgroupsX, workgroupsY, 1);
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
        if (!updateDisplay) {
            return;
        }
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

        if (countBuffer != null) {
            int[] agentCountArray = {(int)agentCount};
            countBuffer.SetData(agentCountArray);
            computeShader.SetBuffer(updateKernel, "rootCount", countBuffer);
        }
        
        if (agentCount > 0) {
            computeShader.Dispatch(updateKernel, (int)agentCount, 1, 1);
        } else {
            Debug.Log("No agents " + isResetable);
            
            if (isResetable) {
                StartCoroutine(ResetSeed());
            }
        }

        int growKernel = computeShader.FindKernel("Grow");        
        int workgroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int workgroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        computeShader.Dispatch(growKernel, workgroupsX, workgroupsY, 1);
    }

    private IEnumerator ResetSeed() {
        isResetable = false;
        yield return new WaitForSeconds(3f);

        controller.Reset();
    }

    private RootAgent CreateAgent(Vector2 pos, float newAngle, int age) {
        RootAgent agent = new RootAgent();
        agent.position = pos;
        agent.angle = newAngle;
        agent.age = ++age;
        agent.speed = 1f;
        agent.width = (5f - age) + 0.2f;
        agent.alive = 1; 
        agent.deadTime = Time.time + UnityEngine.Random.Range(1f, 3f);
        agent.rootIndex = seedIndex;

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
        rootBuffer?.Release();
        countBuffer?.Release();
    }

} 