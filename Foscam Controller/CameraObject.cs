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
using System.Runtime.Serialization;

namespace Foscam_Controller
{
    // Type 'Foscam_Controller.CameraViewModel' cannot be serialized. Consider marking it with the DataContractAttribute attribute, and marking all of its members you want serialized with the DataMemberAttribute attribute.

    [DataContractAttribute]
    public class CameraObject
    {
        public enum CameraVersion
        {
            Unknown, FI8918W, FI8908W,
        }

        [DataMemberAttribute]
        public string Title { get; set; }
        [DataMemberAttribute]
        public string Username { get; set; }
        [DataMemberAttribute]
        public string Password { get; set; }
        [DataMemberAttribute]
        public string URL { get; set; }
        [DataMemberAttribute]
        public string Timezone { get; set; }
        [DataMemberAttribute]
        public CameraVersion Version { get; set; }

        // We use this to determine if this is a new context or a context update
        public CameraViewModel Update { get; set; }
    }
}
