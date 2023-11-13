using LobbySelection.Components;
using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEngine;

namespace LobbySelection.Authoring
{
    #if UNITY_EDITOR
    [DisallowMultipleComponent]
    public class StartingHubAuthoring : MonoBehaviour
    {
        public UnityEditor.SceneAsset lobbyScene;
        public UnityEditor.SceneAsset coreScene;
        

        internal class StartingHubAuthoringBaker : Baker<StartingHubAuthoring>
        {
            public override void Bake(StartingHubAuthoring authoring)
            {
                var entity = GetEntity(authoring, TransformUsageFlags.None);
                AddComponent(entity, new StartingHub()
                {
                    lobbyScene = new EntitySceneReference(authoring.lobbyScene),
                    coreScene = new EntitySceneReference(authoring.coreScene),
                });
                AddComponent(entity, new LoadLobbySceneRequest());
            }
        }
    }
    #endif

}

