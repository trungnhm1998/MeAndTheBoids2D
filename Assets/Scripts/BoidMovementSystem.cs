using NativeTrees;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct BoidMovementSystem : ISystem
{
    private EntityQuery _boidQuery;
    private EntityQuery _quadTreeQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _boidQuery = SystemAPI.QueryBuilder().WithAll<Boid, LocalToWorld>().Build();
        _quadTreeQuery = SystemAPI.QueryBuilder().WithAll<QuadTreeComponent>().Build();

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
        QuadTreeComponent quadTreeComponent;
        if (_quadTreeQuery.CalculateEntityCount() == 0 ||
            SystemAPI.TryGetSingletonEntity<QuadTreeComponent>(out var quadTreeEntity) == false)
        {
            var bounds = new AABB2D(worldConfig.BottomLeft, worldConfig.TopRight);
            var qTree = new NativeQuadtree<Boid>(bounds, Allocator.Persistent);

            quadTreeEntity = state.EntityManager.CreateEntity();
            quadTreeComponent = new QuadTreeComponent { Value = qTree };
            state.EntityManager.AddComponentData(quadTreeEntity, quadTreeComponent);
        }
        else
        {
            quadTreeComponent = state.EntityManager.GetComponentData<QuadTreeComponent>(quadTreeEntity);
        }

        var boidEntities = _boidQuery.ToEntityArray(state.WorldUpdateAllocator);
        NativeArray<LocalToWorld> boidTransforms =
            _boidQuery.ToComponentDataArray<LocalToWorld>(state.WorldUpdateAllocator);
        NativeArray<Boid> boids = _boidQuery.ToComponentDataArray<Boid>(state.WorldUpdateAllocator);
        state.Dependency = new MoveJob
        {
            QuadTree = quadTreeComponent.Value,
            Boids = boids,
            BoidTransforms = boidTransforms,
            BoidEntities = boidEntities,
            Bound = worldConfig.Bound,
            DeltaTime = SystemAPI.Time.DeltaTime,
            Config = boidConfig
        }.ScheduleParallel(state.Dependency);

        state.Dependency = new BuildQuadTreeJob
        {
            Boids = boids,
            BoidTransforms = boidTransforms,
            QuadTree = quadTreeComponent.Value
        }.Schedule(state.Dependency);

        boidEntities.Dispose(state.Dependency);
        boids.Dispose(state.Dependency);
        boidTransforms.Dispose(state.Dependency);
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
    public NativeQuadtree<Boid> QuadTree;
    [ReadOnly] public NativeArray<Boid> Boids;
    [ReadOnly] public NativeArray<LocalToWorld> BoidTransforms;

    public void Execute()
    {
        QuadTree.Clear();
        for (int i = 0; i < BoidTransforms.Length; i++)
        {
            var transform = BoidTransforms[i];
            QuadTree.InsertPoint(Boids[i], new float2(transform.Position.x, transform.Position.y));
        }
    }
}

[BurstCompile]
public partial struct MoveJob : IJobEntity
{
    public float DeltaTime;
    public float2 Bound;

    public BoidConfig Config;
    public bool EnableBoid => Config.EnableBoid;
    public float MaxSteeringForce => Config.MaxSteeringForce;
    public float PerceptionRadius => Config.PerceptionRadius;
    public float MaxSpeed => Config.MaxSpeed;
    
    [ReadOnly] public NativeQuadtree<Boid> QuadTree;
    [ReadOnly] public NativeArray<Boid> Boids;
    [ReadOnly] public NativeArray<LocalToWorld> BoidTransforms;
    [ReadOnly] public NativeArray<Entity> BoidEntities;

    void Execute(ref LocalToWorld transform, ref Boid boid)
    {
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

        // var nearbyBoids = new NativeQueue<BoidWrapper>(Allocator.Temp);
        // var set = new NativeParallelHashSet<int>(10, Allocator.Temp);
        // var nearestTen = new NearestTen
        // {
        //     NearestEntities = nearbyBoids,
        //     Set = set,
        // };
        // QuadTree.Nearest(new float2(position.x, position.y), Config.PerceptionRadius, ref nearestTen,
        //     default(NativeQuadtreeExtensions.AABBDistanceSquaredProvider<BoidWrapper>));
        //
        // while (nearbyBoids.TryDequeue(out BoidWrapper other))
        for (int i = 0; i < BoidEntities.Length; i++)
        {
            var otherBoid = Boids[i];
            if (otherBoid.Id == boid.Id) continue;
            var otherPosition = BoidTransforms[i].Position;
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

        // nearbyBoids.Dispose();
        // set.Dispose();
    }

    private struct NearestTen : IQuadtreeNearestVisitor<BoidWrapper>
    {
        public NativeQueue<BoidWrapper> NearestEntities;
        public NativeParallelHashSet<int> Set;

        public bool OnVist(BoidWrapper entity)
        {
            if (Set.Add(entity.Boid.Id)) NearestEntities.Enqueue(entity);

            return NearestEntities.Count < 10;
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

public struct QuadTreeComponent : IComponentData
{
    public NativeQuadtree<Boid> Value;
}

public struct BoidWrapper
{
    public Boid Boid;
    public float3 Position;
}