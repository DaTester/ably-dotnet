﻿using Ably.Realtime;
using Ably.Transport;
using Moq;
using Xunit;

namespace Ably.Tests
{
    public class RealtimeTests
    {
        private static readonly string Debug_Key = "123.456:789";

        [Fact]
        public void When_HostNotSetInOptions_UseBinaryProtocol_TrueByDefault()
        {
            // Arrange
            AblyRealtimeOptions options = new AblyRealtimeOptions();

            // Act
            Assert.True(options.UseBinaryProtocol);
        }

        [Fact]
        public void New_Realtime_HasConnection()
        {
            AblyRealtime realtime = new AblyRealtime(Debug_Key);
            Assert.NotNull(realtime.Connection);
        }

        [Fact]
        public void New_Realtime_HasChannels()
        {
            AblyRealtime realtime = new AblyRealtime(Debug_Key);
            Assert.NotNull(realtime.Channels);
        }

        [Fact]
        public void New_Realtime_HasAuth()
        {
            AblyRealtime realtime = new AblyRealtime(Debug_Key);
            Assert.NotNull(realtime.Auth);
        }

        [Fact]
        public void New_Realtime_WithConnectAutomatically_False_DoesNotConnect()
        {
            // Arrange
            Mock<IConnectionManager> mock = new Mock<IConnectionManager>();
            AblyRealtimeOptions options = new AblyRealtimeOptions(Debug_Key) { AutoConnect = false };

            // Act
            AblyRealtime realtime = new AblyRealtime(options, mock.Object);

            // Assert
            Assert.Equal<ConnectionState>(ConnectionState.Initialized, realtime.Connection.State);
            mock.Verify(c => c.Connect(), Times.Never());
        }

        [Fact]
        public void New_Realtime_WithConnectAutomatically_True_ConnectsAutomatically()
        {
            // Arrange
            Mock<IConnectionManager> mock = new Mock<IConnectionManager>();
            AblyRealtimeOptions options = new AblyRealtimeOptions(Debug_Key);

            // Act
            AblyRealtime realtime = new AblyRealtime(options, mock.Object);

            // Assert
            Assert.Equal<ConnectionState>(ConnectionState.Connecting, realtime.Connection.State);
            mock.Verify(c => c.Connect(), Times.Once());
        }
    }
}
