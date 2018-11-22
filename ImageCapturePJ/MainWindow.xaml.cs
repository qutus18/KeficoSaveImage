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
using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace ImageCapturePJ
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        WebcamObject mainWebcam = new WebcamObject(0);
        public MainWindow()
        {
            InitializeComponent();
            mainWebcam.Start();
        }

        private void ButtonStart_Click(object sender, RoutedEventArgs e)
        {
            if (mainWebcam.CameraStopped) mainWebcam.Start();
            imageDisplay.Source = BitmapSourceConvert.ToBitmapSource(mainWebcam.GetMatImage());
        }

        private void ButtonStop_Click(object sender, RoutedEventArgs e)
        {
            mainWebcam.Stop();
        }
    }
}
