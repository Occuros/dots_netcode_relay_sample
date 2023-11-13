using LobbyAndRelay.Components;
using LobbySelection.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace LobbySelection.Systems
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct DetectJoinServerTrigger : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StartHostingTrigger>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var cubes = SystemAPI.QueryBuilder()
                                 .WithAll<Cube>()
                                 .WithAll<LocalTransform>()
                                 .Build()
                                 .ToEntityArray(Allocator.Temp);
            var lobbyRoomEntities = SystemAPI.QueryBuilder()
                                        .WithAll<LobbyRoom>()
                                        .WithAll<LocalTransform>()
                                        .Build()
                                        .ToEntityArray(Allocator.Temp);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var joined = false;

            foreach (var cubeEntity in cubes)
            {
                if (joined) break;
                var cubeTransform = SystemAPI.GetComponent<LocalTransform>(cubeEntity);
                foreach (var lobbyRoomEntity in lobbyRoomEntities)
                {
                    var lobbyRoom = SystemAPI.GetComponent<LobbyRoom>(lobbyRoomEntity);
                    var lobbyRoomTransform = SystemAPI.GetComponent<LocalTransform>(lobbyRoomEntity);
                    if (math.distance(cubeTransform.Position, lobbyRoomTransform.Position) > 0.5f)
                    {
                        continue;
                    }
                    
                    var world = World.All[0].Unmanaged;
                    var requestEntity = world.EntityManager.CreateEntity(ComponentType.ReadOnly<JoinLobbyRequest>());
                    world.EntityManager.AddComponentData(requestEntity, new JoinLobbyRequest()
                    {
                        lobbyId = lobbyRoom.lobbyId
                    });

                    joined = true;
                    break;
                }
            }

            if (joined)
            {
                ecb.DestroyEntity(lobbyRoomEntities);
                ecb.DestroyEntity(SystemAPI.GetSingletonEntity<StartHostingTrigger>());
                
            }

            ecb.Playback(state.EntityManager);
            
           
        }
    }
}