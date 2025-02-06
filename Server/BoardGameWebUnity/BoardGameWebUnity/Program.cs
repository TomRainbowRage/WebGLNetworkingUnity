using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Fleck;
using IniParser;
using IniParser.Model;
using MessagePack;

namespace BoardGameWebUnity
{
    internal class Program
    {

        static IniData data;
        static int GamePort;

        public static WebSocketServer server;

        public static Dictionary<int, PlayerClient> ConnectedClientsServer { get; private set; } = new Dictionary<int, PlayerClient>();

        public const int sophSize = 5;
        public const int eophSize = 9;

        public static int playerIsHost = -1;

        static void Main(string[] args)
        {
            AppContext.SetSwitch("System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization", true);

            var parser = new FileIniDataParser();

            if(File.Exists("config.ini") == false)
            {
                IniData Inidata = new IniData();
                Inidata.Sections.AddSection("Settings");
                Inidata["Settings"].AddKey("GamePort", "");
                parser.WriteFile("config.ini", Inidata);
            }

            data = parser.ReadFile("config.ini");

            //string test = data["Settings"]["GamePort"];

            //Console.WriteLine("TestKey = " + test);
            try
            {
                GamePort = int.Parse(data["Settings"]["GamePort"]);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
                return;
            }


            server = new WebSocketServer("ws://0.0.0.0:" + GamePort);

            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    Console.WriteLine("Connection opened.");

                    int id = GetLeastID();

                    ConnectedClientsServer[id] = new PlayerClient() { client = socket, Id = id, Nickname = "", CustomProperties = new Dictionary<string, object>() };

                    bool hostFound = false;
                    for (int i = 0; i < ConnectedClientsServer.Count; i++)
                    {
                        if(ConnectedClientsServer[i].Id == playerIsHost)
                        {
                            hostFound = true;
                        }
                    }

                    if (hostFound == false)
                    {
                        playerIsHost = id;
                    }

                    BroadcastPlayerList(id);



                };

                socket.OnClose = () =>
                {
                    Console.WriteLine("Connection closed.");

                    int index = -1;

                    foreach(KeyValuePair<int, PlayerClient> client in ConnectedClientsServer)
                    {
                        if(client.Value.client == socket) { index = client.Key; break; }
                    }

                    if(index != -1)
                    {
                        ConnectedClientsServer.Remove(index);
                        
                        if(index == playerIsHost)
                        {
                            if (ConnectedClientsServer.Count != 0) { playerIsHost = ConnectedClientsServer.ElementAt(0).Key; }
                            else { playerIsHost = -1; }
                        }

                        BroadcastPlayerList(index);
                    }
                    else
                    {
                        Console.WriteLine("Unkown Player to Remove");
                    }
                };

                socket.OnBinary = bytes =>
                {
                    //Console.WriteLine($"Received: {System.BitConverter.ToString(bytes).Replace("-", " ")}");
                    Handle(new Packet(bytes));
                };
            });


            Console.ReadLine();
        }


        public static void Handle(Packet packet)
        {
            switch (packet.Type)
            {
                case PacketType.Redirect:
                {
                    
                    int sendToClient = packet.BufferReader.ReadInt32();
                    int byteArrayLength = packet.BufferReader.ReadInt32();
                    byte[] packetData = packet.BufferReader.ReadBytes(byteArrayLength);

                    SendPacketToClient(sendToClient, new Packet(packetData));

                    break;
                }
                case PacketType.Client_BroadcastClientProperties:
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

                        ConnectedClientsServer[clientId].CustomProperties = propDict;
                    }

                    BroadcastPlayerProperties();
                    
                    break;
                }
                case PacketType.ClientUpdateNickname:
                {
                    
                    int clientId = packet.BufferReader.ReadInt32();
                    string nickname = packet.BufferReader.ReadString();

                    ConnectedClientsServer[clientId].Nickname = nickname;

                    BroadcastPlayerList(-1);
                    

                    break;
                }
            }
        }

        public static void SendPacketToClient(int clientId, Packet packet)
        {
            if (ConnectedClientsServer.TryGetValue(clientId, out var targetPeer))
            {
                targetPeer.client.Send(packet.makeByteToSend());
                //if (!ignoreLogPackets.Contains(packet.Type)) { Debug.Log($"Server sent data to Client {clientId} " + packet.Type); }
            }
            else
            {
                Console.WriteLine($"Client {clientId} not found.");
            }
        }

        public static void BroadcastPlayerList(int clientIdNick)
        {
            Packet broadcastList = new Packet(PacketType.BroadcastClientList, 0);
            broadcastList.BufferWriter.Write(ConnectedClientsServer.Count);

            broadcastList.BufferWriter.Write(clientIdNick);

            broadcastList.BufferWriter.Write(playerIsHost);



            foreach (PlayerClient client in ConnectedClientsServer.Values)
            {
                broadcastList.BufferWriter.Write(client.Id);
                broadcastList.BufferWriter.Write(client.Nickname);

                /*
                using (var memoryStream = new MemoryStream())
                {
                    var formatter = new BinaryFormatter();
                    formatter.Serialize(memoryStream, client.CustomProperties);
                    byte[] propDictData = memoryStream.ToArray();

                    broadcastList.BufferWriter.Write(propDictData.Length);
                    broadcastList.BufferWriter.Write(propDictData);
                }
                */

                byte[] propDictData = MessagePackSerializer.Serialize(client.CustomProperties);

                broadcastList.BufferWriter.Write(propDictData.Length);
                broadcastList.BufferWriter.Write(propDictData);
            }

            foreach (int i in ConnectedClientsServer.Keys)
            {
                Packet setClientLocalID = new Packet(PacketType.ClientSetLocalID, 0);
                setClientLocalID.BufferWriter.Write(i);

                SendPacketToClient(i, setClientLocalID);

                SendPacketToClient(i, broadcastList);
            }
        }

        private static void BroadcastPlayerProperties()
        {

            Packet broadcastProperties = new Packet(PacketType.BroadcastClientProperties, 0);
            broadcastProperties.BufferWriter.Write(ConnectedClientsServer.Count);

            foreach (PlayerClient client in ConnectedClientsServer.Values)
            {
                broadcastProperties.BufferWriter.Write(client.Id);

                /*
                using (var memoryStream = new MemoryStream())
                {
                    var formatter = new BinaryFormatter();
                    formatter.Serialize(memoryStream, client.CustomProperties);
                    byte[] propDictData = memoryStream.ToArray();

                    broadcastProperties.BufferWriter.Write(propDictData.Length);
                    broadcastProperties.BufferWriter.Write(propDictData);
                }
                */

                byte[] propDictData = MessagePackSerializer.Serialize(client.CustomProperties);
                
                broadcastProperties.BufferWriter.Write(propDictData.Length);
                broadcastProperties.BufferWriter.Write(propDictData);


            }

            foreach (int i in ConnectedClientsServer.Keys)
            {
                SendPacketToClient(i, broadcastProperties);
            }
        }

        private static int GetLeastID()
        {
            int id = 0;
            while (ConnectedClientsServer.ContainsKey(id)) { id++; }

            return id;
        }
    }

    public class PlayerClient
    {
        public IWebSocketConnection client;
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

        public Packet(PacketType packetType, int channel)
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
            if (data.Length < Program.sophSize + Program.eophSize) { throw new System.Exception("packet size too small"); }

            using (MemoryStream buf = new MemoryStream())
            {
                buf.Write(data, 0, data.Length);

                BinaryReader binaryReader = new BinaryReader(buf);

                long dataLen = data.Length - Program.sophSize - Program.eophSize;

                byte[] test = new byte[3];
                //buf.Read(test, 0, 3); // Read the next 3 bytes from the buffer
                test = binaryReader.ReadBytes(3);
                buf.Seek(0, SeekOrigin.Begin); // Reset the position in the buffer

                //Official start of packet header
                //Size: 5 bytes + data
                //0x0  (4 bytes, uint32) - Packet timestamp
                //0x4  (1 byte,  byte)   - Packet type
                //0x5… (x bytes)         - Packet data, to be interpreted by the packet type's handler

                Buffer = new MemoryStream();
                BufferReader = new BinaryReader(Buffer);
                BufferWriter = new BinaryWriter(Buffer);
                Timestamp = binaryReader.ReadUInt32();
                Type = (PacketType)binaryReader.ReadByte();

                if (dataLen > 0)
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

            str += $": {Type} ";

            str += "[";
            byte[] b = Buffer.ToArray();

            for (int i = 0; i < b.Length; i++)
            {
                if (i > 0) { str += " "; }
                str += System.BitConverter.ToString(new byte[] { b[i] }).Replace("-", "");
            }

            str += "]";

            return str;
        }

        public byte[] makeByteToSend()
        {
            uint serverRealTime = (uint)System.DateTimeOffset.Now.ToUnixTimeSeconds();
            byte[] array = new byte[Buffer.ToArray().Length + Program.sophSize + Program.eophSize];
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

    public enum PacketType : byte
    {
        Redirect,
        BroadcastClientList,
        BroadcastClientProperties,
        Client_BroadcastClientProperties,
        ClientUpdateNickname,
        ClientSetLocalID,

        Null = 255,
    }
}
