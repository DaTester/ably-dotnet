﻿using System;
using System.Threading.Tasks;
using IO.Ably.Types;

namespace IO.Ably.Transport.States.Connection
{
    internal class ConnectionClosingState : ConnectionStateBase
    {
        private const int CloseTimeout = 1000;
        private readonly ICountdownTimer _timer;

        public ConnectionClosingState(IConnectionContext context) :
            this(context, null, new CountdownTimer("Closing state timer"))
        {
        }

        public ConnectionClosingState(IConnectionContext context, ErrorInfo error, ICountdownTimer timer) :
            base(context)
        {
            _timer = timer;
            Error = error ?? ErrorInfo.ReasonClosed;
        }

        public override Realtime.ConnectionState State => Realtime.ConnectionState.Closing;

        public override Task<bool> OnMessageReceived(ProtocolMessage message)
        {
            switch (message.Action)
            {
                case ProtocolMessage.MessageAction.Closed:
                    {
                        TransitionState(new ConnectionClosedState(Context));
                        return TaskConstants.BooleanTrue;
                    }
                case ProtocolMessage.MessageAction.Disconnected:
                    {
                        TransitionState(new ConnectionDisconnectedState(Context, message.Error));
                        return TaskConstants.BooleanTrue;
                    }
                case ProtocolMessage.MessageAction.Error:
                    {
                        TransitionState(new ConnectionFailedState(Context, message.Error));
                        return TaskConstants.BooleanTrue;
                    }
            }
            return TaskConstants.BooleanFalse;
        }

        public override void AbortTimer()
        {
            _timer.Abort();
        }

        public override Task OnAttachToContext()
        {
            var transport = Context.Transport;
            if (transport?.State == TransportState.Connected)
            {
                Context.SendToTransport(new ProtocolMessage(ProtocolMessage.MessageAction.Close));
                _timer.Start(TimeSpan.FromMilliseconds(CloseTimeout), OnTimeOut);
            }
            else
            {
                Context.SetState(new ConnectionClosedState(Context));
            }
            return TaskConstants.BooleanTrue;
        }

        private void OnTimeOut()
        {
            Context.Execute(() =>
                Context.SetState(new ConnectionClosedState(Context)));
        }

        private void TransitionState(ConnectionStateBase newState)
        {
            _timer.Abort();
            Context.SetState(newState);
        }
    }
}