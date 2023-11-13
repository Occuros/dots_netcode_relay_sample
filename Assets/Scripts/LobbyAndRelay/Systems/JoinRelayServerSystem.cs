using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LobbyAndRelay.Components;
using Unity.Entities;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace LobbyAndRelay.Systems
{
    /// <summary>
    /// Responsible for joining relay server using join code retrieved from <see cref="HostRelayServer"/>.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class JoinRelayServerSystem : SystemBase
    {
        private Task<JoinAllocation> _joinTask;
        
        protected override void OnCreate()
        {
            RequireForUpdate<RequestToJoinRelayServer>();
        }
        

        protected override void OnUpdate()
        {
            var request = SystemAPI.GetSingleton<RequestToJoinRelayServer>();

            if (_joinTask == null)
            {
                _joinTask = RelayService.Instance.JoinAllocationAsync(request.joinCode.Value);
                return;
            }

            if (_joinTask.IsCompleted)
            {
                if (_joinTask.IsFaulted)
                {
                    Debug.LogError("Join Relay request failed");
                    Debug.LogException(_joinTask.Exception);
                    EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<RequestToJoinRelayServer>());
                }

                if (_joinTask.IsCompleted)
                {
                    var allocation = _joinTask.Result;
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
        }
      
        private static RelayServerData PlayerRelayData(JoinAllocation allocation, string connectionType = "dtls")
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
        
        private static RelayServerEndpoint GetEndpointForConnectionType(List<RelayServerEndpoint> endpoints, string connectionType)
        {
            return endpoints.FirstOrDefault(endpoint => endpoint.ConnectionType == connectionType);
        }
    }
}
