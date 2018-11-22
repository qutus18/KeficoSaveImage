using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System.Threading;
using System.IO;
using System.Configuration;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ActUtlTypeLib;
using System.Drawing.Imaging;
using System.Timers;

[assembly: log4net.Config.XmlConfigurator(Watch =true)]

namespace Kefico
{
    public partial class Kefico001 : Form
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public delegate void ChangeValuePLC(string Mbit);
        public event ChangeValuePLC valueChangeEvent;
        ActUtlType plcKefico = new ActUtlType();
        VideoCapture captureV;
        string posSaveImage = "D:";
        int CameraNumber;
        Socket socketConnect_one;
        private Thread threadReceive_one;
        private string stringReceive;
        private System.Timers.Timer timerCapture;
        private Thread threadPLCConnection;
        private int M1000;
        private int M3010;
        private bool captureDone;
        private int SM400;
        private Mat m;
        private bool cameraRunning;
        private bool inCameraProcess;
        private WebcamObject mainCamera;
        StreamWriter mainWriter;
        System.Timers.Timer errorTimer;

        public Kefico001()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //            // Đọc giá trị cài đặt Camera
            //#pragma warning disable CS0618 // Type or member is obsolete
            //            CameraNumber = int.Parse(ConfigurationSettings.AppSettings["CameraNumber"]);
            //#pragma warning restore CS0618 // Type or member is obsolete

            //            // Khởi tạo giá trị mặc định cho string đọc Barcode
            //            stringReceive = "NoBarcode" + DateTime.Now.ToString("yy_MM_dd_hh_mm_ss");

            //            // Khởi chạy kết nối PLC
            //            StartPLCCommunication();

            //            // Khởi tạo Camera
            //            mainCamera = new WebcamObject(CameraNumber);

            //            // Khởi tạo timer đếm thời gian để báo lỗi
            //            errorTimer = new System.Timers.Timer();
            //            errorTimer.Interval = 5000;
            //            errorTimer.Elapsed += SetErrFlag;

            Console.WriteLine("Hello");
            log.Error("This is my error message");
        }

        /// <summary>
        /// Khởi chạy kết nối PLC
        /// </summary>
        private void StartPLCCommunication()
        {
            // Khai báo Station kết nối PLC
            plcKefico.ActLogicalStationNumber = 3;
            plcKefico.Open();

            // Khai báo và chạy Thread PLC : UpdateDataPLC
            threadPLCConnection = new Thread(UpdateDataPLC);
            threadPLCConnection.Name = "PLC";
            threadPLCConnection.IsBackground = true;
            threadPLCConnection.Start();
            valueChangeEvent += st => SetConfirmbit(st);
        }

        private void SetConfirmbit(string st)
        {
            switch (st)
            {
                case "M100":
                    break;
                default:
                    break;

            }
        }

        private void UpdateDataPLC()
        {
            // Chạy liên tục Thread
            while (true)
            {
                // Kiểm tra Bit SM400 để xác nhận PLC có đang kết nối không
                var iret = plcKefico.GetDevice("SM400", out SM400); // Zig Up
                if (iret != 0)
                {
                    MessageBox.Show(String.Format("0x{0:x8} [HEX]", iret));
                    return;
                }

                // Kiểm tra đã kết nối Barcode hay chưa, nếu chưa thì ResetBit M3012 PLC
                if (btnConnect.Text == "Connected") plcKefico.SetDevice("M3012", 1);
                else plcKefico.SetDevice("M3012", 0);

                // Nhận tín hiệu chụp ảnh gửi từ PLC - M3010
                iret = plcKefico.GetDevice("M3010", out M3010); // Barcode Begin
                if ((M3010 == 1) && (!captureDone))
                {
                    // Chạy hàm chụp ảnh
                    CaptureImage(stringReceive);
                    captureDone = true;
                    // Đặt lại giá trị Barcode mặc định là NoBarcode
                    stringReceive = "NoBarcode" + DateTime.Now.ToString("yy_MM_dd_hh_mm_ss");
                    // Bật bit PLC - M3011 báo đang chụp
                    plcKefico.SetDevice("M3011", 1);
                }
                // Reset trạng thái chụp ảnh về chưa chụp
                if ((M3010 == 0) && (captureDone))
                {
                    captureDone = false;
                }

                Thread.Sleep(100);
            }
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            ConnectIP();
        }

        /// <summary>
        /// Connect TCP/IP
        /// </summary>
        private void ConnectIP()
        {
            bool bRet = true;

            if (socketConnect_one != null)
            {
                socketConnect_one.Close();
            }

            socketConnect_one = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var IPAddress_one = IPAddress.Parse(txtIP.Text);
            var IPEndPoint_one = new IPEndPoint(IPAddress_one, 23);

            try
            {
                if (!socketConnect_one.Connected) socketConnect_one.Connect(IPEndPoint_one);

                if (socketConnect_one.Connected)
                {
                    btnConnect.BackColor = Color.Green;
                    btnConnect.Text = "Connected";
                    if (threadReceive_one == null)
                    {
                        threadReceive_one = new System.Threading.Thread(ReceiveDataIp_one);
                        threadReceive_one.IsBackground = true;
                        threadReceive_one.Start();
                    }
                }
                else
                {
                    btnConnect.BackColor = Color.Red;
                    btnConnect.Text = "Disconnected";

                    bRet = false;
                }
            }
            catch
            {
                btnConnect.BackColor = Color.Red;
                btnConnect.Text = "Disconnected";
                btnConnect.Enabled = true;
                bRet = false;
            }

        }

        /// <summary>
        /// Nhận Barcode
        /// </summary>
        private void ReceiveDataIp_one()
        {
            Thread.Sleep(100);
            Byte[] byteReceive = new Byte[1000];
            while (true)
            {
                Thread.Sleep(100);
                try
                {
                    var ret = socketConnect_one.Receive(byteReceive, byteReceive.Length, 0);
                    if (ret > 5)
                    {
                        stringReceive = "";
                        for (int i = 0; i < ret; i++)
                        {
                            string temp = ((char)byteReceive[i]).ToString();
                            stringReceive += ((char)byteReceive[i]).ToString();
                        }
                        stringReceive = stringReceive.Replace("\r\n", "").Replace("\r", "").Replace("\n", "");
                        Invoke(new MethodInvoker(delegate
                        {
                            lblBarcode.Text = stringReceive;
                        }));
                        //StartSetupWebcam();
                        mainCamera.Start();
                    }

                }
                catch
                {
                    if (!socketConnect_one.Connected)
                    {
                        btnConnect.BackColor = Color.Red;
                        break;
                    }
                }
            }
        }

        private void txtSavePos_TextChanged(object sender, EventArgs e)
        {
            posSaveImage = txtSavePos.Text;
        }

        /// <summary>
        /// Khởi tạo các giá trị khi load form
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>

        private void SetErrFlag(object sender, ElapsedEventArgs e)
        {
            plcKefico.SetDevice("M3020", 1);
            errorTimer.Stop();
        }

        /// <summary>
        /// Hidden Button Capture;
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnTest_Click(object sender, EventArgs e)
        {
            CaptureImage(stringReceive);
        }

        private void StartSetupWebcam()
        {
            cameraRunning = true;
            captureV = new VideoCapture(CameraNumber);
            captureV.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.Contrast, double.Parse(txtContrast.Text));
            captureV.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.Brightness, double.Parse(txtBrighness.Text));
            captureV.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.Exposure, double.Parse(txtExplosure.Text));
            captureV.SetCaptureProperty(CapProp.Fps, 30);
            captureV.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameHeight, 1300);
            captureV.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameWidth, 1300);
            m = captureV.QueryFrame();
            m = captureV.QueryFrame();
            m = captureV.QueryFrame();
        }

        /// <summary>
        /// Hàm chụp ảnh và lưu ảnh theo Barcode
        /// </summary>
        /// <param name="filename"></param>
        private async void CaptureImage(string filename)
        {
            // Nếu filename vẫn đang là mặc định, thì ảnh không có Barcode
            if (filename.IndexOf("Barcode") > 0) filename = "NoBarcode" + DateTime.Now.ToString("yy_MM_dd_hh_mm_ss");

            // chạy Timer đếm lỗi
            errorTimer.Start();

            // Nếu chưa chạy Camera thì gửi lệnh chạy
            if (mainCamera.CameraStopped) mainCamera.Start();

            // Đợi cho đến khi đủ 10Frame
            // 
            while (mainCamera.CurrentFrame < 10)
            {
                Console.Write("camera Working!");
                await Task.Delay(50);
            }

            // Dừng camera
            mainCamera.Stop();

            // Lấy ảnh từ đầu ra của Class Camera
            m = mainCamera.GetMatImage();

            // Kiểm tra thư mục lưu file ảnh, nếu chưa có thì tạo thư mục
            string urlBase = @txtSavePos.Text + "\\" + DateTime.Now.ToString("yyyyMMdd");
            if (!Directory.Exists(urlBase))
                Directory.CreateDirectory(urlBase);

            // Thử lưu ảnh, nếu lưu được thì tắt timer lỗi, nếu không lưu được thì báo lỗi sau thời gian timerError
            // Kiểm tra đoạn này??? hàm chuyển đổi saveJpeg có thể có lỗi -_-
            try
            {
                saveJpeg(@urlBase + "\\" + filename + ".jpg", m.Bitmap, 100);
                errorTimer.Stop();
            }
            catch (Exception E)
            {
                MessageBox.Show("Lỗi file ảnh");
                Console.WriteLine(E.ToString());
                File.WriteAllText(@urlBase + "\\" + "Error" + DateTime.Now.ToString("yyyyMMdd_hhmmss") + ".txt", filename);
            }

            // Hiển thị ảnh ra Form
            if (!m.IsEmpty) imageBox1.Image = m;
            if (!Directory.Exists(@txtSavePos.Text))
                Directory.CreateDirectory((@txtSavePos.Text));
        }

        private async void Wait100MiliSeconds()
        {
            await Task.Delay(100);
        }

        // Chuyển đổi ảnh qua Jpg, lưu vào đường dẫn
        private void saveJpeg(string path, Bitmap img, long quality)
        {
            // Encoder parameter for image quality

            EncoderParameter qualityParam = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);

            // Jpeg image codec
            ImageCodecInfo jpegCodec = this.getEncoderInfo("image/jpeg");

            if (jpegCodec == null)
                return;

            EncoderParameters encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = qualityParam;

            img.Save(path, jpegCodec, encoderParams);
        }

        private ImageCodecInfo getEncoderInfo(string mimeType)
        {
            // Get image codecs for all image formats
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();

            // Find the correct image codec
            for (int i = 0; i < codecs.Length; i++)
                if (codecs[i].MimeType == mimeType)
                    return codecs[i];
            return null;
        }

        // Đóng form thì Reset trạng thái kết nối Barcode về 0
        private void Kefico001_FormClosing(object sender, FormClosingEventArgs e)
        {
            plcKefico.SetDevice("M3012", 0);
        }
    }
}
