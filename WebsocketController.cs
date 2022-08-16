using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using CHAT.DatabaseAccess;
using CHAT.Models;
using CHAT.Processor;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Newtonsoft.Json;

namespace CHAT.Controllers {
    public class WebsocketController : Controller {
        private readonly AllUsersWebsocketConnections _allUserCons;
        private readonly UserDatabaseAccess _userDatabaseAccess;

        public WebsocketController(AllUsersWebsocketConnections allUserCons,
            UserDatabaseAccess userDatabaseAccess) {
            _allUserCons = allUserCons;
            _userDatabaseAccess = userDatabaseAccess;
        }

        [HttpPost]
        public string GetConnectedUsers() {
            var allConnection = _allUserCons.GetAllUsersConnection();
            var email = User.Claims
                .FirstOrDefault(p => p.Type == ClaimTypes.Email)?.Value;

            var allOtherUsersWithoutMe = allConnection
                .Where(p => p.Email != email).ToList();

            var toSend = "";
            foreach (var user in allOtherUsersWithoutMe) {
                var userDetails = _userDatabaseAccess
                    .QueryOne(p => p.Email == user.Email);

                if (userDetails == null)
                    continue;

                toSend += $"<div class='col-12'>" +
                          $"<i class='fa fa-user-circle' aria-hidden='true'></i>&nbsp;" +
                          $"<a class='gh' href='#' " +
                          $"onclick='ChatUser(\"{user.Email}\")'>" +
                          $"{userDetails.FirstName} " +
                          $"{userDetails.Surname}</a></div>" +
                          $"<div class='separator'></div>";
            }

            return toSend;
        }

        
        public async Task Connect() {
            var email = User.Claims
                .FirstOrDefault(p => p.Type == ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email))
                return;

            if (HttpContext.WebSockets.IsWebSocketRequest) {
                var websocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                var newConnection = new WebsocketModel {
                    Email = email,
                    UserWebsocket = websocket
                };

                var allConnection = _allUserCons.GetAllUsersConnection();
                foreach (var user in allConnection) {
                    var toSend = new WebsocketTransaction {
                        Communication = SocketTransactionEnums.Connected,
                        Message = $"This user, {email} is Connected"
                    };
                    var toSendByte = Encoding.UTF8
                        .GetBytes(JsonConvert.SerializeObject(toSend));

                    await user.UserWebsocket.SendAsync(toSendByte,
                        WebSocketMessageType.Text,
                        WebSocketMessageFlags.EndOfMessage,
                        CancellationToken.None);
                }

                _allUserCons.AddNewConnection(newConnection);
                await WebSocketCommunication(newConnection);
            }
        }

        private async Task WebSocketCommunication(WebsocketModel conn) {
            var receiveByte = new byte[1024 * 10];
            while (true) {
                try {
                    var receivedConn = await conn.UserWebsocket
                        .ReceiveAsync(new ArraySegment<byte>(receiveByte),
                            CancellationToken.None);
                    var receivedText = Encoding.UTF8
                        .GetString(receiveByte);
                    var dataReceived = JsonConvert
                        .DeserializeObject<WebsocketTransaction>(receivedText);
                    if (dataReceived == default)
                        continue;

                    if (dataReceived.Communication ==
                        SocketTransactionEnums.Message) {
                        var receiver = _allUserCons
                            .GetSingleConnection(dataReceived.Receiver);
                        if (receiver == null)
                            continue;

                        if (receiver.UserWebsocket.State != 
                            WebSocketState.Open)
                            continue;

                        var toRelayMessage = Encoding.UTF8
                            .GetBytes(JsonConvert.SerializeObject(
                                dataReceived));
                        await receiver.UserWebsocket
                            .SendAsync(toRelayMessage,
                                WebSocketMessageType.Text,
                                WebSocketMessageFlags.EndOfMessage,
                                CancellationToken.None);
                    }


                }
                catch (WebSocketException ex) {
                    if (ex.WebSocketErrorCode != WebSocketError.Success) {
                        _allUserCons.RemoveUserConnection(conn.Email);
                        return;
                    }

                }
            }
        }
    }
}