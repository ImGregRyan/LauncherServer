using Newtonsoft.Json;
using RestSharp;
using RiptideNetworking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Requests : MonoBehaviour
{
    public static int RequestId;
    public static int FromAccountId;
    public static int ToAccountId;
    public static string FromAccountName;
    public static string ToAccountName;
    public static string RequestType;
    public static string ClanName;
    public static string PartyName;

    public static string RequestAnswer;

    [MessageHandler((ushort)ClientToServerId.SendRequestAnswer)]
    private static async void HandleRequestAnswer(ushort fromClientId, Message message)
    {
        RequestAnswer = message.GetString();
        RequestType = message.GetString();
        FromAccountName = message.GetString();
        ToAccountName = message.GetString();

        var fromClient = Player.ClientList.FirstOrDefault(x => x.Value.AccountName == FromAccountName).Key;
        var toClient = Player.ClientList.FirstOrDefault(x => x.Value.AccountName == ToAccountName).Key;
        var storedProcedure = "";

        if (RequestAnswer == "CANCEL")
        {
            storedProcedure = "AccountApi/CancelRequest";
        }
        if (RequestAnswer == "ACCEPT")
        {
            storedProcedure = "AccountApi/AcceptRequest";
        }
        if (RequestAnswer == "DECLINE")
        {
            storedProcedure = "AccountApi/DeclineRequest";
        }

        try
        {
            RestClient client = new RestClient(NetworkManager.apiUrl);
            RestRequest request = new RestRequest(storedProcedure, Method.Post);

            request.AddBody(new RequestModel
            {
                RequestType = RequestType,
                FromAccountName = FromAccountName,
                ToAccountName = ToAccountName
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

            }

            if (response.Content == "1")
            {
                SendUpdateRequest(fromClient);
                SendUpdateRequest(toClient);
            }
        }
        catch (Exception ex)
        {
            throw ex;
        }
        

    }

    public static void SendUpdateRequest(ushort toClientId)
    {
        Message message = Message.Create(MessageSendMode.reliable, ServerToClientId.SendUpdateRequests);
        message.AddBool(true);
        NetworkManager.Singleton.Server.Send((message), toClientId);
    }
}
