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
using System.Threading;

namespace Foscam_Controller
{
    public partial class MainPage : PhoneApplicationPage
    {
        public MainPage()
        {
            InitializeComponent();
            DataContext = App.ViewModel;

            // bad perf
            return;

            // Start all the cameras when opening the main page, deep links won't invoke this code.
            foreach(var cam in App.ViewModel.Cameras)
            {
                try { cam.Start(); }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error starting cam: " + ex);
                }
            }
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var box = (sender as ListBox);
            if (box.SelectedIndex == -1) return;

            App.ViewModel.SelectedCamera = box.SelectedItem as CameraViewModel;
            NavigationService.Navigate(new Uri("/camera.xaml?Title=" + App.ViewModel.SelectedCamera.Title, UriKind.Relative));

            box.SelectedItem = null;
        }

        private void ApplicationBarIconButton_Click(object sender, EventArgs e)
        {
            App.ViewModel.SettingsContext = new CameraObject();
            NavigationService.Navigate(new Uri("/SettingsPage.xaml", UriKind.Relative));
        }

        private void btnClearCameras_Click(object sender, EventArgs e)
        {
            Settings.Set("cams", null);
            throw new AppQuitException();
        }

        private void mnuDelete_Click(object sender, RoutedEventArgs e)
        {
            var cam = (sender as MenuItem).Tag as CameraViewModel;
            App.ViewModel.Cameras.Remove(cam);
        }

        private void mnuConfigure_Click(object sender, RoutedEventArgs e)
        {
            var cam = (sender as MenuItem).Tag as CameraViewModel;

            App.ViewModel.SettingsContext = cam.GetConfig();
            NavigationService.Navigate(new Uri("/SettingsPage.xaml", UriKind.Relative));
        }

        private void mnuPinUnpin_Click(object sender, RoutedEventArgs e)
        {
            var cam = (sender as MenuItem).Tag as CameraViewModel;
            cam.ChangePin();
        }

        private void ApplicationBarMenuItem_Click(object sender, EventArgs e)
        {
            PushNotifications.Enabled = false;
            PushNotifications.Enabled = true;
        }

        private void Image_Hold(object sender, System.Windows.Input.GestureEventArgs e)
        {
            ContextMenu cm = new ContextMenu();

            var mi = new MenuItem();
            mi.Header = "davux";
            cm.IsOpen = true;
            

        }
    }
}