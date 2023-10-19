using Samples.HelloNetcode;
using TMPro;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Scenes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Relay
{
    public partial class RelayTestSystem : SystemBase
    {
        private ConnectingPlayer m_HostClientSystem;
        private HostServer m_HostServerSystem;

        private TMP_InputField _tmpInputField;

        private bool _initialized = false;
        // private ConnectionState m_State;

        public struct RelayConnectionState : IComponentData
        {
            public ConnectionState connectionState;
        }

        public enum ConnectionState
        {
            Unknown,
            SetupHost,
            SetupClient,
            JoinGame,
            JoinLocalGame,
        }

        protected override void OnCreate()
        {
            // m_State = ConnectionState.Unknown;
            EntityManager.AddComponent<RelayConnectionState>(this.SystemHandle);
        }

        protected override void OnStartRunning()
        {
            _tmpInputField = GameObject.FindGameObjectWithTag("RelayJoinCode").GetComponent<TMP_InputField>();
            _initialized = true;
        }

        protected override void OnUpdate()
        {
            HandleInput();

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
                    Debug.Log("Join Game");
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
                    Debug.Log("Join Local Game");
                    var hasClientConnectedToRelayService = m_HostClientSystem?.RelayClientData.Endpoint.IsValid;
                    if (hasClientConnectedToRelayService.GetValueOrDefault())
                    {
                        SetupRelayHostedServerAndConnect();
                        m_State = ConnectionState.Unknown;
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
            if (!_initialized) return;
            if (!SystemAPI.HasSingleton<JoinCode>()) return;
            ref var state = ref SystemAPI.GetComponentRW<RelayConnectionState>(SystemHandle).ValueRW;
            if (state.connectionState != ConnectionState.Unknown) return;
            ref var joinCode = ref SystemAPI.GetSingletonRW<JoinCode>().ValueRW;
            if (_tmpInputField != null)
            {
                joinCode.value = _tmpInputField.text;
            }
            if (Input.GetKeyDown(KeyCode.H))
            {
                state.connectionState = ConnectionState.SetupHost;
            }
            else if (Input.GetKeyDown(KeyCode.C))
            {
                state.connectionState = ConnectionState.SetupClient;
            }
     
        
        }

        void JoinAsClient()
        {
            SetupClient();
            var joinCode = SystemAPI.GetSingleton<JoinCode>();
            Debug.Log($"we join using code: {joinCode.value}");
            m_HostClientSystem.JoinUsingCode(joinCode.value.Value);
        }


        void SetupRelayHostedServerAndConnect()
        {
            Debug.Log("setting up realy host server and connect");
            if (ClientServerBootstrap.RequestedPlayType != ClientServerBootstrap.PlayType.ClientAndServer)
            {
                UnityEngine.Debug.LogError(
                    $"Creating client/server worlds is not allowed if playmode is set to {ClientServerBootstrap.RequestedPlayType}");
                return;
            }

            var world = World.All[0];
            var relayStuff = SystemAPI.GetSingleton<EnableRelayServer>();
            var relayClientData = world.GetExistingSystemManaged<ConnectingPlayer>().RelayClientData;
            var relayServerData = world.GetExistingSystemManaged<HostServer>().RelayServerData;
            var joinCode = world.GetExistingSystemManaged<HostServer>().JoinCode;

            var oldConstructor = NetworkStreamReceiveSystem.DriverConstructor;
            NetworkStreamReceiveSystem.DriverConstructor = new RelayDriverConstructor(relayServerData, relayClientData);

            var server = ClientServerBootstrap.CreateServerWorld("ServerWorld");
            SceneSystem.LoadSceneAsync(server.Unmanaged, relayStuff.sceneReference);
            var client = ClientServerBootstrap.CreateClientWorld("ClientWorld");
            SceneSystem.LoadSceneAsync(client.Unmanaged, relayStuff.sceneReference);

            NetworkStreamReceiveSystem.DriverConstructor = oldConstructor;


            if (World.DefaultGameObjectInjectionWorld == null)
                World.DefaultGameObjectInjectionWorld = server;


            // var joinCodeEntity = server.EntityManager.CreateEntity(ComponentType.ReadOnly<JoinCode>());
            ref var joinCodeComponent = ref SystemAPI.GetSingletonRW<JoinCode>().ValueRW;
            // server.EntityManager.SetComponentData(joinCodeEntity, new JoinCode { value = joinCode });
            joinCodeComponent.value = joinCode;
            _tmpInputField.text = joinCode;

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