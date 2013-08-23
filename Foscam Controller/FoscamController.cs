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
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace Foscam_Controller
{
    public class IPCameraController : INotifyPropertyChanged
    {
        public enum PTZAction
        {
            Up = 0,
            Up_Stop = 1,
            Down = 2,
            Down_Stop = 3,
            Left = 4,
            Left_Stop = 5,
            Right = 6,
            Right_Stop = 7,
            Center = 25,
            Patrol_Vertical = 26,
            Patrol_Vertical_Stop = 27,
            Patrol_Horizon = 28,
            Patrol_Horizon_Stop = 29,
            IO_High = 94,
            IO_Low = 95,
        }

        public enum DisplayResolution
        {
            QVGA = 8,
            VGA = 32,
        }

        public enum DisplayMode
        {
            HZ_50 = 0,
            HZ_60 = 1,
            Outdoor = 2,
        }

        public enum DisplayAlteration
        {
            Default = 0,
            Flip = 1,
            Mirror = 2,
            FlipAndMirror = 3,
        }

        string UserName = "";
        string Password = "";
        string BaseURL = "";

        bool fNoCommit = false;

        public IPCameraController(string BaseURL, string UserName, string Password)
        {
            this.BaseURL = BaseURL;
            this.UserName = UserName;
            this.Password = Password;

            LoadVariables();
        }

        void LoadVariables()
        {
            Req("get_camera_params.cgi?", (vars) =>
                    {
                        // TODO:  low risk concurrency issue - need Req lock so that we don't drop other requests
                        // because we're parsing initial values
                        fNoCommit = true;
                        foreach (string s in vars.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            Match m = Regex.Match(s, ".* (.*)=(.*);");
                            if (m.Success)
                            {
                                try
                                {
                                    string key = m.Groups[1].Value;
                                    int value = int.Parse(m.Groups[2].Value);
                                    switch (key)
                                    {
                                        case "resolution":
                                            Resolution = (DisplayResolution)value;
                                            break;
                                        case "brightness":
                                            Brightness = value;
                                            break;
                                        case "contrast":
                                            Contrast = value;
                                            break;
                                        case "mode":
                                            ImageMode = (DisplayMode)value;
                                            break;
                                        case "flip":
                                            ImageAlteration = (DisplayAlteration)value;
                                            break;
                                        default:
                                            Debug.WriteLine("Invalid variable: " + s);
                                            break;
                                    }
                                }
                                catch (FormatException)
                                {
                                    Debug.WriteLine("Variable not integer: " + s);
                                }
                            }
                            else
                            {
                                Debug.WriteLine("Unrecognized variable: " + s);
                            }
                        }
                        fNoCommit = false;
                    });
        }

        public string GetSnapshotURL()
        {
            return string.Format("{0}/snapshot.cgi?user={1}&pwd={2}",
                BaseURL, UserName, Password);
        }

        public string GetMJpegURL()
        {
            return string.Format("{0}/videostream.cgi?user={1}&pwd={2}",
                BaseURL, UserName, Password);
        }

        void Req(string req)
        {
            Req(req, (ret) => { Debug.WriteLine("Ret: " + ret); });
        }

        void Req(string req, Action<string> callback)
        {
            if (fNoCommit) return;

            WebClient wc = new WebClient();

            Uri url = new Uri(string.Format("{0}/{1}&user={2}&pwd={3}&r={4}",
                BaseURL, req, UserName, Password,
                new Random().Next(0, int.MaxValue)));
            Debug.WriteLine("URL: " + url);
            wc.DownloadStringAsync(url);
            wc.DownloadStringCompleted += (o,e) =>
                {
                    
                    if (e.Error != null)
                    {
                        Debug.WriteLine(e.Error);
                    }
                    else
                    {
                        callback(e.Result);
                        Debug.WriteLine(req + " => " + e.Result);
                    }
                };
        }

        /*
         * And here are the URL commands for the PRESET function:
            decoder_control.cgi?command=30 = Set preset 0
            decoder_control.cgi?command=31 = Go preset 0
            decoder_control.cgi?command=32 = Set preset 1
            decoder_control.cgi?command=33 = Go preset 1
            decoder_control.cgi?command=34 = Set preset 2
            decoder_control.cgi?command=35 = Go preset 2
            decoder_control.cgi?command=36 = Set preset 3
            decoder_control.cgi?command=37 = Go preset 3
            And the list goes further until preset 16 */

        int GetPresetCommand(int preset, bool IsGet = true)
        {
            if (preset < 0 || preset > 16) throw new ArgumentException("preset must be between 0 and 16");

            return 30 + (preset * 2) + (IsGet ? 1 : 0);
        }

        public void JumpPtz(int preset)
        {
            DecoderControl(GetPresetCommand(preset));
        }

        public void SetPtzJump(int preset)
        {
            DecoderControl(GetPresetCommand(preset, false));
        }

        void DecoderControl(int cmd, bool OneStep = false)
        {
            string c = "decoder_control.cgi?command=" + cmd;
            if (OneStep)
            {
                c += "&onestep=20";
            }
            Req(c); // NOTE: single step param: "&onestep=20";
        }

        void CameraControl(int param, int value)
        {
            Req("camera_control.cgi?param=" + param + "&value=" + value);
        }

        // 0-255
        int _Brightness = 0;
        public int Brightness
        {
            get
            {
                return _Brightness;
            }
            set
            {
                _Brightness = value;
                PropertyChanged(this, new PropertyChangedEventArgs("Brightness"));
                CameraControl(1, _Brightness);
            }
        }

        // 0-6
        int _Contrast = 0;
        public int Contrast
        {
            get
            {
                return _Contrast;
            }
            set
            {
                _Contrast = value;
                PropertyChanged(this, new PropertyChangedEventArgs("Contrast"));
                CameraControl(2, _Contrast);
            }
        }

        DisplayResolution _Resolution = DisplayResolution.QVGA;
        public DisplayResolution Resolution
        {
            get
            {
                return _Resolution;
            }
            set
            {
                _Resolution = value;
                PropertyChanged(this, new PropertyChangedEventArgs("Resolution"));
                CameraControl(0, (int)_Resolution);
            }
        }

        DisplayMode _ImageMode = DisplayMode.HZ_50;
        public DisplayMode ImageMode
        {
            get
            {
                return _ImageMode;
            }
            set
            {
                _ImageMode = value;
                PropertyChanged(this, new PropertyChangedEventArgs("ImageMode"));
                CameraControl(1, (int)_ImageMode);
            }
        }

        DisplayAlteration _ImageAlteration = DisplayAlteration.Default;
        public DisplayAlteration ImageAlteration
        {
            get
            {
                return _ImageAlteration;
            }
            set
            {
                _ImageAlteration = value;
                PropertyChanged(this, new PropertyChangedEventArgs("ImageAlteration"));
                CameraControl(5, (int)_ImageAlteration);
            }
        }

        public void PTZ(PTZAction action)
        {
            // Note:  for the phone, OneStep seems better.
            DecoderControl((int)action, true);
        }

        public void Reboot()
        {
            Req("reboot.cgi");
        }

        public event PropertyChangedEventHandler PropertyChanged = delegate { };
    }
}
