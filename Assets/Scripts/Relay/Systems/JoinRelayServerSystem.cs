using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Relay.Components;
using Unity.Entities;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace Samples.HelloNetcode
{
    /// <summary>
    /// Responsible for joining relay server using join code retrieved from <see cref="HostRelayServer"/>.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class JoinRelayServerSystem : SystemBase
    {
        Task<JoinAllocation> m_JoinTask;
        ClientStatus m_ClientStatus;
        NetworkEndpoint m_Endpoint;
        NetworkConnection m_ClientConnection;
        // public RelayServerData RelayClientData;


        [Flags]
        enum ClientStatus
        {
            Unknown,
            FailedToConnect,
            Ready,
            GetJoinCodeFromHost,
            WaitForJoin,
            WaitForInit,
        }

        protected override void OnCreate()
        {
            RequireForUpdate<EnableRelayServer>();
            RequireForUpdate<RequestToJoinRelayServer>();
            m_ClientStatus = ClientStatus.Unknown;
        }

        public void GetJoinCodeFromHost()
        {
            m_ClientStatus = ClientStatus.GetJoinCodeFromHost;
        }

        protected override void OnUpdate()
        {
            var request = SystemAPI.GetSingleton<RequestToJoinRelayServer>();

            if (m_JoinTask == null)
            {
                m_JoinTask = RelayService.Instance.JoinAllocationAsync(request.joinCode.Value);
                return;
            }

            if (m_JoinTask.IsCompleted)
            {
                if (m_JoinTask.IsFaulted)
                {
                    Debug.LogError("Join Relay request failed");
                    Debug.LogException(m_JoinTask.Exception);
                    EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<RequestToJoinRelayServer>());
                }

                if (m_JoinTask.IsCompleted)
                {
                    var allocation = m_JoinTask.Result;
                    try
                    {
                       var relayClientData = PlayerRelayData(allocation);
                       var relayClientDataEntity = EntityManager.CreateEntity(ComponentType.ReadOnly<RelayClientData>());
                       EntityManager.AddComponentData(relayClientDataEntity, new RelayClientData()
                       {
                           data = relayClientData,
                       });

                       var requestWorldCreationEntity = EntityManager.CreateEntity();
                       EntityManager.AddComponentData(requestWorldCreationEntity, new RequestWorldCreation());
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                   
                }
            }
            //
            // switch (m_ClientStatus)
            // {
            //     case ClientStatus.Ready:
            //     {
            //         Debug.Log("Success");
            //
            //         m_ClientStatus = ClientStatus.Unknown;
            //         return;
            //     }
            //     case ClientStatus.FailedToConnect:
            //     {
            //         Debug.Log("Failed, check console");
            //         m_ClientStatus = ClientStatus.Unknown;
            //         return;
            //     }
            //     case ClientStatus.GetJoinCodeFromHost:
            //     {
            //         // Debug.Log("Waiting for join code from host server");
            //         
            //         var hostServer = World.GetExistingSystemManaged<HostRelayServer>();
            //         m_ClientStatus = JoinUsingJoinCode(hostServer.JoinCode, out m_JoinTask);
            //         return;
            //     }
            //     case ClientStatus.WaitForJoin:
            //     {
            //         // Debug.Log("Binding to relay server");
            //
            //         m_ClientStatus = WaitForJoin(m_JoinTask, out RelayClientData);
            //         return;
            //     }
            //     case ClientStatus.Unknown:
            //     default:
            //         break;
            // }
        }
        //
        // static ClientStatus WaitForJoin(Task<JoinAllocation> joinTask, out RelayServerData relayClientData)
        // {
        //     if (!joinTask.IsCompleted)
        //     {
        //         relayClientData = default;
        //         return ClientStatus.WaitForJoin;
        //     }
        //
        //     if (joinTask.IsFaulted)
        //     {
        //         relayClientData = default;
        //         Debug.LogError("Join Relay request failed");
        //         Debug.LogException(joinTask.Exception);
        //         return ClientStatus.FailedToConnect;
        //     }
        //     Debug.Log("Binding to relay");
        //     return BindToRelay(joinTask, out relayClientData);
        // }
        //
        // static ClientStatus BindToRelay(Task<JoinAllocation> joinTask, out RelayServerData relayClientData)
        // {
        //     // Collect and convert the Relay data from the join response
        //     var allocation = joinTask.Result;
        //
        //     // Format the server data, based on desired connectionType
        //     try
        //     {
        //         relayClientData = PlayerRelayData(allocation);
        //     }
        //     catch (Exception e)
        //     {
        //         Debug.LogException(e);
        //         relayClientData = default;
        //         return ClientStatus.FailedToConnect;
        //     }
        //
        //     return ClientStatus.Ready;
        // }
        //
        // static ClientStatus JoinUsingJoinCode(string hostServerJoinCode, out Task<JoinAllocation> joinTask)
        // {
        //     if (hostServerJoinCode == null)
        //     {
        //         joinTask = null;
        //         return ClientStatus.GetJoinCodeFromHost;
        //     }
        //     Debug.Log($"Connecting Player tries to join with code: {hostServerJoinCode}");
        //     // Send the join request to the Relay service
        //     joinTask = RelayService.Instance.JoinAllocationAsync(hostServerJoinCode);
        //     return ClientStatus.WaitForJoin;
        // }

        static RelayServerData PlayerRelayData(JoinAllocation allocation, string connectionType = "dtls")
        {
            // Select endpoint based on desired connectionType
            var endpoint = GetEndpointForConnectionType(allocation.ServerEndpoints, connectionType);
            if (endpoint == null)
            {
                throw new Exception($"endpoint for connectionType {connectionType} not found");
            }

            // Prepare the server endpoint using the Relay server IP and port
            var serverEndpoint = NetworkEndpoint.Parse(endpoint.Host, (ushort)endpoint.Port);

            // UTP uses pointers instead of managed arrays for performance reasons, so we use these helper functions to convert them
            var allocationIdBytes = RelayAllocationId.FromByteArray(allocation.AllocationIdBytes);
            var connectionData = RelayConnectionData.FromByteArray(allocation.ConnectionData);
            var hostConnectionData = RelayConnectionData.FromByteArray(allocation.HostConnectionData);
            var key = RelayHMACKey.FromByteArray(allocation.Key);

            // Prepare the Relay server data and compute the nonce values
            // A player joining the host passes its own connectionData as well as the host's
            var relayServerData = new RelayServerData(ref serverEndpoint, 0, ref allocationIdBytes, ref connectionData,
                ref hostConnectionData, ref key, connectionType == "dtls");

            return relayServerData;
        }
        
        public static RelayServerEndpoint GetEndpointForConnectionType(List<RelayServerEndpoint> endpoints, string connectionType)
        {
            return endpoints.FirstOrDefault(endpoint => endpoint.ConnectionType == connectionType);
        }
    }
}
