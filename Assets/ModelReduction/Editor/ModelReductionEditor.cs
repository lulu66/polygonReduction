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
    int perSubModelTriNum = 20000;
    int totalTriNum = 0;
    int count = 0;//subMesh count

    #region 关于消减
    ModelReduction modelReduct;

    private Vector3[] vertices;
    private Vector3[] normals;
    private int[] triangles;

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

        totalTriNum = modelStream._totalTriNum;
    }

    #region 执行消减算法

    void ReduceInit()
    {
        vertices = modelStream.VerticeAfterCompList.ToArray();

        if(vertices.Length > 65000)
        {
            Debug.Log("顶点数超过65000，初始化失败.");
            bCanReduction = false;
            return;
        }
        normals = modelStream.NormalAfterCompList.ToArray() ;

        if(normals.Length == 0)
        {
            normals = new Vector3[vertices.Length];
        }
        triangles = modelStream.TriangleAfterCompList.ToArray();

        modelReduct = new ModelReduction(vertices, triangles);

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
        
        int reduceVertNum = vertNumGUIBefore - vertNumGUI;
        Debug.Log("模型顶点数减少了 " + reduceVertNum + " 个.");

        modelReduct.Reduce(reduceVertNum, triangles);

        if (modelStream._suffix == "obj")
        {
            rootMeshes.vertices = vertices;
            rootMeshes.triangles = triangles;
            root.GetComponent<MeshFilter>().mesh = rootMeshes;
        }

        #region STL File
        else if (modelStream._suffix == "stl")
        {
            rootMeshes.vertices = vertices;
            rootMeshes.triangles = triangles;
            root.GetComponent<MeshFilter>().mesh = rootMeshes;
        }
        #endregion

        vertNumGUIBefore = vertNumGUI;
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
                List<Vector3> vertsList = modelStream.VerticeList;
                List<Vector3> norList = modelStream.NormalList;
                List<int> triangleIndexs = modelStream.TriangleList;

                modelStream.MeshCompression(vertsList, norList, triangleIndexs);

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
        DestroyImmediate(root);
        root = null;
    }

}
