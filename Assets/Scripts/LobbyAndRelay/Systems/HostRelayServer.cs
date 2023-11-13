using System;
using System.Threading.Tasks;
using LobbyAndRelay.Components;
using Unity.Entities;
using UnityEngine;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;

namespace LobbyAndRelay.Systems
{
    /// <summary>
    /// Responsible for contacting relay server and setting up <see cref="_relayServerData"/> and <see cref="_joinCode"/>.
    /// Steps include:
    /// 1. Initializing services
    /// 2. Logging in
    /// 3. Allocating number of players that are allowed to join.
    /// 4. Retrieving join code
    /// 5. Getting relay server information. I.e. IP-address, etc.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class HostRelayServer : SystemBase
    {

        private string _joinCode;
        private HostStatus _hostStatus;
        private Task<Allocation> _allocationTask;
        private Task<string> _joinCodeTask;

        [Flags]
        enum HostStatus
        {
            Unknown,
            GettingRegions,
            Allocating,
            GettingJoinCode,
            GetRelayData,
            Ready,
            FailedToHost,
        }

        protected override void OnCreate()
        {
            RequireForUpdate<UnityServiceInitialized>();
            RequireForUpdate<RequestToHostRelayServer>();
            _hostStatus = HostStatus.GettingRegions;
        }

        protected override void OnUpdate()
        {
          
            var request = SystemAPI.GetSingleton<RequestToHostRelayServer>();
            if (_allocationTask == null)
            {
                if (SystemAPI.HasSingleton<RelayServerHostData>())
                {
                    EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<RelayServerHostData>());
                }

                _allocationTask = RelayService.Instance.CreateAllocationAsync(request.maxPeerConnections, request.region.IsEmpty? null: request.region.Value);
                _hostStatus = HostStatus.Allocating;
            }
            
            switch (_hostStatus)
            {

                case HostStatus.Allocating:
                {
                    // Debug.Log("Waiting for allocation");
                    _hostStatus = WaitForAllocations(_allocationTask, out _joinCodeTask);
                    return;
                }
                case HostStatus.GettingJoinCode:
                {
                    // Debug.Log("Waiting for join code");
                    _hostStatus = WaitForJoin(_joinCodeTask, out _joinCode);
                    return;
                }
                case HostStatus.GetRelayData:
                {
                    // Debug.Log("Getting relay data");
                    _hostStatus = BindToHost(_allocationTask, out var relayServerData);
                    if (_hostStatus == HostStatus.Ready)
                    {    var serverDataEntity = EntityManager.CreateEntity();
                        EntityManager.AddComponentData(serverDataEntity, new RelayServerHostData()
                        {
                            data = relayServerData,
                            joinCode = _joinCode
                        });

                        var connectClientRequest = EntityManager.CreateEntity();
                        EntityManager.AddComponentData(connectClientRequest, new RequestToJoinRelayServer()
                        {
                            joinCode = _joinCode
                        });
                    }
                    return;
                }
                case HostStatus.Ready:
                {
                    // Debug.Log("Success, players may now connect");
                    _hostStatus = HostStatus.Unknown;
                    EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<RequestToHostRelayServer>());
                    _allocationTask = null;
                    _joinCodeTask = null;
                    return;
                }
                case HostStatus.FailedToHost:
                {
                    // Debug.Log("Failed check console");
                    _hostStatus = HostStatus.Unknown;
                    return;
                }
                case HostStatus.Unknown:
                default:
                    break;
            }
        }


        // Bind and listen to the Relay server
        static HostStatus BindToHost(Task<Allocation> allocationTask, out RelayServerData relayServerData)
        {
            var allocation = allocationTask.Result;
            try
            {
                // Format the server data, based on desired connectionType
                relayServerData = HostRelayData(allocation);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                relayServerData = default;
                return HostStatus.FailedToHost;
            }

            return HostStatus.Ready;
        }

        // Get the Join Code, you can then share it with the clients so they can join
        static HostStatus WaitForJoin(Task<string> joinCodeTask, out string joinCode)
        {
            joinCode = null;
            if (!joinCodeTask.IsCompleted)
            {
                return HostStatus.GettingJoinCode;
            }

            if (joinCodeTask.IsFaulted)
            {
                Debug.LogError("Create join code request failed");
                Debug.LogException(joinCodeTask.Exception);
                return HostStatus.FailedToHost;
            }

            Debug.Log("Waiting for join code");
            joinCode = joinCodeTask.Result;
            Debug.Log($"Received join code {joinCode}");
            return HostStatus.GetRelayData;
        }

        static HostStatus WaitForAllocations(Task<Allocation> allocationTask, out Task<string> joinCodeTask)
        {
            if (!allocationTask.IsCompleted)
            {
                joinCodeTask = null;
                return HostStatus.Allocating;
            }

            if (allocationTask.IsFaulted)
            {
                Debug.LogError("Create allocation request failed");
                Debug.LogException(allocationTask.Exception);
                joinCodeTask = null;
                return HostStatus.FailedToHost;
            }

            Debug.Log("Waiting for allocation");
            // Request the join code to the Relay service
            var allocation = allocationTask.Result;
            joinCodeTask = RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            return HostStatus.GettingJoinCode;
        }
        

        // connectionType also supports udp, but this is not recommended
        static RelayServerData HostRelayData(Allocation allocation, string connectionType = "dtls")
        {
            // Select endpoint based on desired connectionType
            var endpoint = RelayUtilities.GetEndpointForConnectionType(allocation.ServerEndpoints, connectionType);
            if (endpoint == null)
            {
                throw new InvalidOperationException($"endpoint for connectionType {connectionType} not found");
            }

            // Prepare the server endpoint using the Relay server IP and port
            var serverEndpoint = NetworkEndpoint.Parse(endpoint.Host, (ushort)endpoint.Port);

            // UTP uses pointers instead of managed arrays for performance reasons, so we use these helper functions to convert them
            var allocationIdBytes = RelayAllocationId.FromByteArray(allocation.AllocationIdBytes);
            var connectionData = RelayConnectionData.FromByteArray(allocation.ConnectionData);
            var key = RelayHMACKey.FromByteArray(allocation.Key);

            // Prepare the Relay server data and compute the nonce value
            // The host passes its connectionData twice into this function
            var relayServerData = new RelayServerData(ref serverEndpoint, 0, ref allocationIdBytes, ref connectionData,
                ref connectionData, ref key, connectionType == "dtls");

            return relayServerData;
        }
    }
}