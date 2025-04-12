using Unity.Burst.Intrinsics;
using Unity.Collections;
using UnityEngine;

using static UnityEngine.Mathf;

public static class ComputeUtil{

    public static int GetStride<T>(){
        return System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
    }

    public static ComputeBuffer CreateStructuredBuffer<T>(int size){
        return new ComputeBuffer(size, GetStride<T>());
    }

    // This args buffer is used for instanced indirect rendering
    public static ComputeBuffer CreateArgsBuffer(Mesh mesh, int numInstances){
        const int subMeshIndex = 0;
        uint[] args = new uint[5];
        args[0] = mesh.GetIndexCount(subMeshIndex);
        args[1] = (uint)numInstances;
        args[2] = mesh.GetIndexStart(subMeshIndex);
        args[3] = mesh.GetBaseVertex(subMeshIndex);
        args[4] = 0; // Offest

        ComputeBuffer argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);
        return argsBuffer;
    }

    public static void SetArgsBufferInstances(ref ComputeBuffer argsBuffer, int numInstances){
        uint[] args = new uint[5];
        argsBuffer.GetData(args);
        args[1] = (uint)numInstances;
        argsBuffer.SetData(args);
    }
}