using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Pr22.Processing;
using Newtonsoft.Json.Linq;

namespace RegisterApp
{
    public partial class Main
    {
        Pr22.DocumentReaderDevice _pr = null;
        Pr22.Task.TaskControl _scanCtrl = null;
        Document _analyzeResult;
        JObject _scannedPhoto = new JObject();
        bool _uploadImage = true;

        private bool ScannerInitialize()
        {
            Utils.Logging("ScannerInitialize");
            try { _pr = new Pr22.DocumentReaderDevice(); }
            catch (Exception ex)
            {
                

                MessageBox.Show("Can't initialize DocumentReaderDevice/n" + ex);
                /*_socket.Emit(MainConfig.Initialize, new JObject
                    {
                        { "status", false },
                        { "message", "Can't initialize DocumentReaderDevice/n" + ex },
                        { "code", "APPREGISTER100A" }
                    }.ToString()
                );*/
                Utils.Logging("ScannerInitialize: Can't initialize DocumentReaderDevice");
                return true;
            }

            //Nhận máy quét
            List<string> Devices = Pr22.DocumentReaderDevice.GetDeviceList();
            //Kiểm tra máy quét
            if (Devices.Count == 0)
            {
                MessageBox.Show("No scanner connected");
                /* _socket.Emit(MainConfig.Initialize, new JObject
                     {
                         { "status", false },
                         { "message", "No scanner connected" },
                         { "code", "APPREGISTER100B" }
                     }.ToString()
                 );*/
                Utils.Logging("ScannerInitialize: No scanner connected");
                return true;
            }

            //Nếu có máy quét thì thiết lập máy quét
            _pr.Connection += DeviceConnected;

            _pr.PresenceStateChanged += DocumentStateChanged;
            _pr.ImageScanned += ImageScanned;
            _pr.ScanFinished += ScanFinished;
            Utils.Logging("ScannerInitialize: Done");
            return false;
        }
        //Nếu máy quét hộ chiếu được kết nối hay không
        //If passport scanner is connected or not
        void DeviceConnected(object sender, Pr22.Events.ConnectionEventArgs e)
        {
            UpdateDeviceList();
        }

        private void UpdateDeviceList()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(UpdateDeviceList));
                return;
            }
            Utils.Logging("UpdateDeviceList");

            List<string> devices = Pr22.DocumentReaderDevice.GetDeviceList();

            //If passport scanner is not connected
            if (devices.Count == 0)
            {
                Utils.Logging("UpdateDeviceList: No device connected");
                MessageBox.Show("No device connected");
               /* _socket.Emit(Config.Scanner.Info, new JObject
                    {
                        { "status", false },
                        { "message", "No device connected" },
                        { "code", "APPREGISTER1701" }
                    }.ToString()
                );*/
            }
            else
            {
                string device = devices[0];  //chọn thiết bị đầu tiên kết nối

                //Connect the device 
                try
                {
                    _pr.UseDevice(device);

                    //Start free detection 
                    _pr.Scanner.StartTask(Pr22.Task.FreerunTask.Detection());

                    //thêm ánh  sáng cho máy quét
                    Config.Scanner.scannedLights.Clear();
                    Config.Scanner.scannedLights.Add(Pr22.Imaging.Light.White);
                    Config.Scanner.scannedLights.Add(Pr22.Imaging.Light.UV);
                    Config.Scanner.scannedLights.Add(Pr22.Imaging.Light.Infra);
                    Utils.Logging("UpdateDeviceList: Device Connected");
                }
                catch
                {
                    Utils.Logging("UpdateDeviceList: Device is currently in use");
                    MessageBox.Show("Device is currently in use, Please exit any other program using the Passport Scanner");
                    /*_socket.Emit(Config.Scanner.Info, new JObject
                    {
                        { "status", false },
                        { "message", "Device is currently in use, Please exit any other program using the Passport Scanner" },
                        { "code", "APPREGISTER1702" }
                    }.ToString()
                );*/
                _fail = true;
                }
            }
        }

        //If document is in the passport scanner
        private void DocumentStateChanged(object sender, Pr22.Events.DetectionEventArgs e)
        {
            if (e.State == Pr22.Util.PresenceState.Present)
            {
                BeginInvoke(new EventHandler(StartScanning), sender, e);
            }
        }

        //Scan document
        private void StartScanning(object sender, EventArgs e)
        {
            Pr22.Task.DocScannerTask ScanTask = new Pr22.Task.DocScannerTask();
            foreach (Pr22.Imaging.Light light in Config.Scanner.scannedLights)
            {
                ScanTask.Add(light);
            }
            _scanCtrl = _pr.Scanner.StartScanning(ScanTask, Pr22.Imaging.PagePosition.First);
        }

        //Send scanned image to server
        void ImageScanned(object sender, Pr22.Events.ImageEventArgs e)
        {
            SendImage(e);
        }

        private void SendImage(Pr22.Events.ImageEventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<Pr22.Events.ImageEventArgs>(SendImage), e);
                return;
            }
            Pr22.Imaging.DocImage docImage = _pr.Scanner.GetPage(e.Page).Select(e.Light);
            Console.WriteLine(e.Page);
            Bitmap bmap = docImage.ToBitmap();

            //Resize image before send
            Bitmap passport = ResizeImage(bmap, Config.Scanner.bmapSize);
            string imageLink = "";

            try
            {
                byte[] bytes = (byte[])_converter.ConvertTo(passport, typeof(byte[]));
                string fileName = e.Light.ToString() + "_photo.jpeg";
                Console.WriteLine(MainConfig.Url + Config.Scanner.UploadImage);
                //string responseText = Utils.UploadImage(MainConfig.Url + Config.Scanner.UploadImage, fileName, "document", bytes);
                //imageLink = JObject.Parse(responseText)["pathUrl"].ToString().Replace(@"\", @"\\");
            }
            catch (Exception ex) {
                Console.WriteLine(ex);
                _uploadImage = false;
                MessageBox.Show("Can't send image" + ex);
               /*_socket.Emit(Config.Scanner.Info, new JObject
                    {
                        { "status", false },
                        { "message", "Can't send image" + ex },
                        { "code", "APPREGISTER1709" }
                    }.ToString()
                );*/
            }
            _scannedPhoto.Add(e.Light.ToString() + "_photo", imageLink);
        }

        //Function to resize bitmap image
        private static Bitmap ResizeImage(Bitmap imgToResize, Size size)
        {
            try
            {
                Bitmap b = new Bitmap(size.Width, size.Height);
                using (Graphics g = Graphics.FromImage(b))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(imgToResize, 0, 0, size.Width, size.Height);
                }
                return b;
            }
            catch
            {
                //Bitmap can't resize
                return imgToResize;
            }
        }

        //Sau khi quét xong
        private void ScanFinished(object sender, Pr22.Events.PageEventArgs e)
        {
            BeginInvoke(new MethodInvoker(Analyze));
            BeginInvoke(new MethodInvoker(CloseScan));
        }

        //Analyze document
        private void Analyze()
        {
            Utils.Logging("Analyze");
            Pr22.Task.EngineTask OcrTask = new Pr22.Task.EngineTask();

            //Add field to analyze
            OcrTask.Add(FieldSource.Mrz, FieldId.All);
            OcrTask.Add(FieldSource.Viz, FieldId.All);
            OcrTask.Add(FieldSource.Barcode, FieldId.All);

            Page page;
            try { page = _pr.Scanner.GetPage(0); }
            catch (Pr22.Exceptions.General) { return; }
            try { _analyzeResult = _pr.Engine.Analyze(page, OcrTask); }
            catch (Pr22.Exceptions.General ex)
            {
                Utils.Logging("Analyze: Fail");
                MessageBox.Show("Analyze Fail: " + ex);
                    /*_socket.Emit(Config.Scanner.Info, new JObject
                    {
                        { "status", false },
                        { "message", "Analyze Fail: " + ex },
                        { "code", "APPREGISTER1703" }
                    }.ToString()
                );*/
                return;
            }
            Utils.Logging("Analyze: Done");
            ParseResult();
        }

        //Collect result from ocr and send to server
        void ParseResult()
        {
            Utils.Logging("ParseResult");
            if (!_uploadImage)
            {
                _scannedPhoto = new JObject();
                _uploadImage = true;
                return;
            }
                
            List<FieldReference> Fields = _analyzeResult.GetFields();

            JArray data = new JArray();  //Array that contain information
            for (int i = 0; i < Fields.Count; i++)
            {
                try
                {
                    Field field = _analyzeResult.GetField(Fields[i]);
                    string[] values = new string[4];
                    values[0] = i.ToString();
                    values[1] = Fields[i].ToString(" ") + new StrCon() + GetAmid(field);
                    try { values[2] = field.GetBestStringValue(); }
                    catch (Pr22.Exceptions.InvalidParameter)
                    {
                        values[2] = PrintBinary(field.GetBinaryValue(), 0, 16);
                    }
                    catch (Pr22.Exceptions.General) { }
                    try { values[3] = field.GetStatus().ToString(); }
                    catch (Pr22.Exceptions.General) { }

                    //Create JSON from each row of informations
                    JObject jsonValue = new JObject
                    {
                        { "ID", values[0] },
                        { "field", values[1] },
                        { "value", values[2] },
                        { "checksum", values[3] }
                    };
                    data.Add(jsonValue);

                    //Send face image
                    if (values[1].Contains("Viz Face"))
                    {
                        try
                        {
                            Bitmap bmap = field.GetImage().ToBitmap();

                            //Resize face image
                            int width = bmap.Width / 2;
                            int height = bmap.Height / 2;
                            Size bmapSizeFace = new Size(width, height);
                            Bitmap face = ResizeImage(bmap, bmapSizeFace);
                            string imageLink = "";

                            try
                            {
                                byte[] bytes = (byte[])_converter.ConvertTo(face, typeof(byte[]));
                                //string responseText = Utils.UploadImage(MainConfig.Url + Config.Scanner.UploadImage, "Face_photo.jpeg", "document", bytes);
                                // imageLink = JObject.Parse(responseText)["pathUrl"].ToString().Replace(@"\", @"\\");
                            }
                            catch (Exception ex)
                            {
                                Utils.Logging("ParseResult: Can't send image " + ex);
                                MessageBox.Show("Can't send image" + ex);
                               /* _socket.Emit(Config.Scanner.Info, new JObject
                                    {
                                        { "status", false },
                                        { "message", "Can't send image" + ex },
                                        { "code", "APPREGISTER1709" }
                                    }.ToString()
                                );*/
                                _scannedPhoto = new JObject();
                                return;
                            }
                            Utils.Logging("ParseResult: Added Face_photo");
                            _scannedPhoto.Add( "Face_photo", imageLink );
                        }
                        catch (Pr22.Exceptions.General) {
                            Utils.Logging("ParseResult: Extract face image fail");
                            MessageBox.Show("Extract face image fail");
                            /*_socket.Emit(Config.Scanner.Info, new JObject
                                {
                                    { "status", false },
                                    { "message", "Extract face image fail" },
                                    { "code", "APPREGISTER1706" }
                                }.ToString()
                            );*/
                            return;
                        }
                    }
                }
                catch (Pr22.Exceptions.General) {
                    Utils.Logging("ParseResult: Exceptions Error");
                    MessageBox.Show("Exceptions Error");
                    /*_socket.Emit(Config.Scanner.Info, new JObject
                        {
                            { "status", false },
                            { "message", "Exceptions Error" },
                            { "code", "APPREGISTER1707" }
                        }.ToString()
                    );*/
                    return;
                }
            }

            JObject information = new JObject
            {
                { "status", true },
                { "message", data },
                { "images", _scannedPhoto },
                { "code", "APPREGISTER1711" }
            };

            Utils.Logging("ParseResult: Done " + information.ToString());

           /* _socket.Emit(Config.Scanner.Info, information.ToString());  //Send the data*/
            _scannedPhoto = new JObject();
        }

        private static string PrintBinary(byte[] arr, int pos, int sz)
        {
            int p0;
            string str = "", str2 = "";
            for (p0 = pos; p0 < arr.Length && p0 < pos + sz; p0++)
            {
                str += arr[p0].ToString("X2") + " ";
                str2 += arr[p0] < 0x21 || arr[p0] > 0x7e ? '.' : (char)arr[p0];
            }
            for (; p0 < pos + sz; p0++) { str += "   "; str2 += " "; }
            return str + str2;
        }

        private string GetAmid(Field field)
        {
            try
            {
                return field.ToVariant().GetChild((int)Pr22.Util.VariantId.AMID, 0);
            }
            catch (Pr22.Exceptions.General) { return ""; }
        }

        private void CloseScan()
        {
            try { if (_scanCtrl != null) _scanCtrl.Wait(); }
            catch (Pr22.Exceptions.General ex)
            {
                MessageBox.Show("Close Scan Fail: " + ex);
                /*_socket.Emit(Config.Scanner.Info, new JObject
                    {
                        { "status", false },
                        { "message", "Close Scan Fail: " + ex },
                        { "code", "APPREGISTER1708" }
                    }.ToString()
                );*/
            }
            _scanCtrl = null;
        }
    }

    /// <summary>
    /// This class makes string concatenation with spaces and prefixes.
    /// </summary>
    public class StrCon
    {
        string fstr = "";
        string cstr = "";

        public StrCon() { }

        public StrCon(string bounder) { cstr = bounder + " "; }

        public static string operator +(StrCon csv, string str)
        {
            if (str != "") str = csv.cstr + str;
            if (csv.fstr != "" && str != "" && str[0] != ',') csv.fstr += " ";
            return csv.fstr + str;
        }

        public static StrCon operator +(string str, StrCon csv)
        {
            csv.fstr = str;
            return csv;
        }
    }
}
