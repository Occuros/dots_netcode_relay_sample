using System.Threading.Tasks;
using Samples.HelloNetcode;
using UI;
using Unity.Entities;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace Relay.Systems
{
    public partial class LobbyRefreshSystem : SystemBase
    {
        private Task<QueryResponse> _lobbyRefreshTask;

        protected override void OnCreate()
        {
            RequireForUpdate<LobbyRefresher>();
            RequireForUpdate<UnityServiceInitialized>();
        }

        protected override void OnUpdate()
        {
            ref var lobbyRefresher = ref SystemAPI.GetSingletonRW<LobbyRefresher>().ValueRW;
            lobbyRefresher.refreshTimer.Update(SystemAPI.Time.DeltaTime);

            if (_lobbyRefreshTask == null && lobbyRefresher.refreshTimer.JustFinished)
            {
                _lobbyRefreshTask = LobbyService.Instance.QueryLobbiesAsync();
            }

            if (_lobbyRefreshTask == null)
            {
                return;
            }

            if (_lobbyRefreshTask.IsCompletedSuccessfully)
            {
                var lobbyItems = SystemAPI.GetSingletonBuffer<LobbyInfoElement>();
                lobbyItems.Clear();
                UIManager.Instance.ClearAllButtons();
                foreach (var lobby in _lobbyRefreshTask.Result.Results)
                {
                    Debug.Log($"WE have lobby {lobby.Id}, {lobby.Name}, {lobby.IsLocked}, {lobby.Players}, data: {lobby.Data} ");
                    var element = new LobbyInfoElement()
                    {
                        lobbyId = lobby.Id,
                    };
                    UIManager.Instance.AddJoinButton(element);

                    lobbyItems.Add(element);
                }
                
                _lobbyRefreshTask = null;
            } 
            else if (_lobbyRefreshTask.IsCompleted)
            {
                Debug.Log($"We have it completed but confused {_lobbyRefreshTask.Status} => {_lobbyRefreshTask.Exception}");
                _lobbyRefreshTask = null;
            }
        }
    }
}