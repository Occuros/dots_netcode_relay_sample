using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Mathematics;

namespace LobbySelection.Components
{
    public struct LobbyRoom: IComponentData
    {
        public FixedString64Bytes lobbyId;
        public FixedString64Bytes joinCode;
    }

    public struct LobbyRoomSpawnSpot : IBufferElementData
    {
        public FixedString64Bytes lobbyId;
        public float3 position;
        public Entity roomCubeEntity;
    }

    public struct StartHostingTrigger : IComponentData
    {
        
    }
    


    public struct StartingHub : IComponentData
    {
        public EntitySceneReference coreScene;
        public EntitySceneReference lobbyScene;
    }

    public struct LoadLobbySceneRequest : IComponentData
    {
    }
}