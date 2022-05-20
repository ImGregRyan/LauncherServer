using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RiptideNetworking;
using System;
using System.Linq;

public class Player : MonoBehaviour
{
    public static Dictionary<ushort, Player> ClientList = new Dictionary<ushort, Player>();

    public ushort Id { get; private set; }
    public int DatabaseId { get; private set; }
    public string AccountName { get; private set; }
    public string ChatMessageString { get; set; }
    public string ChatMessageType { get; set; }
    public bool IsConnected { get; set; }


    public string PartyName { get; set; }

    public bool IsPartyLeader = false;
    public bool InAParty { get; set; }
    public bool PartyInvitePending { get; set; }


    public string ClanName { get; set; }
    public bool IsClanLeader { get; set; }


    private void OnDestroy()
    {
        ClientList.Remove(Id);       
    }

    #region Messages

    [MessageHandler((ushort)ClientToServerId.SendAccountInfo)]
    private static void AccountInfo(ushort fromClientId, Message message)
    {
        var databaseId = message.GetInt();
        var accountName = message.GetString();

        //check if this account is already online
        foreach (KeyValuePair<ushort, Player> client in ClientList)
        {
            // If a second clients tries to login to an account already connected
            if (client.Value.AccountName == accountName && client.Value.IsConnected == true)
            {
                //make server message packet, send message saying that account is alreayd logged in, and disconnect the attempting connection
                NetworkManager.Singleton.Server.DisconnectClient(fromClientId);
                //send a message to the user logged in to let them know
                return;
            }
        }


        Spawn(fromClientId, databaseId, accountName);
    }
    public static void Spawn(ushort id, int databaseId, string accountName)
    {
        //Get list of clients already on before we get added to the list
        foreach (Player otherPlayer in ClientList.Values)
            otherPlayer.SendSpawned(id);

        //Spawn in a new client
        Player player = Instantiate(GameLogic.Singleton.PlayerPrefab, new Vector3(0f,1f,0f), Quaternion.identity).GetComponent<Player>();
        player.name = $"Player {id} ({accountName})";
        player.Id = id;
        player.DatabaseId = databaseId;
        player.AccountName = accountName;
        player.IsConnected = true;
        player.SendSpawned();
        ClientList.Add(id, player);

        FindActiveParty(player);
        SendServerWelcomeMessage(id);
    }
    private void SendSpawned()
    {
        NetworkManager.Singleton.Server.SendToAll(AddSpawnData(Message.Create(MessageSendMode.reliable, ServerToClientId.SendPlayerSpawned)));
    }
    private void SendSpawned(ushort toClientId)
    {
        NetworkManager.Singleton.Server.Send(AddSpawnData(Message.Create(MessageSendMode.reliable, ServerToClientId.SendPlayerSpawned)), toClientId);
    }
    private Message AddSpawnData(Message message)
    {
        message.AddUShort(Id);
        message.AddString(AccountName);
        message.AddBool(IsConnected);
        //message.AddBool(true);
        message.AddVector3(transform.position);
        return message;
    }
    private static void SendServerWelcomeMessage(ushort toClientId)
    {
        Message message = Message.Create(MessageSendMode.reliable, ServerToClientId.SendChatMessage);
        message.AddUShort(toClientId);
        message.AddString("Server");
        message.AddString
            (
            $"There are currently {Player.ClientList.Count} player(s) online." +
            $" \n Type /Help for a list of commands."
            );

        message.AddString("Server");
        NetworkManager.Singleton.Server.Send(message, toClientId);
    }
    #endregion

    private static void FindActiveParty(Player playerToCheckFor)
    {
        foreach (var Parties in ClientParties.PartyList)
        {
            foreach (var player in Parties.Value)
            {
                if(player.AccountName == playerToCheckFor.AccountName)
                {
                    playerToCheckFor.PartyName = player.PartyName;
                    playerToCheckFor.IsPartyLeader = player.IsPartyLeader;
                    playerToCheckFor.InAParty = player.InAParty;
                    playerToCheckFor.PartyInvitePending = player.PartyInvitePending;

                    Parties.Value.Remove(player);
                    Parties.Value.Add(playerToCheckFor);

                    ClientParties.SendPartyList(player.PartyName);
                    return;
                }
            }
        }
    }
}
