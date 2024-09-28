using Unity.Burst;
using Unity.Entities;

public partial struct FlockingSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BoidConfig>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // foreach (var (transform, boid) in SystemAPI.Query<RefRW<LocalTransform>, RefRW<Boid>>())
        // {
        // var position = transform.ValueRO.Position;
        // var velocity = boid.ValueRO.Velocity;
        // var acceleration = boid.ValueRO.Acceleration;
        //
        // foreach (var (otherTransform, otherBoid) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<Boid>>())
        // {
        //     var otherPosition = otherTransform.ValueRO.Position;
        //     var otherVelocity = otherBoid.ValueRO.Velocity;
        //
        //     var distance = otherPosition - position;
        //     var distanceMagnitude = math.length(distance);
        //     if (distanceMagnitude < 1e-6f)
        //         continue;
        //
        //     var separation = math.normalize(distance) / distanceMagnitude;
        //     var alignment = math.normalize(otherVelocity);
        //     var cohesion = distance / distanceMagnitude;
        //
        //     acceleration += separation * 0.1f + alignment * 0.1f + cohesion * 0.1f;
        // }
        //
        // boid.ValueRW.Acceleration = acceleration;
        // boid.ValueRW.Velocity += acceleration;
        // }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state) { }
}