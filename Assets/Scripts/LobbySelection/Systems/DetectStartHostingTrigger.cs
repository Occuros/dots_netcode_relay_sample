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
    public partial struct DetectStartHostingTrigger : ISystem
    {
 
        // [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var cubes = SystemAPI.QueryBuilder()
                                 .WithAll<Cube>()
                                 .WithAll<LocalTransform>()
                                 .Build()
                                 .ToEntityArray(Allocator.Temp);
            var hostTriggers = SystemAPI.QueryBuilder()
                                        .WithAll<StartHostingTrigger>()
                                        .WithAll<LocalTransform>()
                                        .Build()
                                        .ToEntityArray(Allocator.Temp);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var cubeEntity in cubes)
            {
                var cubeTransform = SystemAPI.GetComponent<LocalTransform>(cubeEntity);
                foreach (var hostTriggerEntity in hostTriggers)
                {
                    var hostTriggerTransform = SystemAPI.GetComponent<LocalTransform>(hostTriggerEntity);
                    if (math.distance(cubeTransform.Position, hostTriggerTransform.Position) > 0.5f)
                    {
                        continue;
                    }
                    
                    var world = World.All[0].Unmanaged;
                    Debug.Log($"We have the world for hosting: {world.Name}");
                    var requestEntity = world.EntityManager.CreateEntity(ComponentType.ReadOnly<RequestToHostRelayServer>());
                    world.EntityManager.AddComponentData(requestEntity, new RequestToHostRelayServer()
                    {
                        maxPeerConnections = 5,
                    });
                    ecb.DestroyEntity(hostTriggerEntity);
                }
            }

            ecb.Playback(state.EntityManager);
            cubes.Dispose();
            hostTriggers.Dispose();


        }
    }
}