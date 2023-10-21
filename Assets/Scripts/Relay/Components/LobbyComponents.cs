using Relay.Components;
using Unity.Collections;
using Unity.Entities;

namespace Relay
{

    public struct UnityServiceInitialized : IComponentData
    {
        
    }
    
    public struct CreateLobbyRequest : IComponentData
    {
        public FixedString32Bytes joinCode;
        public int maxPlayers;
        public FixedString512Bytes lobbyName;
    }

    public struct RequestClientRelayWithJoinCode : IComponentData
    {
        public FixedString64Bytes joinCode;
    }

    public struct LobbyRefresher : IComponentData
    {
        public Timer refreshTimer;
    }

    public struct LobbyInfoElement : IBufferElementData
    {
        public FixedString64Bytes lobbyId;
        public FixedString64Bytes joinCode;
    }
}