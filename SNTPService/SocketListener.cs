using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NLog;

namespace SNTPService
{
    public abstract class SocketListener : IDisposable
    {
        Logger logger = LogManager.GetCurrentClassLogger();

        protected Socket Socket { get; set; }
        protected Thread Thread { get; set; }
        private byte[] sendBuffer = new byte[1460];        

        public IPAddress InterfaceAddress { get; set; }
        public int ReceiveTimeout { get; set; }
        public int SendTimeout { get; set; }
        public int ListenBacklog { get; set; }
        public int BufferSize { get; set; }
        public bool IsActive { get; set; }

        public int ActivePort {
            get {
                if(Socket == null) {
                    return -1;
                }
                return ((IPEndPoint)Socket.LocalEndPoint).Port;
            }
        }

        public SocketListener()
        {
            InterfaceAddress = IPAddress.Any;
            BufferSize = 65535;
            IsActive = false;
            ListenBacklog = 10;
            ReceiveTimeout = -1;
            SendTimeout = -1;
        }

        ~SocketListener()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if(disposing) {
                if(IsActive) {
                    Stop();
                }
            }
            if(Thread != null) {
                Thread = null;
            }
        }

        #region Methods

        /// <summary>
        ///  Stops the service listener if in started state.
        /// </summary>
        public bool Stop()
        {
            try {
                if(!IsActive) { 
                    throw new InvalidOperationException("Listener is not active and must be started before stopping");
                }

                IsActive = false;
                logger.Info("Stopped listening for requests on " + Socket.LocalEndPoint.ToString());
                
                return true;
            }
            catch (Exception ex) {
                logger.Error(ex, "Stopping listener Failed: " + ex.Message.ToString());
            }        
            return false;
        }

        /// <summary>
        /// Reads socket and processes packet
        /// </summary>
        /// <param name="socket">The active socket.</param>
        protected virtual void OnSocket(Socket socket)
        {
            try {
                if (socket.Poll(-1, SelectMode.SelectRead))
                {
                    var args = new ClientConnectedEventArgs(socket, BufferSize);

                    EndPoint remoteEndPoint = new  IPEndPoint(0, 0);

                    if(socket.Available == 0) {
                        return;
                    }

                    args.ChannelBuffer.BytesTransferred = socket.ReceiveFrom(args.ChannelBuffer.Buffer, SocketFlags.None, ref remoteEndPoint);
                    args.Channel.RemoteEndpoint = remoteEndPoint;

                    if (args.ChannelBuffer.BytesTransferred > 0) {
                        OnClientConnected(args);

                        if (args.AllowConnect == false) {
                            if (args.Response != null) {
                                int sentBytes = 0;
                                while ((sentBytes = args.Response.Read(sendBuffer, 0, sendBuffer.Length)) > 0) {
                                    socket.Send(sendBuffer, sentBytes, SocketFlags.None);
                                }
                            }
                            logger.Debug("PACKET request from  " +
                                args.Channel.RemoteEndpoint.ToString() +
                                " with channel id " +
                                args.Channel.ChannelId.ToString() +
                                " was denied access to connect.");
                        }
                    }
                }  else {
                    Thread.Sleep(10);
                }
            }
            catch (SocketException ex)
            {
                if (ex.ErrorCode == (int)SocketError.ConnectionReset)
                    return;

            }
            catch (Exception ex)
            {
                HandleDisconnect(SocketError.SocketError, ex);
            }
        }

        /// <summary>
        ///     A client has connected (nothing have been sent or received yet)
        /// </summary>
        /// <returns></returns>
        protected virtual void OnClientConnected(ClientConnectedEventArgs args)
        {
            ClientConnected(this, args);
        }

        /// <summary>
        ///     A client has disconnected
        /// </summary>
        /// <param name="socket">Channel representing the client that disconnected</param>
        /// <param name="exception">
        ///     Exception which was used to detect disconnect (<c>SocketException</c> with status
        ///     <c>Success</c> is created for graceful disconnects)
        /// </param>
        protected virtual void OnClientDisconnected(Socket socket, Exception exception)
        {
            ClientDisconnected(this, new ClientDisconnectedEventArgs(socket, exception));
        }

        /// <summary>
        /// Detected a disconnect
        /// </summary>
        /// <param name="socketError">ProtocolNotSupported = decoder failure.</param>
        /// <param name="exception">Why socket got disconnected</param>
        protected void HandleDisconnect(SocketError socketError, Exception exception)
        {
            try
            {
                Socket.Close();
            }
            catch (Exception ex)
            {
                HandleDisconnect(SocketError.ConnectionReset, ex);
            }
        }

        #endregion Methods

        #region Events

        /// <summary>
        ///     A client has connected (nothing have been sent or received yet)
        /// </summary>
        public event ClientConnectedEventHandler ClientConnected = delegate { };

        /// <summary>
        ///     A client has connected (nothing have been sent or received yet)
        /// </summary>
        public delegate void ClientConnectedEventHandler(object sender, ClientConnectedEventArgs args);

        /// <summary>
        ///     A client has disconnected
        /// </summary>
        public event ClientDisconnectedEventHandler ClientDisconnected = delegate { };

        /// <summary>
        ///     A client has disconnected
        /// </summary>
        public delegate void ClientDisconnectedEventHandler(object sender, ClientDisconnectedEventArgs args);

        /// <summary>
        ///     An internal error occured
        /// </summary>
        public event ErrorHandler ListenerError = delegate { };

        #endregion Events

    }
}
