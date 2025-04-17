using Unity.Mathematics;
using UnityEngine;

public class ParticleDisplay2D : MonoBehaviour
{

    public Mesh mesh;
    public Shader shader;
    
    Material material;
    ComputeBuffer argsBuffer;
    Bounds bounds;
    //Texture2D gradientTexture;

    GameObject background;
    Mesh backgroundMesh;
    Material backgroundMaterial;
    public Shader backgroundShader;

    private int currentNumParticles;

    public void Init(Sim2D sim)
    {
        // Create background assets
        backgroundMaterial = new Material(backgroundShader);
        UpdateBackgroundMaterial(sim);
        CreateBackgroundMesh(sim.boundsSize);
        background = new GameObject("background", typeof(MeshFilter), typeof(MeshRenderer));
        background.GetComponent<Renderer>().material = backgroundMaterial;
        background.GetComponent<MeshFilter>().mesh = backgroundMesh;

        // Create particle material
        material = new Material(shader);
        UpdateParticleMaterial(sim);

        // Set up other arguments for particle rendering
        bounds = new Bounds(Vector3.zero, Vector3.one * 100);
        currentNumParticles = sim.numParticles;
        argsBuffer = ComputeUtil.CreateArgsBuffer(mesh, currentNumParticles);
    }

    void CreateBackgroundMesh(Vector2 boundsSize){
        backgroundMesh = new Mesh();
        Bounds backgroundBounds = new Bounds(Vector3.zero, (Vector3)boundsSize + Vector3.forward);
        Vector3 bottomLeft = new Vector3(backgroundBounds.min.x, backgroundBounds.min.y, 1);
        Vector3 bottomRight = new Vector3(backgroundBounds.max.x, backgroundBounds.min.y, 1);
        Vector3 topLeft = new Vector3(backgroundBounds.min.x, backgroundBounds.max.y, 1);
        Vector3 topRight = new Vector3(backgroundBounds.max.x, backgroundBounds.max.y, 1);
        Vector3[] backgroundVerts = {bottomLeft, bottomRight, topLeft, topRight};
        Vector2[] backgroundUvs = {(Vector2)bottomLeft, (Vector2)bottomRight, (Vector2)topLeft, (Vector2)topRight};
        int[] backgroundTris = {1, 0, 2, 1, 2, 3};
        backgroundMesh.vertices = backgroundVerts;
        backgroundMesh.uv = backgroundUvs;
        backgroundMesh.triangles = backgroundTris;
    }

    void UpdateParticleMaterial(Sim2D sim){
        material.SetBuffer("Positions2D", sim.positionsBuffer);
        material.SetBuffer("Selected", sim.particleColorBuffer);
        material.SetFloat("scale", sim.scale);
        material.SetColor("particleColor", sim.particleColor);
        material.SetColor("particleSecondColor", sim.particleSelectedColor);
        material.SetColor("particleThirdColor", sim.particleMultipleSelectedColor);
    }

    void UpdateBackgroundMaterial(Sim2D sim){
        backgroundMaterial.SetBuffer("Positions2D", sim.positionsBuffer);
        backgroundMaterial.SetInt("numParticles", sim.numParticles);
        backgroundMaterial.SetFloat("densityRadius", sim.densityRadius);
        backgroundMaterial.SetFloat("restDensity", sim.restDensity);
        backgroundMaterial.SetColor("lowDensityColor", sim.lowDensityColor);
        backgroundMaterial.SetColor("highDensityColor", sim.highDensityColor);
        backgroundMaterial.SetColor("restDensityColor", sim.restDensityColor);
        backgroundMaterial.SetFloat("highDensityColorSaturation", sim.highDensityColorSaturation);
    }

    public void UpdateMaterials(Sim2D sim){
        UpdateParticleMaterial(sim);
        UpdateBackgroundMaterial(sim);
    }

    public void SetNumParticles(int numParticles){
        if (numParticles != currentNumParticles){
            ComputeUtil.SetArgsBufferInstances(ref argsBuffer, numParticles);
            currentNumParticles = numParticles;
        }
    }

    void Update()
    {
        Graphics.DrawMeshInstancedIndirect(mesh, 0, material, bounds, argsBuffer);
    }

    void OnDisable()
    {
        argsBuffer.Release();        
    }
}
