using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.Web.WebSockets;
using System.Web.Helpers;

namespace WinCast.App_Code.SocketHandlers
{
    public class Viewer : WebSocketHandler
    {
        private static WebSocketCollection socketCollection;

        public static WebSocketCollection SocketCollection
        {
            get
            {
                if (socketCollection == null)
                {
                    socketCollection = new WebSocketCollection();
                }
                return socketCollection;
            }
            set
            {
                socketCollection = value;
            }
        }
        public Viewer()
        {
            this.MaxIncomingMessageSize = 999999999;
        }
        public override void OnOpen()
        {
            SocketCollection.Add(this);
        }
        public override void OnMessage(byte[] message)
        {
            Partner.Send(message);
        }
        public override void OnMessage(string message)
        {
            dynamic jsonMessage;
            try
            {
                jsonMessage = Json.Decode(message);
            }
            catch (Exception ex)
            {
                // TODO: Error handling.
                throw ex;
            }
            string type = jsonMessage.Type;
            if (type == null)
            {
                // TODO: Error handling.
                return;
            }
            switch (type)
            {
                case "ConnectionType":
                    {
                        ConnectionType = Enum.Parse(typeof(ConnectionTypes), jsonMessage.ConnectionType);
                        var random = new Random();
                        var sessionID = random.Next(0, 999).ToString().PadLeft(3, '0') + " " + random.Next(0, 999).ToString().PadLeft(3, '0');
                        SessionID = sessionID.Replace(" ", "");
                        var request = new
                        {
                            Type = "SessionID",
                            SessionID = sessionID
                        };
                        Send(Json.Encode(request));
                        break;
                    }
                case "Connect":
                    {
                        var client = SocketCollection.FirstOrDefault(sock => ((Viewer)sock).SessionID == jsonMessage.SessionID.Replace(" ", "") && ((Viewer)sock).ConnectionType == ConnectionTypes.ClientApp);
                        if (client != null)
                        {
                            Partner = (Viewer)client;
                            ((Viewer)client).Partner = this;
                            SessionID = (client as Viewer).SessionID;
                            client.Send(message);
                        }
                        else
                        {
                            var request = new
                            {
                                Type = "Connect",
                                Status = "InvalidID"
                            };
                            Send(Json.Encode(request));
                        }
                        break;
                    }
                case "PartnerClose":
                    Partner = null;
                    Partner.Send(message);
                    break;
                case "PartnerError":
                    Partner = null;
                    Partner.Send(message);
                    break;
                default:
                    {
                        Partner.Send(message);
                        break;
                    }
            }
        }

        public override void OnClose()
        {
            if (Partner != null)
            {
                var request = new
                {
                    Type = "PartnerClose",
                };
                Partner.Send(Json.Encode(request));
            }
            SocketCollection.Remove(this);
        }
        public override void OnError()
        {
            if (Partner != null)
            {
                var request = new
                {
                    Type = "PartnerError",
                };
                Partner.Send(Json.Encode(request));
            }
            SocketCollection.Remove(this);
        }
        public string SessionID { get; set; }
        public Viewer Partner { get; set; }
        public ConnectionTypes ConnectionType { get; set; }

        public enum ConnectionTypes
        {
            Customer,
            Technician,
            ClientApp,
            HostApp
        }
    }
}