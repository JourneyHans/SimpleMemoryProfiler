using System.Collections;
using System.Collections.Generic;
using UnityEditor.MemoryProfiler;
using UnityEngine;

/// <summary>
/// C#类
/// </summary>
public class PackedManagedType
{
    public bool IsValueType;        // 是否是值类型
    public bool IsArray;            // 是否是数组
    public int ArrayRank;           // 几维数组
    public string Name;             // 类型名
    public int Size;                // 大小
}

public class PackedManagedTypeUtil
{
    public static PackedManagedType[] ParseFromTypeDescription(TypeDescription[] sources)
    {
        PackedManagedType[] results = new PackedManagedType[sources.Length];

        for (int i = 0; i < sources.Length; i++)
        {
            TypeDescription source = sources[i];
            results[i] = new PackedManagedType
            {
                IsValueType = source.isValueType,
                IsArray = source.isArray,
                ArrayRank = source.arrayRank,
                Name = source.name,
                Size = source.size,
            };
            if (!string.IsNullOrEmpty(results[i].Name) && results[i].Name[0] == '.')
            {
                results[i].Name = results[i].Name.Substring(1);
            }
        }

        return results;
    }
}
