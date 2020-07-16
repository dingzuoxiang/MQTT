## 基于MQTT协议通信

### 1.什么是MQTT

```shell
MQTT（Message Queuing Telemetry Transport，消息队列遥测传输）是 IBM 开发的一个即时通讯协议，有可能成为物联网的重要组成部分。MQTT 是基于二进制消息的发布/订阅编程模式的消息协议，如今已经成为 OASIS 规范，由于规范很简单，非常适合需要低功耗和网络带宽有限的 IoT 场景。
```

### 2.实现MQTT通信

在vs2019中新建一个控制台项目，然后在项目下面的依赖项右键单击-选择“管理NuGet程序包” -在“浏览”选项卡下面搜索MQTTnet，安装，本项目使用的是2.7.5版本。

#### 2.1.初始化服务器

1.首先是创建一个方法去初始化服务器

```c#
private static async Task StartMqttServer_2_7_5()
        {
            if (mqttServer == null)
            {
                // Configure MQTT server.
                //WithDefaultEndpointPort是设置使用的端口，协议里默认是用1883，不过调试我改成8222了。
                //WithConnectionValidator是用于连接验证，验证client id，用户名，密码什么的。
                //还有其他配置选项，比如加密协议，可以在官方文档里看看，示例就是先简单能用。
                var optionsBuilder = new MqttServerOptionsBuilder()
                    .WithConnectionBacklog(100)
                    .WithDefaultEndpointPort(8222)
                    .WithConnectionValidator(ValidatingMqttClients())
                    ;

                // Start a MQTT server.
                //添加事件触发
                //ApplicationMessageReceived 是服务器接收到消息时触发的事件，可用来响应特定消息。
                //ClientConnected 是客户端连接成功时触发的事件。
                //ClientDisconnected 是客户端断开连接时触发的事件。
                mqttServer = new MqttFactory().CreateMqttServer();
                mqttServer.ApplicationMessageReceived += MqttServer_ApplicationMessageReceived;
                mqttServer.ClientConnected += MqttServer_ClientConnected;
                mqttServer.ClientDisconnected += MqttServer_ClientDisconnected;

                //启动服务器，
                Task.Run(async () => { await mqttServer.StartAsync(optionsBuilder.Build()); });
                //mqttServer.StartAsync(optionsBuilder.Build());
                Console.WriteLine("MQTT服务启动成功！");
            }
        }
```

ValidatingMqttClients方法是客户端连接服务器时的验证，我这里写死了，可以根据自己的需求设置

```c#
private static Action<MqttConnectionValidatorContext> ValidatingMqttClients()
        {
            // Setup client validator.    
            var options = new MqttServerOptions();
            options.ConnectionValidator = c =>
            {
                Dictionary<string, string> c_u = new Dictionary<string, string>();
                c_u.Add("client001", "username001");
                c_u.Add("client002", "username002");
                Dictionary<string, string> u_psw = new Dictionary<string, string>();
                u_psw.Add("username001", "psw001");
                u_psw.Add("username002", "psw002");

                if (c_u.ContainsKey(c.ClientId) && c_u[c.ClientId] == c.Username)
                {
                    if (u_psw.ContainsKey(c.Username) && u_psw[c.Username] == c.Password)
                    {
                        c.ReturnCode = MqttConnectReturnCode.ConnectionAccepted;
                    }
                    else
                    {
                        c.ReturnCode = MqttConnectReturnCode.ConnectionRefusedBadUsernameOrPassword;
                    }
                }
                else
                {
                    c.ReturnCode = MqttConnectReturnCode.ConnectionRefusedIdentifierRejected;
                }
            };
            return options.ConnectionValidator;
        }
```

2.在main方法中调用初始化方法

```c#
            Task.Run(async () => { await StartMqttServer_2_7_5(); });
```

#### 2.2.初始化客户端，连接服务器

1.首先是创建一个客户端接口的对象，然后通过工厂类去创建一个客户端

```c#
private IMqttClient mqttClient = null;

				var factory = new MqttFactory();
                mqttClient = factory.CreateMqttClient();
```

2.客户端创建成功后，就要去连接服务端

```c#
                await mqttClient.ConnectAsync(options);
```

3.在连接服务端时，要传递一个IMqttClientOptions对象，我们通过MqttClientOptionsBuilder去构建这个对象，包含了客户端ID标识 `ClientId`、服务端地址（可以使用IP地址或域名）`Server`、端口号 `Port`、用户名 `UserName`、密码 `Password` 等信息

```c#
var options = new MqttClientOptionsBuilder()
                    .WithClientId(info.ClientId)
                    .WithTcpServer("127.0.0.1", 8222)
                    .WithCredentials(info.Username, info.Password)
                    //.WithTls()//服务器端没有启用加密协议，这里用tls的会提示协议异常
                    .WithCleanSession()
                    .Build();
```

4.同样的，客户端也需要设置触发事件

```c#
				mqttClient.ApplicationMessageReceived += MqttClient_ApplicationMessageReceived;
                mqttClient.Connected += MqttClient_Connected;
                mqttClient.Disconnected += MqttClient_Disconnected;
```

#### 2.消息发送

1.发布消息

mqtt的消息包含topic和payload两部分。topic就是消息主题（类型），用于另外一端判断这个消息是干什么用的。payload就是实际想要发送的数据。
WithTopic给一个topic。
WithPayload给一个msg。
WithAtMostOnceQoS设置QoS，至多1次。也可以设为别的。
PublishAsync异步发送出去。

```c#
public async Task Publish(string payLoad)
        {
            string topic = "topic/test";

            if (string.IsNullOrEmpty(topic))
            {
                throw new Exception("发布主题不能为空！");
            }

            ///qos=0，WithAtMostOnceQoS,消息的分发依赖于底层网络的能力。
            ///接收者不会发送响应，发送者也不会重试。消息可能送达一次也可能根本没送达。
            ///感觉类似udp
            ///QoS 1: 至少分发一次。服务质量确保消息至少送达一次。
            ///QoS 2: 仅分发一次
            ///这是最高等级的服务质量，消息丢失和重复都是不可接受的。使用这个服务质量等级会有额外的开销。
            ///
            ///例如，想要收集电表读数的用户可能会决定使用QoS 1等级的消息，
            ///因为他们不能接受数据在网络传输途中丢失
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payLoad)
                .WithAtMostOnceQoS()
                .WithRetainFlag(true)
                .Build();

            await mqttClient.PublishAsync(message);
        }
```

2.发布消息后，我们可以通过订阅topic拿到消息的内容

```c#
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
```

#### 3.完整源码

1.服务端

```c#
using MQTTnet;
using MQTTnet.Diagnostics;
using MQTTnet.Protocol;
using MQTTnet.Server;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MQTT.Test
{
    public class Program
    {
        private static IMqttServer mqttServer = null;
        private static List<string> connectedClientId = new List<string>();

        static void Main(string[] args)
        {

            Task.Run(async () => { await StartMqttServer_2_7_5(); });

            Client client = new Client();
            Task.Run(async () => { await client.ConnectMqttServerAsync(); });
            // Write all trace messages to the console window.
            /*MQTT提供的一个静态类 MqttNetGlobalLogger 来对消息进行跟踪，该类可用于服务端和客户端。用于跟踪服务端和客户端应用的日志消息，比如启动、停止、心跳、消息订阅和发布等。事件参数 MqttNetTraceMessagePublishedEventArgs 包含了线程ID ThreadId、来源 Source、日志级别 Level、日志消息 Message、异常信息 Exception 等。*/
            MqttNetGlobalLogger.LogMessagePublished += MqttNetTrace_TraceMessagePublished;

            while (true)
            {
                if (mqttServer == null)
                {
                    Console.WriteLine("Please await mqttServer.StartAsync()");
                    Thread.Sleep(1000);
                    continue;
                }

                var inputString = Console.ReadLine().ToLower().Trim();

                if (inputString == "exit")
                {
                    Task.Run(async () => { await EndMqttServer_2_7_5(); });
                    Console.WriteLine("MQTT服务已停止！");
                    break;
                }
                else if (inputString == "clients")
                {
                    var connectedClients = mqttServer.GetConnectedClientsAsync();

                    Console.WriteLine($"客户端标识：");

                }
                else if (inputString.StartsWith("hello:"))
                {
                    string msg = inputString.Substring(6);
                    Topic_Hello(msg);
                }
                else if (inputString.StartsWith("control:"))
                {
                    string msg = inputString.Substring(8);
                    Topic_Host_Control(msg);
                }
                else if (inputString.StartsWith("subscribe:"))
                {
                    string msg = inputString.Substring(10);
                    Subscribe(msg);
                }
                else
                {
                    Console.WriteLine($"命令[{inputString}]无效！");
                }
                Thread.Sleep(100);
            }
        }


        private static void MqttServer_ClientConnected(object sender, MqttClientConnectedEventArgs e)
        {
            Console.WriteLine($"客户端[{e.Client.ClientId}]已连接，协议版本：{e.Client.ProtocolVersion}");
            connectedClientId.Add(e.Client.ClientId);
        }

        private static void MqttServer_ClientDisconnected(object sender, MqttClientDisconnectedEventArgs e)
        {
            Console.WriteLine($"客户端[{e.Client.ClientId}]已断开连接！");
            connectedClientId.Remove(e.Client.ClientId);
        }

        private static void MqttServer_ApplicationMessageReceived(object sender, MqttApplicationMessageReceivedEventArgs e)
        {
            string recv = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
            Console.WriteLine("### RECEIVED APPLICATION MESSAGE ###");
            Console.WriteLine($"客户端[{e.ClientId}]>>");
            Console.WriteLine($"+ Topic = {e.ApplicationMessage.Topic}");
            Console.WriteLine($"+ Payload = {recv}");
            Console.WriteLine($"+ QoS = {e.ApplicationMessage.QualityOfServiceLevel}");
            Console.WriteLine($"+ Retain = {e.ApplicationMessage.Retain}");
            Console.WriteLine();
        }

        private static void MqttNetTrace_TraceMessagePublished(object sender, MqttNetLogMessagePublishedEventArgs e)
        {
            var trace = $">> [{e.TraceMessage.Timestamp:O}] [{e.TraceMessage.ThreadId}] [{e.TraceMessage.Source}] [{e.TraceMessage.Level}]: {e.TraceMessage.Message}";
            if (e.TraceMessage.Exception != null)
            {
                trace += Environment.NewLine + e.TraceMessage.Exception.ToString();
            }

            Console.WriteLine(trace);
        }

        #region 2.7.5

        private static async Task StartMqttServer_2_7_5()
        {
            if (mqttServer == null)
            {
                // Configure MQTT server.
                var optionsBuilder = new MqttServerOptionsBuilder()
                    .WithConnectionBacklog(100)
                    .WithDefaultEndpointPort(8222)
                    .WithConnectionValidator(ValidatingMqttClients())
                    ;

                // Start a MQTT server.
                mqttServer = new MqttFactory().CreateMqttServer();
                mqttServer.ApplicationMessageReceived += MqttServer_ApplicationMessageReceived;
                mqttServer.ClientConnected += MqttServer_ClientConnected;
                mqttServer.ClientDisconnected += MqttServer_ClientDisconnected;

                Task.Run(async () => { await mqttServer.StartAsync(optionsBuilder.Build()); });
                //mqttServer.StartAsync(optionsBuilder.Build());
                Console.WriteLine("MQTT服务启动成功！");
            }
        }

        private static async Task EndMqttServer_2_7_5()
        {
            if (mqttServer != null)
            {
                await mqttServer.StopAsync();
            }
            else
            {
                Console.WriteLine("mqttserver=null");
            }
        }

        private static Action<MqttConnectionValidatorContext> ValidatingMqttClients()
        {
            // Setup client validator.    
            var options = new MqttServerOptions();
            options.ConnectionValidator = c =>
            {
                Dictionary<string, string> c_u = new Dictionary<string, string>();
                c_u.Add("client001", "username001");
                c_u.Add("client002", "username002");
                Dictionary<string, string> u_psw = new Dictionary<string, string>();
                u_psw.Add("username001", "psw001");
                u_psw.Add("username002", "psw002");

                if (c_u.ContainsKey(c.ClientId) && c_u[c.ClientId] == c.Username)
                {
                    if (u_psw.ContainsKey(c.Username) && u_psw[c.Username] == c.Password)
                    {
                        c.ReturnCode = MqttConnectReturnCode.ConnectionAccepted;
                    }
                    else
                    {
                        c.ReturnCode = MqttConnectReturnCode.ConnectionRefusedBadUsernameOrPassword;
                    }
                }
                else
                {
                    c.ReturnCode = MqttConnectReturnCode.ConnectionRefusedIdentifierRejected;
                }
            };
            return options.ConnectionValidator;
        }

        private static void Usingcertificate(ref MqttServerOptions options)
        {
            var certificate = new X509Certificate(@"C:\certs\test\test.cer", "");
            options.TlsEndpointOptions.Certificate = certificate.Export(X509ContentType.Cert);
            var aes = new System.Security.Cryptography.AesManaged();

        }

        #endregion

        #region Topic

        private static async void Topic_Hello(string msg)
        {
            string topic = "topic/hello";

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(msg)
                .WithAtMostOnceQoS()
                .WithRetainFlag()
                .Build();
            await mqttServer.PublishAsync(message);
        }

        private static async void Topic_Host_Control(string msg)
        {
            string topic = "topic/host/control";

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(msg)
                .WithAtMostOnceQoS()
                .WithRetainFlag(false)
                .Build();
            await mqttServer.PublishAsync(message);
        }

        private static async void Topic_Serialize(string msg)
        {
            string topic = "topic/serialize";

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(msg)
                .WithAtMostOnceQoS()
                .WithRetainFlag(false)
                .Build();
            await mqttServer.PublishAsync(message);
        }

        /// <summary>
        /// 替指定的clientID订阅指定的内容
        /// </summary>
        /// <param name="topic"></param>
        private static void Subscribe(string topic)
        {
            List<TopicFilter> topicFilter = new List<TopicFilter>();
            topicFilter.Add(new TopicFilterBuilder()
                .WithTopic(topic)
                .WithAtMostOnceQoS()
                .Build());
            //给"client001"订阅了主题为topicFilter的payload
            mqttServer.SubscribeAsync("client001", topicFilter);
            Console.WriteLine($"Subscribe:[{"client001"}]，Topic：{topic}");
        }

        #endregion

    }
}
```

2.客户端

```c#
using MQTTnet;
using MQTTnet.Client;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MQTT.Test
{
    public class Client
    {
        private MqttClientInfo info = MqttClientInfo.GetInstance();

        private IMqttClient mqttClient = null;
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

            //非托管客户端
            try
            {
                //Create TCP based options using the builder.
                var options = new MqttClientOptionsBuilder()
                    .WithClientId(info.ClientId)
                    .WithTcpServer("127.0.0.1", 8222)
                    .WithCredentials(info.Username, info.Password)
                    //.WithTls()//服务器端没有启用加密协议，这里用tls的会提示协议异常
                    .WithCleanSession()
                    .Build();

                //// For .NET Framwork & netstandard apps:
                //MqttTcpChannel.CustomCertificateValidationCallback = (x509Certificate, x509Chain, sslPolicyErrors, mqttClientTcpOptions) =>
                //{
                //    if (mqttClientTcpOptions.Server == "server_with_revoked_cert")
                //    {
                //        return true;
                //    }

                //    return false;
                //};

                await mqttClient.ConnectAsync(options);
            }
            catch (Exception ex)
            {
                FlagMsg += ($"连接到MQTT服务器失败！" + Environment.NewLine + ex.Message + Environment.NewLine);
            }
            await Publish("Hello World!");
            Console.WriteLine(FlagMsg);
            await Subscribe("topic/test");
            Console.WriteLine(ReceiveMsg);
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

            //Reconnecting
            if (info.IsReconnect)
            {
                FlagMsg += ("正在尝试重新连接" + Environment.NewLine);

                var options = new MqttClientOptionsBuilder()
                    .WithClientId(info.ClientId)
                    .WithTcpServer("127.0.0.1", 8222)
                    .WithCredentials(info.Username, info.Password)
                    //.WithTls()
                    .WithCleanSession()
                    .Build();
                await Task.Delay(TimeSpan.FromSeconds(5));
                try
                {
                    await mqttClient.ConnectAsync(options);
                }
                catch
                {
                    FlagMsg += ("### RECONNECTING FAILED ###" + Environment.NewLine);
                }
            }
            else
            {
                FlagMsg += ("已下线！" + Environment.NewLine);
            }
        }

        private void MqttClient_ApplicationMessageReceived(object sender, MqttApplicationMessageReceivedEventArgs e)
        {
            ReceiveMsg = ($">> {"### RECEIVED APPLICATION MESSAGE ###"}{Environment.NewLine}");
            ReceiveMsg += ($">> Topic = {e.ApplicationMessage.Topic}{Environment.NewLine}");
            ReceiveMsg += ($">> Payload = {Encoding.UTF8.GetString(e.ApplicationMessage.Payload)}{Environment.NewLine}");
            ReceiveMsg += ($">> QoS = {e.ApplicationMessage.QualityOfServiceLevel}{Environment.NewLine}");
            ReceiveMsg += ($">> Retain = {e.ApplicationMessage.Retain}{Environment.NewLine}");
            Console.WriteLine(ReceiveMsg);
        }

        public async Task Publish(string payLoad)
        {
            string topic = "topic/test";

            if (string.IsNullOrEmpty(topic))
            {
                throw new Exception("发布主题不能为空！");
            }

            ///qos=0，WithAtMostOnceQoS,消息的分发依赖于底层网络的能力。
            ///接收者不会发送响应，发送者也不会重试。消息可能送达一次也可能根本没送达。
            ///感觉类似udp
            ///QoS 1: 至少分发一次。服务质量确保消息至少送达一次。
            ///QoS 2: 仅分发一次
            ///这是最高等级的服务质量，消息丢失和重复都是不可接受的。使用这个服务质量等级会有额外的开销。
            ///
            ///例如，想要收集电表读数的用户可能会决定使用QoS 1等级的消息，
            ///因为他们不能接受数据在网络传输途中丢失
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payLoad)
                .WithAtMostOnceQoS()
                .WithRetainFlag(true)
                .Build();

            await mqttClient.PublishAsync(message);
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

    //这个是我参照官方api，自己写的一个类
    public class MqttClientInfo
    {
        private bool _isReconnect = true;
        private string _username = "username001";
        private string _password = "psw001";
        private string _clientId = "client001";
        private int _port = 1883;//mqtt默认端口

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

```



