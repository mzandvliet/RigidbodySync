using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Policy;
using System.Text;
using RamjetAnvil.Volo.MasterServerClient;
using UnityEngine;

public class UnityMasterServerClient : MonoBehaviour {

    [SerializeField] private string _url = "http://127.0.0.1:15492/";
    [SerializeField] private float _requestTimeout = 3f;

    private IMasterServerClient _client;

    public IMasterServerClient Client {
        get {
            if (_client == null) {
                _client = new MasterServerClient(_url, requestTimeout: TimeSpan.FromSeconds(_requestTimeout));
            }
            return _client;
        }
    }
}
