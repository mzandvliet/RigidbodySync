using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using RamjetAnvil.Volo.MasterServerClient;
using UnityEngine;

public class GameHeadless : MonoBehaviour {

    [SerializeField] private RamjetServer _server;

    void Start() {
        Application.runInBackground = true;

        _server.AuthToken = new AuthToken("dummy", "dummy");

        IDictionary<string, object> config;
        using (var fileStream = new FileStream("./config.json", FileMode.Open))
        using (var jsonReader = new JsonTextReader(new StreamReader(fileStream))) {
            var jsonSerializer = new JsonSerializer();
            config = jsonSerializer.Deserialize<IDictionary<string, object>>(jsonReader);
        }
        Application.targetFrameRate = Convert.ToInt32(config["framerate"]);
        _server.Host(
            Convert.ToString(config["hostname"]), 
            Convert.ToInt32(config["port"]));
    }

    void OnDestroy() {
        _server.Stop();
    }
}
