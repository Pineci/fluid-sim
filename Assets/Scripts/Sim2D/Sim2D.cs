using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Analytics;
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
    public Color particleSelectedColor;
    public Color particleMultipleSelectedColor;
    public Color restDensityColor;
    public Color lowDensityColor;
    public Color highDensityColor;
    [Range(0.5f, 5.0f)] public float highDensityColorSaturation;

    [Header("References")]
    public ParticleDisplay2D display;
    public Camera cam;

    [Header("Debug")]
    public bool showForceDebug = false;
    public bool onlyUseNeighborForces = false;
    public bool highlightParticle = false;
    public int selectedParticleIndex;

    // Set up other buffers/variables
    private float2[] forces;
    private float2[] positions;
    private float2[] predictedPositions;
    private float2[] velocities;
    private float[] densities;
    private uint[] particleSelected;

    // Set up ComputeBuffers
    public ComputeBuffer positionsBuffer;
    public ComputeBuffer particleColorBuffer;

    private bool runSimulation = true;

    private InputAction pauseSimulation;
    private InputAction interactionPosition;
    private InputAction attract;
    private InputAction repel;

    private Cells cellHashMap;

    

    void Start()
    {
        positionsBuffer = ComputeUtil.CreateStructuredBuffer<float2>(maxParticles);
        particleColorBuffer = ComputeUtil.CreateStructuredBuffer<uint>(maxParticles);

        positions = ParticleSpawner.CreateGrid(numParticles, scale);
        predictedPositions = new float2[numParticles];
        positions.CopyTo(predictedPositions, 0);
        forces = new float2[numParticles];
        velocities = new float2[numParticles];
        densities = new float[numParticles];
        particleSelected = new uint[numParticles];

        display.Init(this);

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
        interactionPosition = InputSystem.actions.FindAction("Mouse");
        attract = InputSystem.actions.FindAction("Attract");
        repel = InputSystem.actions.FindAction("Repel");
    }

    void OnDisable()
    {
        positionsBuffer.Release();
        particleColorBuffer.Release();
    }

    float CalculateDensity(float2[] particlePositions, float2 densityPos){
        float density = 0;
        foreach (uint i in cellHashMap.GetNeighbors(particlePositions, densityPos)){
            float2 diff = particlePositions[i] - densityPos;
            density += mass * densityKernel.smoothingKernel(diff);
        }
        return density;
    }

    void CalculateParticleDensity(float2[] particlePositions){
        Parallel.For(0, numParticles, i => {
            densities[i] = CalculateDensity(particlePositions, particlePositions[i]);
        });
    }

    void Gravity(){
        Parallel.For(0, numParticles, i => {
            forces[i].y -= gravity * densities[i];
        });
    }

    float CalculatePressure(float density){
        return gasConstant * (density - restDensity);
    }

    void CalculatePressureForce(float2[] particlePositions){
        Parallel.For(0, numParticles, i => {
            float pressure_i = CalculatePressure(densities[i]);
            foreach (uint j in cellHashMap.GetNeighbors(particlePositions, particlePositions[i])){
                float2 diff = particlePositions[i] - particlePositions[j];
                if (diff.x * diff.x + diff.y * diff.y < 1e-8) continue;

                float pressure_j = CalculatePressure(densities[j]);
                float2 force = densityKernel.smoothingKernelGrad(diff);
                force *= -mass * 0.5f * (pressure_i + pressure_j) / densities[j];

                forces[i] += force;
                //forces[j] += -force;
            }
        });
    }

    void CalculateViscocityForce(float2[] particlePositions){
        Parallel.For(0, numParticles, i => {
            foreach (uint j in cellHashMap.GetNeighbors(particlePositions, particlePositions[i])){
                float2 diff = particlePositions[i] - particlePositions[j];
                if (diff.x * diff.x + diff.y * diff.y < 1e-8) continue;
                
                float2 force = viscocityKernel.smoothingKernelGrad(diff);
                force *= viscocityConstant * mass * (velocities[j] - velocities[i]) / densities[j];
                forces[i] += force;
            }
        });
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

    void HandleInputs(){
        if (pauseSimulation.WasPressedThisFrame()){
            runSimulation = !runSimulation;
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        
        HandleInputs();
        if (runSimulation){

            float simFrameRate = 1 / 120f;

            UpdatePositions(ref predictedPositions, simFrameRate);

            // Update neighbor map
            cellHashMap.UpdateSpatialLookup(predictedPositions);

            // Do all forces first
            CalculateParticleDensity(predictedPositions);
            Gravity();
            CalculatePressureForce(predictedPositions);
            CalculateViscocityForce(predictedPositions);
            ApplyForces(dt);

            // Do positions and collisions last
            UpdatePositions(ref positions, dt);
            HandleCollisions();


            positions.CopyTo(predictedPositions, 0);

        }

        if (attract.IsPressed()){
            cellHashMap.UpdateSpatialLookup(positions);
            Vector2 mouseScreenPos = interactionPosition.ReadValue<Vector2>();
            Vector3 mouseWorldPos = cam.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, 1));
            Vector2 selectedPos = (Vector2)mouseWorldPos;

            Array.Clear(particleSelected, 0, numParticles);

            var coord = cellHashMap.FindCellCoord((float2)selectedPos);
            (int coordX, int coordY) = coord;
            var hash = cellHashMap.Hash(coordX, coordY);
            var key = cellHashMap.KeyFromHash(hash);
            //Debug.Log("NUM CELLS: " + cellHashMap.numCells + "CELL: " + coord + " HASH: " + hash + " KEY: " + key);
            //Debug.Log(cellHashMap.DebugHelper(key));

            if (repel.IsPressed()){
                foreach (uint i in cellHashMap.GetNeighbors(positions, selectedPos)){
                    particleSelected[i] += 1;
                    particleSelected[i] = particleSelected[i] > 2 ? 2 : particleSelected[i];
                }
            } else {
                for (int i = 0; i < numParticles; i++){
                    if (coord == cellHashMap.FindCellCoord(positions[i])){
                        particleSelected[i] = 1;
                    }
                }
            }
        } else {
            for (int i = 0; i < numParticles; i++){
                particleSelected[i] = 0;
            }
        }

        if (highlightParticle){
            particleSelected[selectedParticleIndex] = 2;
        }
        
        // Update Buffers
        positionsBuffer.SetData(positions);
        particleColorBuffer.SetData(particleSelected);

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
