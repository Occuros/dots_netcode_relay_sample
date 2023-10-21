using Relay.Components;
using Unity.Entities;
using Unity.Services.Lobbies;
using UnityEngine;

namespace Relay.Systems
{
    public partial class LobbyHeartBeatSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<LobbyHearthBeat>();
        }

        protected override void OnUpdate()
        {
            ref var lobbyTimer = ref SystemAPI.GetSingletonRW<LobbyHearthBeat>().ValueRW;
            lobbyTimer.timer.Update(SystemAPI.Time.DeltaTime);

            if (lobbyTimer.timer.JustFinished)
            {
                Debug.Log("Sending HeartBeat");
                LobbyService.Instance.SendHeartbeatPingAsync(lobbyTimer.lobbyId.Value);
            }
        }
    }
}