using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public partial struct BoidSpawnerSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BoidSpawner>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (SystemAPI.HasSingleton<SpawnerExecuted>()) return;
        var spawner = SystemAPI.GetSingleton<BoidSpawner>();
        var instances = state.EntityManager.Instantiate(spawner.Prefab, spawner.Count, Allocator.Temp);
        Debug.Log($"Spawn {instances.Length} boids");

        foreach (var boid in SystemAPI.Query<RefRW<Boid>>())
        {
            var speed = Random.Range(spawner.SpeedRandomRange.x, spawner.SpeedRandomRange.y);
            boid.ValueRW.Velocity = math.normalize(new float3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0)) * speed;
        }

        state.EntityManager.AddComponent<SpawnerExecuted>(state.SystemHandle);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state) { }
}

public struct SpawnerExecuted : IComponentData { }