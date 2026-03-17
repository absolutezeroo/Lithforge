namespace Lithforge.Network
{
    public static class PipelineId
    {
        public const int Unreliable = 0;
        public const int UnreliableSequenced = 1;
        public const int ReliableSequenced = 2;
        public const int FragmentedReliable = 3;
        public const int Count = 4;
    }
}
