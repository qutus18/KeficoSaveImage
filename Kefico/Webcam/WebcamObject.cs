using Emgu.CV;
using System;
using System.Threading.Tasks;
using System.Timers;

namespace Kefico
{
    public class WebcamObject
    {
        public delegate void CaptureEventHandle(int x);
        public delegate void CameraFail(int x);
        public static VideoCapture _videoCapture;
        private Timer timerCamera;
        private int _videoFrame = 30;
        private int _cameraNumber;
        private Mat _currentFrame;
        private Mat _frameImage;
        private bool _runCapture = false;
        private int _currentFrameNumber = 0;
        private bool _cameraStopped;
        private bool _needCreat;
        private bool lockOn;

        public event CaptureEventHandle imageCaptured;
        public event CameraFail cameraFailEvent;

        /// <summary>
        /// Khởi tạo Camera
        /// Khai báo timer lấy ảnh, chu kỳ 1000/Frame
        /// </summary>
        /// <param name="cameraNumber"></param>
        /// <param name="frameCapture"></param>
        public WebcamObject(int cameraNumber, int frameCapture)
        {
            _videoFrame = frameCapture;
            _cameraNumber = cameraNumber;
            //SetupCapture(_cameraNumber);
            timerCamera = new Timer();
            timerCamera.Interval = (1000 / _videoFrame);
            timerCamera.Elapsed += CaptureFromCamera;
            timerCamera.Start();
            //_videoCapture.ImageGrabbed += ProcessImage;
            _needCreat = true;
            _cameraStopped = true;
        }

        /// <summary>
        /// Hiện tại không dùng
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ProcessImage(object sender, EventArgs e)
        {
            if (_currentFrameNumber > 0)
            {
                _currentFrame = _frameImage;
            }
            _frameImage = new Mat();
            Console.WriteLine("");
            Console.WriteLine("Start" + _currentFrameNumber.ToString());
            _videoCapture.Retrieve(_frameImage);
            Console.WriteLine("");
            Console.WriteLine("Done" + _currentFrameNumber.ToString());
            _currentFrameNumber += 1;
            if (imageCaptured != null)
            {
                imageCaptured(1);
            }
        }

        /// <summary>
        /// Khởi tạo Camera, kích thước ảnh tối đa 1300x1300
        /// </summary>
        /// <param name="cameraNumber"></param>
        private void SetupCapture(int cameraNumber)
        {
            _videoCapture = new VideoCapture(cameraNumber);
            _videoCapture.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameHeight, 1300);
            _videoCapture.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameWidth, 1300);
        }

        /// <summary>
        /// Khởi tạo Camera với Frame mặc định là 20
        /// </summary>
        /// <param name="cameraNumber"></param>
        public WebcamObject(int cameraNumber) : this(cameraNumber, 20)
        {
        }

        /// <summary>
        /// Hàm chụp ảnh gọi từ timer
        /// Nếu trạng thái Camera là đang chụp, và khóa lock không ON thì tiến hành chụp
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CaptureFromCamera(object sender, ElapsedEventArgs e)
        {
            if ((_runCapture) && (!lockOn))
            {
                // Bật khóa LogCamera
                lockOn = true;
                // Nếu đã chụp được ảnh, thì chuyển lưu Frame hiện tại lại
                if (_currentFrameNumber > 0)
                {
                    _currentFrame = _frameImage;
                }
                // Khởi tạo lại Frame 
                _frameImage = new Mat();
                Console.WriteLine("");
                Console.WriteLine("Start" + _currentFrameNumber.ToString());
                // Lấy Frame từ Camera
                bool ireturn = _videoCapture.Retrieve(_frameImage);
                Console.WriteLine("");
                Console.WriteLine("Done" + _currentFrameNumber.ToString());
                // Nếu kết quả trả về ireturn = false thì nghĩa là ảnh chụp bị lỗi
                if (ireturn)
                {
                    _currentFrameNumber += 1;
                    imageCaptured?.Invoke(1);
                }
                else
                {
                    cameraFailEvent?.Invoke(CurrentFrameNumber);
                    _currentFrameNumber = 0;
                    Console.WriteLine("Capture Result : Fail to get Image!");
                }
                lockOn = false;
            }
            else if (lockOn) Console.Write("Lock--");
        }

        /// <summary>
        /// Số Frame chụp được hiện tại
        /// </summary>
        public int CurrentFrameNumber
        {
            get { return _currentFrameNumber; }
            private set { }
        }

        /// <summary>
        /// Khởi tạo Camera
        /// Chụp 1 ảnh ban đầu
        /// </summary>
        public void Start()
        {
            // Thêm Log ở đây
            if (_needCreat)
            {
                _needCreat = false;
                SetupCapture(_cameraNumber);
                _videoCapture.Start();
                _frameImage = _videoCapture.QueryFrame();
            }
            _cameraStopped = false;
            _runCapture = true;
            _currentFrameNumber = 0;
        }

        /// <summary>
        /// Đóng Camera? Đợi đến khi chạy xong lockOn = false thì đóng và hủy đổi tượng _video Capture
        /// Bật các giá trị cameraStopped và needCreat
        /// </summary>
        public async void Stop()
        {
            _runCapture = false;
            while (lockOn)
            {
                Console.Write("-");
                await Task.Delay(100);
            }
            _videoCapture.Stop();
            _videoCapture.Dispose();
            _needCreat = true;
            _cameraStopped = true;
            Console.WriteLine("STOPPED");
        }

        /// <summary>
        /// Trả về ảnh hiện tại lấy được
        /// </summary>
        /// <returns></returns>
        public Mat GetMatImage()
        {
            return _currentFrame;
        }

        /// <summary>
        /// Kiểm tra trạng thái Stop Camera
        /// </summary>
        public bool CameraStopped
        {
            get { return _cameraStopped; }
            private set { }
        }
    }

}
