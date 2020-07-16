using MQTTnet;
using MQTTnet.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace dome7
{
    public class Client
    {
        private MqttClientInfo info = MqttClientInfo.GetInstance();

        private IMqttClient mqttClient = null;
        public string Topic = "";
        public string Message = "";
        public string ReceiveMsg = "";
        public string FlagMsg = "";


        public async Task ConnectMqttServerAsync()
        {
            // Create a new MQTT client.
            if (mqttClient == null)
            {
                var factory = new MqttFactory();
                mqttClient = factory.CreateMqttClient();

                mqttClient.ApplicationMessageReceived += MqttClient_ApplicationMessageReceived;
                mqttClient.Connected += MqttClient_Connected;
                mqttClient.Disconnected += MqttClient_Disconnected;
            }

            try
            {
                //Create TCP based options using the builder.
                var options = new MqttClientOptionsBuilder()
                    .WithClientId(info.ClientId)
                    .WithTcpServer("127.0.0.1", 8222)
                    .WithCredentials(info.Username, info.Password)
                    .WithCleanSession()
                    .Build();

                await mqttClient.ConnectAsync(options);
            }
            catch (Exception ex)
            {
                FlagMsg += ($"连接到MQTT服务器失败！" + Environment.NewLine + ex.Message + Environment.NewLine);
                throw new Exception();
            }
            await Subscribe(info.Topice);
        }

        private void MqttClient_Connected(object sender, EventArgs e)
        {
            FlagMsg = ("已连接到MQTT服务器！" + Environment.NewLine);
        }

        private async void MqttClient_Disconnected(object sender, EventArgs e)
        {
            DateTime curTime = new DateTime();
            curTime = DateTime.UtcNow;
            FlagMsg = ($">> [{curTime.ToLongTimeString()}]");
            FlagMsg += ("已断开MQTT连接！" + Environment.NewLine);

            ////Reconnecting
            //if (info.IsReconnect)
            //{
            //    FlagMsg += ("正在尝试重新连接" + Environment.NewLine);

            //    var options = new MqttClientOptionsBuilder()
            //        .WithClientId(info.ClientId)
            //        .WithTcpServer("127.0.0.1", 8222)
            //        .WithCredentials(info.Username, info.Password)
            //        //.WithTls()
            //        .WithCleanSession()
            //        .Build();
            //    await Task.Delay(TimeSpan.FromSeconds(5));
            //    try
            //    {
            //        await mqttClient.ConnectAsync(options);
            //    }
            //    catch
            //    {
            //        FlagMsg += ("### RECONNECTING FAILED ###" + Environment.NewLine);
            //    }
            //}
            //else
            //{
            //    FlagMsg += ("已下线！" + Environment.NewLine);
            //}
        }

        private void MqttClient_ApplicationMessageReceived(object sender, MqttApplicationMessageReceivedEventArgs e)
        {
            Topic = e.ApplicationMessage.Topic;
            Message = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
            ReceiveMsg = ($">> {"### RECEIVED APPLICATION MESSAGE ###"}{Environment.NewLine}");
            ReceiveMsg += ($">> Topic = {e.ApplicationMessage.Topic}{Environment.NewLine}");
            ReceiveMsg += ($">> Payload = {Encoding.UTF8.GetString(e.ApplicationMessage.Payload)}{Environment.NewLine}");
            ReceiveMsg += ($">> QoS = {e.ApplicationMessage.QualityOfServiceLevel}{Environment.NewLine}");
            ReceiveMsg += ($">> Retain = {e.ApplicationMessage.Retain}{Environment.NewLine}");
        }

        public async Task Subscribe(string topic)
        {
            if (string.IsNullOrEmpty(topic))
            {
                throw new Exception("订阅主题不能为空！");
            }

            if (!mqttClient.IsConnected)
            {
                throw new Exception("MQTT客户端尚未连接！");
            }

            // Subscribe to a topic
            await mqttClient.SubscribeAsync(new TopicFilterBuilder()
                .WithTopic(topic)
                .WithAtMostOnceQoS()
                .Build()
                );

            FlagMsg += ($"已订阅[{topic}]主题{Environment.NewLine}");
        }

    }

    public class MqttClientInfo
    {
        private bool _isReconnect = true;
        private string _username = "username002";
        private string _password = "psw002";
        private string _clientId = "client002";
        private int _port = 1883;//mqtt默认端口
        private string _topic = "topic/test";

        public bool IsReconnect
        {
            get
            {
                return _isReconnect;
            }

            set
            {
                _isReconnect = value;
            }
        }

        public string Username
        {
            get
            {
                return _username;
            }

            set
            {
                _username = value;
            }
        }

        public string Password
        {
            get
            {
                return _password;
            }

            set
            {
                _password = value;
            }
        }

        public string ClientId
        {
            get
            {
                return _clientId;
            }

            set
            {
                _clientId = value;
            }
        }

        public string Sever { get; set; }
        public int Port
        {
            get { return _port; }
            set { _port = value; }
        }
        public string Topice
        {
            get { return _topic; }
            set { _topic = value; }
        }

        //创建类的一个内部对象
        private static MqttClientInfo instance = new MqttClientInfo();


        //让构造函数为 private，这样该类就不会被实例化
        private MqttClientInfo() { }

        //获取唯一可用的对象
        public static MqttClientInfo GetInstance()
        {
            return instance;
        }

    }
}
