using System.Collections.Generic;
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
        private ConnectingPlayer m_HostClientSystem;
        private HostServer m_HostServerSystem;

        private float waitingTime = 0.0f;

        public struct RelayConnectionState : IComponentData
        {
            public ConnectionState connectionState;
        }

        public enum ConnectionState
        {
            Unknown,
            SetupHost,
            SetupClient,
            DestroyExistingWorlds,
            JoinGame,
            JoinLocalGame,
        }

        protected override void OnCreate()
        {
            // m_State = ConnectionState.Unknown;
            EntityManager.AddComponent<RelayConnectionState>(this.SystemHandle);
        }


        protected override void OnUpdate()
        {
            HandleInput();


            if (SystemAPI.TryGetSingleton<RequestClientRelayWithJoinCode>(out var request))
            {
                JoinWithCode(request.joinCode);
                EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<RequestClientRelayWithJoinCode>());
                return;
            }

            ref var state = ref SystemAPI.GetComponentRW<RelayConnectionState>(SystemHandle).ValueRW;
            var m_State = state.connectionState;


            switch (m_State)
            {
                case ConnectionState.SetupHost:
                {
                    Debug.Log("Setting up Host");
                    HostServer();
                    m_State = ConnectionState.SetupClient;
                    goto case ConnectionState.SetupClient;
                }
                case ConnectionState.SetupClient:
                {
                    var isServerHostedLocally = m_HostServerSystem?.RelayServerData.Endpoint.IsValid;
                    if (isServerHostedLocally.GetValueOrDefault())
                    {
                        Debug.Log("Setting up Client");
                        SetupClient();
                        m_HostClientSystem.GetJoinCodeFromHost();
                        m_State = ConnectionState.JoinLocalGame;
                        goto case ConnectionState.JoinLocalGame;
                    }

                    if (SystemAPI.HasSingleton<JoinCode>())
                    {
                        var joinCode = SystemAPI.GetSingleton<JoinCode>();

                        var enteredJoinCode = !joinCode.value.IsEmpty;
                        if (enteredJoinCode)
                        {
                            Debug.Log($"Join Code found: {joinCode.value} and join as client");

                            JoinAsClient();
                            m_State = ConnectionState.JoinGame;
                            goto case ConnectionState.JoinGame;
                        }
                    }


                    break;
                }
                case ConnectionState.JoinGame:
                {
                    // Debug.Log($"Join Game {m_HostClientSystem?.RelayClientData.Endpoint.IsValid}");
                    var hasClientConnectedToRelayService = m_HostClientSystem?.RelayClientData.Endpoint.IsValid;
                    if (hasClientConnectedToRelayService.GetValueOrDefault())
                    {
                        ConnectToRelayServer();
                        m_State = ConnectionState.Unknown;
                    }

                    break;
                }
                case ConnectionState.JoinLocalGame:
                {
                    var hasClientConnectedToRelayService = m_HostClientSystem?.RelayClientData.Endpoint.IsValid;
                    if (hasClientConnectedToRelayService.GetValueOrDefault())
                    {
                        Debug.Log("Join Local Game");
                        m_State = SetupRelayHostedServerAndConnect();
                        // m_State = ConnectionState.Unknown;
                    }

                    break;
                }
                case ConnectionState.Unknown:
                default: return;
            }

            state.connectionState = m_State;
        }

        private void HandleInput()
        {
            if (!SystemAPI.HasSingleton<JoinCode>()) return;
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

        public void Host()
        {
            ref var state = ref SystemAPI.GetComponentRW<RelayConnectionState>(SystemHandle).ValueRW;
            state.connectionState = ConnectionState.SetupHost;
        }


        public void JoinWithCode(FixedString64Bytes code)
        {
            Debug.Log($"We are joining as client with code {code}");
            ref var joinCode = ref SystemAPI.GetSingletonRW<JoinCode>().ValueRW;
            joinCode.value = code;
            ref var state = ref SystemAPI.GetComponentRW<RelayConnectionState>(SystemHandle).ValueRW;
            
            JoinAsClient();
            state.connectionState = ConnectionState.JoinGame;
        }

        void JoinAsClient()
        {
            SetupClient();
            var joinCode = SystemAPI.GetSingleton<JoinCode>();
            Debug.Log($"we join using code: {joinCode.value}");
            m_HostClientSystem.JoinUsingCode(joinCode.value.Value);
        }


        ConnectionState SetupRelayHostedServerAndConnect()
        {
            Debug.Log("setting up realy host server and connect");
            if (ClientServerBootstrap.RequestedPlayType != ClientServerBootstrap.PlayType.ClientAndServer)
            {
                UnityEngine.Debug.LogError(
                    $"Creating client/server worlds is not allowed if playmode is set to {ClientServerBootstrap.RequestedPlayType}");
                return ConnectionState.Unknown;
            }
            var world = World.All[0];

            // var clientTicket = 0;
            // var serverTicket = 0;
            // var migrationSystem = world.GetOrCreateSystemManaged<DriverMigrationSystem>();
            for (int i = World.All.Count - 1; i >= 0; i--)
            {
                var previousWorld = World.All[i];
                if (previousWorld.IsClient() || previousWorld.IsServer())
                {
                    Debug.Log($"We dispose of world {previousWorld.Name}");
                    // if (previousWorld.IsClient())
                    // {
                    //     // clientTicket =  migrationSystem.StoreWorld(previousWorld);
                    // }
                    // else
                    // {
                    //     serverTicket = migrationSystem.StoreWorld(previousWorld);
                    // }
                    previousWorld.Dispose();
                }
            }


            var relayStuff = SystemAPI.GetSingleton<EnableRelayServer>();
            var relayClientData = world.GetExistingSystemManaged<ConnectingPlayer>().RelayClientData;
            var relayServerData = world.GetExistingSystemManaged<HostServer>().RelayServerData;
            var joinCode = world.GetExistingSystemManaged<HostServer>().JoinCode;


            var createLobbyEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(createLobbyEntity, new CreateLobbyRequest()
            {
                joinCode = joinCode,
                lobbyName = $"CubeTest_{Random.Range(0, 10_000)}",
                maxPlayers = 4
            });

            var oldConstructor = NetworkStreamReceiveSystem.DriverConstructor;
            NetworkStreamReceiveSystem.DriverConstructor = new RelayDriverConstructor(relayServerData, relayClientData);

            var server = ClientServerBootstrap.CreateServerWorld("ServerWorld");
            // var server = migrationSystem.LoadWorld(serverTicket);

            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.ServerSimulation);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(server, systems);
            #if UNITY_DOTSRUNTIME
            AppendWorldToServerTickWorld(server);
            #else
            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(server);
            #endif
            


     
            SceneSystem.LoadSceneAsync(server.Unmanaged, relayStuff.sceneReference);
            var client = ClientServerBootstrap.CreateClientWorld("ClientWorld");
            // var client = migrationSystem.LoadWorld(clientTicket);
            SceneSystem.LoadSceneAsync(client.Unmanaged, relayStuff.sceneReference);
          
            
            
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

        void SetupClient()
        {
            var world = World.All[0];
            m_HostClientSystem = world.GetOrCreateSystemManaged<ConnectingPlayer>();
            var simGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(m_HostClientSystem);
        }

        void HostServer()
        {
            var world = World.All[0];
            m_HostServerSystem = world.GetOrCreateSystemManaged<HostServer>();
            var simGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(m_HostServerSystem);
        }

        void ConnectToRelayServer()
        {
            var world = World.All[0];
            var relayClientData = world.GetExistingSystemManaged<ConnectingPlayer>().RelayClientData;
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