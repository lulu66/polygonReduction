using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public enum Options
{
    obj = 0,
    stl
}

public class ModelReductionEditor : EditorWindow {

    int vertNumGUI = 0;
    int vertNumGUIBefore = 0;
    ModelStreams modelStream;
    //used by .obj
    GameObject root;
    Mesh rootMeshes;
    //used by .stl
    GameObject[] temp;
    Mesh[] tempMeshes;
    Options op;
    int perSubModelTriNum = 20000;
    int totalTriNum = 0;
    int count = 0;//subMesh count

    #region 关于消减
    private Vector3[] vertices;
    private Vector3[] normals;
    private int[] triangles;
    private PRVertex[] prVertices;
    private PRTriangle[] prTriangles;
    private List<ReductionData> reductionData = new List<ReductionData>();
    bool initReduction = false;
    #endregion

    [MenuItem("Model/Analyse")]
    static void InitWindow()
    {
        ModelReductionEditor window = (ModelReductionEditor)EditorWindow.GetWindow(typeof(ModelReductionEditor));
        window.position = new Rect(500, 500, 300, 400);
        window.Show();
    }

    void LoadModel()
    {
        string fileType = "";
        if(op == Options.obj)
        {
            fileType = "obj";
        }
        else if(op == Options.stl)
        {
            fileType = "stl";
        }
        string strModelPath = EditorUtility.OpenFilePanel("Open STL model", "", fileType);
        if (string.IsNullOrEmpty(strModelPath))
            return;
        FileStream fs = File.OpenRead(strModelPath);
        if (modelStream == null)
        {
            modelStream = new ModelStreams();
        }
        modelStream.Load(fs, strModelPath);
        fs.Close();

        totalTriNum = modelStream._totalTriNum;
    }

    #region 执行消减算法
    void ReduceInit()
    {
        vertices = modelStream.VerticeAfterCompList.ToArray();
        normals = modelStream.NormalAfterCompList.ToArray() ;
        if(normals.Length == 0)
        {
            normals = new Vector3[vertices.Length];
        }
        triangles = modelStream.TriangleAfterCompList.ToArray();
        prVertices = new PRVertex[vertices.Length];
        prTriangles = new PRTriangle[triangles.Length/3];

        int i, j;
        //初始化所有顶点的id和pos
        for(i=0; i<vertices.Length; ++i)
        {
            prVertices[i] = new PRVertex(i,vertices[i]);
        }
        //初始化所有三角形的id和包含的三个顶点
        for(i = 0, j = 0; i < triangles.Length; i += 3, j += 1)
        {
            prTriangles[j] = new PRTriangle(i, prVertices[triangles[i]], prVertices[triangles[i+1]], prVertices[triangles[i+2]]);
        }
        //为三角形中的每个顶点添加邻居三角形和邻居顶点
        for(i = 0; i < prTriangles.Length; i++)
        {
            prTriangles[i].vertex[0].face.Add(prTriangles[i]);
            prTriangles[i].vertex[1].face.Add(prTriangles[i]);
            prTriangles[i].vertex[2].face.Add(prTriangles[i]);
            for(j = 0; j < 3; j++)
            {
                for(int k = 0; k < 3; k++)
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
        initReduction = true;
    }

    void Reduce()
    {        
        if(initReduction == false)
        {
            Debug.Log("还没初始化哦~~");
            return;
        }
        if (reductionData != null)
        {
            reductionData.Clear();
        }
        int reduceVertNum = vertNumGUIBefore - vertNumGUI;
        Debug.Log("模型顶点数减少了 " + reduceVertNum + " 个.");

        //执行坍塌算法的次数
        for (int i = 0; i < reduceVertNum; i++)
        {
            EditorUtility.DisplayProgressBar("减面", "正在减面......", (float)i / reduceVertNum);
            PRVertex min = MinCostVertex();
            Collapse(min, min.collapse);
        }

        for (int i = 0; i < reductionData.Count; i++)
        {
            ApplyData(reductionData[i]);
        }

        EditorUtility.ClearProgressBar();

        if (modelStream._suffix == "obj")
        {
            rootMeshes.vertices = vertices;
            rootMeshes.triangles = triangles;
            root.GetComponent<MeshFilter>().mesh = rootMeshes;
        }

        #region STL File
        else if (modelStream._suffix == "stl")
        {
            for (int i = 0; i < count; i++)
            {            
                int startIndex = modelStream.SubIndexList[i * 3 + 0];
                int tmpVertCount = modelStream.SubIndexList[i * 3 + 1];
                int tmpTriCount = modelStream.SubIndexList[i * 3 + 2];
                Vector3[] tmpVertices = new Vector3[tmpVertCount];
                Vector3[] tmpNormals = new Vector3[tmpVertCount];
                int[] tmpTriangles = new int[tmpTriCount];

                for (int j = 0; j < tmpVertCount; j++)
                {
                    tmpVertices[j] = vertices[startIndex + j];
                    tmpNormals[j] = normals[startIndex + j];
                }

                for(int j = 0; j < tmpTriCount; j++)
                {
                    int index = perSubModelTriNum * i * 3 + j;
                    if (triangles[index] == 0)
                    {
                        tmpTriangles[j] = 0;
                    }
                    else
                    {
                       tmpTriangles[j] = triangles[index] - startIndex;
                    }
                }

                tempMeshes[i].vertices = tmpVertices;
                tempMeshes[i].triangles = tmpTriangles;
                temp[i].GetComponent<MeshFilter>().mesh = tempMeshes[i];

            }
        }
        #endregion

        vertNumGUIBefore = vertNumGUI;
    }

    void ApplyData(ReductionData rd)
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
    void Collapse(PRVertex u, PRVertex v)
    {
        if(v == null)
        {
            return;
        }
        List<PRVertex> tmp = new List<PRVertex>();
        for(int i = 0; i< u.neighbor.Count; i++)
        {
            tmp.Add(u.neighbor[i]);
        }
        ReductionData rd = new ReductionData();
        rd.vertexU = u.id;
        rd.vertexV = v.id;
        v.neighbor.Remove(u);

        for(int i = u.face.Count - 1; i >= 0; i--)
        {
            u.face[i].ReplaceVertex(u, v);
        }
        for (int i = 0; i < u.face.Count; i++)
        {
            rd.triangleID.Add(u.face[i].id);
        }
        reductionData.Add(rd);
        ComputeCostPerVertex(v);
        for(int i=0; i<tmp.Count; i++)
        {
            ComputeCostPerVertex(tmp[i]);
        }
        u.cost = 1000000f;
    }

    PRVertex MinCostVertex()
    {
        PRVertex vert = prVertices[0];
        for(int i = 0; i < prVertices.Length; i++)
        {
            if(vert.cost > prVertices[i].cost)
            {
                vert = prVertices[i];
            }
        }
        return vert;
    }

    void ComputeCostPerVertex(PRVertex v)
    {
        if (v.neighbor.Count == 0)
        {
            v.collapse = null;
            v.cost = 1000000f;
            return;
        }
        v.collapse = null;
        v.cost = 1000000f;
        for (int i = 0; i < v.neighbor.Count; i++)
        {
            float c = ComputeCostBetweenEdge(v, v.neighbor[i]);
            if(c < v.cost)
            {
                v.collapse = v.neighbor[i];
                v.cost = c;
            }
        }
    }

    float ComputeCostBetweenEdge(PRVertex u, PRVertex v)
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

    #endregion

    void DrawInitModel()
    {
        if (modelStream.VerticeList != null || modelStream.VerticeAfterCompList != null)
        {
            root = new GameObject("model");
            root.transform.localPosition = Vector3.zero;
            root.transform.localScale = Vector3.one;

            #region STL File
            if (modelStream._suffix == "stl")
            {
                count = totalTriNum / perSubModelTriNum;
                count += (totalTriNum % perSubModelTriNum > 0) ? 1 : 0;

                temp = new GameObject[count];
                tempMeshes = new Mesh[count];

                for (int i = 0; i < count; i++)
                {
                    temp[i] = new GameObject("modelSub" + i);
                    temp[i].transform.SetParent(root.transform);
                    temp[i].transform.localPosition = Vector3.zero;
                    temp[i].transform.localScale = Vector3.one;
                    MeshFilter mf = temp[i].AddComponent<MeshFilter>();
                    MeshRenderer mr = temp[i].AddComponent<MeshRenderer>();
                    int startIndex = i * perSubModelTriNum * 3;
                    int length = perSubModelTriNum * 3;
                    if (startIndex + length > totalTriNum * 3)
                    {
                        length = totalTriNum * 3 - startIndex;
                    }

                    List<Vector3> vertsList = modelStream.VerticeList.GetRange(startIndex, length);
                    List<Vector3> norList = modelStream.NormalList.GetRange(startIndex, length);
                    List<int> triangleIndexs = modelStream.TriangleList.GetRange(0, length);

                    modelStream.MeshCompression(vertsList, norList, triangleIndexs);

                    tempMeshes[i] = new Mesh();
                    tempMeshes[i].name = temp[i].name;
                    tempMeshes[i].vertices = vertsList.ToArray();
                    tempMeshes[i].normals = norList.ToArray();
                    tempMeshes[i].triangles = triangleIndexs.ToArray();
                    mf.mesh = tempMeshes[i];
                    mr.material = new Material(Shader.Find("Standard"));
                }
            }
            #endregion

            #region OBJ File
            else if (modelStream._suffix == "obj")
            {
                MeshFilter mf = root.AddComponent<MeshFilter>();
                MeshRenderer mr = root.AddComponent<MeshRenderer>();

                rootMeshes = new Mesh();
                rootMeshes.name = root.name;
                rootMeshes.vertices = modelStream.VerticeAfterCompList.ToArray();
                rootMeshes.normals = modelStream.NormalAfterCompList.ToArray();
                rootMeshes.triangles = modelStream.TriangleAfterCompList.ToArray();
                mf.mesh = rootMeshes;
                mr.material = new Material(Shader.Find("Standard"));
            }
            #endregion

            vertNumGUI = modelStream.VerticeAfterCompList.Count;
            vertNumGUIBefore = vertNumGUI;
        }
    }
        
    void OnGUI()
    {
        GUILayout.BeginVertical();

        op = (Options)EditorGUILayout.EnumPopup("选择文件类型:", op);

        GUILayout.Label("Load Model", EditorStyles.boldLabel);

        if (GUILayout.Button("选择模型文件"))
        {
            LoadModel();
            DrawInitModel();
        }
        EditorGUILayout.Separator();

        if(GUILayout.Button("初始化"))
        {
            ReduceInit();
            Debug.Log("初始化完成!");
        }

        GUILayout.Label("顶点数", EditorStyles.boldLabel);

        vertNumGUI = EditorGUILayout.IntSlider(vertNumGUI, 0, 100000);

        if (GUILayout.Button("开始减面"))
        {
            Reduce();
        }
        EditorGUILayout.Separator();
        if (GUILayout.Button("存储简化模型"))
        {
            SaveModel();
        }
        EditorGUILayout.Separator();
        if (GUILayout.Button("清理"))
        {
            ClearGameObjects();
            modelStream.Clear();
        }
        GUILayout.EndVertical();
    }

    void SaveModel()//均存储为.obj文件
    {
        if (vertices.Length == 0 || normals.Length == 0 || triangles.Length == 0)
        {
            Debug.Log("vertice | normals | triangles length is zero.");
            return;
        }
        modelStream.FinalVerticeList = new List<Vector3>();
        modelStream.FinalNormalList = new List<Vector3>();
        modelStream.FinalTriangleList = new List<int>();

            //find useful indexes
            for(int i = 0; i < triangles.Length; i += 3)
            {
                if(triangles[i] != 0 || triangles[i+1] != 0 || triangles[i+2] != 0)
                {
                    modelStream.FinalTriangleList.Add(triangles[i]);
                    modelStream.FinalTriangleList.Add(triangles[i+1]);
                    modelStream.FinalTriangleList.Add(triangles[i+2]);
                }
            }

            for(int i = 0; i < modelStream.FinalTriangleList.Count; i++)
            {
                EditorUtility.DisplayProgressBar("存储", "正在存储模型......", (float)i / modelStream.FinalTriangleList.Count);
                int index = modelStream.FinalTriangleList[i];
                Vector3 vert = vertices[index];
                bool bContain = false;
                for(int j = 0; j < modelStream.FinalVerticeList.Count; j++)
                {
                    if (vert.Equals(modelStream.FinalVerticeList[j]))
                    {
                        modelStream.FinalTriangleList[i] = j;
                        bContain = true;
                        break;
                    }
                }
                if(bContain == false)
                {
                    modelStream.FinalVerticeList.Add(vertices[index]); 
                    modelStream.FinalNormalList.Add(normals[index]);
                    modelStream.FinalTriangleList[i] = modelStream.FinalVerticeList.Count - 1;
                }
            }
            EditorUtility.ClearProgressBar();

        string modelPath = Path.Combine(Application.streamingAssetsPath, "ReductionModel");
        if (!Directory.Exists(modelPath))
        {
            Directory.CreateDirectory(modelPath);
        }
        string modelName = Path.Combine(modelPath, "rdModel");
        modelName += ".obj";
        if (File.Exists(modelName))
            File.Delete(modelName);
        FileStream fs = File.Create(modelName);
        modelStream.Save(fs);
        fs.Close();
        Debug.Log("存储完成!");
    }

    void ClearGameObjects()
    {
        if(modelStream._suffix == "stl")
        {
            for(int i = 0; i < count; i++)
            {
                DestroyImmediate(temp[i]);
                DestroyImmediate(tempMeshes[i]);
            }
        }
        DestroyImmediate(root);
        root = null;
    }

}
