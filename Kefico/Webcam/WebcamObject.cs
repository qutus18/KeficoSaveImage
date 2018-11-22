using Emgu.CV;
using System;
using System.Timers;


namespace Kefico
{
    public class WebcamObject
    {
        public delegate void CaptureEventHandle(VideoCapture x);
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
                imageCaptured(_videoCapture);
            }
        }

        public bool CameraStopped
        {
            get { return _cameraStopped; }
            private set { }
        }

        private void SetupCapture(int cameraNumber)
        {
            _videoCapture = new VideoCapture(cameraNumber);
            _videoCapture.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameHeight, 1300);
            _videoCapture.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameWidth, 1300);
        }

        public WebcamObject(int cameraNumber) : this(cameraNumber, 20)
        {
        }

        private void CaptureFromCamera(object sender, ElapsedEventArgs e)
        {
            if ((_runCapture)&&(!lockOn))
            {
                lockOn = true;
                if (_currentFrameNumber > 0)
                {
                    _currentFrame = _frameImage;
                }
                _frameImage = new Mat();
                Console.WriteLine("");
                Console.WriteLine("Start" + _currentFrameNumber.ToString());
                //_frameImage = _videoCapture.QueryFrame();
                _videoCapture.Retrieve(_frameImage);
                Console.WriteLine("");
                Console.WriteLine("Done" + _currentFrameNumber.ToString());
                _currentFrameNumber += 1;
                if (imageCaptured != null)
                {
                    imageCaptured(_videoCapture);
                }
                lockOn = false;
            }
            else Console.Write("p-");
        }

        public int CurrentFrame
        {
            get { return _currentFrameNumber; }
            private set { }
        }

        public void Start()
        {
            if (_needCreat)
            {
                SetupCapture(_cameraNumber);
                _videoCapture.Start();
                _frameImage = _videoCapture.QueryFrame();
                _needCreat = false;
            }
            _runCapture = true;
            _currentFrameNumber = 0;
            _cameraStopped = false;
        }

        public void Stop()
        {
            _runCapture = false;
            while (lockOn) Console.Write("-");
            _videoCapture.Stop();
            _videoCapture.Dispose();
            _needCreat = true;
            _cameraStopped = true;
            Console.WriteLine("STOPPED");
        }

        public Mat GetMatImage()
        {
            return _currentFrame;
        }
    }

}
