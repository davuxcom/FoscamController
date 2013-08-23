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
using Microsoft.Xna.Framework.GamerServices;
using System.Diagnostics;
using Microsoft.Phone.Info;
using System.Device.Location;
using System.Threading;
using DavuxLibSL.Extensions;

namespace Foscam_Controller
{
    public class Beacon
    {
        private const string AttachUrl = "http://daveamenta.com/wp7api/beacon.php?attach={0}&tag={1}&data={2}&geo={3}";
        private const string DetatchUrl = "http://daveamenta.com/wp7api/beacon.php?detatch={0}&tag={1}";

        private static string DeviceString = null;

        private static GeoCoordinate CurrentLocation { get; set; }

        private string ResourceTag;
        private bool Detached = false;

        static Beacon()
        {
            CurrentLocation = new GeoCoordinate(-1, -1);
            DeviceString = DeviceStatus.DeviceManufacturer + " " + DeviceStatus.DeviceName;

            var watcher = new GeoCoordinateWatcher(GeoPositionAccuracy.Default)
                   {
                       MovementThreshold = 20
                   };

            watcher.PositionChanged += (_, e) =>
                {
                    CurrentLocation = e.Position.Location;
                    /*
                    // Access the position information thusly:
                    CurrentLocation.Latitude.ToString("0.000");
                    CurrentLocation.Longitude.ToString("0.000");
                    CurrentLocation.Altitude.ToString();
                    CurrentLocation.HorizontalAccuracy.ToString();
                    CurrentLocation.VerticalAccuracy.ToString();
                    CurrentLocation.Course.ToString();
                    CurrentLocation.Speed.ToString();
                    e.Position.Timestamp.LocalDateTime.ToString();
                    */
                };
            watcher.StatusChanged += (_, e) =>
                {
                    switch (e.Status)
                    {
                        case GeoPositionStatus.Disabled:
                            // location is unsupported on this device
                            break;
                        case GeoPositionStatus.NoData:
                            // data unavailable
                            break;
                    }
                };
            watcher.Start();
        }

        private Beacon(string resourceTag)
        {
            ResourceTag = resourceTag;

            new Thread(() =>
                {
                    AttachInternal();
                    while (!Detached)
                    {
                        Thread.Sleep(1000 * 10);

                        if (!Detached)
                        {
                            UpdateInternal();
                        }
                    }
                }).Start();
        }

        public static Beacon Attach(string resourceTag)
        {
            return new Beacon(resourceTag);
        }

        public void Detach()
        {
            Detached = true;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                DetachInternal();
            });
        }

        private void UpdateInternal()
        {
            AttachInternal();
        }

        private void AttachInternal()
        {
            Debug.WriteLine("Beacon: " + DeviceString);
            WebClient wc = new WebClient();
            wc.DownloadStringCompleted += new DownloadStringCompletedEventHandler(wc_DownloadStringCompleted);
            wc.DownloadStringAsync(new Uri(
                string.Format(AttachUrl, DavuxLibSL.App.Key, ResourceTag, DeviceString.ToBase64(), CurrentLocation.Longitude + " " + CurrentLocation.Latitude)));
        }

        private void DetachInternal()
        {
            Debug.WriteLine("Beacon Detach: " + DeviceString);
            WebClient wc = new WebClient();
            wc.DownloadStringCompleted += new DownloadStringCompletedEventHandler(wc_DownloadStringCompleted);
            wc.DownloadStringAsync(new Uri(
                string.Format(DetatchUrl, DavuxLibSL.App.Key, ResourceTag, DeviceString.ToBase64())));
        }

        void wc_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            try
            {
                if (e.Error != null)
                {
                    Debug.WriteLine("Error connecting to beacon: " + e.Error.Message);
                    return;
                }
                Debug.WriteLine("Beacon Result: " + e.Result);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error connecting to beacon: " + ex.Message);
                return;
            }
        }
    }
}
