using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport.Relay;

namespace LobbyAndRelay.Components
{
    public struct RequestRelayRegions : IComponentData { }

    public struct RelayServerHostData : IComponentData
    {
        public RelayServerData data;
        public FixedString64Bytes joinCode;
    }

    public struct RelayClientData : IComponentData
    {
        public RelayServerData data;
        // public RelayClientStatus status;
    }

    public struct RequestToHostRelayServer : IComponentData
    {
        public int maxPeerConnections;
        public FixedString64Bytes region;
    }

    public struct RequestToJoinRelayServer : IComponentData
    {
        public FixedString64Bytes joinCode;
    }
    
    public struct RequestWorldCreation : IComponentData
    {
        
    }

    public struct RelayRegionElement : IBufferElementData
    {
        public FixedString64Bytes region;
    }
}