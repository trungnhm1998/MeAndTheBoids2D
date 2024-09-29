using NativeTrees;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

// [DisableAutoCreation]
[BurstCompile]
public partial struct BoidMovementSystem : ISystem
{
    private EntityQuery _boidQuery;
    private EntityQuery _quadTreeQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _boidQuery = SystemAPI.QueryBuilder().WithAll<Boid, LocalTransform>().Build();
        _quadTreeQuery = SystemAPI.QueryBuilder().WithAll<QuadTreeComponent>().Build();

        state.RequireForUpdate<Boid>();
        state.RequireForUpdate<BoidConfig>();
        state.RequireForUpdate<WorldConfig>();
        state.RequireForUpdate(_boidQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (_boidQuery.IsEmpty) return;
        
        var boidConfig = SystemAPI.GetSingleton<BoidConfig>();
        var worldConfig = SystemAPI.GetSingleton<WorldConfig>();
        QuadTreeComponent quadTreeComponent;
        if (_quadTreeQuery.CalculateEntityCount() == 0 ||
            SystemAPI.TryGetSingletonEntity<QuadTreeComponent>(out var quadTreeEntity) == false)
        {
            var bounds = new AABB2D(worldConfig.BottomLeft, worldConfig.TopRight);
            var qTree = new NativeQuadtree<BoidWrapper>(bounds, Allocator.Persistent);

            quadTreeEntity = state.EntityManager.CreateEntity();
            quadTreeComponent = new QuadTreeComponent { Value = qTree };
            state.EntityManager.AddComponentData(quadTreeEntity, quadTreeComponent);
        }
        else
        {
            quadTreeComponent = state.EntityManager.GetComponentData<QuadTreeComponent>(quadTreeEntity);
        }

        var boidEntities = _boidQuery.ToEntityArray(state.WorldUpdateAllocator);
        NativeArray<LocalTransform> boidTransforms =
            _boidQuery.ToComponentDataArray<LocalTransform>(state.WorldUpdateAllocator);
        NativeArray<Boid> boids = _boidQuery.ToComponentDataArray<Boid>(state.WorldUpdateAllocator);

        if (boidConfig.EnableBoid)
        {
            state.Dependency = new BoidJob()
            {
                QuadTree = quadTreeComponent.Value,
                Config = boidConfig,
                Boids = boids,
                BoidTransforms = boidTransforms,
                BoidEntities = boidEntities,
            }.ScheduleParallel(state.Dependency);
        }

        state.Dependency = new MoveJob
        {
            Bound = worldConfig.Bound,
            DeltaTime = SystemAPI.Time.DeltaTime,
            Config = boidConfig,
        }.ScheduleParallel(state.Dependency);

        if (boidConfig.EnableBoid)
        {
            state.Dependency = new BuildQuadTreeJob
            {
                BoidEntities = boidEntities,
                Boids = boids,
                BoidTransforms = boidTransforms,
                QuadTree = quadTreeComponent.Value,
            }.Schedule(state.Dependency);
        }

        state.Dependency = boidEntities.Dispose(state.Dependency);
        state.Dependency = boids.Dispose(state.Dependency);
        state.Dependency = boidTransforms.Dispose(state.Dependency);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        var quadTreeEntity = SystemAPI.GetSingletonEntity<QuadTreeComponent>();
        var quadTreeComponent = state.EntityManager.GetComponentData<QuadTreeComponent>(quadTreeEntity);
        quadTreeComponent.Value.Dispose();
    }
}

[BurstCompile]
public partial struct BuildQuadTreeJob : IJob
{
    public NativeQuadtree<BoidWrapper> QuadTree;
    [ReadOnly] public NativeArray<Entity> BoidEntities;
    [ReadOnly] public NativeArray<Boid> Boids;
    [ReadOnly] public NativeArray<LocalTransform> BoidTransforms;

    [BurstCompile]
    public void Execute()
    {
        QuadTree.Clear();
        for (int i = 0; i < BoidTransforms.Length; i++)
        {
            var transform = BoidTransforms[i];
            var boidWrapper = new BoidWrapper
            {
                Entity = BoidEntities[i],
                Boid = Boids[i],
                Position = new float3(transform.Position.x, transform.Position.y, 0)
            };
            QuadTree.InsertPoint(boidWrapper, new float2(transform.Position.x, transform.Position.y));
        }
    }
}

[BurstCompile]
public struct Visitor : IQuadtreeRangeVisitor<BoidWrapper>
{
    public NativeList<BoidWrapper> NearbyBoids;

    [BurstCompile]
    public bool OnVisit(BoidWrapper obj, AABB2D objBounds, AABB2D queryRange)
    {
        NearbyBoids.Add(obj);
        return true;
    }
}

[BurstCompile]
public partial struct BoidJob : IJobEntity
{
    public BoidConfig Config;

    [ReadOnly] public NativeArray<Entity> BoidEntities;
    [ReadOnly] public NativeArray<Boid> Boids;
    [ReadOnly] public NativeArray<LocalTransform> BoidTransforms;
    [ReadOnly] public NativeQuadtree<BoidWrapper> QuadTree;

    [BurstCompile]
    public void Execute(Entity entity, ref Boid boid, ref LocalTransform transform)
    {
        boid.Acceleration *= 0;

        var position = transform.Position;
        var range = new AABB2D(new float2(position.x - Config.PerceptionRadius, position.y - Config.PerceptionRadius),
            new float2(position.x + Config.PerceptionRadius, position.y + Config.PerceptionRadius));

        var NearbyBoids = new NativeList<BoidWrapper>(Allocator.TempJob);
        var visitor = new Visitor { NearbyBoids = NearbyBoids };
        QuadTree.Range(range, ref visitor);

        CalculateSteeringForces(entity, NearbyBoids, Config.PerceptionRadius,
            transform.Position, ref boid,
            out var separation, out var alignment,
            out var cohesion, Config.MaxSpeed, Config.MaxSteeringForce);
        boid.Acceleration += separation * Config.SeparationWeight;
        boid.Acceleration += alignment * Config.AlignmentWeight;
        boid.Acceleration += cohesion * Config.CohesionWeight;

        if (math.any(!math.isfinite(boid.Acceleration))) boid.Acceleration = float3.zero;

        NearbyBoids.Dispose();
    }

    [BurstCompile]
    private static void CalculateSteeringForces(in Entity entity, in NativeList<BoidWrapper> nearbyBoids,
        float perceptionRadius, in float3 position, ref Boid boid, out float3 separation, out float3 alignment,
        out float3 cohesion, in float maxSpeed, in float maxSteeringForce)
    {
        separation = float3.zero;
        alignment = float3.zero;
        cohesion = float3.zero;

        var separationCount = 0;
        var alignmentCount = 0;
        var cohesionCount = 0;

        for (int i = 0; i < nearbyBoids.Length; i++)
        {
            var nearbyBoid = nearbyBoids[i];
            if (entity == nearbyBoid.Entity) continue;
            var otherBoid = nearbyBoid.Boid;
            var otherPosition = nearbyBoid.Position;
            var otherVelocity = otherBoid.Velocity;

            var distance = math.length(otherPosition - position);

            // Separation
            if (distance < perceptionRadius && distance > 0)
            {
                separation += (position - otherPosition) / distance;
                separationCount++;
            }

            // Alignment
            if (distance < perceptionRadius)
            {
                alignment += otherVelocity;
                alignmentCount++;
            }

            // Cohesion
            if (distance < perceptionRadius)
            {
                cohesion += otherPosition;
                cohesionCount++;
            }
        }

        if (separationCount > 0)
        {
            separation /= separationCount;
            separation = math.normalize(separation) * maxSpeed;
            separation -= boid.Velocity;
            separation = separation.Limit(maxSteeringForce);
        }

        if (alignmentCount > 0)
        {
            alignment /= alignmentCount;
            alignment = math.normalize(alignment) * maxSpeed;
            alignment -= boid.Velocity;
            alignment = alignment.Limit(maxSteeringForce);
        }

        if (cohesionCount > 0)
        {
            cohesion /= cohesionCount;
            cohesion -= position;
            cohesion = math.normalize(cohesion) * maxSpeed;
            cohesion -= boid.Velocity;
            cohesion = cohesion.Limit(maxSteeringForce);
        }
    }
}

[BurstCompile]
public partial struct MoveJob : IJobEntity
{
    public float DeltaTime;
    public float2 Bound;

    public BoidConfig Config;
    public float MaxSpeed => Config.MaxSpeed;

    void Execute(ref LocalTransform transform, ref Boid boid)
    {
        var position = transform.Position;

        boid.Velocity += boid.Acceleration;
        boid.Velocity = boid.Velocity.Limit(MaxSpeed);

        var newPosition = position + boid.Velocity * DeltaTime;
        newPosition = TeleportWhenOutOfBound(newPosition);

        transform.Position = newPosition;
        transform.Rotation = quaternion.RotateZ(math.atan2(boid.Velocity.y, boid.Velocity.x));

        boid.Acceleration *= 0;
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

public struct QuadTreeComponent : IComponentData
{
    public NativeQuadtree<BoidWrapper> Value;
}

public struct BoidWrapper
{
    public Entity Entity;
    public Boid Boid;
    public float3 Position;
}