//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Net;
//using System.Text;
//using Lidgren.Network;
//using Newtonsoft.Json;
//using RamjetAnvil.RamNet;
//using RamjetAnvil.Util;
//using UnityEngine;
//
//public class SimpleSocketPuncher : MonoBehaviour {
//
//    [SerializeField] private int _localPort = 22151;
//    [SerializeField] private LidgrenNetworkTransporter _transporter;
//    [SerializeField] private Ipv4Endpoint _remoteEndpoint;
//    [SerializeField] private float _sendInterval = 0.5f;
//    [SerializeField] private UnityCoroutineScheduler _coroutineScheduler;
//
//    private NetBuffer _buffer;
//
//    void Awake() {
//        int port = _localPort;
//        if (File.Exists("./config.json")) {
//            IDictionary<string, object> config;
//            using (var fileStream = new FileStream("./config.json", FileMode.Open))
//            using (var jsonReader = new JsonTextReader(new StreamReader(fileStream))) {
//                var jsonSerializer = new JsonSerializer();
//                config = jsonSerializer.Deserialize<IDictionary<string, object>>(jsonReader);
//            }
//            port = Convert.ToInt32(config["localPort"]);
//            _remoteEndpoint = Ipv4Endpoint.Parse(Convert.ToString(config["remoteEndpoint"]));
//        }
//
//        Debug.Log("Starting socket on " + _localPort);
//        Debug.Log("Remote endpoint is: " + _remoteEndpoint);
//
//        var natFacilitatorConnection =
//            new LidgrenNatFacilitatorConnection(new IPEndPoint(IPAddress.Parse("95.85.31.166"), 15493),
//                _transporter);
//        _coroutineScheduler.Run(natFacilitatorConnection.Register());
//
//        var lidgrenConfig = new NetPeerConfiguration("SimpleSocketPuncher");
//        lidgrenConfig.Port = _localPort;
//        _transporter.Open(lidgrenConfig);
//
//        _buffer = new NetBuffer();
//    }
//
//    IEnumerator Start() {
//        while (_transporter.Status == TransporterStatus.Open) {
//            _buffer.Reset();
//            _buffer.Write("!!!!!");
//            Debug.Log("sending data to "+ _remoteEndpoint );
//            _transporter.SendUnconnected(_remoteEndpoint.ToIpEndPoint(), _buffer);
//            yield return new WaitForSeconds(Mathf.Max(_sendInterval, 0.0166f));
//        }
//    } 
//
//}
