using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TCPServerLib.TCPServer;

namespace TryMyServer
{
    internal class EchoServer : AbstractTCPServer
    {
        public EchoServer():base()
        {
        }

        public EchoServer(int port) : base(port)
        {
        }

        
        public EchoServer(string configFilePath) : base(configFilePath)
        {
        }

        public EchoServer(string name, int port) : base(name, port)
        {
        }



        protected override void TcpServerWork(StreamReader sr, StreamWriter sw)
        {
            String str = sr.ReadLine();
            sw.WriteLine(str);
        }
    }
}
