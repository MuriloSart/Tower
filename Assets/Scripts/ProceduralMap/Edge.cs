[System.Serializable]
public class Edge
{
    public RoomNode From;
    public RoomNode To;
    public int Weight;

    public Edge(RoomNode from, RoomNode to, int weight)
    {
        From = from;
        To = to;
        Weight = weight;
    }

    public override string ToString() => $"{From.name} --({Weight})--> {To.name}";
}
