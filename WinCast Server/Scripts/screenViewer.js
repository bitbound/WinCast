var InstaTech = InstaTech || {};
InstaTech.ScreenViewer = InstaTech.ScreenViewer || {};
InstaTech.ScreenViewer.Socket = InstaTech.ScreenViewer.Socket || {};
InstaTech.ScreenViewer.Connected = false;
var byteArray;
var imageData;
var imgWidth;
var imgHeight;
var imgX;
var imgY;
var url;

function connectToClient() {
    if (!InstaTech.ScreenViewer.Connected)
    {
        if (window.location.href.search("localhost") > -1) {
            InstaTech.ScreenViewer.Socket = new WebSocket("ws://localhost:62637/Sockets/ScreenViewer.cshtml");
        }
        else {
            InstaTech.ScreenViewer.Socket = new WebSocket("wss://translucency.info/InstaTech/Sockets/ScreenViewer.cshtml");
        };
        InstaTech.ScreenViewer.Socket.binaryType = "arraybuffer";
        $("#buttonConnect").text("Disconnect");
        $("#inputSessionID").attr("readonly", "true");
        InstaTech.ScreenViewer.Connected = true;
        drawMessage("Connecting...");
        InstaTech.ScreenViewer.Socket.onopen = function (e) {
            var request = {
                "Type": "ConnectionType",
                "ConnectionType": "HostApp"
            };
            InstaTech.ScreenViewer.Socket.send(JSON.stringify(request));

            var sessionID = $("#inputSessionID").val();
            request = {
                "Type": "Connect",
                "SessionID": sessionID
            };
            InstaTech.ScreenViewer.Socket.send(JSON.stringify(request));
        };
        InstaTech.ScreenViewer.Socket.onclose = function (e) {
            InstaTech.ScreenViewer.Connected = false;
            $("#buttonConnect").text("Connect");
            $("#inputSessionID").attr("readonly", false);
        };
        InstaTech.ScreenViewer.Socket.onerror = function (e) {
            InstaTech.ScreenViewer.Connected = false;
            $("#buttonConnect").text("Connect");
            $("#inputSessionID").removeAttr("readyonly");
            drawMessage("Session disconnected due to error.");
        };
        InstaTech.ScreenViewer.Socket.onmessage = function (e) {
            if (e.data instanceof ArrayBuffer) {
                var isv = InstaTech.ScreenViewer;
                byteArray = new Uint8Array(e.data);
                var length = byteArray.length;
                imgX = Number(byteArray[length - 4] * 100 + byteArray[length - 3]);
                imgY = Number(byteArray[length - 2] * 100 + byteArray[length - 1]);
                url = window.URL.createObjectURL(new Blob([byteArray.subarray(0, length - 5)]));
                img.src = url;
                return;
            }
            else {
                var jsonData = JSON.parse(e.data);
                var isc = InstaTech.ScreenViewer.Context;
                switch (jsonData.Type) {
                    case "Connect":
                        {
                            if (jsonData.Status == "InvalidID")
                            {
                                InstaTech.ScreenViewer.Socket.close();
                                drawMessage("Session ID not found.");
                            }
                            break;
                        }
                    case "PartnerClose":
                        {
                            InstaTech.ScreenViewer.Socket.close();
                            drawMessage("Session disconnected.");
                            break;
                        }
                    case "PartnerError":
                        {
                            InstaTech.ScreenViewer.Socket.close();
                            drawMessage("Session disconnected due to partner error.");
                            break;
                        }
                    default:
                        break;
                };
            }
        }
    }
    else if (InstaTech.ScreenViewer.Connected) {
        InstaTech.ScreenViewer.Socket.close();
    }
};
function drawMessage(strMessage)
{
    var isc = InstaTech.ScreenViewer.Context;
    isc.fillStyle = "lightgray";
    isc.fillRect(0, 0, isc.canvas.width, isc.canvas.height);
    isc.fillStyle = "black";
    isc.textAlign = "center";
    isc.textBaseline = "middle";
    var emSize = Math.round(isc.canvas.width / 400);
    isc.font = emSize + "em sans-serif";
    isc.fillText(strMessage, isc.canvas.width / 2, isc.canvas.height / 2);
}
$(document).ready(function () {
    InstaTech.ScreenViewer.Context = document.getElementById("canvasScreenViewer").getContext("2d");
    img = document.createElement("img");
    img.onload = function () {
        var isv = InstaTech.ScreenViewer;
        if (img.width > isv.Context.canvas.width)
        {
            isv.Context.canvas.width = img.width;
        }
        if (img.height > isv.Context.canvas.height)
        {
            isv.Context.canvas.height = img.height;
        }
        isv.Context.drawImage(img, imgX, imgY, img.width, img.height);
        window.URL.revokeObjectURL(url);
    };
    $("#canvasScreenViewer").on("mousemove", function (e) {
        e.preventDefault();
        if (InstaTech.ScreenViewer.Socket.readyState == WebSocket.OPEN)
        {
            var pointX = e.offsetX / $("#canvasScreenViewer").width();
            var pointY = e.offsetY / $("#canvasScreenViewer").height();
            var request = {
                "Type": "MouseMove",
                "PointX": pointX,
                "PointY": pointY
            };
            InstaTech.ScreenViewer.Socket.send(JSON.stringify(request));
        }
    })
    $("#canvasScreenViewer").on("mousedown", function (e) {
        if (InstaTech.ScreenViewer.Socket.readyState == WebSocket.OPEN)
        {
            if (e.button != 0 && e.button != 2)
            {
                return;
            }
            var pointX = e.offsetX / $("#canvasScreenViewer").width();
            var pointY = e.offsetY / $("#canvasScreenViewer").height();
            var request = {
                "Type": "MouseDown",
                "PointX": pointX,
                "PointY": pointY
            };
            if (e.button == 0)
            {
                request.Button = "Left";
            }
            else if (e.button == 2)
            {
                request.Button = "Right";
            }
            InstaTech.ScreenViewer.Socket.send(JSON.stringify(request));
        }
    })
    $("#canvasScreenViewer").on("mouseup", function (e) {
        if (InstaTech.ScreenViewer.Socket.readyState == WebSocket.OPEN) {
            if (e.button != 0 && e.button != 2) {
                return;
            }
            var pointX = e.offsetX / $("#canvasScreenViewer").width();
            var pointY = e.offsetY / $("#canvasScreenViewer").height();
            var request = {
                "Type": "MouseUp",
                "PointX": pointX,
                "PointY": pointY
            };
            if (e.button == 0) {
                request.Button = "Left";
            }
            else if (e.button == 2) {
                e.preventDefault();
                request.Button = "Right";
            }
            InstaTech.ScreenViewer.Socket.send(JSON.stringify(request));
        }
    });
    $("#canvasScreenViewer").on("click", function(e){
        $("#inputSessionID").blur();
        $("#buttonConnect").blur();
    });
    $(window).on("keydown", function (e) {
        if (!$("#inputSessionID").is(":focus") && !$("#buttonConnect").is(":focus") && InstaTech.ScreenViewer.Socket.readyState == WebSocket.OPEN) {
            e.preventDefault();
            if (e.key == "Alt" || e.key == "Shift" || e.key == "Ctrl") {
                return;
            }
            var key = e.key;
            if (e.altKey) {
                key = "%" + key;
            }
            else if (e.ctrlKey) {
                key = "^" + key;
            }
            else if (e.shiftKey) {
                key = "+" + key;
            }
            var request = {
                "Type": "KeyPress",
                "Key": e.key,
            };
            InstaTech.ScreenViewer.Socket.send(JSON.stringify(request));
        };
    });
});