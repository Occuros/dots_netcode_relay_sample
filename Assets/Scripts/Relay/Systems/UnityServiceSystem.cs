using System.Threading.Tasks;
using Unity.Entities;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace Relay.Systems
{
    public partial class UnityServiceSystem : SystemBase
    {
        private Task _initializeTask;
        private Task _signInTask;

        protected override void OnCreate()
        {
            _initializeTask = UnityServices.InitializeAsync();
        }

        protected override void OnUpdate()
        {
            if (_initializeTask != null)
            {
                if (_initializeTask.IsCompletedSuccessfully)
                {
                    _initializeTask = null;

                    if (!AuthenticationService.Instance.IsSignedIn)
                    {
                        _signInTask = AuthenticationService.Instance.SignInAnonymouslyAsync();
                    }
                    else
                    {
                        var e = EntityManager.CreateEntity();
                        EntityManager.AddComponent<UnityServiceInitialized>(e);
                    }
                }
                else if (_initializeTask.IsFaulted)
                {
                    Debug.LogError(
                        $"Unity services could not be initialized {_initializeTask.Status} => {_initializeTask.Exception}");
                }
            }

            if (_signInTask != null)
            {
                if (_signInTask.IsCompletedSuccessfully)
                {
                    var e = EntityManager.CreateEntity();
                    EntityManager.AddComponent<UnityServiceInitialized>(e);
                    _signInTask = null;
                }
                else if (_signInTask.IsFaulted)
                {
                    Debug.LogError(
                        $"Unity services could not be initialized {_signInTask.Status} => {_signInTask.Exception}");
                }
            }
        }
    }
}