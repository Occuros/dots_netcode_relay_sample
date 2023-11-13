using System.Collections.Generic;
using LobbyAndRelay.Components;
using LobbySelection.Components;
using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEngine;

namespace LobbyAndRelay.Authoring
{


    #if UNITY_EDITOR
    public class EnableRelayServerAuthoring : MonoBehaviour
    {
        public GameObject lobbyCubePrefab;
        public List<Transform> lobbySpawnPoints;
        class Baker : Baker<EnableRelayServerAuthoring>
        {
            public override void Bake(EnableRelayServerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new LobbyManager()
                {
                    lobbyCubeEntity = GetEntity(authoring.lobbyCubePrefab, TransformUsageFlags.Dynamic),
                });

                var roomSpotsBuffer = AddBuffer<LobbyRoomSpawnSpot>(entity);
                
                for (var i = 0; i < authoring.lobbySpawnPoints.Count; i++)
                {
                    var spot = authoring.lobbySpawnPoints[i];
                    roomSpotsBuffer.Add(new LobbyRoomSpawnSpot()
                    {
                        lobbyId = default,
                        position = spot.position,
                    });

                }
            }
        }
    }
    #endif
}
