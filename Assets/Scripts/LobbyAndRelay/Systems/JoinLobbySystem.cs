using System.Threading.Tasks;
using LobbyAndRelay.Components;
using Samples.HelloNetcode;
using Unity.Entities;
using Unity.Services.Lobbies;
using UnityEngine;

namespace LobbyAndRelay.Systems
{
    public partial class JoinLobbySystem : SystemBase
    {
        private Task<Unity.Services.Lobbies.Models.Lobby> _joinLobbyTask;

        protected override void OnCreate()
        {
            RequireForUpdate<JoinLobbyRequest>();
        }

        protected override void OnUpdate()
        {
            if (_joinLobbyTask == null)
            {
                var joinLobbyRequest = SystemAPI.GetSingleton<JoinLobbyRequest>();
                _joinLobbyTask = LobbyService.Instance.JoinLobbyByIdAsync(joinLobbyRequest.lobbyId.Value);
            }
            
            if (_joinLobbyTask.IsCompletedSuccessfully)
            {
                var currentLobby = _joinLobbyTask.Result;
                var joinCode = currentLobby.Data[RelayUtilities.JoinCodeKey].Value;
                Debug.Log($"Lobby Joining Succeeded: {currentLobby.Id}");
                var setupClientRelayEntity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(setupClientRelayEntity, new RequestToJoinRelayServer()
                {
                    joinCode = joinCode
                });
                _joinLobbyTask = null;
                EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<JoinLobbyRequest>());

            }
            else if (_joinLobbyTask.IsFaulted)
            {
                Debug.Log($"Lobby Joining failed: {_joinLobbyTask.Status}, {_joinLobbyTask.Exception}");
                _joinLobbyTask = null;
                EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<JoinLobbyRequest>());
            }
        }
    }
}