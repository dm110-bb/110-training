namespace OrderHub.Core.Ai;

public class AiServiceUnavailableException : Exception
{
    public AiServiceUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
