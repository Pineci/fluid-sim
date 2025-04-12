using System;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class Sim2D : MonoBehaviour
{
    const int maxParticles = 1000;
    const float minScale = 0.01f;
    const float maxScale = 0.1f;

    [Header("Simulation Settings")]
    public Vector2 boundsSize = new Vector2(1.5f, 1);
    [Range(10, maxParticles)] public int numParticles = 10;
    [Range(minScale, maxScale)] public float scale = minScale;
    public float gravity;
    [Range(0, 1)] public float collisionElasticity = 0.95f;
    [Range(0.01f, 0.5f)] public float densityRadius = 0.1f;
    public float mass = 1f;

    public float gasConstant = 0.0001f;
    public float restDensity = 50f;
    public float viscocityConstant = 1f;

    public Kernel.KernelType densityKernelType;
    public Kernel.KernelType viscocityKernelType;
    private Kernel densityKernel;
    private Kernel viscocityKernel;

    [Header("Visuals")]
    public Color particleColor;
    public Color restDensityColor;
    public Color lowDensityColor;
    public Color highDensityColor;
    [Range(0.5f, 5.0f)] public float highDensityColorSaturation;

    [Header("Debug")]
    public bool showForces;
    private const int maxForceResolution = 40;
    [Range(10, maxForceResolution)] public int forceResolution;
    public float arrowMagnitude;
    private bool forceArrowsActive;
    private int forceArrowsCurrentResolution;
    private Line[] forceArrows;

    [Header("References")]
    public ParticleDisplay2D display;

    // Set up other buffers/variables
    private float2[] forces;
    private float2[] positions;
    private float2[] predictedPositions;
    private float2[] velocities;
    private float[] densities;
    public ComputeBuffer positionsBuffer;

    private bool runSimulation = true;

    private InputAction pauseSimulation;
    private InputAction mouseInteraction;

    private Cells cellHashMap;

    void Start()
    {
        positionsBuffer = ComputeUtil.CreateStructuredBuffer<float2>(maxParticles);

        positions = ParticleSpawner.CreateGrid(numParticles, scale);
        predictedPositions = new float2[numParticles];
        positions.CopyTo(predictedPositions, 0);
        forces = new float2[numParticles];
        velocities = new float2[numParticles];
        densities = new float[numParticles];

        display.Init(this);

        // Initialize Debug arrows
        forceArrowsActive = false;
        forceArrows = new Line[maxForceResolution * maxForceResolution];
        for (int i = 0; i < forceArrows.Length; i++){
            forceArrows[i] = new Line
            {
                id = i,
                color = Color.black,
                width = 0.01f
            };
            forceArrows[i].Init();
            forceArrows[i].obj.transform.SetParent(GetComponent<Transform>());
            forceArrows[i].Hide();
        }
        forceArrowsCurrentResolution = 0;

        // Initialize Kernels
        densityKernel = new Kernel
        {
            kernelType = densityKernelType,
            Radius = densityRadius
        };

        viscocityKernel = new Kernel
        {
            kernelType = viscocityKernelType,
            Radius = densityRadius
        };

        // Initialize Cell Neighbor Hash Map
        cellHashMap = new Cells{
            searchRadius = densityRadius,
            cellOrigin = float2.zero,
            numCells = (uint)numParticles
        };

        pauseSimulation = InputSystem.actions.FindAction("Pause");
    }

    void OnDisable()
    {
        positionsBuffer.Release();
    }

    float CalculateDensity(float2[] particlePositions, float2 densityPos){
        float density = 0;
        Debug.Log("DENSITY FOR PARTICLE AT: " + densityPos);
        // foreach (uint particleIndex in cellHashMap.GetNeighbors(particlePositions, densityPos)){
        //     //Debug.Log("Neighbor Particle Index: " + particleIndex);
        //     float2 diff = particlePositions[particleIndex] - densityPos;
        //     density += mass * densityKernel.smoothingKernel(diff);
        // }
        Debug.Log("FINAL DENSITY: " + density);
        for (int i = 0; i < numParticles; i++){
            float2 diff = particlePositions[i] - densityPos;
            density += mass * densityKernel.smoothingKernel(diff);
        }
        return density;
    }

    void CalculateParticleDensity(ref float[] densities, float2[] densityPositions, float2[] particlePositions){
        for (int i = 0; i < densityPositions.Length; i++){
            densities[i] = CalculateDensity(particlePositions, densityPositions[i]);
        }
    }

    void Gravity(ref float2[] forces, float[] densities){
        for (int i = 0; i < forces.Length; i++){
            forces[i].y -= gravity * densities[i];
        }
    }

    float CalculatePressure(float density){
        return gasConstant * (density - restDensity);
    }

    void CalculatePressureForce(ref float2[] forces, float[] densities, float2[] positions, float[] particleDensities, float2[] particlePositions){
        for (int i = 0; i < positions.Length; i++){
            float pressure_i = CalculatePressure(densities[i]);
            for (int j = 0; j < numParticles; j++){
                float2 diff = positions[i] - particlePositions[j];
                if (diff.x * diff.x + diff.y * diff.y < 1e-8) continue;

                float pressure_j = CalculatePressure(particleDensities[j]);
                float2 force = densityKernel.smoothingKernelGrad(diff);
                force *= -mass * 0.5f * (pressure_i + pressure_j) / particleDensities[j];
                forces[i] += force;
                //forces[j] += -force;
            }
        }
    }

    void CalculateViscocityForce(ref float2[] forces, float2[] positions){
        for (int i = 0; i < numParticles; i++){
            for (int j = 0; j < numParticles; j++){
                if (i == j) continue;
                float2 diff = positions[i] - positions[j];
                float2 force = viscocityKernel.smoothingKernelGrad(diff);
                force *= viscocityConstant * mass * (velocities[j] - velocities[i]) / densities[j];
                forces[i] += force;
            }
        }
    }

    void ApplyForces(float dt){
        for (int i = 0; i < numParticles; i++){
            float2 accel = forces[i] / densities[i];
            velocities[i] += accel * dt;
            forces[i] = Vector2.zero;
        }
    }

    void HandleCollisions(){
        float2 halfBoundsSize = boundsSize / 2f;
        for (int i = 0; i < numParticles; i++){
            float2 pos = positions[i];
            float2 vel = velocities[i];
            if (halfBoundsSize.x - Math.Abs(pos.x) <= 0){
                pos.x = halfBoundsSize.x * Math.Sign(pos.x);
                vel.x *= -collisionElasticity;
            }
            if (halfBoundsSize.y - Math.Abs(pos.y) <= 0){
                pos.y = halfBoundsSize.y * Math.Sign(pos.y);
                vel.y *= -collisionElasticity;
            }

            positions[i] = pos;
            velocities[i] = vel;
        }
    }

    void UpdatePositions(ref float2[] positions, float dt){
        for (int i = 0; i < numParticles; i++){
            positions[i] += velocities[i] * dt;
        }
    }

    void UpdateForceArrows(bool activate){

        if (activate != forceArrowsActive){
            forceArrowsActive = activate;
            if (!forceArrowsActive){
                for (int i = 0; i < forceArrowsCurrentResolution * forceArrowsCurrentResolution; i++){
                    forceArrows[i].Hide();
                }
                forceArrowsCurrentResolution = 0;
            }
        }

        if (forceArrowsActive){
            // Turn arrows on or off
            if (forceResolution < forceArrowsCurrentResolution){
                int startIdx = forceResolution * forceResolution;
                int endIdx = forceArrowsCurrentResolution * forceArrowsCurrentResolution;
                for (int i = startIdx; i < endIdx; i++){
                    forceArrows[i].Hide();
                }
            }
            if (forceResolution > forceArrowsCurrentResolution){
                int endIdx = forceResolution * forceResolution;
                int startIdx = forceArrowsCurrentResolution * forceArrowsCurrentResolution;
                for (int i = startIdx; i < endIdx; i++){
                    forceArrows[i].Show();
                }
            }
            forceArrowsCurrentResolution = forceResolution;

            // Update force arrows display
            Vector3 halfBounds = boundsSize / 2f;
            int numArrows = forceArrowsCurrentResolution * forceArrowsCurrentResolution;
            float2[] arrowPositions = new float2[numArrows];
            float[] arrowDensities = new float[numArrows];
            float2[] arrowForces = new float2[numArrows];
            for (int i = 0; i < forceArrowsCurrentResolution; i++){
                for (int j = 0; j < forceArrowsCurrentResolution; j++){
                    int idx = i*forceArrowsCurrentResolution + j;
                    arrowPositions[idx] = float2.zero;
                    arrowPositions[idx].x = Mathf.Lerp(-halfBounds.x, halfBounds.x, (float)i / (float)(forceArrowsCurrentResolution-1));
                    arrowPositions[idx].y = Mathf.Lerp(-halfBounds.y, halfBounds.x, (float)j / (float)(forceArrowsCurrentResolution-1));
                    //forceArrows[idx].Set(point, Vector3.up, 0.1f);
                }
            }

            for (int i = 0; i < numArrows; i++){
                arrowDensities[i] = CalculateDensity(predictedPositions, arrowPositions[i]);
            }
            Gravity(ref arrowForces, arrowDensities);
            CalculatePressureForce(ref arrowForces, arrowDensities, arrowPositions, densities, predictedPositions);
            //CalculateViscocityForce(ref forces, points);

            // Update force arrows display
            for (int i = 0; i < numArrows; i++){
                Vector3 point = new Vector3(arrowPositions[i].x, arrowPositions[i].y, 0);
                Vector3 accel = Vector3.up;
                float scale = 0f;
                if (arrowDensities[i] > 1e-6){
                    accel = new Vector3(arrowForces[i].x, arrowForces[i].y, 0) / arrowDensities[i];
                    scale = accel.magnitude;
                    accel /= scale;
                }
                forceArrows[i].Set(point, accel, scale * arrowMagnitude);
                //forceArrows[i].Set(point, Vector3.right, arrowMagnitude);
            }

        }

        

        

        
    }

    void HandleInputs(){
        if (pauseSimulation.WasPressedThisFrame()){
            runSimulation = !runSimulation;
        }
    }

    void Update()
    {
        HandleInputs();
        if (!runSimulation) return;

        float dt = Time.deltaTime;

        

        //Debug.Log("Density at (0, 0): " + CalculateDensity(positions, new float2(0, 0)));

        UpdatePositions(ref predictedPositions, 1 / 120f);

        // Update neighbor map
        // cellHashMap.UpdateSpatialLookup(predictedPositions);

        // Do all forces first
        CalculateParticleDensity(ref densities, predictedPositions, predictedPositions);
        Gravity(ref forces, densities);
        CalculatePressureForce(ref forces, densities, predictedPositions, densities, predictedPositions);
        CalculateViscocityForce(ref forces, predictedPositions);
        //UpdateForceArrows(showForces);
        ApplyForces(dt);

        // Do positions and collisions last
        UpdatePositions(ref positions, dt);
        HandleCollisions();

        positions.CopyTo(predictedPositions, 0);
        
        positionsBuffer.SetData(positions);
        display.UpdateMaterials(this);
    }

    void OnDrawGizmos(){
        Gizmos.color = Color.yellow;
        Vector3 center = Vector3.zero;
        Vector3 halfBoundsSize = (Vector3)boundsSize / 2f;
        Vector3 i = Vector3.right, j = Vector3.up;
        Vector3 topLeft = center + Vector3.Scale(halfBoundsSize, -i + j);
        Vector3 topRight = center + Vector3.Scale(halfBoundsSize, i + j);
        Vector3 bottomLeft = center + Vector3.Scale(halfBoundsSize, -i - j);
        Vector3 bottomRight = center + Vector3.Scale(halfBoundsSize, i - j);
        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topLeft, bottomLeft);
        Gizmos.DrawLine(bottomRight, bottomLeft);
        Gizmos.DrawLine(bottomRight, topRight);

        Gizmos.DrawWireSphere(Vector3.zero, densityRadius);
    }
}
