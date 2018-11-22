using Emgu.CV;
using Emgu.CV.CvEnum;
using System.Windows.Controls;
using System;
using System.Timers;

namespace ImageCapturePJ
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

        public event CaptureEventHandle imageCaptured;

        public WebcamObject(int cameraNumber, int frameCapture)
        {
            _videoFrame = frameCapture;
            _cameraNumber = cameraNumber;
            SetupCapture(_cameraNumber);
            timerCamera = new Timer();
            timerCamera.Interval = (1000 / _videoFrame);
            timerCamera.Elapsed += CaptureFromCamera;
            timerCamera.Start();
            _needCreat = false;
            _cameraStopped = true;
        }

        public bool CameraStopped
        {
            get { return _cameraStopped; }
            private set { }
        }

        private void SetupCapture(int cameraNumber)
        {
            _videoCapture = new VideoCapture(cameraNumber);
            _videoCapture.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameHeight, 4000);
            _videoCapture.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameWidth, 4000);
        }

        public WebcamObject(int cameraNumber) : this(cameraNumber, 30)
        {
        }

        private void CaptureFromCamera(object sender, ElapsedEventArgs e)
        {
            if (_runCapture)
            {
                if (_currentFrameNumber > 0) _currentFrame = _frameImage;
                _frameImage = _videoCapture.QueryFrame();
                _currentFrameNumber += 1;
                if (imageCaptured != null)
                {
                    imageCaptured(_videoCapture);
                }
            }
        }


        public void Start()
        {
            if (_needCreat)
            {
                SetupCapture(_cameraNumber);
            }
            _runCapture = true;
            _currentFrameNumber = 0;
            _cameraStopped = false;
        }

        public void Stop()
        {
            _runCapture = false;
            _videoCapture.Dispose();
            _needCreat = true;
            _cameraStopped = true;
        }

        public Mat GetMatImage()
        {
            return _currentFrame;
        }
    }

}
