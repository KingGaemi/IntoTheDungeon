namespace IntoTheDungeon.Core.Abstractions.Services
{
    public interface IClock
    {
        float Time { get; }            // scaled
        float DeltaTime { get; }       // scaled
        float UnscaledTime { get; }
        float UnscaledDeltaTime { get; }
        float FixedDeltaTime { get; }  // configured fixed step
    }
}
