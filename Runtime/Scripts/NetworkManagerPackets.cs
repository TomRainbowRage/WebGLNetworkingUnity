using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using NativeWebSocket;
using System.IO;
using System;
//using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine.SceneManagement;
using System.Linq;
using MessagePack;

namespace PacketNetworking
{
    public class NetworkManagerPackets : MonoBehaviour
    {
        public const int sophSize = 5;
        public const int eophSize = 9;
        [SerializeField] private int GamePort = 3000;
        [SerializeField] private string GameHost = "localhost";

        [Header("Debug")]
        [SerializeField] private PacketType[] ignoreLogPackets = { };
        public bool DEBUG_LogPackets = true;
        public bool DEBUG_Log = true;

        public WebSocket _client;
        public static NetworkManagerPackets _instance { get; private set; }

        public bool IsServer { get; private set; }
        public int HostID { get; private set; }
        public bool Connected { get; private set; }

        public Dictionary<int, PlayerClient> ConnectedClientsClient { get; private set; } = new Dictionary<int, PlayerClient>();

        public System.Action PlayerPropertiesChanged;
        public System.Action PlayersListUpdated;
        public System.Action LoadedIntoServer;
        public System.Action NetworkingError;
        public System.Action<Packet> HandleAction;

        [Header("Local")]

        public string localNickName = "";
        public int localID = -1;

        void Awake()
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            #if !UNITY_WEBGL || UNITY_EDITOR
                _client?.DispatchMessageQueue();
            #endif
        }

        public void ConnectToServer()
        {
            ClientWebSocket().Forget();
        }

        public void HandlePackets(Packet packet)
        {
            if(!ignoreLogPackets.Contains(packet.Type) && DEBUG_Log && DEBUG_LogPackets) { Debug.Log("Recived from Server " + ": " + packet); }


            switch (packet.Type)
            {
                case PacketType.ClientSetLocalID:
                {
                    localID = packet.BufferReader.ReadInt32();
                    //Debug.Log("Set LocalID " + localID);

                    break;
                }
                case PacketType.BroadcastClientList:
                {
                    int playerCount = packet.BufferReader.ReadInt32();
                    ConnectedClientsClient.Clear();

                    int clientIdSendNick = packet.BufferReader.ReadInt32();

                    HostID = packet.BufferReader.ReadInt32();
                    IsServer = (HostID == localID);

                    for (int i = 0; i < playerCount; i++)
                    {
                        int clientId = packet.BufferReader.ReadInt32();
                        string clientNick = packet.BufferReader.ReadString();

                        int propDictLengthBytes = packet.BufferReader.ReadInt32();
                        byte[] propDictBytes = packet.BufferReader.ReadBytes(propDictLengthBytes);
                        Dictionary<string, object> propDict;

                        using (var memoryStream = new MemoryStream(propDictBytes))
                        {
                            //var formatter = new BinaryFormatter();
                            //propDict = (Dictionary<string, object>)formatter.Deserialize(memoryStream);
                            propDict = MessagePackSerializer.Deserialize<Dictionary<string, object>>(memoryStream);
                        }

                        ConnectedClientsClient[clientId] = new PlayerClient() { Id = clientId, Nickname = clientNick, CustomProperties = propDict };
                    }

                    //Debug.Log("Needs localID set");

                    if(clientIdSendNick == localID)
                    {
                        //Debug.Log("Sending Nick");

                        Packet UpdateNickname = new Packet(PacketType.ClientUpdateNickname, 0);
                        UpdateNickname.BufferWriter.Write(clientIdSendNick);
                        UpdateNickname.BufferWriter.Write(localNickName);

                        _client.Send(UpdateNickname.makeByteToSend());
                    }

                    if(!Connected) { Connected = true; }

                    PlayersListUpdated?.Invoke();

                    break;
                }
                case PacketType.BroadcastClientProperties:
                {
                    int playerCount = packet.BufferReader.ReadInt32();

                    for (int i = 0; i < playerCount; i++)
                    {
                        int clientId = packet.BufferReader.ReadInt32();

                        int propDictLengthBytes = packet.BufferReader.ReadInt32();
                        byte[] propDictBytes = packet.BufferReader.ReadBytes(propDictLengthBytes);
                        Dictionary<string, object> propDict;

                        using (var memoryStream = new MemoryStream(propDictBytes))
                        {
                            //var formatter = new BinaryFormatter();
                            //propDict = (Dictionary<string, object>)formatter.Deserialize(memoryStream);
                            propDict = MessagePackSerializer.Deserialize<Dictionary<string, object>>(memoryStream);
                        }

                        ConnectedClientsClient[clientId].CustomProperties = propDict;
                    }

                    PlayerPropertiesChanged?.Invoke();



                    break;
                }
                case PacketType.SceneTransition:
                {
                    SceneManager.LoadScene(packet.BufferReader.ReadInt32());
                    break;
                }
            }

            HandleAction?.Invoke(packet);
        }

        public void SendPacketToClient(int clientId, Packet packet)
        {
            Packet redirectPacket = new Packet(PacketType.Redirect, 0);

            byte[] packetData = packet.makeByteToSend();

            redirectPacket.BufferWriter.Write(clientId);
            redirectPacket.BufferWriter.Write(packetData.Length);
            redirectPacket.BufferWriter.Write(packetData);

            _client.Send(redirectPacket.makeByteToSend());

            if(!ignoreLogPackets.Contains(packet.Type) && DEBUG_Log && DEBUG_LogPackets) { Debug.Log("Sent to " + clientId + ": " + packet); }
        }

        public void UpdatePlayerPropertiesFromClient()
        {
            Packet broadcastPropertiesClient = new Packet(PacketType.Client_BroadcastClientProperties, 0);
            broadcastPropertiesClient.BufferWriter.Write(ConnectedClientsClient.Count);

            foreach(PlayerClient client in ConnectedClientsClient.Values)
            {
                broadcastPropertiesClient.BufferWriter.Write(client.Id);

                /*
                using (var memoryStream = new MemoryStream())
                {
                    var formatter = new BinaryFormatter();
                    formatter.Serialize(memoryStream, client.CustomProperties);
                    byte[] propDictData = memoryStream.ToArray();

                    broadcastPropertiesClient.BufferWriter.Write(propDictData.Length);
                    broadcastPropertiesClient.BufferWriter.Write(propDictData);
                }
                */

                byte[] propDictData = MessagePackSerializer.Serialize(client.CustomProperties);

                broadcastPropertiesClient.BufferWriter.Write(propDictData.Length);
                broadcastPropertiesClient.BufferWriter.Write(propDictData);
            }

            _client.Send(broadcastPropertiesClient.makeByteToSend());
        }

        async UniTaskVoid ClientWebSocket()
        {
            _client = new WebSocket("ws://" + GameHost + ":" + GamePort); // "ws://localhost:3000"

            _client.OnOpen += () =>
            {
                if(DEBUG_Log) { Debug.Log("[CLIENT] Connection open!"); }

                LoadedIntoServer?.Invoke();

                
            };

            _client.OnClose += (Message) =>
            {
                if(DEBUG_Log) { Debug.Log("[CLIENT] Connection closed!"); }

                Connected = false;
            };

            _client.OnError += (Message) =>
            {
                if(DEBUG_Log) { Debug.Log("[CLIENT] Error! " + Message); }

                Connected = false;

                NetworkingError?.Invoke();
            };

            _client.OnMessage += (bytes) =>
            {
                //Debug.Log("[CLIENT] OnMessage!");
                //Debug.Log("[CLIENT] " + System.BitConverter.ToString(bytes).Replace("-", " "));
                //ZzzLog._instance.HandleLog("[CLIENT] " + System.BitConverter.ToString(bytes).Replace("-", " "), "", LogType.Log);

                // getting the message as a string
                // var message = System.Text.Encoding.UTF8.GetString(bytes);
                // Debug.Log("OnMessage! " + message);

                HandlePackets(new Packet(bytes));
            };

            await _client.Connect();
        }

        private async void OnApplicationQuit()
        {
            if(_client == null || _client == default) { return; }

            await _client.Close();
        }
    }

    public class PlayerClient
    {
        //public IWebSocketConnection client; // prob want to replace with the GUID
        public string Nickname;
        public int Id;
        public Dictionary<string, object> CustomProperties = new Dictionary<string, object>();
    }

    public class Packet
    {
        public MemoryStream Buffer;
        public BinaryReader BufferReader;
        public BinaryWriter BufferWriter;

        public PacketType Type;
        public int Channel;
        public uint Timestamp;

        public Packet(PacketType packetType, int channel = 0)
        {
            Buffer = new MemoryStream();
            BufferReader = new BinaryReader(Buffer);
            BufferWriter = new BinaryWriter(Buffer);
            Timestamp = (uint)System.DateTimeOffset.Now.ToUnixTimeSeconds();
            Type = packetType;
            
            Channel = channel;
        }

        public Packet(byte[] data)
        {
            if(data.Length < NetworkManagerPackets.sophSize+NetworkManagerPackets.eophSize) { throw new System.Exception("packet size too small"); }

            using (MemoryStream buf = new MemoryStream())
            {
                buf.Write(data, 0, data.Length);

                BinaryReader binaryReader = new BinaryReader(buf);

                long dataLen = data.Length - NetworkManagerPackets.sophSize - NetworkManagerPackets.eophSize;
            
                byte[] test = new byte[3];
                //buf.Read(test, 0, 3); // Read the next 3 bytes from the buffer
                test = binaryReader.ReadBytes(3);
                buf.Seek(0, SeekOrigin.Begin); // Reset the position in the buffer

                //Official start of packet header
                //Size: 5 bytes + data
                //0x0  (4 bytes, uint32) - Packet timestamp
                //0x4  (1 byte,  byte)   - Packet type
                //0x5… (x bytes)         - Packet data, to be interpreted by the packet type's handler
                // Channel

                Buffer = new MemoryStream();
                BufferReader = new BinaryReader(Buffer);
                BufferWriter = new BinaryWriter(Buffer);
                Timestamp = binaryReader.ReadUInt32();
                Type = (PacketType)binaryReader.ReadByte();

                if(dataLen > 0)
                {
                    BufferWriter.Write(binaryReader.ReadBytes((int)dataLen));
                    Buffer.Seek(0, SeekOrigin.Begin);
                }

                Channel = (int)binaryReader.ReadByte();
            }
        }

        public override string ToString()
        {
            string str = $"[{Channel} {Timestamp}]";

            str += $": {(byte)Type}:{Type} ";

            str += "[";
            byte[] b = Buffer.ToArray();

            str += System.BitConverter.ToString(b).Replace("-", " ");

            str += "]";
            
            return str;
        }

        public byte[] makeByteToSend()
        {
            uint serverRealTime = (uint)System.DateTimeOffset.Now.ToUnixTimeSeconds();
            byte[] array = new byte[Buffer.ToArray().Length + NetworkManagerPackets.sophSize + NetworkManagerPackets.eophSize];
            byte[] result;
            using (MemoryStream memoryStream = new MemoryStream(array))
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
                {
                    binaryWriter.Write(serverRealTime);
                    binaryWriter.Write((byte)Type);
                    binaryWriter.Write(Buffer.ToArray());
                    binaryWriter.Write((byte)Channel);
                    result = array;
                }
            }
            return result;
        }

    }
}
