using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using UnityEngine;
using UnityEditor;

public class ModelStreams
{

    public List<Triangle> triList;
    //只针对stl文件，压缩前的顶点、法线和纹理
    public List<Vector3> VerticeList;
    public List<Vector3> NormalList;
    public List<int> TriangleList;
    //压缩后，顶点唯一
    public List<Vector3> VerticeAfterCompList;
    public List<Vector3> NormalAfterCompList;
    public List<int> TriangleAfterCompList;
    public List<int> SubIndexList;//存放顶点列表每个subMesh的起始索引，顶点个数、索引个数
    //用于存储的顶点、法线和纹理
    public List<Vector3> FinalVerticeList;
    public List<Vector3> FinalNormalList;
    public List<int> FinalTriangleList;

    public int _totalTriNum = 0;//总的三角形个数
    public string _suffix;//后缀
    private int startIndex = 0;

    //解析AscII格式的stl文件
    private void LoadASCIIModelFile(Stream stream)
    {
        triList = new List<Triangle>();
        using (StreamReader file = new StreamReader(stream))
        {
            char[] separator = { ' ' };//用于分割字符串的标示符号，即空格
            string normal = "normal";
            while (file.ReadLine() != null)//当读取的一行不为空时，则进入循环
            {
                string line_normal = file.ReadLine();//读取包含“normal”的一行
                if (line_normal.Contains(normal))//如果读取的一行确实存在“normal”,则进行处理
                {
                    string subString_1 = line_normal.Substring(16);//存储这一行索引为16以后的字符串,注：索引从0开始
                    string[] str_normal = new string[3];//定义三个字符串数组，分别存储向量的X、Y、Z
                    str_normal = subString_1.Split(separator);//分割此字符串为三个字符串
                    Triangle tri = new Triangle();//类数组必须要对使用的每一个类进行实例化
                    tri.Normal_x = Convert.ToSingle(str_normal[0]);
                    tri.Normal_y = Convert.ToSingle(str_normal[1]);
                    tri.Normal_z = Convert.ToSingle(str_normal[2]);

                    file.ReadLine();//读取包含“outer loop”这一行，并不存储

                    string vertex_1 = file.ReadLine();//读取包含vertex的第一行,即第1个点的信息
                    string sub_string_2 = vertex_1.Substring(16);//存储这一行索引为16以后的字符串
                    string[] str_vertex_1 = new string[3];//定义3个字符串数组分别存储第1个点3个分向量
                    str_vertex_1 = sub_string_2.Split(separator);//分割此字符串为三个字符串
                    tri.v1_x = Convert.ToSingle(str_vertex_1[0]);
                    tri.v1_y = Convert.ToSingle(str_vertex_1[1]);
                    tri.v1_z = Convert.ToSingle(str_vertex_1[2]);

                    string vertex_2 = file.ReadLine();//读取包含vertex的第二行,即第2个点的信息
                    string sub_string_3 = vertex_2.Substring(16);//存储这一行索引为16以后的字符串
                    string[] str_vertex_2 = new string[3];//定义3个字符串数组分别存储第2个点3个分向量
                    str_vertex_2 = sub_string_3.Split(separator);//分割此字符串为三个字符串
                    tri.v2_x = Convert.ToSingle(str_vertex_2[0]);
                    tri.v2_y = Convert.ToSingle(str_vertex_2[1]);
                    tri.v2_z = Convert.ToSingle(str_vertex_2[2]);

                    string vertex_3 = file.ReadLine();//读取包含vertex的第三行,即第3个点的信息
                    string sub_string_4 = vertex_3.Substring(16);//存储这一行索引为16以后的字符串
                    string[] str_vertex_3 = new string[3];//定义3个字符串数组分别存储第3个点3个分向量
                    str_vertex_3 = sub_string_4.Split(separator);//分割此字符串为三个字符串
                    tri.v3_x = Convert.ToSingle(str_vertex_3[0]);
                    tri.v3_y = Convert.ToSingle(str_vertex_3[1]);
                    tri.v3_z = Convert.ToSingle(str_vertex_3[2]);

                    file.ReadLine();//读取包含“endloop”这一行，并不存储

                    triList.Add(tri);
                    _totalTriNum++;
                }
            }
        }
    }

    //解析二进制格式的stl文件
    private void LoadBinarySTLModelFile(Stream stream)
    {
        VerticeList = new List<Vector3>();
        NormalList = new List<Vector3>();
        TriangleList = new List<int>();

        VerticeAfterCompList = new List<Vector3>();
        NormalAfterCompList = new List<Vector3>();
        TriangleAfterCompList = new List<int>();
        SubIndexList = new List<int>();

        using (BinaryReader reader = new BinaryReader(stream))
        {
            reader.ReadBytes(80);
            _totalTriNum = (int)reader.ReadUInt32();

            if (reader.BaseStream.Length != _totalTriNum * 50 + 84)
                throw new Exception("STL文件长度无效");
            int number = 0;
            while (number < _totalTriNum)
            {
                byte[] bytes;
                bytes = reader.ReadBytes(50);

                if (bytes.Length < 50)
                {
                    number += 1;
                    continue;
                }

                Vector3 vec0 = new Vector3(BitConverter.ToSingle(bytes, 0), BitConverter.ToSingle(bytes, 4), BitConverter.ToSingle(bytes, 8));
                Vector3 vec1 = new Vector3(BitConverter.ToSingle(bytes, 12), BitConverter.ToSingle(bytes, 16), BitConverter.ToSingle(bytes, 20));
                Vector3 vec2 = new Vector3(BitConverter.ToSingle(bytes, 24), BitConverter.ToSingle(bytes, 28), BitConverter.ToSingle(bytes, 32));
                Vector3 vec3 = new Vector3(BitConverter.ToSingle(bytes, 36), BitConverter.ToSingle(bytes, 40), BitConverter.ToSingle(bytes, 44));
                //normal
                NormalList.Add(vec0);
                NormalList.Add(vec0);
                NormalList.Add(vec0);
                //vertex and index
                VerticeList.Add(vec1);
                int indexV1 = VerticeList.Count - 1;
                VerticeList.Add(vec2);
                int indexV2 = VerticeList.Count - 1;
                VerticeList.Add(vec3);
                int indexV3 = VerticeList.Count - 1;
                //index
                TriangleList.Add(indexV1);
                TriangleList.Add(indexV2);
                TriangleList.Add(indexV3);

                number += 1;
            }
        }
    }

    public void MeshCompression(List<Vector3>vertices, List<Vector3>normals, List<int>triangleIndexs)
    {
        int offset = 0;
        //需要删除的顶点索引集合
        List<int> removes = new List<int>();
        for (int i = 0; i < vertices.Count; i++)
        {
            EditorUtility.DisplayProgressBar("压缩网格", "正在压缩网格......", (float)i / vertices.Count);

            if (removes.Contains(i))
            {
                offset += 1;
                continue;
            }
            triangleIndexs[i] = i - offset;
            for (int j = i + 1; j < vertices.Count; j++)
            {
                if (vertices[i] == vertices[j])
                {
                    removes.Add(j);
                    triangleIndexs[j] = triangleIndexs[i];
                }
            }
        }

        removes.Sort();
        removes.Reverse();

        for (int i = 0; i < removes.Count; i++)
        {
            vertices.RemoveAt(removes[i]);
            normals.RemoveAt(removes[i]);
        }
        List<int> tempTriList = new List<int>();
        for (int i = 0; i < triangleIndexs.Count; i++)
        {
            tempTriList.Add(triangleIndexs[i] + startIndex);
        }

        VerticeAfterCompList.AddRange(vertices);
        NormalAfterCompList.AddRange(normals);
        TriangleAfterCompList.AddRange(tempTriList);
        SubIndexList.Add(startIndex);
        SubIndexList.Add(vertices.Count);
        SubIndexList.Add(triangleIndexs.Count);
        startIndex = VerticeAfterCompList.Count;
        EditorUtility.ClearProgressBar();
    }

    private void GetTriangleList(string[] chars)
    {
        string[] s1 = chars[1].Split('/');
        string[] s2 = chars[2].Split('/');
        string[] s3 = chars[3].Split('/');
        TriangleAfterCompList.Add(Convert.ToInt32(s1[0])-1);
        TriangleAfterCompList.Add(Convert.ToInt32(s2[0])-1);
        TriangleAfterCompList.Add(Convert.ToInt32(s3[0])-1);
    }

    private void LoadObjModelFile(Stream stream)
    {
        VerticeAfterCompList = new List<Vector3>();
        NormalAfterCompList = new List<Vector3>();
        TriangleAfterCompList = new List<int>();

        using (StreamReader file = new StreamReader(stream))
        { 
            string line = "";
            while(file.Peek() != -1)
            {
                line = file.ReadLine();
                line = line.Replace("  "," ");
                string[] chars = line.Split(' ');
                switch (chars[0])
                {
                    case "v":
                        VerticeAfterCompList.Add(new Vector3(Convert.ToSingle(chars[1]), Convert.ToSingle(chars[2]), Convert.ToSingle(chars[3])));
                        break;
                    case "vn":
                        NormalAfterCompList.Add(new Vector3(Convert.ToSingle(chars[1]), Convert.ToSingle(chars[2]), Convert.ToSingle(chars[3])));
                        break;
                    case "f":
                        GetTriangleList(chars);
                        _totalTriNum++;
                        break;
                }
            }
        }
    }

    public bool Load(Stream stream, string path)
    { 
        _suffix = GetSuffix(path);

        if (File.Exists(path))
        {
            if(_suffix == "stl")
            {
                LoadBinarySTLModelFile(stream);
                return true;
            }
            if(_suffix == "obj")
            {
                LoadObjModelFile(stream);
                return true;
            }
        }
        return false;
    }

    public void Save(Stream stream)
    {
        StringBuilder sb = new StringBuilder();

        sb.Append("# reductedModel.obj file\n");
        foreach(Vector3 vec in FinalVerticeList)
        {
            sb.Append(string.Format("v {0} {1} {2}\n", vec.x, vec.y, vec.z));
        }
        sb.Append("\n");

        foreach (Vector3 vec in FinalNormalList)
        {
            sb.Append(string.Format("vn {0} {1} {2}\n", vec.x, vec.y, vec.z));
        }
        sb.Append("\n");

        for(int i = 0; i < FinalTriangleList.Count; i += 3)
        {
            sb.Append(string.Format("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\n", FinalTriangleList[i] + 1, 
                FinalTriangleList[i+1] + 1, FinalTriangleList[i+2] + 1));
        }
        string contents = sb.ToString();
        using (StreamWriter file = new StreamWriter(stream))
        {
            file.Write(contents);
        }
    }

    private string GetSuffix(string name)
    {
        string[] array = name.Split('.');
        for(int i=0; i<array.Length; i++)
        {
            array[i] = array[i].ToLower();//将字符串换成小写形式的副本
        }
        return array[array.Length - 1];
    }

    public void Clear()
    {
        if(VerticeList != null)
        {
            VerticeList.Clear();
            NormalList.Clear();
            TriangleList.Clear();
        }
        VerticeAfterCompList.Clear();
        NormalAfterCompList.Clear();
        TriangleAfterCompList.Clear();
    }
}


