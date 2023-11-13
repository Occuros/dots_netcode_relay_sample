using System.Collections.Generic;
using System.Threading.Tasks;
using LobbyAndRelay.Components;
using Unity.Entities;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace LobbyAndRelay.Systems
{
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class CreateLobbySystem : SystemBase
    {
        private Task<Lobby> _createLobbyTask;
        private Task<Lobby> _joinLobbyTask;

        protected override void OnCreate()
        {
            RequireForUpdate<CreateLobbyRequest>();
        }

        protected override void OnUpdate()
        {
            var request = SystemAPI.GetSingleton<CreateLobbyRequest>();

            if (_createLobbyTask == null)
            {
                var createLobbyOptions = new CreateLobbyOptions
                {
                    IsPrivate = false,
                    Data = new Dictionary<string, DataObject>()
                    {
                        {
                            RelayUtilities.JoinCodeKey, new DataObject(
                                visibility: DataObject.VisibilityOptions.Member,
                                value: request.joinCode.Value
                            )
                        }
                    }
                };

                _createLobbyTask
                    = Lobbies.Instance.CreateLobbyAsync(request.lobbyName.Value, request.maxPlayers,
                        createLobbyOptions);
            }
            else if (_createLobbyTask.IsCompletedSuccessfully)
            {
                var currentLobby = _createLobbyTask.Result;
                var heartBeatEntity = EntityManager.CreateEntity(ComponentType.ReadOnly<LobbyHearthBeat>());
                EntityManager.AddComponentData(heartBeatEntity, new LobbyHearthBeat()
                {
                    lobbyId = currentLobby.Id,
                    timer = new Timer(8.0f, true),
                });

                Debug.Log($"Lobby Creation Succeeded: {currentLobby.Id}");
                _createLobbyTask = null;
                EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<CreateLobbyRequest>());

            }
            else if (_createLobbyTask.IsFaulted)
            {
                Debug.Log($"Lobby Creation failed: {_createLobbyTask.Status}, {_createLobbyTask.Exception}");
                _createLobbyTask = null;
                EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<CreateLobbyRequest>());

            }
        }
    }
}