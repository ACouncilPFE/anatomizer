namespace AIGame.Core
{
    /// <summary>Something the suture beam can close up, such as a tear in the artery wall.</summary>
    public interface ISuturable
    {
        void Suture(float amount);
    }
}
