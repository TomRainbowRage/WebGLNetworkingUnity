namespace PacketNetworking
{
    public enum PacketType : byte
    {
        Redirect,
        BroadcastClientList,
        BroadcastClientProperties,
        Client_BroadcastClientProperties,
        ClientUpdateNickname,
        ClientSetLocalID,
        SceneTransition,

        UpdatePlayerPos,

        Null = 255,
    }
}