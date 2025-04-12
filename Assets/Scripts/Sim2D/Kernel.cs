using UnityEngine;
using Unity.Mathematics;

public class Kernel{

    private float _radius;
    private float sqrRadius;
    private float pow5Radius;
    private float pow8Radius;
    
    public float Radius 
    {
        get{
            return _radius;
        }
        set{
            _radius = value;
            sqrRadius = _radius * _radius;
            pow5Radius = sqrRadius * sqrRadius * _radius;
            pow8Radius = sqrRadius * sqrRadius;
            pow8Radius *= pow8Radius;
        }
    }

    public delegate float SmoothingKernel(float2 diffVector);
    public delegate float2 SmoothingKernelGrad(float2 diffVector);

    public enum KernelType {GaussianApprox, Spiky}

    public SmoothingKernel smoothingKernel;
    public SmoothingKernelGrad smoothingKernelGrad;

    private KernelType _kernelType;
    public KernelType kernelType 
    {
        get{
            return _kernelType;
        } 
        set{
            _kernelType = value;
            switch (_kernelType){
                case KernelType.GaussianApprox:
                    smoothingKernel = SmoothingKernelPoly6;
                    smoothingKernelGrad = SmoothingKernelPoly6Grad;
                    break;
                case KernelType.Spiky:
                    smoothingKernel = SmoothingKernelPoly3;
                    smoothingKernelGrad = SmoothingKernelPoly3Grad;
                    break;
            }
        }
    }

    float SmoothingKernelPoly6(float2 diffVector){
        float sqrDistance = diffVector.x * diffVector.x + diffVector.y * diffVector.y;
        if (sqrDistance > sqrRadius){
            return 0f;
        }
        float v = sqrRadius - sqrDistance;
        v = v * v * v;
        return v * (4f / (Mathf.PI * pow8Radius));
    }

    float2 SmoothingKernelPoly6Grad(float2 diffVector){
        float sqrDistance = diffVector.x * diffVector.x + diffVector.y * diffVector.y;
        if (sqrDistance > sqrRadius){
            return Vector2.zero;
        }
        float v = sqrRadius - sqrDistance;
        float2 grad = v * v * diffVector;
        return grad * (-4f * 3f * 2f / (Mathf.PI * pow8Radius));
    }

    float SmoothingKernelPoly3(float2 diffVector){
        float sqrDistance = diffVector.x * diffVector.x + diffVector.y * diffVector.y;
        if (sqrDistance > sqrRadius){
            return 0f;
        }
        float v = _radius - Mathf.Sqrt(sqrDistance);
        v = v * v * v;
        return v * (10f / (Mathf.PI * pow5Radius));
    }

    float2 SmoothingKernelPoly3Grad(float2 diffVector){
        float sqrDistance = diffVector.x * diffVector.x + diffVector.y * diffVector.y;
        if (sqrDistance > sqrRadius || sqrDistance < 1e-8){
            return Vector2.zero;
        }
        float distance = Mathf.Sqrt(sqrDistance);
        float v = _radius - distance;
        v *= v;
        return v * (-30f / (Mathf.PI * pow5Radius * distance)) * diffVector;
    }

}