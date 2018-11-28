using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Collections;
using System.Threading;
using System.IO;
using System.Windows.Forms;
using System.ComponentModel;

namespace CsSocket_Multi2_S
{

    class ClientSocket
    {
        public Socket csocket
        {
            get;
            set;
        }
        public int masterindex
        {
            get;
            set;
        }
        public ClientSocket(Socket fd,int masterindex,string name)
        {
            this.csocket = fd;
            this.masterindex = masterindex;
            this.name = name;
        }
        
        public string name
        {
            get;set;
        }
    }
    class Server
    {
        private static Socket _Listener;
        private static List<ClientSocket> _clientSockets = new List<ClientSocket>();
        private static bool isset = false, isopened;

        private static UdpClient uc, us;
        private static IPEndPoint ipep_b, ipep_c,ServerAddr = new IPEndPoint(IPAddress.Any, 3000);
        private static NetworkStream ns;
        private static byte[] _buffer = new byte[1024];
        private static byte[] getpic;
        private static Thread SelectThread;
        private static ArrayList _master = new ArrayList();
        private static ArrayList _read = new ArrayList();
        private static int picnumber = 0;
        private static bool alreadyrecving = false;
        static void Main(string[] args)
        {
            if (!isset)
            {
                SetupServer();
                isset = true;
                Console.WriteLine("[Server]Server Has Set up.");
                StartWork();
            }
        }

        public static void SetupServer()
        {
            try
            {
                //TCP
                _Listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _Listener.Bind(ServerAddr);
                _Listener.Listen(5);//Backlog(pending connects)不重要 除非一秒要很多Clients 如果設定5，後面就不會接受

                SelectThread = new Thread(SelectionLoop);

                //UDP
                ipep_c = new IPEndPoint(IPAddress.Any, 2000);
                ipep_b = new IPEndPoint(IPAddress.Broadcast, 2000);

                uc = new UdpClient();//Receive
                uc.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                uc.Client.Bind(ipep_c);

                us = new UdpClient();//Broadcast
                us.Client.EnableBroadcast = true;
                //us.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

               
               
            }
            catch
            {
                MessageBox.Show("[Server setup Error!]");
            }
        }
        public static void StartWork()
        {
            isopened = true;
            SelectThread.Start();
        }
        //************************************************
        //TCP
        
        private static void SelectionLoop()
        {
            _read.Clear();
            _master.Clear();
            _master.Add(_Listener);
            _master.Add(uc.Client);
            while (true)
            {
                _read = new ArrayList(_master);
                Socket.Select(_read,null, null, 1000);
                for (int i = 0; i < _read.Count; i++)
                {
                    if (_read[i] == _Listener)
                    {
                        _Listener.BeginAccept(new AsyncCallback(AcceptCallback), null);
                    }
                    else
                    {
                        if(_read[i] == uc.Client)
                        {
                            byte[] buffer = new byte[1024];
                            buffer = uc.Receive(ref ipep_c);
                            string text = Encoding.Default.GetString(buffer);
                            Console.WriteLine("[Server]UDP has Received Text : " + text + " From:" + ipep_c);
                            us.Send(buffer, buffer.Length, ipep_b);
                            Console.WriteLine("[Server]UDP Broadcasted");
                            buffer = uc.Receive(ref ipep_c);
                        }
                        else
                        {
                            Socket current = (Socket)_read[i];
                            int ActiveSocketfd = GetMasterindex((Socket)_read[i]);

                            byte[] extbuf = new byte[3];
                            byte[] sizebuf = new byte[4];
                            try
                            {
                                if (!alreadyrecving)
                                {
                                    ns = new NetworkStream(current);

                                    ReadAllData(ns, extbuf);
                                    Console.WriteLine("[Server]Getting File");
                                    string extension = Encoding.Default.GetString(extbuf);
                                    ReadAllData(ns, sizebuf);//1st get size
                                    int count = BitConverter.ToInt32(sizebuf, 0);
                                    getpic = new byte[count];
                                    ReadAllData(ns, getpic);

                                    
                                    FileStream fs = File.Create(picnumber + "."+extension);
                                    fs.Write(getpic, 0, getpic.Length);
                                    Console.WriteLine("[Server]\""+picnumber+"."+extension+"\" Successfully Received, Size = " + fs.Length);
                                    fs.Close();
                                    picnumber++;
                                    SendFileToOthers(current,extbuf, getpic);

                                }
                            }
                            catch
                            {
                                try
                                {
                                    Console.WriteLine("\n[Server] Client " + _clientSockets[ActiveSocketfd].name + " Disconnected ,from " + current.RemoteEndPoint);
                                    _clientSockets.Remove(_clientSockets[ActiveSocketfd]);
                                    _master.Remove(_read[i]);
                                }
                                catch
                                {
                                    Environment.Exit(0);
                                }
                            }
                        }
                       
                    }

                }//for
            }//while
            
        }
        private static void ReadAllData(NetworkStream n, byte[] toread)
        {
            alreadyrecving = true;
            int sizeleft = toread.Length, offset = 0;
            while(sizeleft>0)
            {
                int get = n.Read(toread , offset, sizeleft);
                if (get <= 0) throw new EndOfStreamException("Error");
                sizeleft -= get;
                offset += get;
            }
            alreadyrecving = false;
            n.Flush();
        }
        private static void AcceptCallback(IAsyncResult AR)
        {
            Socket newSocket = _Listener.EndAccept(AR);
            //Get Picture From client
            int rec =  newSocket.Receive(_buffer);
            byte[] copy = new byte[rec];
            Array.Copy(_buffer, copy, rec);
            string text = Encoding.Default.GetString(copy);
            Console.WriteLine("[Server]TCP Connection Accepted: " + text + " IP:" + newSocket.RemoteEndPoint);

            //Add to List with a udp socket per list element
            _clientSockets.Add(new ClientSocket(newSocket,_clientSockets.Count,text));
            _master.Add(newSocket);
            
            //END ACCEPT
        }
        
           
        private static void SendCallback(IAsyncResult AR)
        {
            Socket newSocket = (Socket)AR.AsyncState;
            newSocket.EndSend(AR);
        }
        //************************************************
       
        //************************************************

        public static int GetMasterindex(Socket s)
        {
            foreach(ClientSocket cs in _clientSockets)
            {
                if (cs.csocket == s) return cs.masterindex;
            }
            return (int)SocketError.SocketError;
        }
        public static Socket GetMasterSocket(int fdnumber)
        {
            foreach (ClientSocket cs in _clientSockets)
            {
                if (cs.masterindex == fdnumber) return cs.csocket;
            }
            return null;
        }
        public static string GetName(Socket s)
        {
            foreach(ClientSocket sk in _clientSockets)
            {
                if (sk.csocket == s) return sk.name;
            }
            return null;
        }

        public static string GetName(int fd)
        {
            foreach (ClientSocket sk in _clientSockets)
            {
                if (sk.masterindex == fd) return sk.name;
            }
            return null;
        }
        
        public static void SendFileToOthers(Socket s,byte[] ext,byte[] data)
        {
            foreach (ClientSocket cs in _clientSockets)
            {
                if (cs.csocket != s)
                {
                    NetworkStream n = new NetworkStream(cs.csocket);
                    n.Write(ext, 0, ext.Length);
                    n.Write(BitConverter.GetBytes(data.Length), 0, 4);
                    n.Write(data, 0, data.Length);
                }
            }
        }
        public static void Stop()
        {
            if (isopened)
            {
                foreach(ClientSocket csk in _clientSockets)
                {
                    csk.csocket.Close();
                }
                SelectThread.Abort();
            }
        }
        
    }//class








}//namespace

