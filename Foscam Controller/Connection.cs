using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Media.Imaging;
using MjpegProcessor;
using System.Diagnostics;

namespace Foscam_Controller
{
    public class Connection
    {
        MjpegDecoder _mjpeg = null;
        int Frames = 0;
        private Beacon beacon = null;

        public event Action<BitmapImage> OnFrame;
        public event Action<string> OnError;

        public BitmapImage LastFrame { get; private set; }
       
        public Connection()
        {
            _mjpeg = new MjpegDecoder();
            _mjpeg.FrameReady += (s, e) =>
                {
                    LastFrame = e.BitmapImage;
                    OnFrame(e.BitmapImage);
                    GotFrame();
                };
        }

        void ResetLag()
        {
            // TODO lag meter
        }

        void GotFrame()
        {
            ResetLag();
            ++Frames;

        }

        public void Start(IPCameraController controller)
        {
            try
            {
                _mjpeg.ParseStream(new Uri(controller.GetMJpegURL()));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                OnError(ex.ToString());
            }

            beacon = Beacon.Attach(controller.GetMJpegURL());
        }

        public void Stop()
        {
            _mjpeg.StopStream();
            beacon.Detach();
        }


        public byte[] CurrentFrame
        {
            get
            {
                return _mjpeg.CurrentFrame;
            }
        }
    }
}
