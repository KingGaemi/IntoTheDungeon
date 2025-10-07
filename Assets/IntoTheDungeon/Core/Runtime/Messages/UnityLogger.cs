using UnityEngine;
public sealed class UnityLogger : IntoTheDungeon.Core.Abstractions.Messages.ILogger
{
    public void Log(string message) => Debug.Log(message);
    public void Warn(string message) => Debug.LogWarning(message);
    public void Error(string message) => Debug.LogError(message);
}
