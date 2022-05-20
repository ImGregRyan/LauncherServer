using RiptideNetworking;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DisconnectClient : MonoBehaviour
{
    public Button disconnectButton;
    public TMP_InputField clientInputField;

    public void DisconnectAClient()
    {
        var fuckThisGuy = Chat.Singleton.GetClientByAccountName(clientInputField.text);
        NetworkManager.Singleton.Server.DisconnectClient(fuckThisGuy);
    }

    [MessageHandler((ushort)ClientToServerId.SendDisconnectMe)]
    private static void DisconnectThisGuy(ushort fromClientId, Message message)
    {
        var something = message.GetString();
        NetworkManager.Singleton.Server.DisconnectClient(fromClientId);
    }
}
