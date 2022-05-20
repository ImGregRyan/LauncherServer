using RiptideNetworking;
using RiptideNetworking.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
public enum ServerToClientId : ushort
{
    SendPlayerSpawned = 1,
    SendChatMessage,
    SendAlertMessage,
    SendUpdateRequests,
    SendPartyInvite,
    SendPartyList,
}
public enum ClientToServerId : ushort
{
    SendAccountInfo = 1,
    SendChatMessage,
    SendRequestAnswer,
    SendPartyInviteAnswer,
    SendRemoveFromParty,
    SendDisconnectMe,
}


public class NetworkManager : MonoBehaviour
{
    private static NetworkManager _singleton;
    public static NetworkManager Singleton
    {
        get => _singleton;
        private set
        {
            if(_singleton == null)
                _singleton = value;
            else if (_singleton != value)
            {
                Debug.Log($"{nameof(NetworkManager)} instance already exists, destroying duplicate!");
                Destroy(value);
            }
        }
    }
    private void Awake()
    {
        Singleton = this;
    }

    public static string apiUrl = "https://eb00-173-56-224-38.ngrok.io/";
    public Server Server { get; private set; }

    [SerializeField] private ushort port;
    [SerializeField] private ushort maxClientCount;

    private void Start()
    {
        Application.targetFrameRate = 60;

        RiptideLogger.Initialize(Debug.Log, Debug.Log, Debug.LogWarning, Debug.LogError, false);

        Server = new Server();
        Server.Start(port, maxClientCount);
        Server.ClientDisconnected += PlayerDisconnected; // event that triggers on disconnect
    }

    private void FixedUpdate()
    {
        Server.Tick();
    }

    private void OnApplicationQuit()
    {
        Server.Stop();
    }

    private void PlayerDisconnected(object sender, ClientDisconnectedEventArgs e)
    {
        var disconnectedPlayer = Player.ClientList.FirstOrDefault(x => x.Key == e.Id).Value;

        if(disconnectedPlayer != null)
        {
            disconnectedPlayer.IsConnected = false;

            ClientParties.DealWithPartyDisconnect(e.Id);


            //int sleepTime = 30000; // in mills, 30 sec
            //await Task.Delay(sleepTime);

            Destroy(disconnectedPlayer.gameObject);
        }       
    }

}
