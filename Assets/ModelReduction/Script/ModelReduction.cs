using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class ModelReduction{

    public PRVertex[] prVertices;
    public PRTriangle[] prTriangles;
    private List<ReductionData> reductionData = new List<ReductionData>();

    public ModelReduction(Vector3[] vertices, int[] triangles)
    {
        prVertices = new PRVertex[vertices.Length];
        prTriangles = new PRTriangle[triangles.Length / 3];

        int i, j;
        //初始化所有顶点的id和pos
        for (i = 0; i < vertices.Length; ++i)
        {
            prVertices[i] = new PRVertex(i, vertices[i]);
        }
        //初始化所有三角形的id和包含的三个顶点
        for (i = 0, j = 0; i < triangles.Length; i += 3, j += 1)
        {
            prTriangles[j] = new PRTriangle(i, prVertices[triangles[i]], prVertices[triangles[i + 1]], prVertices[triangles[i + 2]]);
        }
        //为三角形中的每个顶点添加邻居三角形和邻居顶点
        for (i = 0; i < prTriangles.Length; i++)
        {
            prTriangles[i].vertex[0].face.Add(prTriangles[i]);
            prTriangles[i].vertex[1].face.Add(prTriangles[i]);
            prTriangles[i].vertex[2].face.Add(prTriangles[i]);
            for (j = 0; j < 3; j++)
            {
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
        for (i = 0; i < prVertices.Length; i++)
        {
            ComputeCostPerVertex(prVertices[i]);
        }
    }

    private void ComputeCostPerVertex(PRVertex v)
    {
        if (v.neighbor.Count == 0)
        {
            v.collapse = null;
            v.cost = -0.01f;
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

    public void Reduce(int reduceVertNum, int[] triangles)
    {
        if (reductionData != null)
        {
            reductionData.Clear();
        }

        //执行坍塌算法的次数
        for (int i = 0; i < reduceVertNum; i++)
        {
            EditorUtility.DisplayProgressBar("减面", "正在减面......", (float)i / reduceVertNum);
            PRVertex min = MinCostVertex();
            Collapse(min, min.collapse);
        }

        for (int i = 0; i < reductionData.Count; i++)
        {
            ApplyData(reductionData[i], triangles);
        }

        EditorUtility.ClearProgressBar();
    }

    void ApplyData(ReductionData rd, int[] triangles)
    {
        for (int i = 0; i < rd.triangleID.Count; i++)
        {
            //如果其中有一个点是坍塌指向的点，则包含该点的三角形索引全部为0
            if (triangles[rd.triangleID[i]] == rd.vertexV || triangles[rd.triangleID[i] + 1] == rd.vertexV || triangles[rd.triangleID[i] + 2] == rd.vertexV)
            {
                triangles[rd.triangleID[i]] = triangles[rd.triangleID[i] + 1] = triangles[rd.triangleID[i] + 2] = 0;
            }
            else//如果其中一个点是坍塌点，则该点的索引变为坍塌指向的点
            {
                if (triangles[rd.triangleID[i]] == rd.vertexU)
                {
                    triangles[rd.triangleID[i]] = rd.vertexV;
                    continue;
                }
                if (triangles[rd.triangleID[i] + 1] == rd.vertexU)
                {
                    triangles[rd.triangleID[i] + 1] = rd.vertexV;
                    continue;
                }
                if (triangles[rd.triangleID[i] + 2] == rd.vertexU)
                {
                    triangles[rd.triangleID[i] + 2] = rd.vertexV;
                    continue;
                }
            }

        }
    }

    //顶点和边的坍塌操作
    private void Collapse(PRVertex u, PRVertex v)
    {
        if (v == null)
        {
            return;
        }
        List<PRVertex> tmp = new List<PRVertex>();
        for (int i = 0; i < u.neighbor.Count; i++)
        {
            tmp.Add(u.neighbor[i]);
        }
        ReductionData rd = new ReductionData();
        rd.vertexU = u.id;
        rd.vertexV = v.id;
        v.neighbor.Remove(u);

        for (int i = u.face.Count - 1; i >= 0; i--)
        {
            u.face[i].ReplaceVertex(u, v);
        }
        for (int i = 0; i < u.face.Count; i++)
        {
            rd.triangleID.Add(u.face[i].id);
        }
        reductionData.Add(rd);
        ComputeCostPerVertex(v);
        for (int i = 0; i < tmp.Count; i++)
        {
            ComputeCostPerVertex(tmp[i]);
        }
        u.cost = 1000000f;
    }

    private PRVertex MinCostVertex()
    {
        PRVertex vert = prVertices[0];
        for (int i = 0; i < prVertices.Length; i++)
        {
            if (vert.cost > prVertices[i].cost)
            {
                vert = prVertices[i];
            }
        }
        return vert;
    }

}
