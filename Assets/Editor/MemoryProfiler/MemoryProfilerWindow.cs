using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.MemoryProfiler;
using UnityEngine;
using System.Linq;

// 快照信息类
public class SnapshotInfo
{
    // 类型名：折叠状态
    public Dictionary<string, bool> TypeToFoldedMap = new Dictionary<string, bool>();

    // 类型名：对象列表（最终展示列表）
    private Dictionary<string, List<PackedNativeUnityEngineObject>> _typeToObjectsMap = new Dictionary<string, List<PackedNativeUnityEngineObject>>();

    // 类型名：该类型所占内存总大小
    private Dictionary<string, int> _typeToAllSizeMap = new Dictionary<string, int>();

    // 所占内存总大小
    public int TotalSize;
    // 总对象个数
    public int TotalObjects;

    // 获取类型数量
    public int GetTypeCount()
    {
        return _typeToObjectsMap.Count;
    }

    // 获取展示列表
    public Dictionary<string, List<PackedNativeUnityEngineObject>> GetTypeToObjectsMap()
    {
        return _typeToObjectsMap;
    }

    // 添加对象到指定类型名称下
    public void AddObjectToType(string typeName, PackedNativeUnityEngineObject nativeObject)
    {
        if (_typeToObjectsMap.ContainsKey(typeName))
        {
            _typeToObjectsMap[typeName].Add(nativeObject);
            TypeToFoldedMap[typeName] = false;
            _typeToAllSizeMap[typeName] += nativeObject.size;
        }
        else
        {
            _typeToObjectsMap.Add(typeName, new List<PackedNativeUnityEngineObject> {nativeObject});
            TypeToFoldedMap.Add(typeName, false);
            _typeToAllSizeMap.Add(typeName, nativeObject.size);
        }
    }

    // 将内部数据排序
    public void Sort()
    {
        // 外部类型按占用内存大小排序
        _typeToObjectsMap = _typeToObjectsMap
            .OrderByDescending(objMapItem =>
                _typeToAllSizeMap.FirstOrDefault(sizeMapItem => sizeMapItem.Key == objMapItem.Key).Value)
            .ToDictionary(item => item.Key, item => item.Value);

        // 类型内部对象按占用内存大小排序
        foreach (var typeToObjectsPair in _typeToObjectsMap)
        {
            typeToObjectsPair.Value.Sort((a, b) => b.size - a.size);
        }
    }

    // 清理数据
    public void ClearData()
    {
        _typeToObjectsMap.Clear();
        TypeToFoldedMap.Clear();
        _typeToAllSizeMap.Clear();
        TotalSize = 0;
        TotalObjects = 0;
    }

    // 获取该类型占用内存
    public int GetTypeSize(string typeName)
    {
        if (!_typeToAllSizeMap.ContainsKey(typeName))
        {
            return 0;
        }
        return _typeToAllSizeMap[typeName];
    }
}

public class MemoryProfilerWindow : EditorWindow
{
    // 内存快照
    private PackedMemorySnapshot _snapshot = null;

    // 内存快照信息
    private SnapshotInfo _snapshotInfo = null;
    // 上次内存快照的信息
    private SnapshotInfo _lastSnapshotInfo = null;

    private Vector2 _scrollPos;

    private int _tabIndex;

    [MenuItem("Window/MemoryProfiler")]
    static void Create()
    {
        GetWindow<MemoryProfilerWindow>();
    }

    void OnGUI()
    {
        if (GUILayout.Button("Take Snapshot"))
        {
            // 每次执行前先重置数据
            ResetAllData();

            // 获取快照回调
            MemorySnapshot.OnSnapshotReceived += OnSnapshotReceived;
            // 请求获取内存快照
            MemorySnapshot.RequestNewSnapshot();
        }

        if (_snapshotInfo == null)
        {
            return;
        }

        _tabIndex = GUILayout.Toolbar(_tabIndex, new[] { "NativeObjects", "ManagedType" });
        switch (_tabIndex)
        {
            case 0:
                DrawNativeUnityEngineObjectInfo();
                break;
            case 1:
                break;
            default:
                Debug.LogError("Undefine tabIndex: " + _tabIndex);
                break;
        }
    }

    private void ParsePackedNativeUnityEngineObject()
    {
        _snapshotInfo = new SnapshotInfo();
        Debug.Log("nativeObjects Length: " + _snapshot.nativeObjects.Length);
        foreach (PackedNativeUnityEngineObject nativeObject in _snapshot.nativeObjects)
        {
            string typeName = _snapshot.nativeTypes[nativeObject.nativeTypeArrayIndex].name;

            _snapshotInfo.AddObjectToType(typeName, nativeObject);
            _snapshotInfo.TotalSize += nativeObject.size;
            _snapshotInfo.TotalObjects++;
        }

        _snapshotInfo.Sort();
    }

    // 快照获取成功回调
    private void OnSnapshotReceived(PackedMemorySnapshot snapshot)
    {
        _snapshot = snapshot;
        MemorySnapshot.OnSnapshotReceived -= OnSnapshotReceived;

        if (_snapshotInfo != null)
        {
            _lastSnapshotInfo = _snapshotInfo;
        }

        // 解析C++对象
        ParsePackedNativeUnityEngineObject();

        var packedManagedTypes = PackedManagedTypeUtil.ParseFromTypeDescription(_snapshot.typeDescriptions);
        Debug.Log("ManagedType Length: " + packedManagedTypes.Length);
        foreach (PackedManagedType packedManagedType in packedManagedTypes)
        {
            Debug.LogFormat("\t Name: {0} Size: {1}", packedManagedType.Name, packedManagedType.Size);
//            Debug.Log("\t Size: " + packedManagedType.Size);
//            Debug.Log("\t IsValueType: " + packedManagedType.IsValueType);
//            Debug.Log("\t IsArray: " + packedManagedType.IsArray);
//            Debug.Log("\t ArrayRank: " + packedManagedType.ArrayRank);
        }
    }

    // 绘制NativeObjects信息
    private void DrawNativeUnityEngineObjectInfo()
    {
        // 绘制概览信息
        DrawBriefInfo();

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
        foreach (var _typeToObjectsPair in _snapshotInfo.GetTypeToObjectsMap())
        {
            string typeName = _typeToObjectsPair.Key;
            List<PackedNativeUnityEngineObject> nativeObjects = _typeToObjectsPair.Value;

            // 绘制每个类型的折叠表头
            DrawTypeFolderTitle(typeName, nativeObjects.Count);

            // 绘制类型下的所有对象信息
            if (_snapshotInfo.TypeToFoldedMap[typeName])
            {
                DrawObjectsInfo(nativeObjects);
            }
        }
        EditorGUILayout.EndScrollView();
    }

    // 绘制概览信息
    private void DrawBriefInfo()
    {
        EditorGUILayout.BeginHorizontal("Box", GUILayout.ExpandWidth(true));
        string typeCount = string.Format("类型数: {0}", _snapshotInfo.GetTypeCount());
        if (_lastSnapshotInfo != null)
        {
            typeCount = GetDifference(_snapshotInfo.GetTypeCount(), _lastSnapshotInfo.GetTypeCount(), typeCount);
        }
        EditorGUILayout.LabelField(typeCount, GUILayout.ExpandWidth(false));

        string objectCount = string.Format("对象总数：{0}", _snapshotInfo.TotalObjects);
        if (_lastSnapshotInfo != null)
        {
            objectCount = GetDifference(_snapshotInfo.TotalObjects, _lastSnapshotInfo.TotalObjects, objectCount);
        }
        EditorGUILayout.LabelField(objectCount, GUILayout.ExpandWidth(false));

        string totalSize = string.Format("所占内存：{0}", EditorUtility.FormatBytes(_snapshotInfo.TotalSize));
        if (_lastSnapshotInfo != null)
        {
            totalSize = GetDifferenceBytes(_snapshotInfo.TotalSize, _lastSnapshotInfo.TotalSize, totalSize);
        }
        EditorGUILayout.LabelField(totalSize, GUILayout.ExpandWidth(false));
        EditorGUILayout.EndHorizontal();
    }

    // 绘制类型折叠列表
    private void DrawTypeFolderTitle(string typeName, int objectCount)
    {
        EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
        // 类型名
        _snapshotInfo.TypeToFoldedMap[typeName] = EditorGUILayout.Foldout(_snapshotInfo.TypeToFoldedMap[typeName], typeName);
        // 对象个数
        string objectCountStr = string.Format("对象数: {0}", objectCount);
        if (_lastSnapshotInfo != null)
        {
            if (_lastSnapshotInfo.GetTypeToObjectsMap().ContainsKey(typeName))
            {
                objectCountStr = GetDifference(objectCount, _lastSnapshotInfo.GetTypeToObjectsMap()[typeName].Count, objectCountStr);
            }
        }
        EditorGUILayout.LabelField(objectCountStr, GUILayout.ExpandWidth(false));
        // 所占大小
        int typeSize = _snapshotInfo.GetTypeSize(typeName);
        string typeSizeStr = string.Format("占用: {0}", EditorUtility.FormatBytes(typeSize));
        if (_lastSnapshotInfo != null)
        {
            typeSizeStr = GetDifferenceBytes(typeSize, _lastSnapshotInfo.GetTypeSize(typeName), typeSizeStr);
        }
        EditorGUILayout.LabelField(typeSizeStr, GUILayout.ExpandWidth(false));
        EditorGUILayout.EndHorizontal();
    }

    // 绘制对象
    private void DrawObjectsInfo(List<PackedNativeUnityEngineObject> nativeObjects)
    {
        EditorGUILayout.BeginVertical("Box");
        foreach (PackedNativeUnityEngineObject nativeObject in nativeObjects)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            // 对象名可能为空
            string nativeObjectName = string.IsNullOrEmpty(nativeObject.name) ? "[NoName]" : nativeObject.name;
            EditorGUILayout.LabelField(string.Format("    {0}", nativeObjectName), GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField(string.Format("占用: {0}", EditorUtility.FormatBytes(nativeObject.size)), GUILayout.ExpandWidth(false));
            EditorGUILayout.LabelField(string.Format("实例ID: {0}", nativeObject.instanceId), GUILayout.ExpandWidth(false));
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    void OnDestroy()
    {
        ResetAllData();
    }

    // 重置所有数据
    private void ResetAllData()
    {
        // 每次快照前先释放，不然内存消耗会越来越大
        FreeSnapshot();
    }

    // 释放快照，清理GC
    private void FreeSnapshot()
    {
        _snapshot = null;
        GC.Collect();
    }

    /// <summary>
    /// 获取差异
    /// </summary>
    /// <param name="currentNumber">当前数量</param>
    /// <param name="lastNumber">上次数量</param>
    /// <param name="origin">原文</param>
    /// <returns></returns>
    private string GetDifference(int currentNumber, int lastNumber, string origin)
    {
        int diffNumber = currentNumber - lastNumber;
        string diffNumberStr;
        if (diffNumber < 0)
        {
            diffNumberStr = string.Format("({0})", diffNumber);
        }
        else if (diffNumber > 0)
        {
            diffNumberStr = string.Format("(+{0})", diffNumber);
        }
        else
        {
            diffNumberStr = "";
        }
        origin = string.Format("{0} {1}", origin, diffNumberStr);
        return origin;
    }

    /// <summary>
    /// 获取占用大小差异
    /// </summary>
    /// <param name="currentBytes"></param>
    /// <param name="lastBytes"></param>
    /// <param name="origin"></param>
    /// <returns></returns>
    private string GetDifferenceBytes(int currentBytes, int lastBytes, string origin)
    {
        int diffBytes = currentBytes - lastBytes;
        string diffBytesStr;
        if (diffBytes < 0)
        {
            diffBytesStr = string.Format("(-{0})", EditorUtility.FormatBytes(Mathf.Abs(diffBytes)));
        }
        else if (diffBytes > 0)
        {
            diffBytesStr = string.Format("(+{0})", EditorUtility.FormatBytes(diffBytes));
        }
        else
        {
            diffBytesStr = "";
        }

        origin = string.Format("{0} {1}", origin, diffBytesStr);
        return origin;
    }
}
