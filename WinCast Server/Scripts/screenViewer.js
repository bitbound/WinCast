///* User variables.  Update these values. *///

// Important: If your site isn't configured for SSL/TLS, change this to "ws://".
var webSocketProtocol = "wss://";

// Do not include "http"/"https".
var siteHostURL = "www.yoursitehere.com";

// Full URL to the webpage where the client EXE can be downloaded.
var downloadURL = "";

///* End of user variables. *///

var WinCast = WinCast || {};
WinCast.ScreenViewer = WinCast.ScreenViewer || {};
WinCast.ScreenViewer.Socket = WinCast.ScreenViewer.Socket || {};
WinCast.ScreenViewer.Connected = false;
var byteArray;
var imageData;
var imgWidth;
var imgHeight;
var imgX;
var imgY;
var url;
var img;

function connectToClient() {
    if (!WinCast.ScreenViewer.Connected)
    {
        if (window.location.href.search("localhost") > -1) {
            WinCast.ScreenViewer.Socket = new WebSocket("ws://localhost:62637/Sockets/ScreenViewer.cshtml");
        }
        else {
            WinCast.ScreenViewer.Socket = new WebSocket(webSocketProtocol + siteHostURL + "/WinCast/Sockets/ScreenViewer.cshtml");
        };
        WinCast.ScreenViewer.Socket.binaryType = "arraybuffer";
        $("#buttonConnect").text("Disconnect");
        $("#inputSessionID").attr("readonly", "true");
        WinCast.ScreenViewer.Connected = true;
        drawMessage("Connecting...");
        WinCast.ScreenViewer.Socket.onopen = function (e) {
            var request = {
                "Type": "ConnectionType",
                "ConnectionType": "HostApp"
            };
            WinCast.ScreenViewer.Socket.send(JSON.stringify(request));

            var sessionID = $("#inputSessionID").val();
            request = {
                "Type": "Connect",
                "SessionID": sessionID
            };
            WinCast.ScreenViewer.Socket.send(JSON.stringify(request));
        };
        WinCast.ScreenViewer.Socket.onclose = function (e) {
            WinCast.ScreenViewer.Connected = false;
            $("#buttonConnect").text("Connect");
            $("#inputSessionID").attr("readonly", false);
        };
        WinCast.ScreenViewer.Socket.onerror = function (e) {
            WinCast.ScreenViewer.Connected = false;
            $("#buttonConnect").text("Connect");
            $("#inputSessionID").attr("readonly", false);
            drawMessage("Session disconnected due to error.");
        };
        WinCast.ScreenViewer.Socket.onmessage = function (e) {
            if (e.data instanceof ArrayBuffer) {
                var wc = WinCast.ScreenViewer;
                byteArray = new Uint8Array(e.data);
                var length = byteArray.length;
                imgX = Number(byteArray[length - 4] * 100 + byteArray[length - 3]);
                imgY = Number(byteArray[length - 2] * 100 + byteArray[length - 1]);
                url = window.URL.createObjectURL(new Blob([byteArray.subarray(0, length - 4)]));
                img.src = url;
                return;
            }
            else {
                var jsonData = JSON.parse(e.data);
                var isc = WinCast.ScreenViewer.Context;
                switch (jsonData.Type) {
                    case "Connect":
                        {
                            if (jsonData.Status == "InvalidID")
                            {
                                WinCast.ScreenViewer.Socket.close();
                                drawMessage("Session ID not found.");
                            }
                            break;
                        }
                    case "PartnerClose":
                        {
                            WinCast.ScreenViewer.Socket.close();
                            drawMessage("Session disconnected.");
                            break;
                        }
                    case "PartnerError":
                        {
                            WinCast.ScreenViewer.Socket.close();
                            drawMessage("Session disconnected due to partner error.");
                            break;
                        }
                    default:
                        break;
                };
            }
        }
    }
    else if (WinCast.ScreenViewer.Connected) {
        WinCast.ScreenViewer.Socket.close();
    }
};
function drawMessage(strMessage) {
    var isc = WinCast.ScreenViewer.Context;
    isc.fillStyle = "lightgray";
    isc.fillRect(0, 0, isc.canvas.width, isc.canvas.height);
    isc.fillStyle = "black";
    isc.textAlign = "center";
    isc.textBaseline = "middle";
    var emSize = isc.canvas.width / 500;
    isc.font = emSize + "em sans-serif";
    isc.fillText(strMessage, isc.canvas.width / 2, isc.canvas.height / 2);
};

function copyClientLink() {
    $("#inputClientLink").select();
    try {
        var result = document.execCommand("copy");
    }
    catch (ex) {
        showTooltip($("#inputClientLink"), "bottom", "red", "Failed to copy to clipboard.");
    };
    if (result) {
        showTooltip($("#inputClientLink"), "bottom", "seagreen", "Link copied to clipboard.");
    }
    else {
        showTooltip($("#inputClientLink"), "bottom", "red", "Failed to copy to clipboard.");
    };
};
function showTooltip(objPlacementTarget, strPlacementDirection, strColor, strMessage) {
    if (objPlacementTarget instanceof jQuery) {
        objPlacementTarget = objPlacementTarget[0];
    }
    var divTooltip = document.createElement("div");
    divTooltip.innerText = strMessage;
    divTooltip.classList.add("tooltip");
    divTooltip.style.zIndex = 3;
    divTooltip.id = "tooltip" + String(Math.random());
    $(divTooltip).css({
        "position": "absolute",
        "background-color": "whitesmoke",
        "color": strColor,
        "border-radius": "10px",
        "padding": "5px",
        "border": "1px solid dimgray",
    });
    var rectPlacement = objPlacementTarget.getBoundingClientRect();
    switch (strPlacementDirection) {
        case "top":
            {
                divTooltip.style.bottom = Number(rectPlacement.top - 5) + "px";
                divTooltip.style.left = rectPlacement.left + "px";
                break;
            }
        case "right":
            {
                divTooltip.style.top = rectPlacement.top + "px";
                divTooltip.style.left = Number(rectPlacement.right + 5) + "px";
                break;
            }
        case "bottom":
            {
                divTooltip.style.top = Number(rectPlacement.bottom + 5) + "px";
                divTooltip.style.left = rectPlacement.left + "px";
                break;
            }
        case "left":
            {
                divTooltip.style.top = rectPlacement.top + "px";
                divTooltip.style.right = Number(rectPlacement.left - 5) + "px";
                break;
            }
        case "center":
            {
                divTooltip.style.top = Number(rectPlacement.bottom - (rectPlacement.height / 2)) + "px";
                divTooltip.style.left = Number(rectPlacement.right - (rectPlacement.width / 2)) + "px";
                divTooltip.style.transform = "translateX(-50%)";
            }
        default:
            break;
    }
    $(document.body).append(divTooltip);
    window.setTimeout(function () {
        $(divTooltip).animate({ opacity: 0 }, 1000, function () {
            $("#" + divTooltip.id).remove();
        })
    }, 1500);
}
$(document).ready(function () {
    WinCast.ScreenViewer.Context = document.getElementById("canvasScreenViewer").getContext("2d");
    $("#inputClientLink").val(downloadURL);
    drawMessage("Enter partner's session ID and click Connect.")
    img = document.createElement("img");
    img.onload = function () {
        var wc = WinCast.ScreenViewer;
        if (img.width > wc.Context.canvas.width)
        {
            wc.Context.canvas.width = img.width;
        }
        if (img.height > wc.Context.canvas.height)
        {
            wc.Context.canvas.height = img.height;
        }
        wc.Context.drawImage(img, imgX, imgY, img.width, img.height);
        window.URL.revokeObjectURL(url);
    };
    $("#canvasScreenViewer").on("mousemove", function (e) {
        e.preventDefault();
        if (WinCast.ScreenViewer.Socket.readyState == WebSocket.OPEN)
        {
            var pointX = e.offsetX / $("#canvasScreenViewer").width();
            var pointY = e.offsetY / $("#canvasScreenViewer").height();
            var request = {
                "Type": "MouseMove",
                "PointX": pointX,
                "PointY": pointY
            };
            WinCast.ScreenViewer.Socket.send(JSON.stringify(request));
        }
    })
    $("#canvasScreenViewer").on("mousedown", function (e) {
        if (WinCast.ScreenViewer.Socket.readyState == WebSocket.OPEN)
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
            WinCast.ScreenViewer.Socket.send(JSON.stringify(request));
        }
    })
    $("#canvasScreenViewer").on("mouseup", function (e) {
        if (WinCast.ScreenViewer.Socket.readyState == WebSocket.OPEN) {
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
            WinCast.ScreenViewer.Socket.send(JSON.stringify(request));
        }
    });
    $("#canvasScreenViewer").on("click", function(e){
        $("#inputSessionID").blur();
        $("#buttonConnect").blur();
    });
    $(window).on("keydown", function (e) {
        if (!$("#inputSessionID").is(":focus") && !$("#buttonConnect").is(":focus") && WinCast.ScreenViewer.Socket.readyState == WebSocket.OPEN) {
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
            WinCast.ScreenViewer.Socket.send(JSON.stringify(request));
        };
    });
});