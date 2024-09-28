using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using UnityEngine;

public class BoidsRenderer : MonoBehaviour
{
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");
    public Mesh quadMesh;
    public Material instancedMaterial;
    public Texture2D spriteTexture;
    private List<Matrix4x4[]> _batches = new();
    private EntityManager _entityManager;

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
        // quadMesh.RecalculateNormals();
        // quadMesh.RecalculateBounds();
        instancedMaterial.SetTexture(MainTex, spriteTexture);
        instancedMaterial.enableInstancing = true;
    }

    void Update()
    {
        RenderBatches();
    }

    private void RenderBatches()
    {
        if (CreateBatches()) return;
        foreach (var batch in _batches)
        {
            Graphics.DrawMeshInstanced(quadMesh, 0, instancedMaterial, batch);
        }
    }

    private bool CreateBatches()
    {
        var boidEntities = new EntityQueryBuilder(Allocator.Temp).WithAll<Boid, LocalToWorld>().Build(_entityManager);
        if (boidEntities.CalculateEntityCount() == 0)
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

        _batches = new(nativeMatrices.Length / 1000);
        
        for (int i = 0; i < nativeMatrices.Length; i += 1000)
        {
            var batch = new Matrix4x4[Mathf.Min(1000, nativeMatrices.Length - i)];
            nativeMatrices.Slice(i, batch.Length).CopyTo(batch);
            _batches.Add(batch);
        }

        localToWorlds.Dispose();
        nativeMatrices.Dispose();
        boidEntities.Dispose();
        boidEntitiesArray.Dispose();
        return false;
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