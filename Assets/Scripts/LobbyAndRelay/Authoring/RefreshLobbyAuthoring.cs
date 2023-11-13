using LobbyAndRelay.Components;
using Unity.Entities;
using UnityEngine;

namespace LobbyAndRelay.Authoring
{
    public class RefreshLobbyAuthoring : MonoBehaviour
    {
        public float refreshTime;
        private class RefreshLobbyAuthoringBaker : Baker<RefreshLobbyAuthoring>
        {
            public override void Bake(RefreshLobbyAuthoring authoring)
            {
                var entity = GetEntity(authoring, TransformUsageFlags.None);
                AddComponent(entity, new LobbyRefresher()
                {
                    refreshTimer = new Timer(authoring.refreshTime, true)
                });
                AddBuffer<LobbyInfoElement>(entity);
            }
        }
    }
}