//-----------------------------------------------------------------------------
// Filename: SctpAssociationUnitTest.cs
//
// Description: Unit tests for the SctpAssociation class.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
// 
// History:
// 31 Mar 2021	Aaron Clauson	Created, Dublin, Ireland.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;

namespace SIPSorcery.Net.UnitTests
{
    public class SctpAssociationUnitTest
    {
        private ILogger logger = null;

        public SctpAssociationUnitTest(Xunit.Abstractions.ITestOutputHelper output)
        {
            logger = SIPSorcery.UnitTests.TestLogHelper.InitTestLogger(output);
        }

        /// <summary>
        /// Tests that two associations can exchange INIT and COOKIE packets to establish
        /// a connection.
        /// </summary>
        [Fact]
        public void ConnectAssociations()
        {
            logger.LogDebug("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            logger.BeginScope(System.Reflection.MethodBase.GetCurrentMethod().Name);

            BlockingCollection<byte[]> _aOut = new BlockingCollection<byte[]>();
            BlockingCollection<byte[]> _bOut = new BlockingCollection<byte[]>();

            var aTransport = new MockB2BSctpTransport(_aOut, _bOut);
            var aAssoc = new SctpAssociation(aTransport, null, 5000, 5000, 1400);
            aTransport.OnSctpPacket += aAssoc.OnPacketReceived;
            var aAssocTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            aAssoc.OnAssociationStateChanged += (state) =>
            {
                if (state == SctpAssociationState.Established)
                {
                    aAssocTcs.TrySetResult(true);
                }
            };
            _ = Task.Run(aTransport.Listen);

            var bTransport = new MockB2BSctpTransport(_bOut, _aOut);
            var bAssoc = new SctpAssociation(bTransport, null, 5000, 5000, 1400);
            bTransport.OnSctpPacket += bAssoc.OnPacketReceived;
            bTransport.OnCookieEcho += bAssoc.GotCookie;
            var bAssocTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            bAssoc.OnAssociationStateChanged += (state) =>
            {
                if (state == SctpAssociationState.Established)
                {
                    bAssocTcs.TrySetResult(true);
                }
            };
            _ = Task.Run(bTransport.Listen);

            aAssoc.Init();

            Task.WaitAll(new Task[] { aAssocTcs.Task, bAssocTcs.Task }, 5000);

            Assert.Equal(SctpAssociationState.Established, aAssoc.State);
            Assert.Equal(SctpAssociationState.Established, bAssoc.State);

            aTransport.Close();
            bTransport.Close();
        }

        /// <summary>
        /// Tests that two associations can establish a connection and then send a data chunk
        /// between them.
        /// </summary>
        [Fact]
        public void SendDataChunk()
        {
            logger.LogDebug("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            logger.BeginScope(System.Reflection.MethodBase.GetCurrentMethod().Name);

            BlockingCollection<byte[]> _aOut = new BlockingCollection<byte[]>();
            BlockingCollection<byte[]> _bOut = new BlockingCollection<byte[]>();

            var aTransport = new MockB2BSctpTransport(_aOut, _bOut);
            var aAssoc = new SctpAssociation(aTransport, null, 5000, 5000, 1400);
            aTransport.OnSctpPacket += aAssoc.OnPacketReceived;
            var aAssocTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            aAssoc.OnAssociationStateChanged += (state) =>
            {
                if (state == SctpAssociationState.Established)
                {
                    aAssocTcs.TrySetResult(true);
                }
            };
            _ = Task.Run(aTransport.Listen);

            var bTransport = new MockB2BSctpTransport(_bOut, _aOut);
            var bAssoc = new SctpAssociation(bTransport, null, 5000, 5000, 1400);
            bTransport.OnSctpPacket += bAssoc.OnPacketReceived;
            bTransport.OnCookieEcho += bAssoc.GotCookie;
            var bAssocTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            bAssoc.OnAssociationStateChanged += (state) =>
            {
                if (state == SctpAssociationState.Established)
                {
                    bAssocTcs.TrySetResult(true);
                }
            };
            _ = Task.Run(bTransport.Listen);

            aAssoc.Init();

            Task.WaitAll(new Task[] { aAssocTcs.Task, bAssocTcs.Task }, 5000);

            Assert.Equal(SctpAssociationState.Established, aAssoc.State);
            Assert.Equal(SctpAssociationState.Established, bAssoc.State);

            string message = "hello world";
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            bAssoc.OnData += (frame) => tcs.TrySetResult(Encoding.UTF8.GetString(frame.UserData));
            aAssoc.SendData(0, 0, 0, Encoding.UTF8.GetBytes(message));

            tcs.Task.Wait(1000);

            Assert.True(tcs.Task.IsCompleted);
            Assert.Equal(message, tcs.Task.Result);

            aTransport.Close();
            bTransport.Close();
        }

        /// <summary>
        /// Tests that two associations can establish a connection and then send multiple data 
        /// chunks representing a single fragmented user data payload between them.
        /// </summary>
        [Fact]
        public void SendFragmentedDataChunk()
        {
            logger.LogDebug("--> " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            logger.BeginScope(System.Reflection.MethodBase.GetCurrentMethod().Name);

            BlockingCollection<byte[]> _aOut = new BlockingCollection<byte[]>();
            BlockingCollection<byte[]> _bOut = new BlockingCollection<byte[]>();

            // Setting a very small MTU to force the sending association to use fragmented data chunks.
            ushort dummyMTU = 4;

            var aTransport = new MockB2BSctpTransport(_aOut, _bOut);
            var aAssoc = new SctpAssociation(aTransport, null, 5000, 5000, dummyMTU);
            aTransport.OnSctpPacket += aAssoc.OnPacketReceived;
            var aAssocTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            aAssoc.OnAssociationStateChanged += (state) =>
            {
                if (state == SctpAssociationState.Established)
                {
                    aAssocTcs.TrySetResult(true);
                }
            };
            _ = Task.Run(aTransport.Listen);

            var bTransport = new MockB2BSctpTransport(_bOut, _aOut);
            var bAssoc = new SctpAssociation(bTransport, null, 5000, 5000, dummyMTU);
            bTransport.OnSctpPacket += bAssoc.OnPacketReceived;
            bTransport.OnCookieEcho += bAssoc.GotCookie;
            var bAssocTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            bAssoc.OnAssociationStateChanged += (state) =>
            {
                if (state == SctpAssociationState.Established)
                {
                    bAssocTcs.TrySetResult(true);
                }
            };
            _ = Task.Run(bTransport.Listen);

            aAssoc.Init();

            Task.WaitAll(new Task[] { aAssocTcs.Task, bAssocTcs.Task }, 5000);

            Assert.Equal(SctpAssociationState.Established, aAssoc.State);
            Assert.Equal(SctpAssociationState.Established, bAssoc.State);

            string message = "hello world";
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            bAssoc.OnData += (frame) => tcs.TrySetResult(Encoding.UTF8.GetString(frame.UserData));
            aAssoc.SendData(0, 0, 0, Encoding.UTF8.GetBytes(message));

            tcs.Task.Wait(99999);

            Assert.True(tcs.Task.IsCompleted);
            Assert.Equal(message, tcs.Task.Result);

            aTransport.Close();
            bTransport.Close();
        }
    }

    /// <summary>
    /// This mock transport is designed so that two separate SCTP associations can
    /// exchange SCTP packets. The two SCTP associations must use the same BlockingCollections
    /// but in reverse order.
    /// </summary>
    internal class MockB2BSctpTransport : SctpTransport
    {
        private BlockingCollection<byte[]> _input;
        private BlockingCollection<byte[]> _output;

        private bool _exit;

        public event Action<SctpPacket> OnSctpPacket;
        public event Action<SctpTransportCookie> OnCookieEcho;

        public MockB2BSctpTransport(BlockingCollection<byte[]> output, BlockingCollection<byte[]> input)
        {
            _output = output;
            _input = input;
        }

        public void Listen()
        {
            while (!_exit)
            {
                if (_input.TryTake(out var buffer, 1000))
                {
                    SctpPacket pkt = SctpPacket.Parse(buffer);

                    // Process packet.
                    if (pkt.Chunks.Any(x => x.KnownType == SctpChunkType.INIT))
                    {
                        var initAckPacket = base.GetInitAck(pkt, null);
                        var initAckBuffer = initAckPacket.GetBytes();
                        Send(null, initAckBuffer, 0, initAckBuffer.Length);
                    }
                    else if (pkt.Chunks.Any(x => x.KnownType == SctpChunkType.COOKIE_ECHO))
                    {
                        var cookieEcho = pkt.Chunks.Single(x => x.KnownType == SctpChunkType.COOKIE_ECHO);
                        var cookie = base.GetCookie(cookieEcho, out var errorPacket);
                        OnCookieEcho?.Invoke(cookie);
                    }
                    else
                    {
                        OnSctpPacket?.Invoke(pkt);
                    }
                }
            }
        }

        public override void Send(string associationID, byte[] buffer, int offset, int length)
        {
            _output.Add(buffer.Skip(offset).Take(length).ToArray());
        }

        public void Close()
        {
            _exit = true;
        }
    }
}