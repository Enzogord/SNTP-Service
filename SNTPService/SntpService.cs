using System;
using System.Threading;
using System.Net;
using NLog;

namespace SNTPService
{
    public class SntpService
    {
        Logger logger = LogManager.GetCurrentClassLogger();

        private UdpListener udpListener;
        private readonly int port;

        public IPAddress InterfaceAddress { get; set; }

        public SntpService(int port)
        {
            this.port = port;
        }

        public bool Start()
        {
            udpListener = new UdpListener();
            udpListener.InterfaceAddress = InterfaceAddress;
            udpListener.BufferSize = Constants.SNTP_MAX_MESSAGE_SIZE;
            udpListener.ReceiveTimeout = Constants.SNTP_RECEIVE_TIMEOUT;
            udpListener.SendTimeout = Constants.SNTP_SEND_TIMEOUT;
            udpListener.ClientConnected += OnClientConnect;
            udpListener.ClientDisconnected += OnClientDisconnect;
            return udpListener.Start(port, true);
        }

        public bool Stop()
        {
            try  {
                return udpListener.Stop();
            }
            catch (Exception ex) {
                logger.Error(ex, "Stopping Service failed");
            }

            return false;
        }

        private void OnClientConnect(object sender, ClientConnectedEventArgs args)
        {
            SocketBuffer channelBuffer = args.ChannelBuffer;

            if (channelBuffer != null
                && args.Channel.IsConnected
                && channelBuffer.BytesTransferred >= Constants.SNTP_MIN_MESSAGE_SIZE
                && channelBuffer.BytesTransferred <= Constants.SNTP_MAX_MESSAGE_SIZE)
            {
                logger.Debug("PACKET request with channel id " +
                    args.Channel.ChannelId.ToString() +
                    " was received from " +
                    args.Channel.RemoteEndpoint.ToString() +
                    " and queued for processing...");

                SntpMessageEventArgs messageArgs = new SntpMessageEventArgs(args.Channel, args.ChannelBuffer);
                OnSntpMessageReceived(this, messageArgs);

                ThreadPool.QueueUserWorkItem(new WaitCallback(ProcessRequest), messageArgs);
            }
        }

        private void OnClientDisconnect(object sender, ClientDisconnectedEventArgs args)
        {
            logger.Debug("Remote client was disconnected");
        }
        
        private void ProcessRequest(object state)
        {
            Console.WriteLine("Получен запрос");
            SntpMessageEventArgs args = (SntpMessageEventArgs)state;

            if (args.RequestMessage != null) {
                switch (args.RequestMessage.Mode) {
                    case Mode.Client:                        
                        args.ResponseMessage = args.RequestMessage;
                        args.ResponseMessage.Mode = Mode.Server;
                        args.ResponseMessage.ReceiveDateTime = DateTime.Now;
                        args.ResponseMessage.TransmitDateTime = DateTime.Now;
                        args.ResponseMessage.Stratum = Stratum.Secondary;
                        args.ResponseMessage.ReferenceIdentifier = ReferenceIdentifier.LOCL;
                        this.SendReply(args);
                        
                        break;
                    case Mode.Broadcast:
                    case Mode.Reserved:
                    case Mode.ReservedNTPControl:
                    case Mode.ReservedPrivate:
                    case Mode.Server:
                    case Mode.SymmetricActive:
                    case Mode.SymmetricPassive:
                        break;
                    default:
                        logger.Info("UNKNOWN message mode received and ignored");
                        break;
                }
            }
        }

        private void SendReply(SntpMessageEventArgs args)
        {
            try {
                args.Channel.SendTo(args.ResponseMessage.ToArray(), args.Channel.RemoteEndpoint);

                logger.Debug("PACKET with channel id " +
                                args.Channel.ChannelId.ToString() +
                                " successfully sent to client endpoint " +
                                args.Channel.RemoteEndpoint.ToString());

                logger.Debug(args.ResponseMessage.ToString());

                OnSntpMessageSent(this, args);
            }
            catch (Exception ex) {
                logger.Error(ex, "Error Message");
            }
        }

        #region Events

        /// <summary>
        ///     A sntp message was recived and processed.
        /// </summary>
        public event SntpMessageEventHandler OnSntpMessageReceived = delegate { };

        /// <summary>
        ///     A sntp message was recived and processed and sent to an sntp address.
        /// </summary>
        public event SntpMessageEventHandler OnSntpMessageSent = delegate { };

        #endregion Events
    }
}
