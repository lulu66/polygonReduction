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
    Options op;

    #region 关于消减
    ModelReduction modelReduct;

    private List<Vector3> vertices;
    private List<int> triangles;

    private List<Vector3> renderVertices = new List<Vector3>();
    private List<int> renderTriangles = new List<int>();

    bool initReduction = false;
    bool bCanReduction = true;
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

    }

    void ReduceInit()
    {
        vertices = modelStream.VerticeAfterCompList;

        if(vertices.Count> 65000)
        {
            Debug.Log("顶点数超过65000，初始化失败.");
            bCanReduction = false;
            return;
        }

        triangles = modelStream.TriangleAfterCompList;

        modelReduct = new ModelReduction(vertices, triangles);
        modelReduct.ProgressiveMesh();
        modelReduct.PermuteVertices(vertices, triangles);
        initReduction = true;
    }

    void Reduce()
    {        
        if(initReduction == false)
        {
            Debug.Log("还没初始化哦~~");
            return;
        }
        if(bCanReduction == false)
        {
            return;
        }
        
        int curNum = vertNumGUI;
        if(curNum > modelStream.VerticeAfterCompList.Count)
        {
            curNum = modelStream.VerticeAfterCompList.Count;
        }
        Debug.Log("模型顶点数减少了 " + curNum + " 个.");

        modelReduct.Reduce(curNum, vertices, triangles,  renderVertices, renderTriangles);

        rootMeshes.vertices = renderVertices.ToArray();
        rootMeshes.triangles = renderTriangles.ToArray();
        rootMeshes.RecalculateNormals();
        root.GetComponent<MeshFilter>().mesh = rootMeshes;

        vertNumGUIBefore = vertNumGUI;
    }

    void DrawInitModel()
    {
        if (modelStream.VerticeAfterCompList != null)
        {
            root = new GameObject("model");
            root.transform.localPosition = Vector3.zero;
            root.transform.localScale = Vector3.one;

            MeshFilter mf = root.AddComponent<MeshFilter>();
            MeshRenderer mr = root.AddComponent<MeshRenderer>();

            rootMeshes = new Mesh();
            rootMeshes.name = root.name;
            rootMeshes.vertices = modelStream.VerticeAfterCompList.ToArray();
            rootMeshes.triangles = modelStream.TriangleAfterCompList.ToArray();
            rootMeshes.RecalculateNormals();
            mf.mesh = rootMeshes;
            mr.material = new Material(Shader.Find("Standard"));

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
        if (vertices.Count == 0 || triangles.Count == 0)
        {
            Debug.Log("vertice | triangles length is zero.");
            return;
        }

        modelStream.CompressModel(vertices, triangles);

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
        DestroyImmediate(root);
        root = null;
    }

}
