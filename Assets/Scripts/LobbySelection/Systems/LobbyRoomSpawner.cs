using System.Net;
using LobbyAndRelay.Components;
using LobbySelection.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Services.Lobbies.Models;
using Unity.Transforms;
using UnityEngine;

namespace LobbySelection.Systems
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct LobbyRoomSpawner : ISystem
    {
        private EntityQuery _lobbyElementQuery;
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<LobbyManager>();
            var world = World.All[0];
            
            _lobbyElementQuery = world.EntityManager.CreateEntityQuery(new ComponentType(typeof(LobbyInfoElement)));
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (_lobbyElementQuery.IsEmpty) return;
            var lobbyInfoBuffer = _lobbyElementQuery.GetSingletonBuffer<LobbyInfoElement>();
            var spawnSpots = SystemAPI.GetSingletonBuffer<LobbyRoomSpawnSpot>();
            var manager = SystemAPI.GetSingleton<LobbyManager>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            for (int i = spawnSpots.Length - 1; i >= 0; i--)
            {
                var spawnSpot = spawnSpots[i];
                if (spawnSpot.roomCubeEntity == Entity.Null) continue;
                if (LobbyExists(lobbyInfoBuffer, spawnSpot.lobbyId)) continue;
                ecb.DestroyEntity(spawnSpot.roomCubeEntity);
                spawnSpot.lobbyId = default;
                spawnSpot.roomCubeEntity = Entity.Null;
            }

            for (int i = 0; i < lobbyInfoBuffer.Length; i++)
            {
                var lobby = lobbyInfoBuffer[i];
                if (RoomExists(spawnSpots, lobby.lobbyId)) continue;

                for (var j = 0; j < spawnSpots.Length; j++)
                {
                    var spot = spawnSpots[j];
                    if (spot.lobbyId != default) continue;
                    var roomCube = state.EntityManager.Instantiate(manager.lobbyCubeEntity);
                    ref var room = ref SystemAPI.GetComponentRW<LobbyRoom>(roomCube).ValueRW;
                    room.lobbyId = lobby.lobbyId;
                    room.joinCode = lobby.joinCode;
                    ref var transform = ref SystemAPI.GetComponentRW<LocalTransform>(roomCube).ValueRW;
                    transform.Position = spot.position + math.up() * 0.1f;
                    spot.lobbyId = lobby.lobbyId;
                    spot.roomCubeEntity = roomCube;
                    spawnSpots[i] = spot;
                    break;
                }
            }
        }


        private bool LobbyExists(DynamicBuffer<LobbyInfoElement> lobbyInfoBuffer, FixedString64Bytes lobbyId)
        {
            foreach (var lobbyInfoElement in lobbyInfoBuffer)
            {
                if (lobbyInfoElement.lobbyId == lobbyId) return true;
            }

            return false;
        }

        private bool RoomExists(DynamicBuffer<LobbyRoomSpawnSpot> spawnSpots, FixedString64Bytes lobbyId)
        {
            foreach (var spot in spawnSpots)
            {
                if (spot.lobbyId == lobbyId) return true;
            }

            return false;
        }
    }
}