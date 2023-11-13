using LobbyAndRelay.Components;
using LobbySelection.Components;
using Samples.HelloNetcode;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Scenes;
using UnityEngine;


namespace LobbyAndRelay.Systems
{
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class RelayWorldCreationSystem : SystemBase
    {
        
        protected override void OnCreate()
        {
            RequireForUpdate<RequestWorldCreation>();
        }


        protected override void OnUpdate()
        {
            if (SystemAPI.TryGetSingleton<RelayServerHostData>(out var serverHostData) &&
                SystemAPI.TryGetSingleton<RelayClientData>(out var relayClientData))
            {
                Debug.Log("Setting up server + client worlds");
                SetupRelayHostedServerAndConnect(serverHostData.data, relayClientData.data,
                    serverHostData.joinCode);
                EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<RequestWorldCreation>());
                EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<LobbyRefresher>());
            }
            else if (SystemAPI.TryGetSingleton<RelayClientData>(out relayClientData))
            {
                Debug.Log("Setting up client worlds");

                ConnectToRelayServer(relayClientData.data);
                EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<LobbyRefresher>());
                EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<RequestWorldCreation>());
            }
            
        }


        void SetupRelayHostedServerAndConnect(RelayServerData relayServerData,
                                                         RelayServerData relayClientData, FixedString64Bytes joinCode)
        {
            Debug.Log("setting up realy host server and connect");
            if (ClientServerBootstrap.RequestedPlayType != ClientServerBootstrap.PlayType.ClientAndServer)
            {
                UnityEngine.Debug.LogError(
                    $"Creating client/server worlds is not allowed if playmode is set to {ClientServerBootstrap.RequestedPlayType}");
            }


            for (int i = World.All.Count - 1; i >= 0; i--)
            {
                var previousWorld = World.All[i];
                if (previousWorld.IsClient() || previousWorld.IsServer())
                {
                    Debug.Log($"We dispose of world {previousWorld.Name}");
                    previousWorld.Dispose();
                }
            }

            var relayStuff = SystemAPI.GetSingleton<StartingHub>();
            var createLobbyEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(createLobbyEntity, new CreateLobbyRequest()
            {
                joinCode = joinCode,
                lobbyName = $"CubeTest_{Random.Range(0, 10_000)}",
                maxPlayers = 4
            });

            ClientServerBootstrap.AutoConnectPort = 0;
            var oldConstructor = NetworkStreamReceiveSystem.DriverConstructor;
            NetworkStreamReceiveSystem.DriverConstructor = new RelayDriverConstructor(relayServerData, relayClientData);
            
            var server = ClientServerBootstrap.CreateServerWorld("ServerWorld");
            SceneSystem.LoadSceneAsync(server.Unmanaged, relayStuff.coreScene);
            var snapQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<NetworkSnapshotAck>();
            
            server.EntityManager.DestroyEntity(server.EntityManager.CreateEntityQuery(snapQuery));
            
            var client = ClientServerBootstrap.CreateClientWorld("ClientWorld");
            SceneSystem.LoadSceneAsync(client.Unmanaged, relayStuff.coreScene);
            client.EntityManager.DestroyEntity(client.EntityManager.CreateEntityQuery(snapQuery));
            
            NetworkStreamReceiveSystem.DriverConstructor = oldConstructor;
            
            
            if (World.DefaultGameObjectInjectionWorld == null)
                World.DefaultGameObjectInjectionWorld = server;
            
            
            var networkStreamEntity
                = server.EntityManager.CreateEntity(ComponentType.ReadWrite<NetworkStreamRequestListen>());
            server.EntityManager.SetName(networkStreamEntity, "NetworkStreamRequestListen");
            server.EntityManager.SetComponentData(networkStreamEntity,
                new NetworkStreamRequestListen { Endpoint = NetworkEndpoint.AnyIpv4 });
            
            networkStreamEntity
                = client.EntityManager.CreateEntity(ComponentType.ReadWrite<NetworkStreamRequestConnect>());
            client.EntityManager.SetName(networkStreamEntity, "NetworkStreamRequestConnect");
            // For IPC this will not work and give an error in the transport layer. For this sample we force the client to connect through the relay service.
            // For a locally hosted server, the client would need to connect to NetworkEndpoint.AnyIpv4, and the relayClientData.Endpoint in all other cases.
            client.EntityManager.SetComponentData(networkStreamEntity,
                new NetworkStreamRequestConnect { Endpoint = relayClientData.Endpoint });

        }


        void ConnectToRelayServer(RelayServerData relayClientData)
        {
            ClientServerBootstrap.AutoConnectPort = 0;
            for (int i = World.All.Count - 1; i >= 0; i--)
            {
                var previousWorld = World.All[i];
                if (previousWorld.IsClient() || previousWorld.IsServer())
                {
                    Debug.Log($"We dispose of world {previousWorld.Name}");
                    previousWorld.Dispose();
                }
            }
            var relayStuff = SystemAPI.GetSingleton<StartingHub>();

            var oldConstructor = NetworkStreamReceiveSystem.DriverConstructor;
            NetworkStreamReceiveSystem.DriverConstructor
                = new RelayDriverConstructor(new RelayServerData(), relayClientData);
            var client = ClientServerBootstrap.CreateClientWorld("ClientWorld");
            SceneSystem.LoadSceneAsync(client.Unmanaged, relayStuff.coreScene);
            NetworkStreamReceiveSystem.DriverConstructor = oldConstructor;

            //Destroy the local simulation world to avoid the game scene to be loaded into it
            //This prevent rendering (rendering from multiple world with presentation is not greatly supported)
            //and other issues.
            if (World.DefaultGameObjectInjectionWorld == null)
                World.DefaultGameObjectInjectionWorld = client;


            var networkStreamEntity
                = client.EntityManager.CreateEntity(ComponentType.ReadWrite<NetworkStreamRequestConnect>());
            client.EntityManager.SetName(networkStreamEntity, "NetworkStreamRequestConnect");
            // For IPC this will not work and give an error in the transport layer. For this sample we force the client to connect through the relay service.
            // For a locally hosted server, the client would need to connect to NetworkEndpoint.AnyIpv4, and the relayClientData.Endpoint in all other cases.
            client.EntityManager.SetComponentData(networkStreamEntity,
                new NetworkStreamRequestConnect { Endpoint = relayClientData.Endpoint });
        
        }
    }
}