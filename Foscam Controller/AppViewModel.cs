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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using DavuxLibSL;
using System.Collections.Generic;

namespace Foscam_Controller
{
    public class AppViewModel
    {
        public ObservableCollection<CameraViewModel> Cameras { get; set; }
        public CameraViewModel SelectedCamera { get; set; }

        public List<string> Timezones { get; set; }

        public AppViewModel()
        {
            Cameras = new ObservableCollection<CameraViewModel>();

            PushNotifications.RemoveOldChannel("com.davux.FoscamController3");
            PushNotifications.OnToastNotification += (title, msg) =>
            {
                Debug.WriteLine("Toast: " + msg);
            };
            PushNotifications.ErrorOccurred += msg => MessageBox.Show("Push Notification error: " + msg);
            PushNotifications.ChannelName = "com.davux.FoscamController";
            PushNotifications.Initialize();
            PushNotifications.TryEnableOnce();

            var SavedConfig = Settings.Get<List<CameraObject>>("cams", null);

            if (SavedConfig == null)
            {
                var NewConfig = new List<CameraObject>();
                NewConfig.Add(new CameraObject
                {
                    URL = "http://example.com",
                    Title = "Dave-Home",
                    Timezone = "America/New_york",
                    Password = "dave",
                    Username = "pwd",
                    Version = CameraObject.CameraVersion.FI8918W,
                });
                NewConfig.Add(new CameraObject
                {
                    URL = "http://example.com:8080",
                    Title = "Other",
                    Timezone = "America/Los_Angeles",
                    Username = "admin",
                    Password = "pwd",
                    Version = CameraObject.CameraVersion.FI8918W,
                });

                Settings.Set("cams", NewConfig);
                SavedConfig = Settings.Get<List<CameraObject>>("cams", null);
            }

            if (SavedConfig == null)
            {

                // Cameras.Add(new CameraViewModel { Title = "TEST", });
            }
            else
            {
                foreach (var config in SavedConfig)
                {
                    Cameras.Add(new CameraViewModel(config));
                }
            }

            Timezones = new List<string> {
                "America/New_York",
                "America/Los_Angeles"
            };
        }

        public void Save()
        {
            var co = new List<CameraObject>();
            foreach (var cam in Cameras)
            {
                co.Add(cam.GetConfig());
            }
            Settings.Set("cams",co);
        }

        public CameraObject SettingsContext { get; set; }
        public void SaveContext()
        {
            if (SettingsContext.Update != null)
            {
                SettingsContext.Update.SetConfig(SettingsContext);
            }
            else
            {
                Cameras.Add(new CameraViewModel(SettingsContext));
                SettingsContext = null;
            }
            Save();
        }
    }
}
