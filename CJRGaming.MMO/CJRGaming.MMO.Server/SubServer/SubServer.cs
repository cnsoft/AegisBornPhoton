﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using CJRGaming.MMO.Common;
using CJRGaming.MMO.Server.MasterServer;
using ExitGames.Logging;
using ExitGames.Logging.Log4Net;
using log4net;
using log4net.Config;
using Photon.SocketServer;
using Photon.SocketServer.ServerToServer;
using LogManager = ExitGames.Logging.LogManager;

namespace CJRGaming.MMO.Server.SubServer
{
    public abstract class SubServer : ApplicationBase
    {
        #region Constants and Fields

        public static readonly Guid ServerId = Guid.NewGuid();

        protected static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        private static SubServer _instance;

        private static OutgoingMasterServerPeer _masterPeer;

        private byte _isReconnecting;

        private Timer _retry;

        private SubServerType _serverType;

        #endregion

        #region Properties

        public static SubServer Instance
        {
            get
            {
                return _instance;
            }

            protected set
            {
                Interlocked.Exchange(ref _instance, value);
            }
        }

        public int? GamingTcpPort { get; protected set; }

        public int? GamingUdpPort { get; protected set; }

        public IPEndPoint MasterEndPoint { get; protected set; }

        public OutgoingMasterServerPeer MasterPeer
        {
            get
            {
                return _masterPeer;
            }

            protected set
            {
                Interlocked.Exchange(ref _masterPeer, value);
            }
        }

        public SubServerType ServerType { get { return _serverType; } protected set { _serverType = value; } }

        public IPAddress PublicIpAddress { get; protected set; }

        protected int ConnectRetryIntervalSeconds { get; set; }

        protected bool _acceptsSubServerConnections;
        protected Dictionary<Server2Server.Operations.OperationCode, IPhotonRequestHandler> SubServerRequestHandlers = new Dictionary<Server2Server.Operations.OperationCode, IPhotonRequestHandler>();
        protected Dictionary<Server2Server.Operations.EventCode, IPhotonEventHandler> SubServerEventHandlers = new Dictionary<Server2Server.Operations.EventCode, IPhotonEventHandler>();
        protected Dictionary<Server2Server.Operations.OperationCode, IPhotonResponseHandler> SubServerResponseHandlers = new Dictionary<Server2Server.Operations.OperationCode, IPhotonResponseHandler>();
        
        #endregion

        public SubServer()
        {
            IPAddress address = IPAddress.Parse(SubServerSettings.Default.MasterIPAddress);
            int port = SubServerSettings.Default.OutgoingMasterServerPeerPort;
            MasterEndPoint = new IPEndPoint(address, port);

            GamingTcpPort = SubServerSettings.Default.GamingTcpPort;
            GamingUdpPort = SubServerSettings.Default.GamingUdpPort;
            ConnectRetryIntervalSeconds = SubServerSettings.Default.ConnectReytryInterval;
            PublicIpAddress = IPAddress.Parse(SubServerSettings.Default.PublicIPAddress);
        }

        protected virtual void InitLogging()
        {
            LogManager.SetLoggerFactory(Log4NetLoggerFactory.Instance);
            GlobalContext.Properties["LogFileName"] = "SS" + ApplicationName;
            XmlConfigurator.ConfigureAndWatch(new FileInfo(Path.Combine(BinaryPath, "log4net.config")));
        }

        public void ConnectToMaster()
        {
            if (ConnectToServer(MasterEndPoint, "Master", MasterEndPoint) == false)
            {
                Log.Warn("master connection refused");
                return;
            }

            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat(_isReconnecting == 0 ? "Connecting to master at {0}" : "Reconnecting to master at {0}", MasterEndPoint);
            }
        }

        public void ReconnectToMaster()
        {
            Thread.VolatileWrite(ref _isReconnecting, 1);
            _retry = new Timer(o => ConnectToMaster(), null, ConnectRetryIntervalSeconds * 1000, 0);
        }

        protected virtual OutgoingMasterServerPeer CreateMasterPeer(InitResponse initResponse)
        {
            return new OutgoingMasterServerPeer(initResponse.Protocol, initResponse.PhotonPeer, this);
        }

        #region Overrides of ApplicationBase

        protected override void Setup()
        {
            Instance = this;
            InitLogging();

            Protocol.AllowRawCustomValues = true;

            ConnectToMaster();
            AddHandlers();
        }

        protected override void TearDown()
        {
            
        }

        protected override void OnServerConnectionFailed(int errorCode, string errorMessage, object state)
        {
            if (_isReconnecting == 0)
            {
                Log.ErrorFormat("Master connection failed with err {0}: {1}", errorCode, errorMessage);
            }
            else if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("Master connection failed with err {0}: {1}", errorCode, errorMessage);
            }

            ReconnectToMaster();
        }

        protected override ServerPeerBase CreateServerPeer(InitResponse initResponse, object state)
        {
            // master
            Thread.VolatileWrite(ref _isReconnecting, 0);
            return MasterPeer = CreateMasterPeer(initResponse);
        }

        #endregion

        #region Handlers

        public abstract void AddHandlers();

        public void AddRequestHandler(OperationSubCode subCode, IPhotonRequestHandler handler)
        {
            MasterPeer.RequestHandlers.Add(subCode, handler);
        }

        public void AddEventHandler(EventCode eventCode, IPhotonEventHandler handler)
        {
            MasterPeer.EventHandlers.Add(eventCode, handler);
        }

        public void AddResponseHandler(OperationSubCode subCode, IPhotonResponseHandler handler)
        {
            MasterPeer.ResponseHandlers.Add(subCode, handler);
        }

        public void AddSubServerRequestHandler(Server2Server.Operations.OperationCode subCode, IPhotonRequestHandler handler)
        {
            SubServerRequestHandlers.Add(subCode, handler);
        }

        public void AddSubServerEventHandler(Server2Server.Operations.EventCode eventCode, IPhotonEventHandler handler)
        {
            SubServerEventHandlers.Add(eventCode, handler);
        }

        public void AddSubServerResponseHandler(Server2Server.Operations.OperationCode subCode, IPhotonResponseHandler handler)
        {
            SubServerResponseHandlers.Add(subCode, handler);
        }

        #endregion

        #region Overrides of ApplicationBase

        protected override PeerBase CreatePeer(InitRequest initRequest)
        {
            if(_acceptsSubServerConnections)
            {
                if (IsSubServerPeer(initRequest))
                {
                    if (Log.IsDebugEnabled)
                    {
                        Log.DebugFormat("Received init request from sub server");
                    }

                    var subserver = new IncomingSubServerToSubServerPeer(initRequest, this)
                                        {
                                            SubServerRequestHandlers = SubServerRequestHandlers,
                                            SubServerResponseHandlers = SubServerResponseHandlers,
                                            SubServerEventHandlers = SubServerEventHandlers
                                        };
                    return subserver;
                }
            }
            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("Connection Rejected from {0}:{1}", initRequest.RemoteIP, initRequest.RemotePort);
            }
            return null;
        }

        protected virtual bool IsSubServerPeer(InitRequest initRequest)
        {
            return false;
        }

        #endregion

    }
}
