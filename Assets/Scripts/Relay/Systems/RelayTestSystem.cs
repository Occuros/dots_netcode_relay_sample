using System.Collections.Generic;
using Relay.Components;
using Samples.HelloNetcode;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Scenes;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Relay
{
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class RelayTestSystem : SystemBase
    {
        private JoinRelayServerSystem m_HostClientSystem;
        private HostRelayServer _mHostRelayServerSystem;

        private float waitingTime = 0.0f;

        public struct RelayConnectionState : IComponentData
        {
            public ConnectionState connectionState;
        }

        public enum ConnectionState
        {
            Unknown,
            SetupHost,
            AwaitingHostSetup,
            SetupClient,
            JoinGame,
            JoinLocalGame,
        }

        protected override void OnCreate()
        {
            EntityManager.AddComponent<RelayConnectionState>(this.SystemHandle);
            RequireForUpdate<RequestWorldCreation>();
            RequireForUpdate<EnableRelayServer>();
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
            }
            else if (SystemAPI.TryGetSingleton<RelayClientData>(out relayClientData))
            {
                Debug.Log("Setting up client worlds");

                ConnectToRelayServer(relayClientData.data);
                EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<RequestWorldCreation>());
            }

            // HandleInput();
            //
            // ref var state = ref SystemAPI.GetComponentRW<RelayConnectionState>(SystemHandle).ValueRW;
            // var m_State = state.connectionState;
            //
            //
            // switch (m_State)
            // {
            //     case ConnectionState.SetupHost:
            //     {
            //         Debug.Log("Setting up Host");
            //         var hostRequestEntity
            //             = EntityManager.CreateEntity(ComponentType.ReadOnly<RequestToHostRelayServer>());
            //         EntityManager.AddComponentData(hostRequestEntity, new RequestToHostRelayServer()
            //         {
            //             region = default,
            //             maxPeerConnections = 5,
            //         });
            //         m_State = ConnectionState.AwaitingHostSetup;
            //         break;
            //     }
            //     case ConnectionState.AwaitingHostSetup:
            //     {
            //         if (SystemAPI.TryGetSingleton<RelayServerHostData>(out var serverData))
            //         {
            //             if (!SystemAPI.HasSingleton<RelayClientData>() &&
            //                 !SystemAPI.HasSingleton<RequestToJoinRelayServer>())
            //             {
            //                 var requestToJoinRelayEntity = EntityManager.CreateEntity(ComponentType.ChunkComponentReadOnly<RequestToJoinRelayServer>());
            //                 EntityManager.AddComponentData(requestToJoinRelayEntity, new RequestToJoinRelayServer()
            //                 {
            //                     joinCode = serverData.joinCode
            //                 });
            //             }
            //
            //             if (SystemAPI.TryGetSingleton<RelayClientData>(out var clientData))
            //             {
            //                 SetupRelayHostedServerAndConnect(serverData.data, clientData.data, serverData.joinCode);
            //                 EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<RequestToHostRelayServer>());
            //             }
            //         }
            //         break;
            //     }
            //     case ConnectionState.SetupClient:
            //     {
            //         if (SystemAPI.HasSingleton<JoinCode>())
            //         {
            //             var joinCode = SystemAPI.GetSingleton<JoinCode>();
            //
            //             var enteredJoinCode = !joinCode.value.IsEmpty;
            //             if (enteredJoinCode)
            //             {
            //                 Debug.Log($"Join Code found: {joinCode.value} and join as client");
            //
            //                 
            //                 m_State = ConnectionState.JoinGame;
            //                 goto case ConnectionState.JoinGame;
            //             }
            //         }
            //
            //
            //         break;
            //     }
            //     case ConnectionState.JoinGame:
            //     {
            //         // Debug.Log($"Join Game {m_HostClientSystem?.RelayClientData.Endpoint.IsValid}");
            //         var hasClientConnectedToRelayService = m_HostClientSystem?.RelayClientData.Endpoint.IsValid;
            //         if (hasClientConnectedToRelayService.GetValueOrDefault())
            //         {
            //             ConnectToRelayServer();
            //             m_State = ConnectionState.Unknown;
            //         }
            //
            //         break;
            //     }
            //     case ConnectionState.JoinLocalGame:
            //     {
            //         var hasClientConnectedToRelayService = m_HostClientSystem?.RelayClientData.Endpoint.IsValid;
            //         if (hasClientConnectedToRelayService.GetValueOrDefault())
            //         {
            //             Debug.Log("Join Local Game");
            //             m_State = SetupRelayHostedServerAndConnect();
            //             // m_State = ConnectionState.Unknown;
            //         }
            //
            //         break;
            //     }
            //     case ConnectionState.Unknown:
            //     default: return;
            // }
            //
            // state.connectionState = m_State;
        }

        private void HandleInput()
        {
            ref var state = ref SystemAPI.GetComponentRW<RelayConnectionState>(SystemHandle).ValueRW;
            if (state.connectionState != ConnectionState.Unknown) return;

            if (Input.GetKeyDown(KeyCode.H))
            {
                state.connectionState = ConnectionState.SetupHost;
            }
            else if (Input.GetKeyDown(KeyCode.C))
            {
                state.connectionState = ConnectionState.SetupClient;
            }
        }


        ConnectionState SetupRelayHostedServerAndConnect(RelayServerData relayServerData,
                                                         RelayServerData relayClientData, FixedString64Bytes joinCode)
        {
            Debug.Log("setting up realy host server and connect");
            if (ClientServerBootstrap.RequestedPlayType != ClientServerBootstrap.PlayType.ClientAndServer)
            {
                UnityEngine.Debug.LogError(
                    $"Creating client/server worlds is not allowed if playmode is set to {ClientServerBootstrap.RequestedPlayType}");
                return ConnectionState.Unknown;
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

            var relayStuff = SystemAPI.GetSingleton<EnableRelayServer>();
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
            SceneSystem.LoadSceneAsync(server.Unmanaged, relayStuff.sceneReference);
            var snapQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<NetworkSnapshotAck>();

            server.EntityManager.DestroyEntity(server.EntityManager.CreateEntityQuery(snapQuery));

            var client = ClientServerBootstrap.CreateClientWorld("ClientWorld");
            SceneSystem.LoadSceneAsync(client.Unmanaged, relayStuff.sceneReference);
            client.EntityManager.DestroyEntity(client.EntityManager.CreateEntityQuery(snapQuery));

            NetworkStreamReceiveSystem.DriverConstructor = oldConstructor;


            if (World.DefaultGameObjectInjectionWorld == null)
                World.DefaultGameObjectInjectionWorld = server;


            ref var joinCodeComponent = ref SystemAPI.GetSingletonRW<JoinCode>().ValueRW;
            joinCodeComponent.value = joinCode;

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

            return ConnectionState.Unknown;
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
            var relayStuff = SystemAPI.GetSingleton<EnableRelayServer>();

            var oldConstructor = NetworkStreamReceiveSystem.DriverConstructor;
            NetworkStreamReceiveSystem.DriverConstructor
                = new RelayDriverConstructor(new RelayServerData(), relayClientData);
            var client = ClientServerBootstrap.CreateClientWorld("ClientWorld");
            SceneSystem.LoadSceneAsync(client.Unmanaged, relayStuff.sceneReference);
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

        protected void DestroyLocalSimulationWorld()
        {
            foreach (var world in World.All)
            {
                if (world.Flags == WorldFlags.Game)
                {
                    world.Dispose();
                    break;
                }
            }
        }
    }
}