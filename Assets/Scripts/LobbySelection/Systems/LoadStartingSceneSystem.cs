using LobbySelection.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using Unity.Scenes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LobbySelection.Systems
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct LoadStartingSceneSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StartingHub>();
            state.RequireForUpdate<LoadLobbySceneRequest>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var startingHub = SystemAPI.GetSingleton<StartingHub>();
            Debug.Log("we are loading lobby scene");
            foreach (var worldManaged in World.All)
            {
                var world = worldManaged.Unmanaged;
                if (world.IsClient() || world.IsServer())
                {
                    Debug.Log($"loading lobby into world {world.Name}");
                    SceneSystem.LoadSceneAsync(world, startingHub.lobbyScene);
                }
                state.EntityManager.RemoveComponent<LoadLobbySceneRequest>(SystemAPI.GetSingletonEntity<StartingHub>());
            }
        }

    }
}