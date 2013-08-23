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

namespace Foscam_Controller
{
    public partial class SettingsPage : PhoneApplicationPage
    {
        public SettingsPage()
        {
            InitializeComponent();

            DataContext = App.ViewModel;
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            var conf = (DataContext as AppViewModel).SettingsContext;

            if (string.IsNullOrEmpty(conf.URL) ||
                string.IsNullOrEmpty(conf.Username) ||
                string.IsNullOrEmpty(conf.Password) ||
             /*   string.IsNullOrEmpty(conf.TimeZone) || */ // TODO FIXME - not working, causes timezone to never really be populated
                string.IsNullOrEmpty(conf.Title))
            {
                MessageBox.Show("All fields are required.");
                return;
            }

            if (!conf.URL.StartsWith("http://"))
            {
                conf.URL = "http://" + conf.URL;
            }

            App.ViewModel.SaveContext();

            NavigationService.GoBack();
        }
    }
}