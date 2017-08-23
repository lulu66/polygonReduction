using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PRTriangle
{

    public int id;
    public PRVertex[] vertex = new PRVertex[3];
    public Vector3 normal;

    public PRTriangle(int id, PRVertex v1, PRVertex v2, PRVertex v3)
    {
        this.id = id;
        vertex[0] = v1;
        vertex[1] = v2;
        vertex[2] = v3;
        ComputeNormal();
    }

    public void ComputeNormal()
    {
        Vector3 p1 = vertex[1].pos - vertex[0].pos;
        Vector3 p2 = vertex[2].pos - vertex[1].pos;
        
        normal = Vector3.Cross(p1, p2);
        if (Vector3.Dot(normal, normal) == 0)
            return;
        normal = normal.normalized;
    }

    public bool HasVertex(PRVertex v)
    {
        if (vertex[0] == v)
            return true;
        if (vertex[1] == v)
            return true;
        if (vertex[2] == v)
            return true;
        return false;
    }

    public void ReplaceVertex(PRVertex u, PRVertex v)
    {
        if (vertex[0] == u)
        {
            vertex[0] = v;
        }
        if (vertex[1] == u)
        {
            vertex[1] = v;
        }
        if (vertex[2] == u)
        {
            vertex[2] = v;
        }

        v.AddFace(this);
        for (int i = 0; i < 3; i++)
        {
            if (vertex[i].neighbor.Contains(u))
            {
                vertex[i].neighbor.Remove(u);
                vertex[i].AddNeighbor(v);
            }
            v.AddNeighbor(vertex[i]);
        }
        ComputeNormal();
    }
}

