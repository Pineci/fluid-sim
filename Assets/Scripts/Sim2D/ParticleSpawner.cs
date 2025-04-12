using System;
using Unity.Mathematics;
using UnityEngine;

public class ParticleSpawner : MonoBehaviour
{

    public static float2[] CreateGrid(int numParticles, float radius){
        int side_length = (int)Math.Ceiling(Math.Sqrt((double)numParticles));

        Vector2 center = Vector2.zero;

        float2[] positions = new float2[numParticles];

        for (int i = 0; i < side_length; i++){
            float ypos = center.y + (side_length - 1 - 2 * i) * radius;
            for (int j = 0; j < side_length; j++){
                int idx = i*side_length + j;
                if (idx < numParticles){
                    float xpos = center.x + (side_length - 1 - 2 * j) * radius;
                    positions[idx] = new float2(xpos, ypos);
                }
                
            }
        }

        return positions;
    }
    
}
