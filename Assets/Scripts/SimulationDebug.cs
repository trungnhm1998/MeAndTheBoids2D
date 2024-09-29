using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public class SimulationDebug : MonoBehaviour
{
    public bool ShowQuadTree = false;

    private void OnDrawGizmos()
    {
        if (Application.isPlaying == false) return;
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        entityManager.CompleteAllTrackedJobs();

        HandleDebugQuadTree(entityManager);
    }

    private void HandleDebugQuadTree(EntityManager entityManager)
    {
        if (ShowQuadTree == false) return;
        Gizmos.color = Color.white;
        var quadTreeQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<QuadTreeComponent>().Build(entityManager);
        if (quadTreeQuery.IsEmpty == false)
        {
            var entity = quadTreeQuery.GetSingletonEntity();
            var quadTreeComponent = entityManager.GetComponentData<QuadTreeComponent>(entity);

            var bounds = quadTreeComponent.Value.Bounds;
            Gizmos.DrawWireSphere(new Vector3(bounds.min.x, bounds.min.y, 0f), 1);
            Gizmos.DrawWireSphere(new Vector3(bounds.max.x, bounds.max.y, 0f), 1);
            quadTreeComponent.Value.DrawGizmos();
        }

        quadTreeQuery.Dispose();
    }
}