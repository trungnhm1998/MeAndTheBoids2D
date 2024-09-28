using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(FlockingSystem))]
public partial struct BoidMovementSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Boid>();
        state.RequireForUpdate<BoidConfig>();
        state.RequireForUpdate<WorldConfig>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var boidConfig = SystemAPI.GetSingleton<BoidConfig>();
        var worldConfig = SystemAPI.GetSingleton<WorldConfig>();
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (transform, boid) in
                 SystemAPI.Query<RefRW<LocalToWorld>, RefRW<Boid>>())
        {
            var position = transform.ValueRO.Position;

            var newPosition = position + boid.ValueRW.Velocity * deltaTime;
            boid.ValueRW.Velocity += boid.ValueRO.Acceleration;

            if (newPosition.x <= -worldConfig.Bound.x / 2)
                newPosition.x = worldConfig.Bound.x / 2;
            else if (newPosition.x >= worldConfig.Bound.x / 2)
                newPosition.x = -worldConfig.Bound.x / 2;
            else if (newPosition.y >= worldConfig.Bound.y / 2)
                newPosition.y = -worldConfig.Bound.y / 2;
            else if (newPosition.y <= -worldConfig.Bound.y / 2)
                newPosition.y = worldConfig.Bound.y / 2;

            transform.ValueRW = new LocalToWorld
            {
                Value = float4x4.TRS(newPosition, quaternion.identity, new float3(1))
            };
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state) { }
}