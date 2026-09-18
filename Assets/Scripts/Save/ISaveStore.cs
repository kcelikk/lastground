namespace LastGround.Save
{
    /// <summary>Raw key/value persistence. A future CloudSaveStore implements the same contract (TDD_02 §24).</summary>
    public interface ISaveStore
    {
        bool TryRead(string key, out string data);
        void Write(string key, string data);
    }
}
