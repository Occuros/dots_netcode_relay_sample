using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Relay.Components;
using Samples.HelloNetcode;
using Unity.Collections;
using Unity.Entities;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Relay
{
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class LobbySystem : SystemBase
    {
        private Task<Lobby> _createLobbyTask;
        private Task<Lobby> _joinLobbyTask;
        private Lobby _currentLobby;


        protected override void OnCreate()
        {
        }

        protected override void OnUpdate()
        {
            if (SystemAPI.TryGetSingleton<CreateLobbyRequest>(out var lobbyData))
            {
                var createLobbyOptions = new CreateLobbyOptions();
                createLobbyOptions.IsPrivate = false;
                createLobbyOptions.Data = new Dictionary<string, DataObject>()
                {
                    {
                        RelayUtilities.JoinCodeKey, new DataObject(
                            visibility: DataObject.VisibilityOptions.Member,
                            value: lobbyData.joinCode.Value
                        )
                    }
                };

                _createLobbyTask = Lobbies.Instance.CreateLobbyAsync(lobbyData.lobbyName.Value, lobbyData.maxPlayers, createLobbyOptions);
                EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<CreateLobbyRequest>());
            }

            if (SystemAPI.TryGetSingleton<JoinLobbyRequest>(out var joinLobbyRequest))
            {
                _joinLobbyTask = LobbyService.Instance.JoinLobbyByIdAsync(joinLobbyRequest.lobbyId.Value);
                EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<JoinLobbyRequest>());
            }
            
            
            
            HandleCreateLobby();
            HandleJoinLobby();

      
        }

        private void HandleCreateLobby()
        {
            if (_createLobbyTask == null) return;

            if (_createLobbyTask.IsCompletedSuccessfully)
            {
                _currentLobby = _createLobbyTask.Result;
                var heartBeatEntity =  EntityManager.CreateEntity(ComponentType.ReadOnly<LobbyHearthBeat>());
                EntityManager.AddComponentData(heartBeatEntity, new LobbyHearthBeat()
                {
                    lobbyId = _currentLobby.Id,
                    timer = new Timer(8.0f, true),
                });
               
                Debug.Log($"Lobby Creation Succeeded: {_currentLobby.Id}");
                _createLobbyTask = null;

            }
            else if (_createLobbyTask.IsFaulted)
            {
                Debug.Log($"Lobby Creation failed: {_createLobbyTask.Status}, {_createLobbyTask.Exception}");
                _createLobbyTask = null;

            }
        }

        private void HandleJoinLobby()
        {
            if (_joinLobbyTask == null) return;
            
            if (_joinLobbyTask.IsCompletedSuccessfully)
            {
                _currentLobby = _joinLobbyTask.Result;
                var joinCode = _currentLobby.Data[RelayUtilities.JoinCodeKey].Value;
                Debug.Log($"Lobby Joining Succeeded: {_currentLobby.Id}");
                var setupClientRelayEntity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(setupClientRelayEntity, new RequestToJoinRelayServer()
                {
                    joinCode = joinCode
                });
                _joinLobbyTask = null;
            }
            else if (_joinLobbyTask.IsFaulted)
            {
                Debug.Log($"Lobby Joining failed: {_joinLobbyTask.Status}, {_joinLobbyTask.Exception}");
                _joinLobbyTask = null;

            }
        }
        
        public void JoinLobby(FixedString64Bytes lobbyId)
        {
           _joinLobbyTask = LobbyService.Instance.JoinLobbyByIdAsync(lobbyId.Value);
        }

        
    }
}