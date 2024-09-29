using Drawing;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class SimulationDebug : MonoBehaviour
{
    public bool ShowQuadTree = false;
    public bool ShowPerceptionRadius = false;

    private void Update()
    {
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        entityManager.CompleteAllTrackedJobs();
        DrawBoidPerceptions(entityManager);
    }

    private void DrawBoidPerceptions(EntityManager entityManager)
    {
        if (ShowPerceptionRadius == false) return;
        var boidsQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Boid, LocalTransform, LocalToWorld>()
            .Build(entityManager);
        var builder = DrawingManager.GetBuilder(true);

        // NativeArray<Boid> boids = boidsQuery.ToComponentDataArray<Boid>(Allocator.TempJob);
        NativeArray<LocalTransform> transforms = boidsQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

        var job = new DrawJob()
        {
            Transform = transforms,
            // Boids = boids,
            Builder = builder
        }.Schedule();

        job.Complete();

        builder.DisposeAfter(job);
        // boids.Dispose(job);
        transforms.Dispose(job);
    }

    [BurstCompile]
    struct DrawJob : IJob
    {
        public CommandBuilder Builder;
        // [ReadOnly] public NativeArray<Boid> Boids;
        [ReadOnly] public NativeArray<LocalTransform> Transform;

        public void Execute()
        {
            for (int i = 0; i < Transform.Length; i++)
            {
                var boid = Transform[i];
                Builder.xy.Circle(new float2(boid.Position.x, boid.Position.y), 1f, Color.green);
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (Application.isPlaying == false) return;
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        entityManager.CompleteAllTrackedJobs();

        HandleDebugQuadTree(entityManager);
    }

    private void OnGUI()
    {
        if (Application.isPlaying == false) return;
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        entityManager.CompleteAllTrackedJobs();

        HandleOverlay(entityManager);
    }

    private void HandleDebugQuadTree(EntityManager entityManager)
    {
        if (ShowQuadTree == false) return;
        Gizmos.color = Color.white;
        var quadTreeQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<QuadTreeComponent>().Build(entityManager);
        if (quadTreeQuery.IsEmpty == false)
        {
            var entity = quadTreeQuery.GetSingletonEntity();
            var quadTreeComponent = entityManager.GetComponentData<QuadTreeComponent>(entity);
            quadTreeComponent.Value.DrawGizmos();
        }

        quadTreeQuery.Dispose();
    }

    private void HandleOverlay(EntityManager entityManager)
    {
        var boidsQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Boid>().Build(entityManager);

        // draw a number of boids top left of screen
        var boidsCount = boidsQuery.CalculateEntityCount();
        var text = $"Boids: {boidsCount}";
        GUI.Label(new Rect(10, 10, 100, 20), text);

        boidsQuery.Dispose();
    }
}