using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PRVertex
{
    public int id;
    public Vector3 pos;
    public List<PRVertex> neighbor = new List<PRVertex>();
    public List<PRTriangle> face = new List<PRTriangle>();
    public float cost;
    public PRVertex collapse;//所要坍塌的边
    public PRVertex(int id, Vector3 pos)
    {
        this.id = id;
        this.pos = pos;
    }
    public void AddNeighbor(PRVertex v)
    {
        if (!neighbor.Contains(v) && v != this)
        {
            neighbor.Add(v);
        }
    }
    public void AddFace(PRTriangle t)
    {
        if (!face.Contains(t))
        {
            face.Add(t);
        }
    }

    public void RemoveFace(PRTriangle t)
    {
        if (!face.Contains(t))
        {
            Debug.Log("u has no current triangle.");
        }
        if (face.Contains(t))
        {
            face.Remove(t);
        }
        if (face.Contains(t))
        {
            Debug.Log("still exist triangle t.");
        }
    }

    public void RemoveIfNonNeighbor(PRVertex n)
    {
        if (!neighbor.Contains(n))
            return;
        for(int i = 0; i < face.Count; i++)
        {
            if (face[i].HasVertex(n))
                return;
        }
        neighbor.Remove(n);
        if (neighbor.Contains(n))
        {
            Debug.Log("still exsist vertex n.");
        }
    }

    public void DeleteVertex()
    {
        if (face.Count != 0)
        {
            Debug.Log("the faces is not null.");
        }
        while (neighbor.Count > 0)
        {
            neighbor[0].neighbor.Remove(this);
            neighbor.Remove(neighbor[0]);
        }        
    }
}
