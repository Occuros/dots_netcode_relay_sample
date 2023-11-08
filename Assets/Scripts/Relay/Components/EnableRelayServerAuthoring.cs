using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEngine;

namespace Samples.HelloNetcode
{
    public struct EnableRelayServer : IComponentData
    {
        public EntitySceneReference sceneReference;
        public EntitySceneReference lobbyScene;
    }

    #if UNITY_EDITOR
    public class EnableRelayServerAuthoring : MonoBehaviour
    {
        public UnityEditor.SceneAsset coreScene;
        public UnityEditor.SceneAsset lobbyScene;
        class Baker : Baker<EnableRelayServerAuthoring>
        {
            public override void Bake(EnableRelayServerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var reference = new EntitySceneReference(authoring.coreScene);
                AddComponent(entity, new EnableRelayServer()
                {
                    sceneReference = reference,
                    lobbyScene = new EntitySceneReference(authoring.lobbyScene),
                });
            }
        }
    }
    #endif
}
