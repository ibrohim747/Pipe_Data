using System;

namespace MEP_Data.Path_Finder
{
    public class Node
    {
        public int X, Y, Z;
        public bool IsWalkable;
        public double G, H;
        public Node Parent;

        public double F => G + H;

        public Node(int x, int y, int z, bool isWalkable = true)
        {
            X = x;
            Y = y;
            Z = z;
            IsWalkable = isWalkable;
        }
    }
}