namespace Domain.ValueObjects
{
    public readonly record struct NodeId(string value)
    {
        public static NodeId NewGuid()
        {
            return new NodeId(Guid.NewGuid().ToString("N"));
        }

        public override string ToString()
        {
            return value;
        }
    }
}
