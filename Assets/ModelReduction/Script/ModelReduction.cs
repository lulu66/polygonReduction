using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class ModelReduction{

    public int renderTriNum = 0;
    public List<PRVertex> prVertices;
    public List<PRTriangle> prTriangles;
    public int[] collapse_map;
    private int[] permutation;

    public ModelReduction(List<Vector3> vertices, List<int> triangles)
    {
        prVertices = new List<PRVertex>();
        prTriangles = new List<PRTriangle>();

        int i, j;
        //初始化所有顶点的id和pos
        for (i = 0; i < vertices.Count; ++i)
        {
            prVertices.Add(new PRVertex(i, vertices[i]));
        }
        //初始化所有三角形的id和包含的三个顶点
        for (i = 0, j = 0; i < triangles.Count; i += 3, j += 1)
        {
            prTriangles.Add(new PRTriangle(i, prVertices[triangles[i]], prVertices[triangles[i + 1]], prVertices[triangles[i + 2]]));
        }
        //为三角形中的每个顶点添加邻居三角形和邻居顶点
        for (i = 0; i < prTriangles.Count; i++)
        {
            for (j = 0; j < 3; j++)
            {
                prTriangles[i].vertex[j].face.Add(prTriangles[i]);
                for (int k = 0; k < 3; k++)
                {
                    if (k == j) continue;
                    if (!prTriangles[i].vertex[j].neighbor.Contains(prTriangles[i].vertex[k]))
                    {
                        prTriangles[i].vertex[j].neighbor.Add(prTriangles[i].vertex[k]);
                    }
                }
            }
        }
        //计算每个顶点向它的邻居边坍塌的代价
        for (i = 0; i < prVertices.Count; i++)
        {
            ComputeCostPerVertex(prVertices[i]);
        }
    }

    public void ProgressiveMesh()
    {
        permutation = new int[prVertices.Count];
        collapse_map = new int[prVertices.Count];
        while(prVertices.Count > 0)
        {
            PRVertex mn = MinCostVertex();
            permutation[mn.id] = prVertices.Count - 1;
            collapse_map[prVertices.Count - 1] = (mn.collapse != null) ? mn.collapse.id : -1;
            Collapse(mn, mn.collapse);
            if(mn.collapse != null)
                Debug.Log((prVertices.Count - 1) + " " + mn.id+ " "+mn.collapse.id);
        }

        for(int i = 0; i < collapse_map.Length; i++) 
        {
            collapse_map[i] = (collapse_map[i] == -1) ? 0 : permutation[collapse_map[i]];
        }
    }

    public void PermuteVertices(List<Vector3> vert, List<int> tri)
    {
        // rearrange the vertex Array 
        List<Vector3> temp_Array = new List<Vector3>();
        int i;

        for (i = 0; i < vert.Count; i++)
        {
            temp_Array.Add(vert[i]);
        }
        for (i = 0; i < vert.Count; i++)
        {
            vert[permutation[i]] = temp_Array[i];
        }
        // update the changes in the entries in the triangle Array
        int triCount = tri.Count / 3;
        for (i = 0; i < triCount; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                tri[i * 3 + j] = permutation[tri[i * 3 + j]];
            }
        }
    }

    private void ComputeCostPerVertex(PRVertex v)
    {
        if (v.neighbor.Count == 0)
        {
            v.collapse = null;
            v.cost = 100000f;
            return;
        }
        v.collapse = null;
        v.cost = 1000000f;
        for (int i = 0; i < v.neighbor.Count; i++)
        {
            float c = ComputeEdgeCollapseCost(v, v.neighbor[i]);
            if (c < v.cost)
            {
                v.collapse = v.neighbor[i];
                v.cost = c;
            }
        }
    }

    private float ComputeEdgeCollapseCost(PRVertex u, PRVertex v)
    {
        float edgeLength = Vector3.Distance(u.pos, v.pos);
        float curvature = 0f;
        List<PRTriangle> sides = new List<PRTriangle>();
        for (int i = 0; i < u.face.Count; i++)
        {
            if (u.face[i].HasVertex(v))
                sides.Add(u.face[i]);
        }
        for (int i = 0; i < u.face.Count; i++)
        {
            float mincurv = 1f;
            for (int j = 0; j < sides.Count; j++)
            {
                float dotprod = Vector3.Dot(u.face[i].normal, sides[j].normal);
                mincurv = Mathf.Min(mincurv, (1f - dotprod) * 0.5f);
            }
            curvature = Mathf.Max(curvature, mincurv);
        }
        return edgeLength * curvature;
    }

    public void Reduce(int curNum, List<Vector3> vertices, List<int> triangles, List<Vector3> renderVerts, List<int> renderTri)
    {
        if(renderVerts.Count != 0 || renderTri.Count != 0)
        {
            renderVerts.Clear();
            renderTri.Clear();
        }
        int triCount = triangles.Count / 3;
        //执行坍塌算法的次数
        for (int i = 0; i < triCount; i++)
        {
//            EditorUtility.DisplayProgressBar("减面", "正在减面......", (float)i / triCount);
            int p0 = Map(triangles[i * 3 + 0], curNum);
            int p1 = Map(triangles[i * 3 + 1], curNum);
            int p2 = Map(triangles[i * 3 + 2], curNum);
            if (p0 == p1 || p1 == p2 || p2 == p0)
                continue;
            renderTriNum++;

            renderVerts.Add(vertices[p0]);
            renderVerts.Add(vertices[p1]);
            renderVerts.Add(vertices[p2]);
            renderTri.Add(p0);
            renderTri.Add(p1);
            renderTri.Add(p2);
        }
//        EditorUtility.ClearProgressBar();
    }

    //顶点和边的坍塌操作
    private void Collapse(PRVertex u, PRVertex v)
    {
        if (v == null)
        {
            u.DeleteVertex();
            prVertices.Remove(u);
            return;
        }
        List<PRVertex> tmp = new List<PRVertex>();
        for (int i = 0; i < u.neighbor.Count; i++)
        {
            tmp.Add(u.neighbor[i]);
        }
        for (int i = u.face.Count - 1; i >= 0; i--)
        {
            if (u.face[i].HasVertex(v))
            {
                prTriangles.Remove(u.face[i]);
                u.face[i].DeleteFace();
            }
        }

        for (int i = u.face.Count - 1; i >= 0; i--)
        {
            u.face[i].ReplaceVertex(u, v);
        }

        u.DeleteVertex();
        prVertices.Remove(u);

        for (int i = 0; i < tmp.Count; i++)
        {
            ComputeCostPerVertex(tmp[i]);
        }
    }

    private PRVertex MinCostVertex()
    {
        PRVertex vert = prVertices[0];
        for (int i = 0; i < prVertices.Count; i++)
        {
            if (prVertices[i].cost < vert.cost)
            {
                vert = prVertices[i];
            }
        }
        return vert;
    }

    private int Map(int a, int mx)
    {
        if (mx <= 0) return 0;
        while (a >= mx)
        {
            a = collapse_map[a];
        }
        return a;
    }
}
