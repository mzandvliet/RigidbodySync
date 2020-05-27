using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Lidgren.Network;
using RamjetAnvil.Coroutine;
using RamjetAnvil.Util;
using UnityEngine;
using Guid = System.Guid;

namespace RamjetAnvil.RamNet {

    public class LidgrenNatPunchClient : INatPunchClient, IDisposable {

        private static readonly OnNatPunchSuccess EmptyOnSuccess = (punchId, endpoint) => { };
        private static readonly OnNatPunchFailure EmptyOnFailure = endpoint => { };

        private readonly IConnectionlessTransporter _transporter;
        private readonly TimeSpan _punchSessionCacheTimeout = TimeSpan.FromSeconds(5f);
        private readonly TimeSpan _punchAttemptTimeout = TimeSpan.FromSeconds(30f);
        private readonly LidgrenNatFacilitatorConnection _facilitatorConnection;
        //private readonly ICache<IPEndPoint, IPEndPoint> _natPunchSessionCache; 
        private readonly IDictionary<NatPunchId, PunchAttempt> _natPunchAttempts;
        private readonly IList<PunchAttempt> _natPunchRegistrations;

        private readonly IDisposable _cleanupRoutine;

        private readonly NetBuffer _tempBuffer;
        
        public LidgrenNatPunchClient(ICoroutineScheduler coroutineScheduler, LidgrenNatFacilitatorConnection facilitatorConnection,
            IConnectionlessTransporter transporter) {

            _tempBuffer = new NetBuffer();
            _transporter = transporter;
            _facilitatorConnection = facilitatorConnection;

            _natPunchAttempts = new Dictionary<NatPunchId, PunchAttempt>();
            _natPunchRegistrations = new List<PunchAttempt>();
//            _natPunchSessionCache = new RamjetCache<IPEndPoint, IPEndPoint>(
//                expireAfterAccess: _punchSessionCacheTimeout, expireAfterWrite: null);

            _facilitatorConnection.OnNatPunchSuccess += OnNatPunchSuccess;
            _facilitatorConnection.OnNatPunchFailure += OnNatPunchFailure;
            _cleanupRoutine = coroutineScheduler.Run(ConnectionTimeoutCleanup());
        }

        public NatPunchId Punch(IPEndPoint remoteEndpoint, OnNatPunchSuccess onSuccess = null, OnNatPunchFailure onFailure = null) {
            onSuccess = onSuccess ?? EmptyOnSuccess;
            onFailure = onFailure ?? EmptyOnFailure;

            var attempt = new PunchAttempt();
            attempt.Timestamp = DateTime.Now;
            attempt.EndPoint = remoteEndpoint;
            // TODO Recycle tokens
            attempt.PunchId = new NatPunchId(Guid.NewGuid().ToString());
            attempt.OnSuccess += onSuccess;
            attempt.OnFailure += onFailure;
            AddNatPunchAttempt(attempt);

            _facilitatorConnection.SendIntroduction(remoteEndpoint, attempt.PunchId);

            return attempt.PunchId;
        }

        public void Dispose() {
            _facilitatorConnection.OnNatPunchSuccess -= OnNatPunchSuccess;
            _facilitatorConnection.OnNatPunchFailure -= OnNatPunchFailure;
            _cleanupRoutine.Dispose();
        }

        private void OnNatPunchSuccess(NatPunchId punchId, IPEndPoint actualEndpoint) {
            PunchAttempt attempt;
            
            if (_natPunchAttempts.TryGetValue(punchId, out attempt)) {
                Debug.Log("Received NAT punch success to endpoint: " + actualEndpoint);

                attempt.OnSuccess(punchId, actualEndpoint);
                RemoveNatPunchAttempt(attempt);

//                _natPunchSessionCache.Insert(desiredEndpoint, desiredEndpoint);
//                _natPunchSessionCache.Insert(actualEndpoint, desiredEndpoint);
            } else {
//                _natPunchSessionCache.Insert(actualEndpoint, actualEndpoint);       
            }
        }
        
        private void OnNatPunchFailure(NatPunchId punchId, IPEndPoint actualEndpoint) {
            PunchAttempt attempt;
            if (_natPunchAttempts.TryGetValue(punchId, out attempt)) {
                attempt.OnFailure(punchId);
                RemoveNatPunchAttempt(attempt);
            }
        }
        
        private void AddNatPunchAttempt(PunchAttempt attempt) {
            _natPunchAttempts[attempt.PunchId] = attempt;
            _natPunchRegistrations.Add(attempt);
        }

        private void RemoveNatPunchAttempt(PunchAttempt attempt) {
            _natPunchAttempts.Remove(attempt.PunchId);
            _natPunchRegistrations.Remove(attempt);
        }

        private readonly IList<PunchAttempt> _removeableAttempts = new List<PunchAttempt>(); 
        private IEnumerator<WaitCommand> ConnectionTimeoutCleanup() {
            while (true) {
                _removeableAttempts.Clear();
                for (int i = 0; i < _natPunchRegistrations.Count; i++) {
                    var attempt = _natPunchRegistrations[i];
                    if (attempt.Timestamp + _punchAttemptTimeout < DateTime.Now) {
                        attempt.OnFailure(attempt.PunchId);
                        _removeableAttempts.Add(attempt);
                    }
                }
                for (int i = 0; i < _removeableAttempts.Count; i++) {
                    RemoveNatPunchAttempt(_removeableAttempts[i]);
                }
                yield return WaitCommand.WaitSeconds((float) _punchAttemptTimeout.TotalSeconds);
            }
        }

        // TODO Pool this class
        private class PunchAttempt {
            public DateTime Timestamp;
            public IPEndPoint EndPoint;
            public NatPunchId PunchId;
            public OnNatPunchSuccess OnSuccess;
            public OnNatPunchFailure OnFailure;
        }

    }
}
