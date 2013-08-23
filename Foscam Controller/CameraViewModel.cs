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
using DavuxLibSL;
using System.Diagnostics;
using System.ComponentModel;
using Microsoft.Phone.Shell;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Imaging;

namespace Foscam_Controller
{

    public class CameraViewModel : INotifyPropertyChanged
    {
        public event Action<System.Windows.Media.Imaging.BitmapImage> OnFrame = delegate { };

        private string _Title = "";
        public string Title
        {
            get { return _Title; }
            set
            {
                if (value != _Title)
                {
                    _Title = value;
                    Change("Title");
                }
            }
        }

        private string _Username = "";
        public string Username
        {
            get { return _Username; }
            set
            {
                if (value != _Username)
                {
                    _Username = value;
                    Change("Username");
                }
            }
        }

        private string _Password = "";
        public string Password
        {
            get { return _Password; }
            set
            {
                if (value != _Password)
                {
                    _Password = value;
                    Change("Password");
                }
            }
        }

        private string _URL = "";
        public string URL
        {
            get { return _URL; }
            set
            {
                if (value != _URL)
                {
                    _URL = value;
                    Change("URL");
                }
            }
        }

        private bool _IsRunning = false;
        public bool IsRunning
        {
            get { return _IsRunning; }
            set
            {
                if (value != _IsRunning)
                {
                    _IsRunning = value;
                    Change("IsRunning");
                }
            }
        }

        public string PinState
        {
            get
            {
                Uri tileUri = new Uri(Uri.EscapeUriString(String.Format("/Camera.xaml?Title={0}",
                    Title)), UriKind.Relative);

                var tile = ShellTile.ActiveTiles.FirstOrDefault(t => t.NavigationUri.ToString() ==
                    tileUri.ToString());
                return tile == null ? "Pin" : "Unpin";
            }
        }

        public IPCameraController Controller { get; set; }

        private Connection Link { get; set; }
        private CameraObject Config = null;

        public CameraViewModel(CameraObject conf)
        {
            SetConfig(conf);
        }

        public void SetConfig(CameraObject conf)
        {
            Title = conf.Title;
            URL = conf.URL;
            Username = conf.Username;
            Password = conf.Password;

            Config = conf;
            Config.Update = this;

            Controller = new IPCameraController(URL, Username, Password);

            if (IsRunning)
            {
                Stop();
                Start();
            }

            if (string.IsNullOrEmpty(conf.Timezone))
            {
                // FIXME:  we shouldn't have this problem, but possibly still do.
                conf.Timezone = "America/New_York";
            }

            // for com.davux.FoscamController, options is defined as:
            // <Camera snapshot URL> [<PHP Timezone> <Tile Link Page>]
            // just URL is v1 appcompat

            PushNotifications.RegisterApp(PushNotifications.ChannelName, Title,
            string.Format("{0} {1} {2}", Controller.GetSnapshotURL(),
            conf.Timezone, new Uri(Uri.EscapeUriString(String.Format("/Camera.xaml?Title={0}",
               conf.Title)), UriKind.Relative).ToString()),
            (success, message) =>
            {
                Debug.WriteLine("RegisterApp: " + success + " " + message);
            });
        }

        public CameraObject GetConfig()
        {
            return Config;
        }

        public byte[] GetFrame()
        {
            return Link.CurrentFrame;
        }

        public BitmapImage LastFrame
        {
            get
            {
                return (Link == null) ? null : Link.LastFrame;
            }
        }

        public void ChangePin()
        {
            Uri tileUri = new Uri(Uri.EscapeUriString(String.Format("/Camera.xaml?Title={0}",
               Title)), UriKind.Relative);

            var tile = ShellTile.ActiveTiles.FirstOrDefault(t => t.NavigationUri.ToString() ==
                tileUri.ToString());
            if (tile == null)
            {
                StandardTileData initialData = new StandardTileData()
                {
                    BackgroundImage = new Uri("Images/Clear.png", UriKind.Relative),
                    Title = Title,
                };

                try
                {
                    ShellTile.Create(tileUri, initialData);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "Error creating tile", MessageBoxButton.OK);
                }
            }
            else
            {
                tile.Delete();
            }
            Change("PinState");
        }

        int keyFrame = 0;

        public void Start()
        {
            if (Link == null)
            {
                IsRunning = true;
                Link = new Connection();

                
                Link.OnFrame += frame =>
                    {
                        OnFrame(frame);
                        keyFrame++;
                        if (keyFrame % 50 == 0)
                        {
                           // Change("LastFrame");
                        }
                    };
                Link.Start(Controller);
            }
            else
            {
                Debug.WriteLine("Error: Link alraedy running!");
            }
        }

        public void Stop()
        {
            if (Link != null)
            {
                IsRunning = false;
                Link.Stop();
                Link = null;
            }
        }

        public void Change(string property)
        {
            var pc = PropertyChanged;
            if (pc != null) pc(this, new PropertyChangedEventArgs(property));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
