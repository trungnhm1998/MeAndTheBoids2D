using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct BoidMovementSystem : ISystem
{
    private EntityQuery _boidQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _boidQuery = SystemAPI.QueryBuilder().WithAll<Boid, LocalToWorld>().Build();
        state.RequireForUpdate<Boid>();
        state.RequireForUpdate<BoidConfig>();
        state.RequireForUpdate<WorldConfig>();
        state.RequireForUpdate(_boidQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var boidConfig = SystemAPI.GetSingleton<BoidConfig>();
        var worldConfig = SystemAPI.GetSingleton<WorldConfig>();

        var boidEntities = _boidQuery.ToEntityArray(state.WorldUpdateAllocator);
        NativeArray<LocalToWorld> boidTransforms =
            _boidQuery.ToComponentDataArray<LocalToWorld>(state.WorldUpdateAllocator);
        NativeArray<Boid> boids = _boidQuery.ToComponentDataArray<Boid>(state.WorldUpdateAllocator);

        var movementJob = new MoveJob
        {
            Boids = boids,
            BoidEntities = boidEntities,
            BoidsTransform = boidTransforms,
            Bound = worldConfig.Bound,
            DeltaTime = SystemAPI.Time.DeltaTime,
            Config = boidConfig
        };
        state.Dependency = movementJob.ScheduleParallel(state.Dependency);

        // var commandBuilder = DrawingManager.GetBuilder(true);
        // var visualizeJob = new VisualizeJob
        // {
        //     Builder = commandBuilder,
        //     ShowPerceptionRadius = boidConfig.ShowPerceptionRadius,
        //     PerceptionRadius = boidConfig.PerceptionRadius,
        //     FoV = boidConfig.FieldOfView
        // };
        // state.Dependency = visualizeJob.Schedule(state.Dependency);
        // commandBuilder.DisposeAfter(state.Dependency);

        // state.Dependency.Complete();

        boids.Dispose(state.Dependency);
        boidEntities.Dispose(state.Dependency);
        boidTransforms.Dispose(state.Dependency);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state) { }
}

[BurstCompile]
public partial struct MoveJob : IJobEntity
{
    public float DeltaTime;
    public float2 Bound;

    [ReadOnly] public NativeArray<Entity> BoidEntities;
    [ReadOnly] public NativeArray<LocalToWorld> BoidsTransform;
    [ReadOnly] public NativeArray<Boid> Boids;

    private Entity _entity;
    public BoidConfig Config;
    public bool EnableBoid => Config.EnableBoid;
    public float MaxSteeringForce => Config.MaxSteeringForce;
    public float PerceptionRadius => Config.PerceptionRadius;
    public float MaxSpeed => Config.MaxSpeed;

    void Execute(Entity entity, ref LocalToWorld transform, ref Boid boid)
    {
        _entity = entity;
        var position = transform.Position;

        if (EnableBoid)
        {
            boid.Acceleration *= 0;
            CalculateSteeringForces(position, ref boid, out var separation, out var alignment, out var cohesion);
            boid.Acceleration += separation * Config.SeparationWeight;
            boid.Acceleration += alignment * Config.AlignmentWeight;
            boid.Acceleration += cohesion * Config.CohesionWeight;
            
            if (math.any(!math.isfinite(boid.Acceleration))) boid.Acceleration = float3.zero;
        }
        
        boid.Velocity += boid.Acceleration;
        boid.Velocity = boid.Velocity.Limit(MaxSpeed);

        var newPosition = position + boid.Velocity * DeltaTime;
        newPosition = TeleportWhenOutOfBound(newPosition);

        var rotation = quaternion.RotateZ(math.atan2(boid.Velocity.y, boid.Velocity.x));
        transform = new LocalToWorld
        {
            Value = float4x4.TRS(newPosition, rotation, 1)
        };
        boid.Acceleration *= 0;
    }

    private void CalculateSteeringForces(float3 position, ref Boid boid, out float3 separation, out float3 alignment,
        out float3 cohesion)
    {
        separation = float3.zero;
        alignment = float3.zero;
        cohesion = float3.zero;

        var separationCount = 0;
        var alignmentCount = 0;
        var cohesionCount = 0;

        for (int i = 0; i < BoidEntities.Length; i++)
        {
            var otherBoidEntity = BoidEntities[i];
            if (otherBoidEntity == _entity) continue;
            var otherBoid = Boids[i];
            var otherPosition = BoidsTransform[i].Position;
            var otherVelocity = otherBoid.Velocity;

            var distance = math.length(otherPosition - position);

            // Separation
            if (distance < PerceptionRadius && distance > 0)
            {
                separation += (position - otherPosition) / distance;
                separationCount++;
            }

            // Alignment
            if (distance < PerceptionRadius)
            {
                alignment += otherVelocity;
                alignmentCount++;
            }

            // Cohesion
            if (distance < PerceptionRadius)
            {
                cohesion += otherPosition;
                cohesionCount++;
            }
        }

        if (separationCount > 0)
        {
            separation /= separationCount;
            separation = math.normalize(separation) * MaxSpeed;
            // steer = desired - velocity
            separation -= boid.Velocity;
            separation = separation.Limit(MaxSteeringForce);
        }

        if (alignmentCount > 0)
        {
            alignment /= alignmentCount;
            alignment = math.normalize(alignment) * MaxSpeed;
            // steer = desired - velocity
            alignment -= boid.Velocity;
            alignment = alignment.Limit(MaxSteeringForce);
        }

        if (cohesionCount > 0)
        {
            cohesion /= cohesionCount;
            cohesion -= position;
            cohesion = math.normalize(cohesion) * MaxSpeed;
            // steer = desired - velocity
            cohesion -= boid.Velocity;
            cohesion = cohesion.Limit(MaxSteeringForce);
        }
    }


    private float3 TeleportWhenOutOfBound(float3 newPosition)
    {
        if (newPosition.x <= -Bound.x / 2)
            newPosition.x = Bound.x / 2;
        else if (newPosition.x >= Bound.x / 2)
            newPosition.x = -Bound.x / 2;
        if (newPosition.y >= Bound.y / 2)
            newPosition.y = -Bound.y / 2;
        else if (newPosition.y <= -Bound.y / 2)
            newPosition.y = Bound.y / 2;
        return newPosition;
    }
}

// [BurstCompile]
// public partial struct VisualizeJob : IJobEntity
// {
//     public CommandBuilder Builder;
//     public bool ShowPerceptionRadius;
//     public float PerceptionRadius;
//     public float FoV;
//
//     public void Execute(in LocalToWorld transform, ref Boid boid)
//     {
//         // Builder.xy.Arrowhead(transform.Position, boid.Velocity, .1f, color: Color.white);
//         // Builder.xy.SolidCircle(transform.Position, .1f, Color.white);
//         // Builder.xy.SolidTriangle(transform.Position, math.normalize(boid.Velocity), .25f, Color.white);
//         
//         // draw a SolidArc to visualize the field of view
//         // Builder.xy.SolidArc(transform.Position, boid.Velocity, FoV, PerceptionRadius, Color.red);
//         if (ShowPerceptionRadius)
//         {
//             Builder.xy.Circle(transform.Position, PerceptionRadius, Color.green);
//         }
//     }
// }