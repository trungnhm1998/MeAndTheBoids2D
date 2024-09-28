using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

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
        if (SystemAPI.HasSingleton<SpawnerExecuted>()) return;
        var spawner = SystemAPI.GetSingleton<BoidSpawner>();
        var worldConfig = SystemAPI.GetSingleton<WorldConfig>();

        var size = worldConfig.Bound;
        float3 topRight = new float3(size.x / 2, size.y / 2, 0);
        float3 bottomLeft = new float3(-size.x / 2, -size.y / 2, 0);

        var commandBuffer = new EntityCommandBuffer(Allocator.TempJob);

        // var positions = new NativeArray<float3>(spawner.Count, Allocator.TempJob);
        // var velocities = new NativeArray<float3>(spawner.Count, Allocator.TempJob);

        var job = new RandomizeBoidJob
        {
            CommandBuffer = commandBuffer.AsParallelWriter(),
            // Positions = positions,
            // Velocities = velocities,
            Prefab = spawner.Prefab,
            TopRight = topRight,
            BottomLeft = bottomLeft,
            SpeedRandomRange = spawner.SpeedRandomRange
        };

        var handle = job.Schedule(spawner.Count, 50);
        handle.Complete();

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
        // positions.Dispose();
        // velocities.Dispose();
        state.EntityManager.AddComponent<SpawnerExecuted>(state.SystemHandle);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state) { }
}

[BurstCompile]
public partial struct RandomizeBoidJob : IJobParallelFor
{
    public EntityCommandBuffer.ParallelWriter CommandBuffer;
    public Entity Prefab;
    // public NativeArray<float3> Positions;
    // public NativeArray<float3> Velocities;
    public float3 TopRight;
    public float3 BottomLeft;
    public float2 SpeedRandomRange;

    public void Execute(int index)
    {
        var random = Random.CreateFromIndex((uint)index);
        var speed = random.NextFloat(SpeedRandomRange.x, SpeedRandomRange.y);
        var velocity = math.normalize(new float3(random.NextFloat(-1f, 1f), random.NextFloat(-1f, 1f), 0)) * speed;
        var position = new float3(random.NextFloat(BottomLeft.x, TopRight.x),
            random.NextFloat(BottomLeft.y, TopRight.y), 0);

        var entity = CommandBuffer.Instantiate(index, Prefab);
        CommandBuffer.SetComponent(index, entity, new LocalTransform() { Position = position });
        CommandBuffer.SetComponent(index, entity, new Boid() { Velocity = velocity, Id = index });
    }
}

public struct SpawnerExecuted : IComponentData { }