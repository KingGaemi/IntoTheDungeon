namespace IntoTheDungeon.Core.Abstractions.Messages
{
    public interface ILogger
    {
        void Log(string message);
        void Warn(string message);
        void Error(string message);
    }
}
