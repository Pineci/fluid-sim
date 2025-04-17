

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

public class Cells{

    private struct CellKey : IComparable<CellKey>{
        public uint index;
        public uint hash;
        public uint key;

        public CellKey(uint _index, uint _hash, uint _key){
            index = _index;
            hash = _hash;
            key = _key;
        }

        public readonly int CompareTo(CellKey other)
        {
            return key.CompareTo(other.key);
        }
    }

    public float searchRadius;
    public float2 cellOrigin;
    public double closePointTolerance = 1e-8;

    private CellKey[] spatialLookup;
    private uint[] startIndices;
    private bool[] alreadyReturned;
    private uint _numCells;
    public uint numCells
    {
        get{
            return _numCells;
        }
        set{
            _numCells = value;
            spatialLookup = new CellKey[_numCells];
            startIndices = new uint[_numCells];
            alreadyReturned = new bool[_numCells];
        }
    }

    public uint Hash(int a, int b){
        // Using an implementation of MurmurHash
        const uint c1 = 0xcc9e2d51;
        const uint c2 = 0x1b873593;
        const uint c3 = 0x85ebca6b;
        const uint c4 = 0xc2b2ae35;
        const uint r1 = 15;
        const uint r2 = 13;
        const uint m = 5;
        const uint n = 0xe6546b64;
        const uint seed = 42;
        const uint len = 2; // This is the number of 32-bit blocks we are hashing

        uint hash = seed;
        
        
        // Manually doing this step to the two inputs. Normally, the input would be an array
        // and this would be done in a for loop

        uint k;
        // Iteration 1
        k = (uint)a;
        k *= c1;
        k = (k << (int)r1) | (k >> (int)(32 - r1));
        k *= c2;

        hash ^= k;
        hash = ((hash << (int)r2) | (hash >> (int)(32-r2))) * m + n;

        // Iteration 2
        k = (uint)b;
        k *= c1;
        k = (k << (int)r1) | (k >> (int)(32 - r1));
        k *= c2;

        hash ^= k;
        hash = ((hash << (int)r2) | (hash >> (int)(32-r2))) * m + n;

        // Post-processing of the hash
        hash ^= len;
        hash ^= hash >> 16;
        hash *= c3;
        hash ^= hash >> 13;
        hash *= c4;
        hash ^= hash >> 16;

        return hash;
    }

    public uint KeyFromHash(uint hash){
        return hash % _numCells;
    }

    public (int, int) FindCellCoord(float2 position){
        int2 coord = (int2)math.floor((position - cellOrigin + searchRadius * 0.5f) / searchRadius);
        return (coord.x, coord.y);
    }

    public void UpdateSpatialLookup(float2[] points){

        // Compute the hash of every point
        Parallel.For(0, _numCells, i => {
            (int cellX, int cellY) = FindCellCoord(points[i]);
            uint hash = Hash(cellX, cellY);
            uint key = KeyFromHash(hash);
            spatialLookup[i] = new CellKey((uint)i, hash, key);
            startIndices[i] = uint.MaxValue;
        });

        // Sort by the cell key
        Array.Sort(spatialLookup);

        // Set the start indices for each key in the spatial lookup
        Parallel.For(0, _numCells, i => {
            uint key = spatialLookup[i].key;
            if (i == 0){
                startIndices[key] = 0;
            } else {
                uint keyPrev = spatialLookup[i-1].key;
                if (key != keyPrev){
                    startIndices[key] = (uint)i;
                }
            }
        });
    }

    readonly (int, int)[] cellNeighborOffsets = {
        (0, 0),
        (0, 1),
        (0, -1),
        (1, 0),
        (1, 1),
        (1, -1),
        (-1, 0),
        (-1, 1),
        (-1, -1)
    };

    public IEnumerable<uint> GetNeighbors(float2[] points, float2 position){

        (int cellX, int cellY) = FindCellCoord(position);
        float sqrRadius = searchRadius * searchRadius;

        foreach ((int offsetX, int offsetY) in cellNeighborOffsets){
            uint hash = Hash(cellX + offsetX, cellY + offsetY);
            uint key = KeyFromHash(hash);

            uint cellStartIndex = startIndices[key];

            for (uint i = cellStartIndex; i < _numCells; i++){
                if (spatialLookup[i].key != key) break;
                if (spatialLookup[i].hash != hash) continue;


                uint particleIndex = spatialLookup[i].index;
                float2 diff = points[particleIndex] - position;
                float mag2 = diff.x * diff.x + diff.y * diff.y;
                if (mag2 <= sqrRadius){
                    yield return particleIndex;
                }
            }
        }
    }
}