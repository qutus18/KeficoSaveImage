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
using ActUtlTypeLib;
using System.Drawing.Imaging;
using System.Timers;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace Kefico
{
    public partial class Kefico001 : Form
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        #region Khai báo
        public delegate void ChangeValuePLC(string Mbit);
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
        private bool captureRunning;
        private int SM400;
        private Mat m;
        private bool cameraRunning;
        private bool inCameraProcess;
        private WebcamObject mainCamera;
        StreamWriter mainWriter;
        System.Timers.Timer errorTimer;
        #endregion

        /// <summary>
        /// Khởi tạo
        /// </summary>
        public Kefico001()
        {
            InitializeComponent();
            InitialSub();
        }

        /// <summary>
        /// Hàm khởi tạo, lấy ra từ hàm Load
        /// </summary>
        private void InitialSub()
        {
            // Đọc giá trị cài đặt Camera
            CameraNumber = int.Parse(ConfigurationSettings.AppSettings["CameraNumber"]);

            // Khởi tạo giá trị mặc định cho string đọc Barcode
            stringReceive = "NoBarcode" + DateTime.Now.ToString("yy_MM_dd_hh_mm_ss");

            // Khởi chạy kết nối PLC
            StartPLCCommunication();

            // Khởi tạo Camera
            mainCamera = new WebcamObject(CameraNumber);

            // Khởi tạo timer đếm thời gian để báo lỗi
            errorTimer = new System.Timers.Timer();
            errorTimer.Interval = 5000;
            errorTimer.Elapsed += SetErrFlag;

            // Test Log
            Console.WriteLine("Hello");
            log.Error("This is my error message");
        }

        /// <summary>
        /// Form load, không sử dụng nữa
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Form1_Load(object sender, EventArgs e)
        {
        }

        /// <summary>
        /// Khởi chạy kết nối PLC
        /// </summary>
        private void StartPLCCommunication()
        {
            // Khai báo Station kết nối PLC
            plcKefico.ActLogicalStationNumber = 11; //3
            plcKefico.Open();

            // Khai báo và chạy Thread PLC : UpdateDataPLC
            threadPLCConnection = new Thread(UpdateDataPLC);
            threadPLCConnection.Name = "PLC";
            threadPLCConnection.IsBackground = true;
            threadPLCConnection.Start();
        }

        private void UpdateDataPLC()
        {
            // Chạy liên tục Thread
            while (true)
            {
                /// Kiểm tra kết nối PLC và Barcode
                /// Nếu chưa kết nối, báo Message, Setbit Error Connect
                #region <Summary> Kiểm tra kết nối PLC Barcode
                // Kiểm tra Bit SM400 để xác nhận PLC có đang kết nối không
                var iret = plcKefico.GetDevice("SM400", out SM400); 
                if (iret != 0)
                {
                    MessageBox.Show(String.Format("0x{0:x8} [HEX]", iret));
                    return;
                }

                // Kiểm tra đã kết nối Barcode hay chưa, nếu chưa thì ResetBit M3012 PLC
                if (btnConnect.Text == "Connected") plcKefico.SetDevice("M3012", 1);
                else plcKefico.SetDevice("M3012", 0);
                #endregion
                #region <Summary> Xử lý khi nhận được tín hiệu chụp ảnh từ PLC
                /// Nếu nhận tín hiệu = 1, và cờ trạng thái captureRunning chưa bật thì chạy hàm CaptureImage
                iret = plcKefico.GetDevice("M3010", out M3010); // Barcode Begin
                if ((M3010 == 1) && (!captureRunning))
                {
                    // Bật cờ báo đang chụp
                    captureRunning = true;
                    // Đặt lại giá trị Barcode mặc định là NoBarcode
                    stringReceive = "NoBarcode" + DateTime.Now.ToString("yy_MM_dd_hh_mm_ss");
                    // Bật bit PLC - M3011 báo đang chụp
                    plcKefico.SetDevice("M3011", 1);
                    // Chạy hàm chụp ảnh 
                    CaptureImage(stringReceive);
                }
                // Reset trạng thái chụp ảnh về chưa chụp
                if ((M3010 == 0) && (captureRunning))
                {
                    // Tắt cờ báo đang chụp khi tín hiệu chụp ảnh từ PLC off
                    captureRunning = false;
                }
                #endregion
                Thread.Sleep(100);
            }
        }

        /// <summary>
        /// Nút nhấn Exit
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        /// <summary>
        /// Nút nhấn Connect
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnConnect_Click(object sender, EventArgs e)
        {
            ConnectIP();
        }

        /// <summary>
        /// Kết nối với Barcode
        /// </summary>
        private void ConnectIP()
        {
            bool bRet;
            // Nếu Socket đang mở thì đóng Socket
            if (socketConnect_one != null)
            {
                socketConnect_one.Close();
            }

            /// Tạo Socket mới, khai báo Ip, tạo địa chỉ kết nối
            socketConnect_one = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var IPAddress_one = IPAddress.Parse(txtIP.Text);
            var IPEndPoint_one = new IPEndPoint(IPAddress_one, 23);

            /// Thử kết nối Socket
            /// Nếu kết nối thành công => Chuyển trạng thái thành Connected
            /// Tạo Thread nhận dữ liệu từ Barcode
            /// Nếu thất bại => Chuyển trạng thái thành Disconnected!
            try
            {
                // Kết nối
                if (!socketConnect_one.Connected) socketConnect_one.Connect(IPEndPoint_one);
                // Xử lý nếu thành công
                if (socketConnect_one.Connected)
                {
                    btnConnect.BackColor = Color.Green;
                    btnConnect.Text = "Connected";
                    if (threadReceive_one == null)
                    {
                        threadReceive_one = new System.Threading.Thread(threadProcessTCP);
                        threadReceive_one.IsBackground = true;
                        threadReceive_one.Start();
                    }
                }
                // Xử lý nếu thất bại
                else
                {
                    btnConnect.BackColor = Color.Red;
                    btnConnect.Text = "Disconnected";
                    bRet = false;
                }
            }
            // Nếu gặp lỗi
            catch
            {
                btnConnect.BackColor = Color.Red;
                btnConnect.Text = "Disconnected";
                btnConnect.Enabled = true;
                bRet = false;
            }

        }

        /// <summary>
        /// Nhận Barcode, nếu message nhận về độ dài > 5 thì xử lý
        /// Lấy dữ liệu QRCode
        /// 
        /// </summary>
        private void threadProcessTCP()
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
                        // Xóa bỏ hết các ký tự thừa
                        stringReceive = stringReceive.Replace("\r\n", "").Replace("\r", "").Replace("\n", "");
                        Invoke(new MethodInvoker(delegate
                        {
                            lblBarcode.Text = stringReceive;
                        }));
                        // Sửa? Phải kiểm tra Camera đã chạy chưa, nếu chưa thì mới Start
                        if (mainCamera.CameraStopped) mainCamera.Start();
                        // Thêm Log và thời gian ở đây!
                    }

                }
                // Nếu có lỗi xảy ra thì chuyển trạng thái button Connect
                catch
                {
                    if (!socketConnect_one.Connected)
                    {
                        btnConnect.BackColor = Color.Red;
                        btnConnect.Text = "Disconnected";
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Cập nhật khi đường dẫn ảnh thay đổi
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void txtSavePos_TextChanged(object sender, EventArgs e)
        {
            posSaveImage = txtSavePos.Text;
        }

        /// <summary>
        /// Gửi Bit Error sang PLC khi timer lỗi ON
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SetErrFlag(object sender, ElapsedEventArgs e)
        {
            plcKefico.SetDevice("M3020", 1);
            errorTimer.Stop();
        }

        /// <summary>
        /// Nút kiểm tra Camera (ẩn)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnTest_Click(object sender, EventArgs e)
        {
            CaptureImage(stringReceive);
        }

        /// <summary>
        /// Hiện tại không sử dụng Setting Camera
        /// </summary>
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
            // Thêm Log ở đây để kiểm tra số lần gọi hàm Camera

            // Nếu filename vẫn đang là mặc định, thì ảnh không có Barcode
            if (filename.IndexOf("Barcode") > 0) filename = "NoBarcode" + DateTime.Now.ToString("yy_MM_dd_hh_mm_ss");

            // chạy Timer đếm lỗi
            errorTimer.Start();

            // Nếu chưa chạy Camera thì gửi lệnh chạy
            if (mainCamera.CameraStopped) mainCamera.Start();

            // Đợi cho đến khi đủ 10Frame
            while (mainCamera.CurrentFrameNumber < 10)
            {
                Console.Write("Camera Working!");
                await Task.Delay(50);
            }

            // Dừng camera
            mainCamera.Stop();

            // Lấy ảnh từ đầu ra của Class Camera
            m = mainCamera.GetMatImage();

            // Lưu  ảnh
            SaveCurrentImage(filename);

            // Hiển thị ảnh ra Form
            if (!m.IsEmpty) imageBox1.Image = m;
            if (!Directory.Exists(@txtSavePos.Text))
                Directory.CreateDirectory((@txtSavePos.Text));
        }

        /// <summary>
        /// Chuyển đổi ảnh đầu ra từ dạng BMP sang JPG, lưu ảnh theo đường dẫn file name
        /// Hiển thị Messagebox nếu lỗi
        /// </summary>
        /// <param name="filename"></param>
        private void SaveCurrentImage(string filename)
        {
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
        }

        /// <summary>
        /// Hàm đợi 0.1 giây
        /// </summary>
        private async void Wait100MiliSeconds()
        {
            await Task.Delay(100);
        }

        /// Chuyển đổi ảnh qua Jpg, lưu vào đường dẫn
        /// 
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

        /// Đóng form thì Reset trạng thái kết nối Barcode về 0
        /// 
        private void Kefico001_FormClosing(object sender, FormClosingEventArgs e)
        {
            plcKefico.SetDevice("M3012", 0);
        }
    }
}
