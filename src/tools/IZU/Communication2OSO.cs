using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NNanomsg.Protocols;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace OHTC.Tools
{
    /// <summary>
    /// oso 通讯帮助类
    /// </summary>
    public sealed class Communication2OSO : ICommunication2OSO
    {
        private static IPAddress _ip = IPAddress.Parse("224.5.6.7");
        private static readonly int ttl = 1;

        /// <summary>
        /// 发送请求，并接收参数
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public async Task<T> RequestAsync<T>(OSOParameter parameter)
        {
            using (RequestSocket? s = new RequestSocket())
            {
                var url = "tcp://ohtc.wonder-inc.cn:8020";
                s.Connect(url);
                var str = ModelToJson(parameter);

                s.Send(Encoding.UTF8.GetBytes(str));
                var json = Encoding.UTF8.GetString(s.Receive());

                //if (typeof(T).Name.Equals("String"))
                //{
                //    return json;
                //}
                return JsonConvert.DeserializeObject<T>(json);
            }
        }

        public async Task<string> RequestAsync(OSOParameter parameter)
        {
            using (RequestSocket? s = new RequestSocket())
            {
                var url = "tcp://ohtc.wonder-inc.cn:8020";
                s.Connect(url);
                var str = ModelToJson(parameter);

                s.Send(Encoding.UTF8.GetBytes(str));
                byte[] bytes = new byte[2048];
                try
                {
                    bytes = InvokeWithTime(s.Receive, 100000);
                }
                catch (Exception ex)
                {

                    throw;
                }
                var json = Encoding.UTF8.GetString(bytes);
                return json;
            }
        }

        public async Task RequestNoResponseAsync(OSOParameter parameter)
        {
            using (RequestSocket? s = new RequestSocket())
            {
                var url = "tcp://ohtc.wonder-inc.cn:8020";
                s.Connect(url);
                var str = ModelToJson(parameter);
                s.Send(Encoding.UTF8.GetBytes(str));
            }
        }

        public static byte[] InvokeWithTime(Func<byte[]> method, int milliseconds)
        {
            Task<byte[]>[] tasks = new Task<byte[]>[1];

            tasks[0] = Task.Run(() => method());

            Task.WaitAll(tasks, milliseconds);
            if (tasks[0].IsCompletedSuccessfully)
            {
                return tasks[0].Result;
            }
            throw new TimeoutException();
        }


        /// <summary>
        ///pub 推送 数据更新
        /// </summary>
        /// <param name="message"></param>
        public void SendMulticastMessage(object message)
        {
            // Create the Socket
            Socket sock = new Socket(AddressFamily.InterNetwork,
                                     SocketType.Dgram,
                                     ProtocolType.Udp);
            try
            {
                // Set the Time to Live                          
                sock.SetSocketOption(SocketOptionLevel.IP,
                                     SocketOptionName.MulticastTimeToLive,
                                     ttl);

                IPEndPoint ipep = new IPEndPoint(_ip, 4567);
                var msg = ModelToJson(message);
                // Send the data packet
                byte[] inputToBeSent = Encoding.ASCII.GetBytes(msg);
                sock.SendTo(inputToBeSent, 0, inputToBeSent.Length,
                            SocketFlags.None, ipep);
            }
            catch (SocketException se)
            {
                Console.WriteLine("multicast send socket failed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("SendMulticastMessage failed !");
            }
            finally
            {
                sock.Close();
            }
        }

        /// <summary>
        /// 实体转json
        /// </summary>
        /// <param name="ob"></param>
        /// <returns></returns>
        public string ModelToJson(object ob)
        {
            var str = JsonConvert.SerializeObject(ob, new JsonSerializerSettings
            {
                Converters =
                {
                    new StringEnumConverter()
                }
            });
            return str;
        }
    }


    /// <summary>
    /// OSO 通讯接口定义
    /// </summary>
    public interface ICommunication2OSO
    {
        /// <summary>
        /// 发送请求，并接收参数
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        Task<T> RequestAsync<T>(OSOParameter parameter);

        /// <summary>
        /// 推送pub 更新
        /// </summary>
        /// <param name="message"></param>
        void SendMulticastMessage(object message);

        /// <summary>
        /// 将对象转json
        /// </summary>
        /// <param name="ob"></param>
        /// <returns></returns>
        string ModelToJson(object ob);
    }


}
