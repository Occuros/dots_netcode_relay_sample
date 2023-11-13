using Unity.Collections;
using Unity.Entities;

namespace LobbyAndRelay.Components
{
    public struct LobbyHearthBeat : IComponentData
    {
        public FixedString64Bytes lobbyId;
        public Timer timer;
    }
}