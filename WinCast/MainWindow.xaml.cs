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
#if DEBUG
        string servicePath = "ws://localhost:62637/Sockets/ScreenViewer.cshtml";
#else
        string servicePath = "wss://instatech.org/Sockets/ScreenViewer.cshtml";
#endif
        ClientWebSocket socket { get; set; }
        Bitmap bitmap { get; set; }
        Bitmap lastFrame { get; set; }
        Bitmap newFrame { get; set; }
        Graphics graphic { get; set; }
        bool capturing = false;
        int totalHeight = 0;
        int totalWidth = 0;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
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
            bitmap = new Bitmap(totalWidth, totalHeight);
            lastFrame = new Bitmap(totalWidth, totalHeight);
            graphic = Graphics.FromImage(bitmap);
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
            textSessionID.Foreground = new SolidColorBrush(Colors.Black);
            textSessionID.FontWeight = FontWeights.Bold;
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
            try
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
                            case "Connect":
                                beginScreenCapture();
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
                                    if (key.Length > 1 && key.Replace("+", "").Replace("^", "").Replace("%", "").Length > 1)
                                    {
                                        key = "{" + key + "}";
                                    }
                                    SendKeys.SendWait(key);
                                }
                                catch
                                {
                                    // TODO: Report missing keybind.
                                }
                                break;
                            case "PartnerClose":
                                textAgentStatus.Text = "Disonnected";
                                textViewStatus.Visibility = Visibility.Collapsed;
                                capturing = false;
                                break;
                            case "PartnerError":
                                textAgentStatus.Text = "Disonnected";
                                textViewStatus.Visibility = Visibility.Collapsed;
                                capturing = false;
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
            catch
            {
                stackMain.Visibility = Visibility.Collapsed;
                stackReconnect.Visibility = Visibility.Visible;
                textAgentStatus.Text = "Disonnected";
                textViewStatus.Visibility = Visibility.Collapsed;
                capturing = false;
                // TODO;
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
        private async void beginScreenCapture()
        {
            capturing = true;
            textViewStatus.Visibility = Visibility.Visible;
            textAgentStatus.Text = "Connected";
            while (capturing == true)
            {
                await sendFrame();
                await Task.Delay(50);
            }
        }
        private async Task sendFrame()
        {
            if (!capturing)
            {
                return;
            }

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

            var ran = new Random();
            // Occassionally send the whole image to reduce artifacts.
            if (ran.NextDouble() > .3)
            {
                newFrame = removeUnchangedPixels(bitmap, lastFrame, 170);
            }
            else
            {
                newFrame = bitmap;
            }
            lastFrame = (Bitmap)bitmap.Clone();
            using (var ms = new MemoryStream())
            {
                newFrame.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                await socket.SendAsync(new ArraySegment<byte>(ms.ToArray()), WebSocketMessageType.Binary, true, CancellationToken.None);
            }
            // For testing.
            //newFrame.Save(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\Test.png", System.Drawing.Imaging.ImageFormat.Jpeg);
        }
        private void textSessionID_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            System.Windows.Clipboard.SetText(textSessionID.Text);
            textSessionID.SelectAll();
        }

        private void buttonNewSession_Click(object sender, RoutedEventArgs e)
        {
            stackReconnect.Visibility = Visibility.Collapsed;
            stackMain.Visibility = Visibility.Visible;
            initWebSocket();
        }

        private Bitmap removeUnchangedPixels(Bitmap bitmap1, Bitmap bitmap2, byte transparentValue)
        {
            if (bitmap1.Height != bitmap2.Height || bitmap1.Width != bitmap2.Width)
            {
                throw new Exception("Bitmaps are not of equal dimensions.");
            }
            if (!Bitmap.IsAlphaPixelFormat(bitmap1.PixelFormat) || !Bitmap.IsAlphaPixelFormat(bitmap2.PixelFormat) ||
                !Bitmap.IsCanonicalPixelFormat(bitmap1.PixelFormat) || !Bitmap.IsCanonicalPixelFormat(bitmap2.PixelFormat))
            {
                throw new Exception("Bitmaps must be 32 bits per pixel and contain alpha channel.");
            }
            var width = bitmap1.Width;
            var height = bitmap1.Height;
            var bitmapNew = new Bitmap(width, height);

            var bd1 = bitmap1.LockBits(new System.Drawing.Rectangle(0, 0, width, height), System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap1.PixelFormat);
            var bd2 = bitmap2.LockBits(new System.Drawing.Rectangle(0, 0, width, height), System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap2.PixelFormat);
            var bd3 = bitmapNew.LockBits(new System.Drawing.Rectangle(0, 0, width, height), System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmapNew.PixelFormat);

            // Get the address of the first line.
            IntPtr ptr1 = bd1.Scan0;
            IntPtr ptr2 = bd2.Scan0;
            IntPtr ptr3 = bd3.Scan0;

            // Declare an array to hold the bytes of the bitmap.
            int bytes = Math.Abs(bd1.Stride) * bitmap.Height;
            byte[] rgbValues1 = new byte[bytes];
            byte[] rgbValues2 = new byte[bytes];
            byte[] rgbValues3 = new byte[bytes];

            // Copy the RGBA values into the array.
            Marshal.Copy(ptr1, rgbValues1, 0, bytes);
            Marshal.Copy(ptr2, rgbValues2, 0, bytes);
            Marshal.Copy(ptr3, rgbValues3, 0, bytes);

            // Check RGBA value for each pixel.  If no change, set all values to transparent value.
            for (int counter = 0; counter < rgbValues1.Length - 4; counter += 4)
            {
                if (rgbValues1[counter] == rgbValues2[counter] &&
                    rgbValues1[counter + 1] == rgbValues2[counter + 1] &&
                    rgbValues1[counter + 2] == rgbValues2[counter + 2] &&
                    rgbValues1[counter + 3] == rgbValues2[counter + 3])
                {
                    rgbValues3[counter] = transparentValue;
                    rgbValues3[counter + 1] = transparentValue;
                    rgbValues3[counter + 2] = transparentValue;
                    rgbValues3[counter + 3] = transparentValue;
                }
                else
                {
                    rgbValues3[counter] = rgbValues1[counter];
                    rgbValues3[counter + 1] = rgbValues1[counter + 1];
                    rgbValues3[counter + 2] = rgbValues1[counter + 2];
                    rgbValues3[counter + 3] = rgbValues1[counter + 3];
                }
            }
            // Copy the RGB values to the new bitmap.
            Marshal.Copy(rgbValues3, 0, ptr3, bytes);

            // Unlock the bits.
            bitmap1.UnlockBits(bd1);
            bitmap2.UnlockBits(bd2);
            bitmapNew.UnlockBits(bd3);

            return bitmapNew;
        }
    }
}
