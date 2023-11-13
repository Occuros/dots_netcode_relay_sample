using LobbySelection.Components;
using Unity.Entities;
using UnityEngine;

namespace LobbySelection.Authoring
{
    public class LobbyRoomAuthoring : MonoBehaviour
    {
        private class LobbyRoomAuthoringBaker : Baker<LobbyRoomAuthoring>
        {
            public override void Bake(LobbyRoomAuthoring authoring)
            {
                var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
                AddComponent(entity, new LobbyRoom() );
            }
        }
    }
}