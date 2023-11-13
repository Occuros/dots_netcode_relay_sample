using System.Collections.Generic;
using System.Threading.Tasks;
using LobbyAndRelay.Components;
using Unity.Entities;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace LobbyAndRelay.Systems
{
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class RelayRegionGatheringSystem : SystemBase
    {
        private Task<List<Region>> _collectRegionsTask;

        protected override void OnCreate()
        {
            RequireForUpdate<UnityServiceInitialized>();
            RequireForUpdate<RequestRelayRegions>();
            var request = EntityManager.CreateEntity();
            EntityManager.AddComponentData(request, new RequestRelayRegions());
        }

        protected override void OnUpdate()
        {
            if (_collectRegionsTask == null)
            {
                _collectRegionsTask = RelayService.Instance.ListRegionsAsync();
                if (!SystemAPI.TryGetSingletonBuffer<RelayRegionElement>(out var regions, true))
                {
                    var regionsEntity = EntityManager.CreateEntity();
                    EntityManager.AddBuffer<RelayRegionElement>(regionsEntity);
                }
                return;
            }

            if (_collectRegionsTask.IsFaulted)
            {
                Debug.LogError("List regions request failed");
                Debug.LogException(_collectRegionsTask.Exception);
                EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<RequestRelayRegions>());
            }

            if (_collectRegionsTask.IsCompletedSuccessfully)
            {
                var regionList = _collectRegionsTask.Result;
                if (!SystemAPI.TryGetSingletonBuffer<RelayRegionElement>(out var regionsBuffer, false)) return;
                regionsBuffer.Clear();
                // pick a region from the list
                for (var i = 0; i < regionList.Count; i++)
                {
                    var r = regionList[i];
                    regionsBuffer.Add(new RelayRegionElement()
                    {
                        region = r.Id
                    });
                }
                EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<RequestRelayRegions>());
            }
        }
    }
}