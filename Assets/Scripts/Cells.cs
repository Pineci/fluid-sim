

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Mathematics;

public class Cells{

    private struct CellKey : IComparable<CellKey>{
        public uint index;
        public uint key;

        public CellKey(uint _index, uint _key){
            index = _index;
            key = _key;
        }

        public readonly int CompareTo(CellKey other)
        {
            return index.CompareTo(other.index);
        }
    }

    public float searchRadius;
    public float2 cellOrigin;

    private CellKey[] spatialLookup;
    private uint[] startIndices;
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
        }
    }

    private uint Hash(int a, int b){
        return (uint)(a * 849684749 + b * 382318243);
    }

    private uint KeyFromHash(uint hash){
        return hash % _numCells;
    }

    public (int, int) FindCellCoord(float2 position){
        int2 coord = new int2((position - cellOrigin + searchRadius * 0.5f) / searchRadius);
        return (coord.x, coord.y);
    }

    public void UpdateSpatialLookup(float2[] points){

        // Compute the hash of every point
        Parallel.For(0, _numCells, i => {
            (int cellX, int cellY) = FindCellCoord(points[i]);
            uint hash = KeyFromHash(Hash(cellX, cellY));
            spatialLookup[i] = new CellKey((uint)i, hash);
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
            uint key = KeyFromHash(Hash(cellX + offsetX, cellY + offsetY));
            uint cellStartIndex = startIndices[key];

            for (uint i = cellStartIndex; i < _numCells; i++){
                if (spatialLookup[i].key != key) break;

                uint particleIndex = spatialLookup[i].index;
                float2 diff = points[particleIndex] - position;
                if (diff.x * diff.x + diff.y * diff.y <= sqrRadius){
                    yield return particleIndex;
                }
            }
        }
    }
}