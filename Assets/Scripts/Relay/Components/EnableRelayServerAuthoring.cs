using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEngine;

namespace Samples.HelloNetcode
{
    public struct EnableRelayServer : IComponentData
    {
        public EntitySceneReference sceneReference;
    }

    #if UNITY_EDITOR
    public class EnableRelayServerAuthoring : MonoBehaviour
    {
        public UnityEditor.SceneAsset coreScene;
        class Baker : Baker<EnableRelayServerAuthoring>
        {
            public override void Bake(EnableRelayServerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var reference = new EntitySceneReference(authoring.coreScene);
                AddComponent(entity, new EnableRelayServer()
                {
                    sceneReference = reference,
                });
            }
        }
    }
    #endif
}
