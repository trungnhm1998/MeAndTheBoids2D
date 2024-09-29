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
        var entityManager = state.EntityManager;
        var progressEntity = entityManager.CreateEntity(typeof(SpawningProgress));
        entityManager.SetComponentData(progressEntity, new SpawningProgress { Value = 0 });
        
        state.RequireForUpdate<SpawningProgress>();
        state.RequireForUpdate<BoidSpawner>();
        state.RequireForUpdate<WorldConfig>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var entityManager = state.EntityManager;
        var spawner = SystemAPI.GetSingleton<BoidSpawner>();
        var worldConfig = SystemAPI.GetSingleton<WorldConfig>();

        var size = worldConfig.Bound;
        float3 topRight = new float3(size.x / 2, size.y / 2, 0);
        float3 bottomLeft = new float3(-size.x / 2, -size.y / 2, 0);

        var commandBuffer = new EntityCommandBuffer(Allocator.TempJob);

        var progressEntity = SystemAPI.GetSingletonEntity<SpawningProgress>();
        var progress = entityManager.GetComponentData<SpawningProgress>(progressEntity);

        int remaining = spawner.Count - progress.Value;
        int spawnThisFrame = math.min(remaining, spawner.SpawnPerFrame);

        if (spawnThisFrame > 0)
        {
            var job = new RandomizeBoidJob
            {
                CommandBuffer = commandBuffer.AsParallelWriter(),
                Prefab = spawner.Prefab,
                TopRight = topRight,
                BottomLeft = bottomLeft,
                SpeedRandomRange = spawner.SpeedRandomRange
            };

            var handle = job.Schedule(spawner.Count, 1);
            handle.Complete();

            commandBuffer.Playback(state.EntityManager);
            progress.Value += spawnThisFrame;
        }
        
        if (remaining < 0)
        {
            // Destroy excess entities
            var excessCount = -remaining;
            var boidQuery = entityManager.CreateEntityQuery(typeof(Boid));
            var boidEntities = boidQuery.ToEntityArray(Allocator.TempJob);

            for (int i = 0; i < excessCount && i < boidEntities.Length; i++)
            {
                entityManager.DestroyEntity(boidEntities[i]);
                progress.Value--;
            }

            boidEntities.Dispose();
        }

        commandBuffer.Dispose();

        entityManager.SetComponentData(progressEntity, progress);
        // if (progress.Value >= spawner.Count)
        // {
        //     entityManager.DestroyEntity(progressEntity);
        // }
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

public struct SpawningProgress : IComponentData
{
    public int Value;
}