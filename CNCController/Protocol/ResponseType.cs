namespace CNCController.Protocol
{
    public enum ResponseType : short
    {
        Startup = 1,
        Acknowledge = 2,
        Completed = 3,
        Error = 4
    }
}