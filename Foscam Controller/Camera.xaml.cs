using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Phone.Controls;
using DavuxLibSL;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using System.IO;
using MjpegProcessor;
using System.Threading;
using Microsoft.Phone.Shell;
using Microsoft.Xna.Framework.Media;

namespace Foscam_Controller
{
    public partial class Camera : PhoneApplicationPage
    {
        ProgressIndicator tray;
        Thread _lag;
        int frames = 0;

        public Camera()
        {
            InitializeComponent();

            OrientationChanged += (_, e) => OnOrientationChanged(e.Orientation);
        }

        void OnOrientationChanged(PageOrientation ori)
        {
            TitlePanel.Visibility = ptzGrid.Visibility = sliderGrid.Visibility =
            ((ori & PageOrientation.Landscape) == PageOrientation.Landscape) ? 
            System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        }

        protected override void OnNavigatedFrom(System.Windows.Navigation.NavigationEventArgs e)
        {
            // App.ViewModel.SelectedCamera.Stop();
            _lag = null;
            base.OnNavigatedFrom(e);
        }

        protected override void OnNavigatedTo(System.Windows.Navigation.NavigationEventArgs e)
        {
            tray = SystemTray.ProgressIndicator;

            if (tray == null)
            {
                tray = new ProgressIndicator();
            }
            tray.IsIndeterminate = false;
            tray.IsVisible = true;
            SystemTray.SetProgressIndicator(this, tray);

            string title = null;
            if (NavigationContext.QueryString.TryGetValue("Title", out title))
            {
                // NOTE:  This is the entry-point for deep-linked tiles.
                // we will load up the ViewModel *now*
                var camera = App.ViewModel.Cameras.FirstOrDefault(x => x.Title.ToLower() == title.ToLower());
                if (camera != null)
                {
                    App.ViewModel.SelectedCamera = camera;
                }
                else
                {
                    MessageBox.Show("Couldn't find selected camera (did you delete it?): " + title);

                    throw new AppQuitException();
                }
            }

            DataContext = App.ViewModel.SelectedCamera;

            var buttons = new Button[] { ptzUp, ptzDown, ptzLeft, ptzRight };

            // FI8918W
            var actions = new IPCameraController.PTZAction[] { 
                IPCameraController.PTZAction.Up, IPCameraController.PTZAction.Down,
                IPCameraController.PTZAction.Right, IPCameraController.PTZAction.Left 
            };
            var sactions = new IPCameraController.PTZAction[] { 
                IPCameraController.PTZAction.Up_Stop, IPCameraController.PTZAction.Down_Stop,
                IPCameraController.PTZAction.Right_Stop, IPCameraController.PTZAction.Left_Stop 
            };

            if (App.ViewModel.SelectedCamera.GetConfig().Version == CameraObject.CameraVersion.FI8908W)
            {
                // FI8908W
                actions = new IPCameraController.PTZAction[] { 
                    IPCameraController.PTZAction.Up, IPCameraController.PTZAction.Down,
                    IPCameraController.PTZAction.Left, IPCameraController.PTZAction.Right 
                };
                sactions = new IPCameraController.PTZAction[] { 
                    IPCameraController.PTZAction.Up_Stop, IPCameraController.PTZAction.Down_Stop,
                    IPCameraController.PTZAction.Left_Stop, IPCameraController.PTZAction.Right_Stop 
                };
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                int j = i;
                buttons[i].ManipulationStarted += (_, __) => App.ViewModel.SelectedCamera.Controller.PTZ(actions[j]);
                buttons[i].ManipulationCompleted += (_, __) => App.ViewModel.SelectedCamera.Controller.PTZ(sactions[j]);

                buttons[i].Padding = buttons[i].Margin = new Thickness(0);
                buttons[i].Height = buttons[i].Width = 80;
                buttons[i].BorderThickness = new Thickness(0);
            }


            var cam = App.ViewModel.SelectedCamera.URL;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                // Beacon.AttachBeacon(cam);
            });


            App.ViewModel.SelectedCamera.OnFrame += frame =>
            {
                imgCamera.Source = frame;
                tray.Value = 0;
                frames++;
            };
            App.ViewModel.SelectedCamera.Start();

            /*
            new Timer(x => {
                Dispatcher.BeginInvoke(() =>
                {
                    double v = tray.Value;
                    v = v + .1;
                    if (v <= 1.0 && frames == 0)
                    {
                        tray.Value = v;
                    }
                    if (tray.Text != frames + "fps")
                    {
                        tray.Text = frames + "fps";
                    }
                    frames = 0;
                });
                
            }, null, 0, 1000);
            */
            
            
            _lag = new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(950);

                    // we've been killed.
                    if (_lag == null) return;
                    if (_lag != Thread.CurrentThread) return;

                    Dispatcher.BeginInvoke(() =>
                    {
                        double v = tray.Value;
                        v = v + .1;
                        if (v <= 1.0 && frames == 0)
                        {
                            tray.Value = v;
                        }
                        if (tray.Text != frames + "fps")
                        {
                            tray.Text = frames + "fps";
                        }
                        frames = 0;
                    });
                }
            });
            _lag.Start();
            

            OnOrientationChanged(Orientation);
            base.OnNavigatedTo(e);
        }

        private void PTZPreset_Click(object sender, RoutedEventArgs e)
        {
            int preset = int.Parse((sender as Button).Content as string);
            App.ViewModel.SelectedCamera.Controller.JumpPtz(preset);
        }

        private void PTZPreset_Hold(object sender, System.Windows.Input.GestureEventArgs e)
        {
            int preset = int.Parse((sender as Button).Content as string);
            if (MessageBox.Show(string.Format(
                "Are you sure you wish to set Preset {0} to this location?", preset)
                , "Overwrite Preset", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
            {
                App.ViewModel.SelectedCamera.Controller.SetPtzJump(preset);
            }
        }

        private void btnSnapshot_Click(object sender, EventArgs e)
        {
            try
            {
                MediaLibrary library = new MediaLibrary();
                library.SavePicture("snapshot-" + App.ViewModel.SelectedCamera.Title + "-" + DateTime.Now.ToShortTimeString(), App.ViewModel.SelectedCamera.GetFrame());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Couldn't save to image library: " + ex.Message);
            }
        }

        private void btnActions_Click(object sender, EventArgs e)
        {
            Phone.Controls.PickerBoxDialog dialog = new Phone.Controls.PickerBoxDialog();

            var map = new Dictionary<string, IPCameraController.PTZAction>{
                {"Go to Center", IPCameraController.PTZAction.Center},
                {"Patrol Horizon", IPCameraController.PTZAction.Patrol_Horizon},
                {"[Stop] Patrol Horizon", IPCameraController.PTZAction.Patrol_Horizon_Stop},
                {"Patrol Vertical", IPCameraController.PTZAction.Patrol_Vertical},
                {"[Stop] Patrol Vertical", IPCameraController.PTZAction.Patrol_Vertical_Stop},
                {"IR LEDs Off", IPCameraController.PTZAction.IO_High},
                {"IR LEDs On", IPCameraController.PTZAction.IO_Low},
            };

            dialog.Title = "Actions";
            dialog.ItemSource = map.Keys;

            dialog.SelectedIndex = -1;
            dialog.Closed += (_, __) => App.ViewModel.SelectedCamera.Controller.PTZ(map[dialog.SelectedItem.ToString()]);

            dialog.Show();
        }

        private void btnResolution_Click(object sender, EventArgs e)
        {
            Phone.Controls.PickerBoxDialog dialog = new Phone.Controls.PickerBoxDialog();

            var map = new Dictionary<string, IPCameraController.DisplayResolution>{
                {"640x480 @ 15fps", IPCameraController.DisplayResolution.VGA},
                {"320x240 @ 30fps", IPCameraController.DisplayResolution.QVGA},
            };

            dialog.Title = "Camera Resolution";
            dialog.ItemSource = map.Keys;

            int i = 0, index = -1;
            foreach (var item in map)
            {
                if (item.Value == App.ViewModel.SelectedCamera.Controller.Resolution)
                {
                    index = i;
                    break;
                }
                i++;
            }

            // sigh, can't set SelectedItem before ListBox exists...
            dialog.SelectedIndex = index;
            dialog.Closed += (_, __) => App.ViewModel.SelectedCamera.Controller.Resolution = map[dialog.SelectedItem.ToString()];

            dialog.Show();
        }

        private void btnDisplayMode_Click(object sender, EventArgs e)
        {
            Phone.Controls.PickerBoxDialog dialog = new Phone.Controls.PickerBoxDialog();

            var map = new Dictionary<string, IPCameraController.DisplayMode>{
                {"50hz", IPCameraController.DisplayMode.HZ_50},
                {"60hz", IPCameraController.DisplayMode.HZ_60},
                {"Outdoor", IPCameraController.DisplayMode.Outdoor},
            };

            dialog.Title = "Display Mode";
            dialog.ItemSource = map.Keys;

            int i = 0, index = -1;
            foreach (var item in map)
            {
                if (item.Value == App.ViewModel.SelectedCamera.Controller.ImageMode)
                {
                    index = i;
                    break;
                }
                i++;
            }

            // sigh, can't set SelectedItem before ListBox exists...
            dialog.SelectedIndex = index;
            dialog.Closed += (_, __) => App.ViewModel.SelectedCamera.Controller.ImageMode = map[dialog.SelectedItem.ToString()];

            dialog.Show();
        }

        private void btnDisplayAlteration_Click(object sender, EventArgs e)
        {
            Phone.Controls.PickerBoxDialog dialog = new Phone.Controls.PickerBoxDialog();

            var map = new Dictionary<string, IPCameraController.DisplayAlteration>{
                {"Default", IPCameraController.DisplayAlteration.Default},
                {"Flip", IPCameraController.DisplayAlteration.Flip},
                {"Mirror", IPCameraController.DisplayAlteration.Mirror},
                {"Flip And Mirror", IPCameraController.DisplayAlteration.FlipAndMirror},
            };

            dialog.Title = "Image Alteration";
            dialog.ItemSource = map.Keys;

            int i = 0, index = -1;
            foreach (var item in map)
            {
                if (item.Value == App.ViewModel.SelectedCamera.Controller.ImageAlteration)
                {
                    index = i;
                    break;
                }
                i++;
            }

            // sigh, can't set SelectedItem before ListBox exists...
            dialog.SelectedIndex = index;
            dialog.Closed += (_, __) => App.ViewModel.SelectedCamera.Controller.ImageAlteration = map[dialog.SelectedItem.ToString()];

            dialog.Show();
        }

        private void btnReboot_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to reboot " + App.ViewModel.SelectedCamera.Title + "?", "Reboot Camera", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
            {
                App.ViewModel.SelectedCamera.Controller.Reboot();
            }
        }
    }
}