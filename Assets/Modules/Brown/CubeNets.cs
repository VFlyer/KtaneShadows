using UnityEngine;

namespace BrownButton
{
    public static class Ex
    {
        public static BrownButtonScript.Ax[] TrueCopy(this BrownButtonScript.Ax[] axes)
        {
            BrownButtonScript.Ax[] newArr = new BrownButtonScript.Ax[axes.Length];
            for(int i = 0; i < axes.Length; ++i)
                newArr[i] = axes[i];
            return newArr;
        }
        public static BrownButtonScript.Ax Opposite(this BrownButtonScript.Ax a)
        {
            switch(a)
            {
                case BrownButtonScript.Ax.Back:
                    return BrownButtonScript.Ax.Front;
                case BrownButtonScript.Ax.Down:
                    return BrownButtonScript.Ax.Up;
                case BrownButtonScript.Ax.Front:
                    return BrownButtonScript.Ax.Back;
                case BrownButtonScript.Ax.Left:
                    return BrownButtonScript.Ax.Right;
                case BrownButtonScript.Ax.Right:
                    return BrownButtonScript.Ax.Left;
                case BrownButtonScript.Ax.Up:
                    return BrownButtonScript.Ax.Down;
                case BrownButtonScript.Ax.Zag:
                    return BrownButtonScript.Ax.Zig;
                case BrownButtonScript.Ax.Zig:
                    return BrownButtonScript.Ax.Zag;
            }
            throw new System.Exception();
        }
        public static BrownButtonScript.Ax[] RotateFromTo(this BrownButtonScript.Ax[] axes, BrownButtonScript.Ax a, BrownButtonScript.Ax b)
        {
            BrownButtonScript.Ax[] newArr = axes.TrueCopy();
            int a2 = (int)a.Opposite();
            int b2 = (int)b.Opposite();
            newArr[(int)b] = axes[(int)a];
            newArr[(int)a] = axes[b2];
            newArr[b2] = axes[a2];
            newArr[a2] = axes[(int)b];
            return newArr;
        }
        public static BrownButtonScript.Ax[] RotateFromChange(this BrownButtonScript.Ax[] axes, Vector3Int change)
        {
            if(change.x == 1)
                return axes.RotateFromTo(BrownButtonScript.Ax.Right, BrownButtonScript.Ax.Down);
            if(change.x == -1)
                return axes.RotateFromTo(BrownButtonScript.Ax.Left, BrownButtonScript.Ax.Down);
            if(change.y == 1)
                return axes.RotateFromTo(BrownButtonScript.Ax.Zig, BrownButtonScript.Ax.Down);
            if(change.y == -1)
                return axes.RotateFromTo(BrownButtonScript.Ax.Zag, BrownButtonScript.Ax.Down);
            if(change.z == 1)
                return axes.RotateFromTo(BrownButtonScript.Ax.Front, BrownButtonScript.Ax.Down);
            if(change.z == -1)
                return axes.RotateFromTo(BrownButtonScript.Ax.Back, BrownButtonScript.Ax.Down);
            throw new System.Exception();
        }
    }
}