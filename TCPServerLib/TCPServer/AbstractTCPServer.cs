using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Xml.Linq;
using System.Xml;

namespace TCPServerLib.TCPServer
{
    /// <summary>
    /// An abstract TCP Server template to easy make TCP-servers
    /// </summary>
    public abstract class AbstractTCPServer
    {
        private List<Task> _currentClients;

        protected int PORT;
        protected int STOP_PORT;
        protected String NAME;
        protected TraceSource _trace;
        protected const String CONFIG_FILE = "TCPConfigFile.xml";


        /// <summary>
        /// Default constructor with name=dummy and port=65000
        /// </summary>
        public AbstractTCPServer() : this("dummy", 65000)
        {
        }
        /// <summary>
        /// Constructor with name=dummy
        /// </summary>
        /// <param name="port">The port number the server will start on</param>
        public AbstractTCPServer(int port) : this("dummy", port)
        {
        }
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name">The name of the server</param>
        /// <param name="port">The port number the server will start on</param>
        public AbstractTCPServer(String name, int port)
        {
            Configuration conf = new Configuration();
            conf.ServerPort = port;
            conf.ShutdownPort = port+1;
            conf.ServerName = name;

            SetupAbstractTCPServer(conf);
        }

        /// <summary>
        /// Constructor supporting reading a configuration File
        /// </summary>
        /// <param name="configFilePath">The path to the configurationfile</param>
        public AbstractTCPServer(String configFilePath)
        {
            Configuration conf = new Configuration();

            String fullConfigFilename = configFilePath + @"\" + CONFIG_FILE;
            if (!File.Exists(fullConfigFilename))
            {
                throw new FileNotFoundException(fullConfigFilename);
            }

            XmlDocument configDoc = new XmlDocument();
            configDoc.Load(configFilePath + @"\" + CONFIG_FILE);

            /*
             * Read Serverport
             */
            XmlNode portNode = configDoc.DocumentElement.SelectSingleNode("ServerPort");
            if (portNode != null)
            {
                String str = portNode.InnerText.Trim();
                conf.ServerPort = Convert.ToInt32(str);
            }

            /*
             * Read Shutdown port
             */
            XmlNode sdportNode = configDoc.DocumentElement.SelectSingleNode("StopServerPort");
            if (sdportNode != null)
            {
                String str = sdportNode.InnerText.Trim();
                conf.ShutdownPort = Convert.ToInt32(str);
            }

            /*
             * Read server name
             */
            XmlNode nameNode = configDoc.DocumentElement.SelectSingleNode("ServerName");
            if (nameNode != null)
            {
                conf.ServerName = nameNode.InnerText.Trim();
            }

            /*
             * Read Debug Level
             */
            XmlNode debugNode = configDoc.DocumentElement.SelectSingleNode("DebugLevel");
            if (debugNode != null)
            {
                string str = debugNode.InnerText.Trim();
                SourceLevels level = SourceLevels.All;
                SourceLevels.TryParse(str, true, out level);
                conf.DebugLevel = level;
            }

            /*
             * Read Log Files location
             */
            XmlNode logFilesNode = configDoc.DocumentElement.SelectSingleNode("LogFilesPath");
            if (debugNode != null)
            {
                conf.LogFilePath = logFilesNode.InnerText.Trim();
            }

            SetupAbstractTCPServer(conf);
        }

        /// <summary>
        /// Constructor supporting reading a configuration File
        /// </summary>
        /// <param name="configFilePath">The name of the configurationfile</param>
        public void SetupAbstractTCPServer(Configuration conf)
        {
            PORT = conf.ServerPort;
            STOP_PORT = conf.ShutdownPort;
            NAME = conf.ServerName;

            _currentClients = new List<Task>();

            _trace = new TraceSource(NAME);
            SetUpTracing(conf.DebugLevel, conf.LogFilePath);
        }


        /// <summary>
        /// This variable tell if the TCP server should stop - initial false
        /// </summary>
        protected bool stop = false;
        /// <summary>
        /// This starts the TCP-server, inclusive the stopping server 
        /// </summary>
        public void Start()
        {
            TcpListener listener = new TcpListener(IPAddress.Any, PORT);
            listener.Start();
            _trace.TraceEvent(TraceEventType.Information, PORT, $"Server '{NAME}' started on port={PORT}");
            
            // Start stop server
            Task.Run(() => ShutDownServer());


            while (!stop)
            {
                if (listener.Pending())
                {
                    TcpClient client = listener.AcceptTcpClient();
                    _trace.TraceEvent(TraceEventType.Information, PORT, "Client incoming");

                    _currentClients.Add(     // add new client task to current runing task
                        Task.Run(() =>
                        {
                            TcpClient tmpClient = client;
                            DoOneClient(client);
                        })
                    );
                }
                else
                {
                    Thread.Sleep(500);
                }

            }

            // wait for all task to finished
            foreach (Task task in _currentClients)
            {
                task.Wait();
            }
            _trace.TraceEvent(TraceEventType.Information, PORT, $"Server '{NAME}' on port={PORT} is stopped");
            _trace.Close();
        }

        private void DoOneClient(TcpClient sock)
        {
            using (StreamReader sr = new StreamReader(sock.GetStream()))
            using (StreamWriter sw = new StreamWriter(sock.GetStream()))
            {
                sw.AutoFlush = true;
                _trace.TraceEvent(TraceEventType.Information, PORT, "Handle one client");

                // template call
                TcpServerWork(sr, sw);
            }
        }

        /// <summary>
        /// Template Method - do the actual work for this server here
        /// </summary>
        /// <param name="sr">The incomming socket as a text reading stream</param>
        /// <param name="sw">The outgoing socket as a text writing stream - autoflush is set to true</param>
        protected abstract void TcpServerWork(StreamReader sr, StreamWriter sw);


        /* 
         * Help method to Set Up Tracing
         */
        private void SetUpTracing(SourceLevels level, String filename)
        {
            _trace.Switch = new SourceSwitch(NAME + "trace", level.ToString());

            _trace.Listeners.Add(new ConsoleTraceListener());
            
            TraceListener txtLog = new TextWriterTraceListener(filename + "-Log.txt");
            _trace.Listeners.Add(txtLog);

            TraceListener xmlLog = new XmlWriterTraceListener(filename + "-Log.xml");
            _trace.Listeners.Add(xmlLog);


        }


        

        /*
         * For Soft Shutdown
         */

        /// <summary>
        /// This variable tell if the TCP stop-server should shutdown - initial false
        /// </summary>
        protected bool stopShutdown = false;

        private void ShutDownServer()
        {
            TcpListener stopListener = new TcpListener(IPAddress.Any, STOP_PORT);
            stopListener.Start();
            _trace.TraceEvent(TraceEventType.Information, PORT, $"StopServer started on port={STOP_PORT}");

            while (!stopShutdown)
            {
                TcpClient client = stopListener.AcceptTcpClient();
                _trace.TraceEvent(TraceEventType.Warning, PORT, "Client incoming for stopping");

                DoStopClient(client);

            }

            // change server loop condition
            stop=true;
        }

        /// <summary>
        /// This Method determinate the way to validate the stopping of the server
        /// This can be overriden, the default way to stopping is to send 'stop'
        /// </summary>
        /// <param name="client">The socket to the client</param>
        protected virtual void DoStopClient(TcpClient client)
        {
            using (StreamReader sr = new StreamReader(client.GetStream()))
            {
                String str = sr.ReadLine();
                if (str.Trim().ToLower() == "stop")
                {
                    stopShutdown = true;
                }
                else
                {
                    _trace.TraceEvent(TraceEventType.Warning, PORT, $"Someone tried to stop the server, but failed");
                }
            }
        }
    }
}
