# WebGLNetworkingUnity
This is a Custom Package i have worked on that is a networking package for unity that works well with unity WebGL builds but also works with most other build platforms.

- It is a packet based system so you have full control over what you send to clients.
- Easy to understand
- simple to construct Packets to Send and Recive them 
- Has inbuilt player Lists and Ids and Nicknames
- Has IsHost Functionality meaning you can write code in your unity project without modifying server to handle the main game logic to send to clients after.
- Meant for turn based board game games, if your looking for fastpaced lowlatency your in the wrong place.
- Many easy Inbuilt functions and callbacks to use.

## Architecture
This package uses a .net8 console app as the server, this server is fixed and most likely will not change since all the code you write for your project will remain in your unity project.

If you want to you can copy the server code Script and add Fleck to your unity project to allow clients to host themselves if there are no servers to host on.

## Dependencies
This package has got repackaged packages in it i have not modifed any of the code or files however just be aware there in here.
Ill list them here for credit

- [Native WebSockets](https://github.com/endel/NativeWebSocket) - Used as the backbone of this package, it allows sending and reciving data with websockets.
- [UniTask](https://github.com/Cysharp/UniTask) - Used so that WebGL can run async methods in this package.
- [Message Pack](https://github.com/MessagePack-CSharp/MessagePack-CSharp) - Used for serialization and deserialization of data byte[] in this package.
- [Lz4Net](https://github.com/MiloszKrajewski/lz4net) - Used as a dependency of Message Pack so i had to include it
- [Fleck](https://github.com/statianzo/Fleck) - Used for the c# websocket server in this package.
- [Ini Parser](https://github.com/rickyah/ini-parser) - Used in the c# websocket server for configuration purposes. 

## Licenses

This project includes third-party libraries:

- **Native WebSockets** - Licensed under **Apache 2.0** ([View License](./LICENSE_Apache-2.0.txt))
- **UniTask** - Licensed under **MIT** ([View License](./LICENSE_MIT_1.txt))
- **Message Pack** - Licensed under **MIT** ([View License](./LICENSE_MIT_2.txt))
- **lz4net** - Licensed under **BSD 2-Clause** ([View License](./LICENSE_BSD.txt))
- **Fleck** - Licensed under **MIT** ([View License](./LICENSE_MIT_3.txt))
- **Ini Parser** - Licensed under **MIT** ([View License](./LICENSE_MIT_4.txt))

These packages are redistributed in compliance with their original licenses.


## (If ive done any legal stuff wrong please reach out, this is my first time doing something like this!)

## Unity Project Setup and Import

Enter this git link into the package manager to import the package with its dependencies.
`https://github.com/TomRainbowRage/WebGLNetworkingUnity.git#v1.0.5`

Once imported attach the NetworkManagerPackets monobehaviour to a gameobject in the scene.
Make a normal MonoBehaviour script and now you can impliment some logic.
A Sample Script is at the bottom of this readme so look out for that.

## Networking Code

### Connecting to Server

You run one method to join the server, the server chooses the first person to become the "host".
`NetworkManagerPackets._instance.ConnectToServer();`

The callback `System.Action LoadedIntoServer` in NetworkManagerPackets is called when sucsessfully loaded into the server.
and the same is for `System.Action NetworkingError` that is called when you cant connect or there is a networking error.

### Constructing Packets
Defining a Packet is simple, just make a new Packet Instance with a specific PacketType.
PacketTypes are Custom so you can add as many as you want easily.

you can now write whatever data to the packet as you want.
if the type is not supported you can always use BinaryFormatter or MessagePack Packages to Seralise and Deseralise the data on each end.

```cs
Packet testPacket = new Packet(PacketType.TestPacketType); // You Define your Own Packet Types Here.

testPacket.BufferWriter.Write(100); // Can Write any data you want in order, Just remeber to read it back in that order!
testPacket.BufferWriter.Write(false);
testPacket.BufferWriter.Write(50); // You can repeat Same Types if you need to.
testPacket.BufferWriter.Write(50.755f);

// For Sending byte[] i recommed writing your byte[] data length before your byte[] since we need to read back that number of bytes.
testPacket.BufferWriter.Write(3);
testPacket.BufferWriter.Write(new byte[] { 50, 60, 10 }); // Some Abiturary Data, You could use MessagePack or BinaryFormatter.
```

### Sending Data
Its Easy to Send data you just have to specify the clientID and the packet you constrcuted

```cs
// NetworkManagerPackets.SendPacketToClient(int clientId, Packet packet)
NetworkManagerPackets._instance.SendPacketToClient(0, testPacket);
```

to send data to all clients including yourself 

```cs
foreach(int clientID in NetworkManagerPackets._instance.ConnectedClientsClient.Keys)
{
    if(clientID == NetworkManagerPackets._instance.localID) { continue; } // This makes it so that it doesnt send to self.

    NetworkManagerPackets._instance.SendPacketToClient(clientID, testPacket);
}
```

### Client List 
The NetworkManager always keeps a track of the ClientList at all times.
The ClientList is a Dictionary with clientID and the custom Player class.
`Dictionary<int, PlayerClient> // clientID : PlayerClientClass`

it can be ascessed under `NetworkManagerPackets._instance.ConnectedClientsClient`

it runs the callback `System.Action PlayersListUpdated` in NetworkManagerPackets for you to hook.

### Client

the custom Player class contains
```cs

public class PlayerClient
{
    int Id; // (same as the dictionary key)
    string Nickname; // Set By each players localNickname varible in the NetworkManagerPackets
    Dictionary<string, object> CustomProperties // this is synced across the network and will be explained later.   
}
```

Each player can also get there `int localID` and `bool IsServer` (is Host of Server) and `bool Connected` status from the NetworkManagerPackets class.

### Reciving Data

The NetworkManagerPackets class has lots of Action Callbacks but the most important one is the `Action<Packet> HandleAction`.
this gets called whenever this current clients recives a message.
you can hook it like this
```cs
NetworkManagerPackets._instance.HandleAction += Handle;
```

and i recommend using a method layout like this

```cs
void Handle(Packet packet)
{
    switch(packet.Type)
    {
        case PacketType.TestPacketType:
        {
            int int1 = packet.BufferReader.ReadInt32();
            bool bool1 = packet.BufferReader.ReadBoolean();
            int int2 = packet.BufferReader.ReadInt32();
            float float1 = packet.BufferReader.ReadSingle();

            int byteArrayLength = packet.BufferReader.ReadInt32();
            byte[] byteArray1 = packet.BufferReader.ReadBytes(byteArrayLength);

            // do something with this data, run other methods etc..

            break;
        }
        case PacketType.TestPacketType2:
        {
            // more code here
        }
    }
}
```


### Player Properites

Player properties is a nice way to sync client data across the network.
```cs
// This Doesnt have to be our own ID and you can modify other players custom properites but i wouldnt recommend unless your the host.
NetworkManagerPackets._instance.ConnectedClientsClient[NetworkManagerPackets._instance.localID].CustomProperties["testKey"] = "testValue";

// This then adds and updates our local clientList custom properites.

// Run this to sync between all clients.
NetworkManagerPackets._instance.UpdatePlayerPropertiesFromClient();
```

Now on all clients including own it runs the callback `System.Action PlayerPropertiesChanged` in NetworkManagerPackets for you to hook.


### Smaple Code


This script uses GUI to show a join button and textbox, textbox is for the nickname and a playerList.
When the player joins there mouse is synced across the network only when it is moved.

```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PacketNetworking;

public class GameController : MonoBehaviour
{
    public static GameController _instance;


    private Dictionary<int, Vector2> guiPoses = new Dictionary<int, Vector2>();

    private int roundAmount = 50;

    private Vector2 localPosLast = Vector2.zero;

    void Awake()
    {
        _instance = this;
    }

    void Start()
    {
        NetworkManagerPackets._instance.HandleAction += Handle;
    }

    void Handle(Packet packet)
    {
        switch(packet.Type)
        {
            case PacketType.UpdatePlayerPos:
            {
                int playerID = packet.BufferReader.ReadInt32();

                if(playerID == NetworkManagerPackets._instance.localID) { break; }

                Vector2 pos = new Vector2(packet.BufferReader.ReadSingle(), packet.BufferReader.ReadSingle());

                guiPoses[playerID] = pos;

                break;
            }
        }
    }

    void Update()
    {
        if(NetworkManagerPackets._instance.Connected == false) { return; }

        Vector2 roundedPos = new Vector2(RoundToMultiple(Mathf.RoundToInt(Input.mousePosition.x), roundAmount), -RoundToMultiple(Mathf.RoundToInt(Input.mousePosition.y), roundAmount) + Screen.height);

        if(localPosLast != roundedPos)
        {
            Packet updatePlayerPosPacket = new Packet(PacketType.UpdatePlayerPos, 0);
            updatePlayerPosPacket.BufferWriter.Write(NetworkManagerPackets._instance.localID);

            updatePlayerPosPacket.BufferWriter.Write(roundedPos.x);
            updatePlayerPosPacket.BufferWriter.Write(roundedPos.y);

            foreach(int clientID in NetworkManagerPackets._instance.ConnectedClientsClient.Keys)
            {
                NetworkManagerPackets._instance.SendPacketToClient(clientID, updatePlayerPosPacket);
            }

            guiPoses[NetworkManagerPackets._instance.localID] = roundedPos;

            localPosLast = roundedPos;
        }

        NetworkManagerPackets._instance.ConnectedClientsClient[NetworkManagerPackets._instance.localID].CustomProperties["testKey"] = "testValue";
    }
    
    private Vector2 GUIscrollPosition = Vector2.zero;
    void OnGUI()
    {
        if(GUI.Button(new Rect(10, 67.5f, 100, 47.5f), "Join"))
        {
            NetworkManagerPackets._instance.ConnectToServer();
        }

        NetworkManagerPackets._instance.localNickName = GUI.TextField(new Rect(150, 10, 130, 29), NetworkManagerPackets._instance.localNickName);

        GUILayout.BeginArea(new Rect(Screen.width - 200 - 10, 10, 200, 300), GUI.skin.box);
        
        // Start the scroll view
        GUIscrollPosition = GUILayout.BeginScrollView(GUIscrollPosition, GUILayout.Width(200), GUILayout.Height(300));

        // Create buttons inside the scroll view
        foreach (PlayerClient client in NetworkManagerPackets._instance.ConnectedClientsClient.Values)
        {
            if (GUILayout.Button(client.Id + ": " + client.Nickname, GUILayout.Height(40)))
            {
            }
        }

        // End the scroll view
        GUILayout.EndScrollView();
        
        GUILayout.EndArea();

        if(NetworkManagerPackets._instance.Connected == false) { return; }

        foreach(KeyValuePair<int, Vector2> guiPos in guiPoses)
        {
            GUI.Box(new Rect(guiPos.Value.x - 10, guiPos.Value.y - 10, 20, 20), "");
        }
    }


    int RoundToMultiple(int i, int multiple)
    {
        return ((i + multiple / 2) / multiple) * multiple;
    }
}
```
