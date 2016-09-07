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
using System.Drawing.Imaging;

namespace WinCast
{
    public partial class MainWindow : Window
    {
        // IMPORTANT: Update the URLs here.
#if DEBUG
        string servicePath = "ws://localhost:62637/Sockets/ScreenViewer.cshtml";
#else
        string servicePath = "wss://yourWebsite/Sockets/ScreenViewer.cshtml";
#endif
        ClientWebSocket socket { get; set; }

        Bitmap screenshot { get; set; }
        ImageCodecInfo jpgEncoder { get; set; }
        EncoderParameters encoderParameters = new EncoderParameters(1);
        Bitmap lastFrame { get; set; }
        Bitmap croppedFrame { get; set; }
        byte[] newData;
        System.Drawing.Rectangle boundingBox { get; set; }
        Graphics graphic { get; set; }
        bool capturing = false;
        int totalHeight = 0;
        int totalWidth = 0;
        bool firstScreenshot = true;


        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            initEncoder();
            getScreenSizes();
            initWebSocket();
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
            screenshot = new Bitmap(totalWidth, totalHeight);
            lastFrame = new Bitmap(totalWidth, totalHeight);
            graphic = Graphics.FromImage(screenshot);
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
                                User32.sendMouseMove((int)Math.Round(((double)jsonMessage.PointX * totalWidth), 0), (int)Math.Round(((double)jsonMessage.PointY * totalHeight), 0));
                                break;
                            case "MouseDown":
                                if (jsonMessage.Button == "Left")
                                {
                                    User32.sendLeftMouseDown((int)Math.Round(((double)jsonMessage.PointX * totalWidth), 0), (int)Math.Round(((double)jsonMessage.PointY * totalHeight), 0));
                                }
                                else if (jsonMessage.Button == "Right")
                                {
                                    User32.sendRightMouseDown((int)Math.Round(((double)jsonMessage.PointX * totalWidth), 0), (int)Math.Round(((double)jsonMessage.PointY * totalHeight), 0));
                                }
                                break;
                            case "MouseUp":
                                if (jsonMessage.Button == "Left")
                                {
                                    User32.sendLeftMouseUp((int)Math.Round(((double)jsonMessage.PointX * totalWidth), 0), (int)Math.Round(((double)jsonMessage.PointY * totalHeight), 0));
                                }
                                else if (jsonMessage.Button == "Right")
                                {
                                    User32.sendRightMouseUp((int)Math.Round(((double)jsonMessage.PointX * totalWidth), 0), (int)Math.Round(((double)jsonMessage.PointY * totalHeight), 0));
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
                                textAgentStatus.Text = "Disconnected";
                                textViewStatus.Visibility = Visibility.Collapsed;
                                capturing = false;
                                break;
                            case "PartnerError":
                                textAgentStatus.Text = "Disconnected";
                                textViewStatus.Visibility = Visibility.Collapsed;
                                capturing = false;
                                break;
                            default:
                                break;
                        }
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

            newData = getChangedPixels(screenshot, lastFrame);
            if (newData != null)
            {
                croppedFrame = screenshot.Clone(boundingBox, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var ms = new MemoryStream())
                {
                    if (firstScreenshot)
                    {
                        // If first screenshot, send entire screen.
                        screenshot.Save(ms, jpgEncoder, encoderParameters);
                        ms.WriteByte(0);
                        ms.WriteByte(0);
                        ms.WriteByte(0);
                        ms.WriteByte(0);
                        firstScreenshot = false;
                    }
                    else
                    {
                        croppedFrame.Save(ms, jpgEncoder, encoderParameters);
                        // Add x,y coordinates of top-left of image so receiver knows where to draw it.
                        foreach (var metaByte in newData)
                        {
                            ms.WriteByte(metaByte);
                        }
                    }
                    await socket.SendAsync(new ArraySegment<byte>(ms.ToArray()), WebSocketMessageType.Binary, true, CancellationToken.None);
                }
            }
            lastFrame = (Bitmap)screenshot.Clone();
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
        private void initEncoder()
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            jpgEncoder = codecs.FirstOrDefault(ici => ici.FormatID == ImageFormat.Jpeg.Guid);
            System.Drawing.Imaging.Encoder quality = System.Drawing.Imaging.Encoder.Quality;
            encoderParameters.Param[0] = new EncoderParameter(quality, (long)25);
        }
        private byte[] getChangedPixels(Bitmap bitmap1, Bitmap bitmap2)
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
            byte[] newImgData;
            int left = int.MaxValue;
            int top = int.MaxValue;
            int right = int.MinValue;
            int bottom = int.MinValue;

            var bd1 = bitmap1.LockBits(new System.Drawing.Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, bitmap1.PixelFormat);
            var bd2 = bitmap2.LockBits(new System.Drawing.Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, bitmap2.PixelFormat);
            // Get the address of the first line.
            IntPtr ptr1 = bd1.Scan0;
            IntPtr ptr2 = bd2.Scan0;

            // Declare an array to hold the bytes of the bitmap.
            int bytes = Math.Abs(bd1.Stride) * screenshot.Height;
            byte[] rgbValues1 = new byte[bytes];
            byte[] rgbValues2 = new byte[bytes];

            // Copy the RGBA values into the array.
            Marshal.Copy(ptr1, rgbValues1, 0, bytes);
            Marshal.Copy(ptr2, rgbValues2, 0, bytes);

            // Check RGBA value for each pixel.
            for (int counter = 0; counter < rgbValues1.Length - 4; counter += 4)
            {
                if (rgbValues1[counter] != rgbValues2[counter] ||
                    rgbValues1[counter + 1] != rgbValues2[counter + 1] ||
                    rgbValues1[counter + 2] != rgbValues2[counter + 2] ||
                    rgbValues1[counter + 3] != rgbValues2[counter + 3])
                {
                    // Change was found.
                    var pixel = counter / 4;
                    var row = (int)Math.Floor((double)pixel / bd1.Width);
                    var column = pixel % bd1.Width;
                    if (row < top)
                    {
                        top = row;
                    }
                    if (row > bottom)
                    {
                        bottom = row;
                    }
                    if (column < left)
                    {
                        left = column;
                    }
                    if (column > right)
                    {
                        right = column;
                    }
                }
            }
            if (left < right && top < bottom)
            {
                // Bounding box is valid.

                // Byte array that indicates top left coordinates of the image.
                newImgData = new byte[4];
                newImgData[0] = Byte.Parse(left.ToString().PadLeft(4, '0').Substring(0, 2));
                newImgData[1] = Byte.Parse(left.ToString().PadLeft(4, '0').Substring(2, 2));
                newImgData[2] = Byte.Parse(top.ToString().PadLeft(4, '0').Substring(0, 2));
                newImgData[3] = Byte.Parse(top.ToString().PadLeft(4, '0').Substring(2, 2));

                boundingBox = new System.Drawing.Rectangle(left, top, right - left, bottom - top);
                bitmap1.UnlockBits(bd1);
                bitmap2.UnlockBits(bd2);
                return newImgData;
            }
            else
            {
                bitmap1.UnlockBits(bd1);
                bitmap2.UnlockBits(bd2);
                return null;
            }
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

            var bd1 = bitmap1.LockBits(new System.Drawing.Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, bitmap1.PixelFormat);
            var bd2 = bitmap2.LockBits(new System.Drawing.Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, bitmap2.PixelFormat);
            var bd3 = bitmapNew.LockBits(new System.Drawing.Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, bitmapNew.PixelFormat);
            // Get the address of the first line.
            IntPtr ptr1 = bd1.Scan0;
            IntPtr ptr2 = bd2.Scan0;
            IntPtr ptr3 = bd3.Scan0;

            // Declare an array to hold the bytes of the bitmap.
            int bytes = Math.Abs(bd1.Stride) * screenshot.Height;
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
