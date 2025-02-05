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
