using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public partial struct BoidSpawnerSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BoidSpawner>();
        state.RequireForUpdate<WorldConfig>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var boidsQuery = SystemAPI.QueryBuilder().WithAll<Boid>().Build();
        if (!boidsQuery.IsEmpty && !Input.GetKeyDown(KeyCode.Space)) return;
        var entityManager = state.EntityManager;
        var spawner = SystemAPI.GetSingleton<BoidSpawner>();
        var worldConfig = SystemAPI.GetSingleton<WorldConfig>();

        var size = worldConfig.Bound;
        float3 topRight = new float3(size.x / 2, size.y / 2, 0);
        float3 bottomLeft = new float3(-size.x / 2, -size.y / 2, 0);

        var randomPositions = new NativeArray<float3>(spawner.Count, Allocator.TempJob);
        var randomVelocities = new NativeArray<float3>(spawner.Count, Allocator.TempJob);

        var jobHandle = new RandomizeBoidJob
        {
            Seed = (uint)SystemAPI.Time.ElapsedTime,
            TopRight = topRight,
            BottomLeft = bottomLeft,
            SpeedRandomRange = spawner.SpeedRandomRange,
            Positions = randomPositions,
            Velocities = randomVelocities
        }.Schedule(spawner.Count, 20);
        jobHandle.Complete();

        var instances = entityManager.Instantiate(spawner.Prefab, spawner.Count, Allocator.Temp);

        var i = 0;
        foreach (var entity in instances)
        {
            var velocity = randomVelocities[i];
            var position = randomPositions[i];
            entityManager.SetComponentData(entity, new Boid { Velocity = randomVelocities[i] });
            var transform = SystemAPI.GetComponentRW<LocalTransform>(entity);
            transform.ValueRW.Position = position;
            transform.ValueRW.Rotation = quaternion.RotateZ(math.atan2(velocity.y, velocity.x));
            i++;
        }

        state.Dependency = randomVelocities.Dispose(state.Dependency);
        state.Dependency = randomPositions.Dispose(state.Dependency);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state) { }
}

[BurstCompile]
public partial struct RandomizeBoidJob : IJobParallelFor
{
    public NativeArray<float3> Positions;
    public NativeArray<float3> Velocities;
    public float3 TopRight;
    public float3 BottomLeft;
    public float2 SpeedRandomRange;
    public uint Seed;

    public void Execute(int index)
    {
        var random = Random.CreateFromIndex((uint)(Seed + index));
        var speed = random.NextFloat(SpeedRandomRange.x, SpeedRandomRange.y);
        var velocity = math.normalize(new float3(random.NextFloat(-1f, 1f), random.NextFloat(-1f, 1f), 0)) * speed;
        var position = new float3(random.NextFloat(BottomLeft.x, TopRight.x),
            random.NextFloat(BottomLeft.y, TopRight.y), 0);
        Positions[index] = position;
        Velocities[index] = velocity;
    }
}