using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NLog;

namespace SNTPService
{
    /// <summary>
    /// A class that listen for UDP packets from remote clients.
    /// </summary>
    public class UdpListener : SocketListener
    {
        Logger logger = LogManager.GetCurrentClassLogger();

        #region Methods

        /// <summary>
        ///  Starts the service listener if it is in a stopped state.
        /// </summary>
        /// <param name="servicePort">The port used to listen on.</param>
        /// <param name="allowBroadcast">Allows the listener to accept broadcast packets.</param>
        public bool Start(int servicePort, bool allowBroadcast)
        {
            if (servicePort > IPEndPoint.MaxPort || servicePort < IPEndPoint.MinPort)
                throw new ArgumentOutOfRangeException("port", "Port must be less then " + IPEndPoint.MaxPort + " and more then " + IPEndPoint.MinPort);

            if (IsActive)
                throw new InvalidOperationException("Udp listener is already active and must be stopped before starting");

            if (InterfaceAddress == null)
            {
                logger.Debug("Unable to set interface address");
                throw new InvalidOperationException("Unable to set interface address");
            }

            try
            {
                Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);

                if (allowBroadcast)
                    Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);

                Socket.Bind(new IPEndPoint(InterfaceAddress, servicePort));
                Socket.ReceiveTimeout = ReceiveTimeout;
                Socket.SendTimeout = SendTimeout;

                IsActive = true;
                Thread = new Thread(StartUdpListening);
                Thread.Start();

                logger.Info("Started listening for requests on " + Socket.LocalEndPoint.ToString());

            }
            catch (Exception ex)
            {
                logger.Error(ex, "Starting listener Failed: " + ex.Message.ToString());
                return false;
            }
            return true;
        }

        /// <summary>
        ///  Listener thread
        /// </summary>
        private void StartUdpListening()
        {
            while (IsActive)
            {
                try
                {
                    OnSocket(Socket);
                }
                catch (Exception ex)
                {
                    OnClientDisconnected(Socket, ex);
                }
            }
        }

        #endregion Methods
    }
}
