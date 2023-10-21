using System.Collections.Generic;
using Relay;
using TMPro;
using Unity.Entities;
using Unity.Services.Lobbies;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class UIManager: MonoBehaviour
    {
        public Button hostButton;
        public Button joinButtonPrefab;
        public HorizontalLayoutGroup buttonGroup;

        private List<Button> _buttons = new List<Button>();
        public static UIManager Instance { get; private set; }
        private void Awake() 
        { 
            // If there is an instance, and it's not me, delete myself.
    
            if (Instance != null && Instance != this) 
            { 
                Destroy(this); 
            } 
            else 
            { 
                Instance = this; 
            } 
        }

        private void Start()
        {
            hostButton.onClick.AddListener(() =>
            {
                World.All[0].GetExistingSystemManaged<RelayTestSystem>().Host();
            });
        }

        public void AddJoinButton(LobbyInfoElement lobbyInfoElement)
        {
            var joinButton = Instantiate(joinButtonPrefab, buttonGroup.transform, true).GetComponent<Button>();
            var buttonText = joinButton.GetComponentInChildren<TextMeshProUGUI>();
            buttonText.text = $"{_buttons.Count + 1}";
            _buttons.Add(joinButton);
            joinButton.onClick.AddListener(() =>
            {
                World.All[0].GetExistingSystemManaged<LobbySystem>().JoinLobby(lobbyInfoElement.lobbyId);
            });
        }

        public void ClearAllButtons()
        {
            foreach (var button in _buttons)
            {
                Destroy(button.gameObject);
            }
            
            _buttons.Clear();
        }
    }

}