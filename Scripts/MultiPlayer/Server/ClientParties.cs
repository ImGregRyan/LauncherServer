using RiptideNetworking;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ClientParties : MonoBehaviour
{
    private static ClientParties _singleton;
    public static ClientParties Singleton
    {
        get => _singleton;
        private set
        {
            if (_singleton == null)
                _singleton = value;
            else if (_singleton != value)
            {
                Debug.Log($"{nameof(ClientParties)} instance already exsists, destroying this duplicate!");
                Destroy(value);
            }
        }
    }
    private void Awake()
    {
        Singleton = this;
    }

    public static Dictionary<string, List<Player>> PartyList = new Dictionary<string, List<Player>>();
    public static Dictionary<string, List<Player>> ClanList = new Dictionary<string, List<Player>>();

    [SerializeField] private static int maxPartySize = 5;

    public void SendPartyInvite(string fromAccountName, string toAccountName)
    {
        var fromClientId = Chat.Singleton.GetClientByAccountName(fromAccountName);
        var toClientId = Chat.Singleton.GetClientByAccountName(toAccountName);

        var senderClient = Player.ClientList.FirstOrDefault(x => x.Value.AccountName == fromAccountName).Value;
        var receivingClient = Player.ClientList.FirstOrDefault(x => x.Value.AccountName == toAccountName).Value;

        if (senderClient.IsPartyLeader == true)
        {

        }

        if (receivingClient.InAParty == false && receivingClient.PartyInvitePending == false)
        {
            if (senderClient.PartyName == "" || senderClient.PartyName == null)
            {
                System.Random random = new System.Random();
                var maxNumber = 10000;
                var roll = random.Next(1, maxNumber + 1).ToString();

                var foundChatName = PartyList.ContainsKey(roll);

                if (foundChatName == true)
                {
                    SendPartyInvite(fromAccountName, toAccountName);
                    return;
                }

                //PartyList.Add($"{senderClient.AccountName}", new List<Player>());
                PartyList.Add($"{roll}", new List<Player>());
                //senderClient.PartyName = $"{senderClient.AccountName}";
                senderClient.PartyName = $"{roll}";
                senderClient.IsPartyLeader = true;               
            }

            if (senderClient.IsPartyLeader == true && PartyList[senderClient.PartyName].Count <= maxPartySize)
            {
                Message message = Message.Create(MessageSendMode.reliable, ServerToClientId.SendPartyInvite);
                message.AddUShort(fromClientId);
                message.AddString(fromAccountName);
                message.AddString(senderClient.PartyName);

                NetworkManager.Singleton.Server.Send(message, toClientId);
                receivingClient.PartyInvitePending = true;
            }
        }
    }

    [MessageHandler((ushort)ClientToServerId.SendPartyInviteAnswer)]
    private static void DealWithPartyInviteAnswer(ushort fromClientId, Message message)
    {
        var senderClientId = message.GetUShort();
        var receiverAccountName = message.GetString();
        var partyName = message.GetString();
        var partyInviteAnswer = message.GetString();

        var receivingClient = Player.ClientList.FirstOrDefault(x => x.Key == fromClientId).Value;
        var senderClient = Player.ClientList.FirstOrDefault(x => x.Key == senderClientId).Value;

        if (partyInviteAnswer == "DECLINE")
        {
            // Declined invitation to a formed group
            if (PartyList[partyName].Count >= 2)
            {
                receivingClient.PartyInvitePending = false;
            }

            // Invitation declined and party never fully formed
            if (PartyList[partyName].Count == 0)
            {
                senderClient.PartyName = "";
                senderClient.IsPartyLeader = false;
                receivingClient.PartyInvitePending = false;
                PartyList.Remove(partyName);
            }
        }
        if (partyInviteAnswer == "ACCEPT")
        {
            // Party is already made, just adding the new client
            if (PartyList[partyName].Count >= 2)
            {
                PartyList[partyName].Add(receivingClient);
                receivingClient.InAParty = true;
                receivingClient.PartyName = partyName;
                receivingClient.PartyInvitePending = false;
            }

            // If its the first invite to a party, Add creator and other client
            if (PartyList[partyName].Count == 0)
            {
                PartyList[partyName].Add(senderClient);
                senderClient.InAParty = true;


                PartyList[partyName].Add(receivingClient);
                receivingClient.InAParty = true;
                receivingClient.PartyName = partyName;
                receivingClient.PartyInvitePending = false;
            }

            SendPartyList(partyName);
        }
    }


    [MessageHandler((ushort)ClientToServerId.SendRemoveFromParty)]
    public static void DealWithRemoveFromParty(ushort personInParty, Message message)
    {
        var targetToRemove = message.GetUShort(); // might be themselves if leaving party
        var partyName = message.GetString();
        var removeOrLeave = message.GetString();

        var receivingClient = Player.ClientList.FirstOrDefault(x => x.Key == personInParty).Value;
        var targetClient = Player.ClientList.FirstOrDefault(x => x.Key == targetToRemove).Value;

        // For the clien that left or was removed, let them know
        string emptyClientList = "";
        Message tempMessage = Message.Create(MessageSendMode.reliable, ServerToClientId.SendPartyList);
        tempMessage.AddString(emptyClientList);
        tempMessage.AddString(partyName);


        if (removeOrLeave == "REMOVE")
        {
            if (receivingClient.IsPartyLeader == true)
            {
                PartyList[partyName].Remove(targetClient);
                targetClient.InAParty = false;
                targetClient.IsPartyLeader = false;
                targetClient.PartyName = "";
                NetworkManager.Singleton.Server.Send(tempMessage, targetToRemove);
            }
        }
        if (removeOrLeave == "LEAVE")
        {
            // Pass party lead on leave
            if (receivingClient.IsPartyLeader == true && PartyList[partyName].Count >= 3)
            {
                Player[] players = (
                from directChild in PartyList[partyName]
                where directChild.GetComponent<Player>().IsPartyLeader != true
                select directChild).ToArray();

                System.Random randomIdiot = new System.Random();
                var newLeader = randomIdiot.Next(players.Length);
                players[newLeader].IsPartyLeader = true;
            }

            PartyList[partyName].Remove(receivingClient);
            receivingClient.InAParty = false;
            receivingClient.IsPartyLeader = false;
            receivingClient.PartyName = "";
            NetworkManager.Singleton.Server.Send(tempMessage, personInParty);
        }

        // If only one person left in party
        if (PartyList[partyName].Count == 1)
        {
            PartyList[partyName][0].InAParty = false;
            PartyList[partyName][0].IsPartyLeader = false;
            PartyList[partyName][0].PartyName = "";


            var lastPersonInParty = PartyList[partyName][0].Id;
            NetworkManager.Singleton.Server.Send(tempMessage, lastPersonInParty);

            PartyList.Remove(partyName);
        }

        SendPartyList(partyName);
    }

    public static void DealWithRemoveFromParty(ushort personInParty, string targetToRemove, string partyName, string removeOrLeave)
    {
        var receivingClient = Player.ClientList.FirstOrDefault(x => x.Key == personInParty).Value;
        var targetClient = Player.ClientList.FirstOrDefault(x => x.Value.AccountName == targetToRemove).Value;

        // For the clien that left or was removed, let them know
        string emptyClientList = "";
        Message tempMessage = Message.Create(MessageSendMode.reliable, ServerToClientId.SendPartyList);
        tempMessage.AddString(emptyClientList);
        tempMessage.AddString(partyName);


        if (removeOrLeave == "REMOVE")
        {
            if (receivingClient.IsPartyLeader == true)
            {
                if(targetClient != null)
                {
                    PartyList[partyName].Remove(targetClient);
                    targetClient.InAParty = false;
                    targetClient.IsPartyLeader = false;
                    targetClient.PartyName = "";
                    NetworkManager.Singleton.Server.Send(tempMessage, targetClient.Id);
                }
                else
                {
                    var thisAsshole = PartyList[partyName].FirstOrDefault(x => x.AccountName == targetToRemove);
                    PartyList[partyName].Remove(thisAsshole);
                }
            }
        }
        if (removeOrLeave == "LEAVE")
        {
            // Pass party lead on leave
            if(receivingClient.IsPartyLeader == true && PartyList[partyName].Count >= 3)
            {
                Player[] players = (
                from directChild in PartyList[partyName]
                where directChild.GetComponent<Player>().IsPartyLeader != true
                select directChild).ToArray();

                System.Random randomIdiot = new System.Random();
                var newLeader = randomIdiot.Next(players.Length);
                players[newLeader].IsPartyLeader = true;
            }

            PartyList[partyName].Remove(receivingClient);
            receivingClient.InAParty = false;
            receivingClient.IsPartyLeader = false;
            receivingClient.PartyName = "";
            NetworkManager.Singleton.Server.Send(tempMessage, personInParty);
        }

        // If only one person left in party
        if (PartyList[partyName].Count == 1)
        {
            PartyList[partyName][0].InAParty = false;
            PartyList[partyName][0].IsPartyLeader = false;
            PartyList[partyName][0].PartyName = "";


            var lastPersonInParty = PartyList[partyName][0].Id;
            NetworkManager.Singleton.Server.Send(tempMessage, lastPersonInParty);

            PartyList.Remove(partyName);
        }

        SendPartyList(partyName);
    }

    public static void DealWithPassingLeader(Player currentLeader, Player futureLeader)
    {
        if (currentLeader.IsPartyLeader == false)
        {
            return;
        }

        currentLeader.IsPartyLeader = false;
        futureLeader.IsPartyLeader = true;
        SendPartyList(futureLeader.PartyName);
    }

    public static void DealWithPartyDisconnect(ushort clientId)
    {
        var disconnectedPlayer = Player.ClientList.FirstOrDefault(x => x.Key == clientId).Value;
        string partyName = disconnectedPlayer.GetComponent<Player>().PartyName;
        bool someoneStillOnline = false;

        if (partyName != "" && partyName != null)
        {
            foreach (var client in PartyList[partyName])
            {
                if (client.IsConnected == true)
                {
                    someoneStillOnline = true;
                    continue;
                }
            }
            if (someoneStillOnline == true)
            {
                SendPartyList(disconnectedPlayer.GetComponent<Player>().PartyName);
            }

            if (someoneStillOnline == false)
            {
                PartyList.Remove(partyName);
            }
        }
    }

    public static void SendPartyList(string partyName)
    {
        if(partyName == null)
        {
            return;
        }

        if (PartyList.ContainsKey(partyName))
        {
            // For each client in this party
            foreach (var client in PartyList[partyName])
            {
                string clientList = "";

                // Send a list of the party clients
                foreach (var clients in PartyList[partyName])
                {
                    var partyLeader = clients.IsPartyLeader.ToString();
                    var connectionStatus = clients.IsConnected.ToString();

                    clientList += clients.AccountName.ToString() + "-" + partyLeader + "-" + connectionStatus + ",";
                }

                Message newMessage = Message.Create(MessageSendMode.reliable, ServerToClientId.SendPartyList);
                newMessage.AddString(clientList);
                newMessage.AddString(partyName);

                NetworkManager.Singleton.Server.Send(newMessage, client.Id);
            }
        }
    }
}