using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class SpawnerAuthoring : MonoBehaviour
{
    [SerializeField] private int _spawnPerFrame;
    [SerializeField] private GameObject _boidPrefab;
    [SerializeField] private int _boidCount;
    public Vector2 RandomSpeedRange = new(1, 2);

    private class Baker : Baker<SpawnerAuthoring>
    {
        public override void Bake(SpawnerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new BoidSpawner
            {
                Prefab = GetEntity(authoring._boidPrefab, TransformUsageFlags.None),
                Count = authoring._boidCount,
                SpeedRandomRange = authoring.RandomSpeedRange,
                SpawnPerFrame = authoring._spawnPerFrame
            });
        }
    }
}

public struct BoidSpawner : IComponentData
{
    public Entity Prefab;
    public int Count;
    public float2 SpeedRandomRange;
    public int SpawnPerFrame;
}