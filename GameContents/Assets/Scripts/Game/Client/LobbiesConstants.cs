namespace Game.Client
{
    public static class LobbiesConstants
    {
        // lobby custom properties
        //=============================================

        // Keys
        public const string LOBBY_NAME = "LobbyName";
        public const string LOBBY_STATE = "LobbyState";

        // Values
        public const string WAITING_FOR_ALL_READY = "WaitingForAllReady";
        public const string FINISHED_ALL_READY_TO_PLAY_GAME = "FinishedAllReadyToPlayGame";

        // User in lobby custom properties
        //=============================================

        public const string IS_MASTER = "IsMaster";
        public const string IS_READY = "IsReady";

        public const string USER_ID = "UserId"; // UserId는 GrpcConnection에서 가져온다.
    }
}
