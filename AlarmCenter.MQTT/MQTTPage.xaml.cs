using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace AlarmCenter.MQTT
{
    /// <summary>
    /// MQTTPage.xaml 的交互逻辑
    /// </summary>
    public partial class MQTTPage
    {
        public MQTTPage()
        {
            InitializeComponent();
            GetData();
        }

        private void GetData()
        {
            var data = AlarmCenter.DataCenter.DataCenter.proxy.GetYCValue(10, 1);
            List<MqttMessage> messages = new List<MqttMessage>();
            messages.Add(new MqttMessage { Message = (string)data });
            dg.ItemsSource = messages;
        }

    }

    public class MqttMessage
    {
        public string Message { get; set; }
    }
}
