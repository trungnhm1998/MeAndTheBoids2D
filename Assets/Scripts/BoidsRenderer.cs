using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using UnityEngine;

public class BoidsRenderer : MonoBehaviour
{
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");
    private static readonly int MatricesBufferID = Shader.PropertyToID("_InstanceMatrices");
    public Mesh quadMesh;
    public Material instancedMaterial;
    public Texture2D spriteTexture;
    private EntityManager _entityManager;
    
    private ComputeBuffer _matricesBuffer;
    private ComputeBuffer _argsBuffer;
    private uint[] _args = new uint[5] { 0, 0, 0, 0, 0 };
    private int _boidsCount;

    void Start()
    {
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        quadMesh = new Mesh();
        quadMesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0),
            new Vector3(-0.5f, 0.5f, 0),
            new Vector3(0.5f, 0.5f, 0),
            new Vector3(0.5f, -0.5f, 0)
        };
        quadMesh.uv = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(0, 1),
            new Vector2(1, 1),
            new Vector2(1, 0)
        };
        quadMesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
        quadMesh.RecalculateNormals();
        quadMesh.RecalculateBounds();
        instancedMaterial.SetTexture(MainTex, spriteTexture);
        instancedMaterial.enableInstancing = true;
        
        _argsBuffer = new ComputeBuffer(1, _args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
    }

    void Update()
    {
        RenderBatches();
    }

    private void RenderBatches()
    {
        if (CreateBatches()) return;
        
        _args[0] = quadMesh.GetIndexCount(0);
        _args[1] = (uint)_boidsCount;
        _argsBuffer.SetData(_args);
        
        instancedMaterial.SetBuffer(MatricesBufferID, _matricesBuffer);
        
        Graphics.DrawMeshInstancedIndirect(quadMesh, 0, instancedMaterial, new Bounds(Vector3.zero, new Vector3(1000, 1000, 1000)), _argsBuffer);
    }

    private bool CreateBatches()
    {
        var boidEntities = new EntityQueryBuilder(Allocator.Temp).WithAll<Boid, LocalToWorld>().Build(_entityManager);
        _boidsCount = boidEntities.CalculateEntityCount();
        if (_boidsCount == 0)
        {
            boidEntities.Dispose();
            return true;
        }

        NativeArray<Entity> boidEntitiesArray = boidEntities.ToEntityArray(Allocator.Temp);
        var localToWorlds = boidEntities.ToComponentDataArray<LocalToWorld>(Allocator.TempJob);
        NativeArray<Matrix4x4> nativeMatrices = new NativeArray<Matrix4x4>(boidEntitiesArray.Length, Allocator.TempJob);

        var job = new CalculateMatricesJob
        {
            LocalToWorlds = localToWorlds,
            Matrices = nativeMatrices
        };
        var handle = job.Schedule(boidEntitiesArray.Length, 50);
        handle.Complete();

        // create batches/compute buffer
        if (_matricesBuffer == null || _matricesBuffer.count != _boidsCount)
        {
            _matricesBuffer?.Release();
            _matricesBuffer?.Dispose();
            // each row has 4 floats x 4 col floats = 16 floats
            // [ 16 floats per matrix ] * [ 4 bytes per float ] = 64 bytes per matrix
            // 64 bytes per matrix * 4 matrices per boid = 256 bytes per boid
            // 256 bytes per boid * 1000 boids = 256,000 bytes
            // 256,000 bytes = 250 KB
            _matricesBuffer = new ComputeBuffer(_boidsCount, 16 * 4);
        }
        _matricesBuffer.SetData(nativeMatrices);

        localToWorlds.Dispose();
        nativeMatrices.Dispose();
        boidEntities.Dispose();
        boidEntitiesArray.Dispose();
        return false;
    }

    private void OnDestroy()
    {
        _matricesBuffer?.Release();
        // _matricesBuffer?.Dispose();
        _argsBuffer?.Release();
        // _argsBuffer?.Dispose();
    }
}

struct CalculateMatricesJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<LocalToWorld> LocalToWorlds;
    public NativeArray<Matrix4x4> Matrices;

    public void Execute(int index)
    {
        LocalToWorld transform = LocalToWorlds[index];
        Matrices[index] = Matrix4x4.TRS(
            new Vector3(transform.Position.x, transform.Position.y, 0f),
            transform.Rotation,
            Vector3.one
        );
    }
}