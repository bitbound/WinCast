using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Security.Principal;
using System.Net.WebSockets;
using System.Threading;
using System.IO;
using Newtonsoft.Json;

namespace WinCast
{
    public partial class MainWindow : Window
    {
        // Add your service paths here.
#if !DEBUG
        string servicePath = "ws://localhost:4668/Sockets/ScreenViewer.cshtml";
#else
        string servicePath = "wss://translucency.info/InstaTech/Sockets/ScreenViewer.cshtml";
#endif
        System.Timers.Timer sendScreenTimer = new System.Timers.Timer(50);
        ClientWebSocket socket { get; set; }
        ImageConverter imageConverter = new ImageConverter();
        bool capturing = false;
        int totalHeight = 0;
        int totalWidth = 0;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            checkAdminStatus();
            getScreenSizes();
            initWebSocket();
        }

        private void sendLeftMouseDown(int x, int y)
        {
            User32.mouse_event(User32.MOUSEEVENTF_LEFTDOWN, (uint)x, (uint)y, 0, 0);
        }
        private void sendLeftMouseUp(int x, int y)
        {
            User32.mouse_event(User32.MOUSEEVENTF_LEFTUP, (uint)x, (uint)y, 0, 0);
        }
        private void sendRightMouseDown(int x, int y)
        {
            User32.mouse_event(User32.MOUSEEVENTF_RIGHTDOWN, (uint)x, (uint)y, 0, 0);
        }
        private void sendRightMouseUp(int x, int y)
        {
            User32.mouse_event(User32.MOUSEEVENTF_RIGHTUP, (uint)x, (uint)y, 0, 0);
        }
        private void sendMouseMove(int x, int y)
        {
            System.Windows.Forms.Cursor.Position = new System.Drawing.Point(x, y);
        }

        private void checkAdminStatus()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            if (principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                runAccountType.Text = "Administrator";
            }
            else
            {
                runAccountType.Text = "Non-Administrator";
            }
        }
        private void getScreenSizes()
        {
            foreach (Screen Monitor in Screen.AllScreens)
            {
                if (Monitor.Bounds.Height > this.Height)
                {
                    this.totalHeight = Monitor.Bounds.Height;
                }
                totalWidth += Monitor.Bounds.Width;
            }
        }
        private async void initWebSocket()
        {
            try
            {
                socket = new ClientWebSocket();
            }
            catch
            {
                System.Windows.MessageBox.Show("Unable to create web socket.", "Web Sockets Not Supported", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            try
            {
                await socket.ConnectAsync(new Uri(servicePath), CancellationToken.None);
            }
            catch
            {
                System.Windows.MessageBox.Show("Unable to connect to server.", "Connection Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var request = new {
                Type = "ConnectionType",
                ConnectionType =  "ClientApp",
            };
            var strRequest = JsonConvert.SerializeObject(request);
            var buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(strRequest));
            await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
            buffer = ClientWebSocket.CreateClientBuffer(65536, 65536);
            var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
            var trimmedBuffer = Encoding.UTF8.GetString(trimBytes(buffer.Array));
            textSessionID.Text = JsonConvert.DeserializeObject<dynamic>(trimmedBuffer).SessionID;
            handleSocket();
        }
        private async void handleSocket()
        {
            while (socket.State == WebSocketState.Connecting || socket.State == WebSocketState.Open)
            {
                var buffer = ClientWebSocket.CreateClientBuffer(65536, 65536);
                var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var trimmedBuffer = Encoding.UTF8.GetString(trimBytes(buffer.Array));
                    var jsonMessage = JsonConvert.DeserializeObject<dynamic>(trimmedBuffer);
                    switch ((string)jsonMessage.Type)
                    {
                        case "ViewSession":
                            beginScreenCapture();
                            break;
                        case "FrameReceived":
                            sendFrame();
                            break;
                        case "MouseMove":
                            sendMouseMove((int)Math.Round(((double)jsonMessage.PointX * totalWidth), 0), (int)Math.Round(((double)jsonMessage.PointY * totalHeight), 0));
                            break;
                        case "MouseDown":
                            if (jsonMessage.Button == "Left")
                            {
                                sendLeftMouseDown((int)Math.Round(((double)jsonMessage.PointX * totalWidth), 0), (int)Math.Round(((double)jsonMessage.PointY * totalHeight), 0));
                            }
                            else if (jsonMessage.Button == "Right")
                            {
                                sendRightMouseDown((int)Math.Round(((double)jsonMessage.PointX * totalWidth), 0), (int)Math.Round(((double)jsonMessage.PointY * totalHeight), 0));
                            }
                            break;
                        case "MouseUp":
                            if (jsonMessage.Button == "Left")
                            {
                                sendLeftMouseUp((int)Math.Round(((double)jsonMessage.PointX * totalWidth), 0), (int)Math.Round(((double)jsonMessage.PointY * totalHeight), 0));
                            }
                            else if (jsonMessage.Button == "Right")
                            {
                                sendRightMouseUp((int)Math.Round(((double)jsonMessage.PointX * totalWidth), 0), (int)Math.Round(((double)jsonMessage.PointY * totalHeight), 0));
                            }
                            break;
                        case "KeyPress":
                            try
                            {
                                string key = jsonMessage.Key;
                                if (key.Length == 1)
                                {
                                    SendKeys.SendWait(key);
                                } else if (key.Length > 1)
                                {

                                    key = "{" + key + "}";
                                    SendKeys.SendWait(key);
                                }
                            }
                            catch (Exception ex)
                            {
                                throw ex;
                            }
                            break;
                        default:
                            break;
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    // TODO.
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    // TODO.
                }
            }
        }

        private byte[] trimBytes(byte[] bytes)
        {
            var firstZero = 0;
            for (int i = bytes.Length - 1; i >= 0; i--)
            {
                if (bytes[i] != 0)
                {
                    firstZero = i + 1;
                    break;
                }
            }
            if (firstZero == 0)
            {
                throw new Exception("Byte array is empty.");
            }
            return bytes.Take(firstZero).ToArray();
        }
        private void beginScreenCapture()
        {
            capturing = true;
            textViewStatus.Visibility = Visibility.Visible;
            sendFrame();
        }
        private void stopScreenCapture()
        {
            capturing = false;
        }
        private void sendFrame()
        {
            if (!capturing)
            {
                return;
            }
            using (var bitmap = new Bitmap(totalWidth, totalHeight))
            {
                using (var graphic = Graphics.FromImage(bitmap))
                {
                    try
                    {
                        graphic.CopyFromScreen(new System.Drawing.Point(0, 0), System.Drawing.Point.Empty, new System.Drawing.Size(totalWidth, totalHeight));
                    }
                    catch
                    {
                        graphic.Clear(System.Drawing.Color.White);
                        var font = new Font(System.Drawing.FontFamily.GenericSansSerif, 30, System.Drawing.FontStyle.Bold);
                        graphic.DrawString("Waiting for screen capture...", font, System.Drawing.Brushes.Black, new PointF((totalWidth / 2), totalHeight / 2), new StringFormat() { Alignment = StringAlignment.Center });
                    }
                    graphic.Save();
                    using (MemoryStream stream = new MemoryStream())
                    {
                        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                        stream.Close();
                        socket.SendAsync(new ArraySegment<byte>(stream.ToArray()), WebSocketMessageType.Binary, true, CancellationToken.None);
                    }
                }
            }
        }
    }
}
