using Newtonsoft.Json;
using RestSharp;
using RiptideNetworking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum CommandName 
{
    FRIENDS,
    F,
    HELP,
    H,
    PARTY,
    P,
    PARTYADD,
    PA,
    PARTYLEADER,
    PL,
    PARTYQUIT,
    PQ,
    PARTYREMOVE,
    PR,
    IGNORE,
    IGNOREREMOVE,
    ROLL,
    WHISPER,
    W,
}

public class Chat : MonoBehaviour
{
    private static Chat _singleton;
    public static Chat Singleton
    {
        get => _singleton;
        private set
        {
            if (_singleton == null)
                _singleton = value;
            else if (_singleton != value)
            {
                Debug.Log($"{nameof(Chat)} instance already exists, destroying duplicate!");
                Destroy(value);
            }
        }
    }
    private void Awake()
    {
        Singleton = this;
    }



    static ushort playerId;
    static string fromAccountName;
    static string playerChatMessageString;
    static string messageType;

    public static string alertMessage;
    public static bool updateRequests = false;
    public static bool updateFriendsOrIgnore = false;

    string commandName;
    string subCommand;
    string subCommandAccountName;
    string fixedSubCommand;
    string commandText;
    string commandTextAccountName;
    string fixedCommandText;

    Player playerFound; // Client that sent the packet

     /// <summary>
     /// Handle outgoing Alert messages
     /// </summary>

    public void SendAlertMessage(ushort toClientId)
    {
        NetworkManager.Singleton.Server.Send(AddAlertData(Message.Create(MessageSendMode.reliable, ServerToClientId.SendAlertMessage)), toClientId);
    }
    private Message AddAlertData(Message message)
    {
        message.AddString(alertMessage);
        message.AddBool(updateRequests);
        message.AddBool(updateFriendsOrIgnore);
        return message;
    }

    /// <summary>
    /// Handle incoming chat messages
    /// </summary>

    // Server received a chat message
    [MessageHandler((ushort)ClientToServerId.SendChatMessage)]
    private static void HandleChatMessage(ushort fromClientId, Message message)
    {
        playerId = fromClientId;
        fromAccountName = message.GetString();
        playerChatMessageString = message.GetString();
        messageType = message.GetString();

        // Make sure client is connected, and set playerFound to that client
        var foundPlayer = Player.ClientList.TryGetValue(fromClientId, out Singleton.playerFound);
        if(foundPlayer)
        {
            Singleton.playerFound.ChatMessageString = playerChatMessageString;
            Singleton.CheckForCommands(Singleton.playerFound, playerChatMessageString);
        }
        Debug.Log($"Client ID: " + playerId.ToString() + "// Account Name: " + fromAccountName + "// Chat message: " + playerChatMessageString);
    }
    private void CheckForCommands(Player playerFound, string chatMessage)
    {
        var fromAccountName = playerFound.AccountName;

        if (chatMessage.Trim().StartsWith("/"))
        {
            int lastLocation = chatMessage.IndexOf("/");

            if (lastLocation >= 0)
            {
                chatMessage = chatMessage.Substring(lastLocation + 1);// message minus the "/"

                // Split the remaining message into 3 parts, at the first two " ".
                string[] words = chatMessage.Split(new string[] { " " }, 3, StringSplitOptions.None);

                // Set the string split to variables
                commandName = words[0].ToUpper();

                if (words.Length >= 2)
                {
                    subCommand = words[1];
                    if (subCommand != null && subCommand != "")
                    {
                        subCommandAccountName = char.ToUpper(subCommand[0]) + subCommand.Substring(1).ToLower();
                    }
                    fixedSubCommand = subCommand.ToUpper();
                }
                else
                {
                    subCommand = null;
                    subCommandAccountName = null;
                    fixedSubCommand = null;
                }


                if (words.Length >= 3)
                { 
                    commandText = words[2];
                    if (commandText != null && commandText != "")
                    {
                        commandTextAccountName = char.ToUpper(commandText[0]) + commandText.Substring(1).ToLower();
                    }
                    fixedCommandText = commandText.ToUpper();
 
                }
                else
                {
                    commandText = null;
                    commandTextAccountName = null;
                    fixedCommandText = null;
                }

                // Start checking for different commands


                if (commandName == CommandName.FRIENDS.ToString() || commandName == CommandName.F.ToString())
                {
                    DealWithFriends(fixedSubCommand);
                    return;
                }

                if (commandName == CommandName.IGNORE.ToString())
                {
                    AddIgnore(playerFound.AccountName, subCommandAccountName);
                    return;
                }

                if (commandName == CommandName.PARTY.ToString() || commandName == CommandName.P.ToString())
                {
                    DealWithPartyChat(subCommand, commandText);
                    return;
                }

                if (commandName == CommandName.PARTYADD.ToString() || commandName == CommandName.PA.ToString())
                {
                    ClientParties.Singleton.SendPartyInvite(playerFound.AccountName, subCommandAccountName);
                    return;
                }

                if (commandName == CommandName.PARTYLEADER.ToString() || commandName == CommandName.PL.ToString())
                {
                    var currentLeader = Player.ClientList.FirstOrDefault(x => x.Value.AccountName == playerFound.AccountName).Value;
                    var futureLeader = Player.ClientList.FirstOrDefault(x => x.Value.AccountName == subCommandAccountName).Value;

                    ClientParties.DealWithPassingLeader(currentLeader, futureLeader);
                    return;
                }

                if (commandName == CommandName.PARTYQUIT.ToString() || commandName == CommandName.PQ.ToString())
                {
                    var clientToRemove = Player.ClientList.FirstOrDefault(x => x.Key == playerId).Value;

                    ClientParties.DealWithRemoveFromParty(playerId, clientToRemove.AccountName, clientToRemove.PartyName, "LEAVE");
                    return;
                }         
                
                if (commandName == CommandName.PARTYREMOVE.ToString() || commandName == CommandName.PR.ToString())
                {
                    var partyOwner = Player.ClientList.FirstOrDefault(x => x.Key == playerId).Value;

                    ClientParties.DealWithRemoveFromParty(playerId, subCommandAccountName, partyOwner.PartyName, "REMOVE");
                    return;
                }

                if (commandName == CommandName.ROLL.ToString())
                {
                    DealWithRolls(subCommand);
                    return;
                }

                if (commandName == CommandName.WHISPER.ToString() || commandName == CommandName.W.ToString())
                {
                    DealWithWhispers(subCommand, commandText);
                    return;
                }
            }
            // Has command identifier but no active command          
            playerFound.ChatMessageString = " Command not found!";
            playerFound.ChatMessageType = "Server";
            SendWhisperMessage(playerFound.Id); // Send failed request only to sender
            return;
        }
        // Has content but no command identifier
        playerFound.ChatMessageType = "Global";
        SendChatMessage();// Send to global chat
        return;
    }
    public void DealWithFriends(string fixedSubCommand)
    {
        if (fixedSubCommand == "A" || fixedSubCommand == "ADD")
        {
            SendFriendRequest(playerFound.AccountName, commandTextAccountName);
        }
        if (fixedSubCommand == "R" || fixedSubCommand == "REMOVE")
        {
            RemoveFriend(playerFound.AccountName, commandTextAccountName);
        }
    }
    public static async void AddIgnore(string FirstAccountName, string SecondAccountName)
    {
        try
        {
            RestClient client = new RestClient(NetworkManager.apiUrl);
            RestRequest request = new RestRequest("AccountApi/AddIgnore", Method.Post);

            request.AddBody(new FriendsOrIgnoredModel
            {
                FirstAccountName = FirstAccountName,
                SecondAccountName = SecondAccountName,
                IsIgnored = true
            });
            RestResponse response = await client.ExecuteAsync(request);

            if (response.ErrorException != null)
            {
                const string errmsg = "Error retrieving reponse. Check inner details for more info.";
                var MyException = new ApplicationException(errmsg, response.ErrorException);
                throw MyException;
            }
            if (response.Content == "-1")
            {
            }
            if (response.Content == "1")
            {
                ushort fromClientId = Singleton.GetClientByAccountName(FirstAccountName);
                Requests.SendUpdateRequest(fromClientId);
            }
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    public void DealWithRolls(string subCommand)
    {
        System.Random random = new System.Random();//unity has a "Random" as well, so defined to system.Random
        int maxNumber = 100; //default to a 1-100 roll

        if (subCommand == null)
        {
            maxNumber = 100;
            var roll = random.Next(1, maxNumber + 1); //the generated number is *equal or above* the min(1) and *below* the max(101)
            playerFound.ChatMessageString = $"{Singleton.playerFound.AccountName} Rolled a {roll} out of {maxNumber}.";
            playerFound.ChatMessageType = "Server";
            SendChatMessage();
            return;
        }

        if (subCommand != null)
        {
            int.TryParse(subCommand, out maxNumber);
            maxNumber = maxNumber == 0 ? 100 : maxNumber;

            var roll = random.Next(1, maxNumber + 1); //the generated number is *equal or above* the min(1) and *below* the maxNumber
            playerFound.ChatMessageString = $"{Singleton.playerFound.AccountName} Rolled a {roll} out of {maxNumber}.";
            playerFound.ChatMessageType = "Server";
            SendChatMessage();
            return;
        }
    }
    public void DealWithWhispers(string subCommand, string commandText)
    {
        ushort playerToMessage = 0;
        playerToMessage = GetClientByAccountName(subCommandAccountName);//Find the player ID in the client dictionary

        //if (playerToMessage == 0)
        //{
        //    playerFound.ChatMessageString = "User not online.";
        //    playerFound.ChatMessageType = "Server";
        //    SendWhisperMessage(playerFound.Id);
        //    return;
        //}

        playerFound.ChatMessageType = "Whisper";
        playerFound.ChatMessageString = commandText;
        SendWhisperMessage(playerToMessage); // Send message to specified player

        playerFound.ChatMessageType = "ISentWhisper";
        playerFound.ChatMessageString = commandText;
        SendWhisperMessage(playerFound.Id); // Send copy of message to sender
        return;
    }

    public void DealWithPartyChat(string subCommand, string commandText)
    {
        var chatToMessage = ClientParties.PartyList[subCommand];

        if (chatToMessage != null)
        {
            foreach (var player in chatToMessage)
            {
                playerFound.ChatMessageType = "Party";
                playerFound.ChatMessageString = commandText;
                SendWhisperMessage(player.Id); // Send message to specific player in chat
            }
        }
    }

    /// <summary>
    /// Handle outgoing chat messages
    /// </summary>

    // Send message to everyone
    public void SendChatMessage()
    {
        NetworkManager.Singleton.Server.SendToAll(AddChatData(Message.Create(MessageSendMode.reliable, ServerToClientId.SendChatMessage)));
    }

    // Send message to all but one
    public void SendChatMessage(ushort toClientId)
    {
        NetworkManager.Singleton.Server.SendToAll(AddChatData(Message.Create(MessageSendMode.reliable, ServerToClientId.SendChatMessage)), toClientId);
    }

    // Send message to a specific user
    public void SendWhisperMessage(ushort toClientId)
    {
        NetworkManager.Singleton.Server.Send(AddChatData(Message.Create(MessageSendMode.reliable, ServerToClientId.SendChatMessage)), toClientId);
    }
    private Message AddChatData(Message message)
    {
        message.AddUShort(Singleton.playerFound.Id);// not used client side yet

        // Adjust the account name if needed
        if (playerFound.ChatMessageType == "Server")
        {
            message.AddString("Server");// Set accountname to server instead of ourselves
        }
        else if (playerFound.ChatMessageType == "ISentWhisper")
        {
            message.AddString(subCommandAccountName);// So the sender knows what chat the message is from
        }                                       // Otherwise every sent message would combine into its own window
        else
        {
            message.AddString(Singleton.playerFound.AccountName);// Send accountname through like normal
        }

        message.AddString(Singleton.playerFound.ChatMessageString);
        message.AddString(Singleton.playerFound.ChatMessageType);
        return message;
    }

    /// <summary>
    /// Friend commands
    /// </summary>
    public async void SendFriendRequest(string fromAccount, string toAccount)
    {
        try
        {
            RestClient client = new RestClient(NetworkManager.apiUrl);
            RestRequest request = new RestRequest("AccountApi/SendRequest", Method.Post);

            request.AddBody(new RequestModel
            {
                RequestType = "FRIEND",
                FromAccountName = fromAccount,
                ToAccountName = toAccount
            });
            RestResponse response = await client.ExecuteAsync(request);

            Debug.Log(response.Content);

            if (response.ErrorException != null)
            {
                const string errmsg = "Error retrieving reponse. Check inner details for more info.";
                var MyException = new ApplicationException(errmsg, response.ErrorException);
                throw MyException;
            }
            if (response.Content == "-1")
            {
                alertMessage = $"Error sending friend request.";
                SendAlertMessage(playerFound.Id);
            }
            if (response.Content == "1")
            {
                ushort fromClientId = GetClientByAccountName(fromAccount);
                ushort toClientId = GetClientByAccountName(toAccount);

                Requests.SendUpdateRequest(fromClientId);
                Requests.SendUpdateRequest(toClientId);
            }
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }
    public async void RemoveFriend(string fromAccount, string toAccount)
    {
        try
        {
            RestClient client = new RestClient(NetworkManager.apiUrl);
            RestRequest request = new RestRequest("AccountApi/RemoveFriend", Method.Post);

            request.AddBody(new FriendsOrIgnoredModel
            {
                FirstAccountName = fromAccount,
                SecondAccountName = toAccount,
                IsFriend = true
            }) ;
            RestResponse response = await client.ExecuteAsync(request);

            Debug.Log(response.Content);

            if (response.ErrorException != null)
            {
                const string errmsg = "Error retrieving reponse. Check inner details for more info.";
                var MyException = new ApplicationException(errmsg, response.ErrorException);
                throw MyException;
            }
            if (response.Content == "-1")
            {

            }
            if (response.Content == "1")
            {
                ushort fromClientId = GetClientByAccountName(fromAccount);
                ushort toClientId = GetClientByAccountName(toAccount);

                Requests.SendUpdateRequest(fromClientId);
                Requests.SendUpdateRequest(toClientId);
            }
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }
    public ushort GetClientByAccountName(string databaseName)
    {
        //var player = Player.ClientList.FirstOrDefault(x => x.Value.AccountName.Contains(databaseName)).Key;
        var player = Player.ClientList.FirstOrDefault(x => x.Value.AccountName == databaseName).Key;
        return player;
    }
}

public class FriendsOrIgnoredModel
{
    public int? FirstAccountId { get; set; }
    public int? SecondAccountId { get; set; }
    public string? FirstAccountName { get; set; }
    public string? SecondAccountName { get; set; }
    public bool? IsIgnored { get; set; }
    public bool? IsFriend { get; set; }
}
public class RequestModel
{
#nullable enable
    public int? RequestId { get; set; }
    public int? FromAccountId { get; set; }
    public int? ToAccountId { get; set; }
    public string? FromAccountName { get; set; }
    public string? ToAccountName { get; set; }
    public string? RequestType { get; set; }
    public string? ClanName { get; set; }
    public string? PartyName { get; set; }

}