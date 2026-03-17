using Lithforge.Network;
using Lithforge.Network.Connection;
using NUnit.Framework;

namespace Lithforge.Network.Tests
{
    [TestFixture]
    public sealed class ConnectionStateMachineTests
    {
        [Test]
        public void InitialState_IsDisconnected()
        {
            ConnectionStateMachine sm = new ConnectionStateMachine();

            Assert.AreEqual(ConnectionState.Disconnected, sm.Current);
        }

        [Test]
        public void ValidTransition_Disconnected_To_Connecting()
        {
            ConnectionStateMachine sm = new ConnectionStateMachine();
            bool result = sm.Transition(ConnectionState.Connecting, 0f);

            Assert.IsTrue(result);
            Assert.AreEqual(ConnectionState.Connecting, sm.Current);
        }

        [Test]
        public void ValidTransition_FullHandshakeSequence()
        {
            ConnectionStateMachine sm = new ConnectionStateMachine();

            Assert.IsTrue(sm.Transition(ConnectionState.Connecting, 0f));
            Assert.IsTrue(sm.Transition(ConnectionState.Handshaking, 1f));
            Assert.IsTrue(sm.Transition(ConnectionState.Loading, 2f));
            Assert.IsTrue(sm.Transition(ConnectionState.Playing, 3f));
            Assert.AreEqual(ConnectionState.Playing, sm.Current);
        }

        [Test]
        public void ValidTransition_Handshaking_To_Disconnecting()
        {
            ConnectionStateMachine sm = new ConnectionStateMachine();
            sm.Transition(ConnectionState.Connecting, 0f);
            sm.Transition(ConnectionState.Handshaking, 1f);

            bool result = sm.Transition(ConnectionState.Disconnecting, 2f);

            Assert.IsTrue(result);
            Assert.AreEqual(ConnectionState.Disconnecting, sm.Current);
        }

        [Test]
        public void ValidTransition_Playing_To_Disconnecting()
        {
            ConnectionStateMachine sm = new ConnectionStateMachine();
            sm.Transition(ConnectionState.Connecting, 0f);
            sm.Transition(ConnectionState.Handshaking, 1f);
            sm.Transition(ConnectionState.Loading, 2f);
            sm.Transition(ConnectionState.Playing, 3f);

            bool result = sm.Transition(ConnectionState.Disconnecting, 4f);

            Assert.IsTrue(result);
            Assert.AreEqual(ConnectionState.Disconnecting, sm.Current);
        }

        [Test]
        public void ValidTransition_AnyState_To_Disconnected()
        {
            ConnectionStateMachine sm = new ConnectionStateMachine();
            sm.Transition(ConnectionState.Connecting, 0f);
            sm.Transition(ConnectionState.Handshaking, 1f);
            sm.Transition(ConnectionState.Loading, 2f);
            sm.Transition(ConnectionState.Playing, 3f);

            bool result = sm.Transition(ConnectionState.Disconnected, 4f);

            Assert.IsTrue(result);
            Assert.AreEqual(ConnectionState.Disconnected, sm.Current);
        }

        [Test]
        public void InvalidTransition_Disconnected_To_Handshaking()
        {
            ConnectionStateMachine sm = new ConnectionStateMachine();
            bool result = sm.Transition(ConnectionState.Handshaking, 0f);

            Assert.IsFalse(result);
            Assert.AreEqual(ConnectionState.Disconnected, sm.Current);
        }

        [Test]
        public void InvalidTransition_Connecting_To_Playing()
        {
            ConnectionStateMachine sm = new ConnectionStateMachine();
            sm.Transition(ConnectionState.Connecting, 0f);

            bool result = sm.Transition(ConnectionState.Playing, 1f);

            Assert.IsFalse(result);
            Assert.AreEqual(ConnectionState.Connecting, sm.Current);
        }

        [Test]
        public void InvalidTransition_Loading_To_Disconnecting()
        {
            ConnectionStateMachine sm = new ConnectionStateMachine();
            sm.Transition(ConnectionState.Connecting, 0f);
            sm.Transition(ConnectionState.Handshaking, 1f);
            sm.Transition(ConnectionState.Loading, 2f);

            bool result = sm.Transition(ConnectionState.Disconnecting, 3f);

            Assert.IsFalse(result);
            Assert.AreEqual(ConnectionState.Loading, sm.Current);
        }

        [Test]
        public void InvalidTransition_Disconnecting_To_Playing()
        {
            ConnectionStateMachine sm = new ConnectionStateMachine();
            sm.Transition(ConnectionState.Connecting, 0f);
            sm.Transition(ConnectionState.Handshaking, 1f);
            sm.Transition(ConnectionState.Disconnecting, 2f);

            bool result = sm.Transition(ConnectionState.Playing, 3f);

            Assert.IsFalse(result);
            Assert.AreEqual(ConnectionState.Disconnecting, sm.Current);
        }

        [Test]
        public void GetTimeInState_ReturnsCorrectDuration()
        {
            ConnectionStateMachine sm = new ConnectionStateMachine();
            sm.Transition(ConnectionState.Connecting, 5.0f);

            float duration = sm.GetTimeInState(8.0f);

            Assert.AreEqual(3.0f, duration, 0.001f);
        }

        [Test]
        public void IsTimedOut_ReturnsFalse_WhenWithinTimeout()
        {
            ConnectionStateMachine sm = new ConnectionStateMachine();
            sm.Transition(ConnectionState.Connecting, 10.0f);

            bool timedOut = sm.IsTimedOut(15.0f, 10.0f);

            Assert.IsFalse(timedOut);
        }

        [Test]
        public void IsTimedOut_ReturnsTrue_WhenExceedsTimeout()
        {
            ConnectionStateMachine sm = new ConnectionStateMachine();
            sm.Transition(ConnectionState.Connecting, 10.0f);

            bool timedOut = sm.IsTimedOut(25.0f, 10.0f);

            Assert.IsTrue(timedOut);
        }

        [Test]
        public void StateEntryTime_UpdatesOnTransition()
        {
            ConnectionStateMachine sm = new ConnectionStateMachine();
            sm.Transition(ConnectionState.Connecting, 1.0f);
            Assert.AreEqual(1.0f, sm.StateEntryTime, 0.001f);

            sm.Transition(ConnectionState.Handshaking, 5.0f);
            Assert.AreEqual(5.0f, sm.StateEntryTime, 0.001f);
        }

        [Test]
        public void IsValidTransition_StaticMethod_Works()
        {
            Assert.IsTrue(ConnectionStateMachine.IsValidTransition(
                ConnectionState.Disconnected, ConnectionState.Connecting));
            Assert.IsTrue(ConnectionStateMachine.IsValidTransition(
                ConnectionState.Playing, ConnectionState.Disconnected));
            Assert.IsFalse(ConnectionStateMachine.IsValidTransition(
                ConnectionState.Disconnected, ConnectionState.Playing));
        }
    }
}
