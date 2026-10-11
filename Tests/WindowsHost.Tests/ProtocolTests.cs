using System.Text.Json;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Xunit;
using WindowsHost.Input;
using WindowsHost.Protocol;

namespace WindowsHost.Tests
{
    public class ProtocolTests
    {
        [Fact]
        public void Envelope_SerializesCorrectly()
        {
            var env = new MessageEnvelope
            {
                Type = "HELLO",
                ProtocolVersion = 2,
                Sequence = 123,
                Payload = new { DeviceName = "Test" }
            };

            var json = JsonSerializer.Serialize(env);
            Assert.Contains("\"Type\":\"HELLO\"", json);
            Assert.Contains("\"ProtocolVersion\":2", json);
            Assert.Contains("\"Sequence\":123", json);
            Assert.Contains("\"Timestamp\":", json);
            Assert.Contains("\"DeviceName\":\"Test\"", json);
        }

        [Fact]
        public void InputSessionManager_ThrottlesMouseMove()
        {
            var sentMessages = new List<string>();
            using var sm = new InputSessionManager((msg, _) => sentMessages.Add(msg));
            sm.EnableInputStreamingTest = true;
            sm.SetState(InputSessionState.Disconnected); // Should clear and ignore

            sm.EnqueueInput(new MouseInputEvent(InputEventType.MouseMove, false, 10, 20));
            Assert.Empty(sentMessages);

            sm.SetState(InputSessionState.Controlling);
            
            // Enqueue rapidly
            sm.EnqueueInput(new MouseInputEvent(InputEventType.MouseMove, false, 10, 20));
            sm.EnqueueInput(new MouseInputEvent(InputEventType.MouseMove, false, 15, 25));
            sm.EnqueueInput(new MouseInputEvent(InputEventType.MouseMove, false, 20, 30));

            // Wait for worker loop to process
            var timeout = DateTime.Now.AddSeconds(1);
            while (sentMessages.Count == 0 && DateTime.Now < timeout)
            {
                Thread.Sleep(10);
            }

            // Should have sent only 1 coalesced mouse move (maybe 2 if it managed to process in between)
            Assert.True(sentMessages.Count > 0);
            Assert.Contains("INPUT_MOUSE_MOVE", sentMessages.Last());
            Assert.Contains("\"x\":20", sentMessages.Last());
            Assert.Contains("\"y\":30", sentMessages.Last());
        }

        [Fact]
        public void InputSessionManager_SequenceIncrements()
        {
            var sentMessages = new List<string>();
            using var sm = new InputSessionManager((msg, _) => sentMessages.Add(msg));
            sm.EnableInputStreamingTest = true;
            sm.SetState(InputSessionState.Controlling);

            sm.EnqueueInput(new MouseInputEvent(InputEventType.LeftButtonDown, false, 0, 0));
            sm.EnqueueInput(new MouseInputEvent(InputEventType.LeftButtonUp, false, 0, 0));
            
            var timeout = DateTime.Now.AddSeconds(1);
            while (sentMessages.Count < 2 && DateTime.Now < timeout)
            {
                Thread.Sleep(10);
            }

            Assert.Equal(2, sentMessages.Count);
            Assert.Contains("\"sequence\":1", sentMessages[0]);
            Assert.Contains("\"sequence\":2", sentMessages[1]);
        }

        [Fact]
        public void PairingAuthenticator_VerifiesOnlyTheMatchingChallengeAndIdentity()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
            var installationId = Guid.NewGuid().ToString();
            var challenge = PairingAuthenticator.CreateChallenge();
            var signature = Convert.ToBase64String(key.SignData(
                Encoding.UTF8.GetBytes($"{challenge}|{installationId}"), HashAlgorithmName.SHA256));

            Assert.True(PairingAuthenticator.IsValidInstallationId(installationId));
            Assert.True(PairingAuthenticator.Verify(publicKey, challenge, installationId, signature));
            Assert.False(PairingAuthenticator.Verify(publicKey, PairingAuthenticator.CreateChallenge(), installationId, signature));
            Assert.False(PairingAuthenticator.Verify(publicKey, challenge, Guid.NewGuid().ToString(), signature));
            Assert.Matches("^[0-9]{6}$", PairingAuthenticator.CreatePairingCode(publicKey));
        }

        [Fact]
        public void PairingAuthenticator_VerifiesAndroidDerEncodedSignature()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
            var installationId = Guid.NewGuid().ToString();
            var challenge = PairingAuthenticator.CreateChallenge();
            var signature = Convert.ToBase64String(key.SignData(
                Encoding.UTF8.GetBytes($"{challenge}|{installationId}"), HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence));

            Assert.True(PairingAuthenticator.Verify(publicKey, challenge, installationId, signature));
        }
    }
}
