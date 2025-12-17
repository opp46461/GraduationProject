using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Reflection;
using UnityEngine.Events;


// 本地配置数据基类
public class LocalConfigDataBase
{
    public virtual string GetFilePath()
    {
        return "";
    }
    static Dictionary<string, Dictionary<string, LocalConfigDataBase>> dataDic = new Dictionary<string, Dictionary<string, LocalConfigDataBase>>();

    /// <summary>
    /// 获取某个配置文件中的单个数据
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="fileName"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static T GetConfigData<T>(string key, string fileName = null) where T : LocalConfigDataBase
    {
        Type setT = typeof(T);
        if (fileName == null)
        {
            // 使用类名作为默认文件名
            fileName = setT.Name;
        }

        // 如果还没加载，先读取配置数据
        if (!dataDic.ContainsKey(fileName))
        {
            ReadConfigDataForFileName<T>(fileName);
        }
        if (!dataDic.ContainsKey(fileName))
        {
            return null;
        }

        // 获取该文件对应的数据字典
        Dictionary<string, LocalConfigDataBase> objDic = dataDic[fileName];
        if (!objDic.ContainsKey(key))
        {
            //throw new Exception("no this config");
            Debug.LogError("no this config");
            return null;
        }
        return (T)(objDic[key]);
    }
    /// <summary>
    /// 一般而言typeName和文件名一致
    /// </summary>
    /// <param name="key"></param>
    /// <param name="typeName"></param>
    /// <param name="fileName"></param>
    /// <returns></returns>
    public static LocalConfigDataBase GetConfigData(string key, string typeName, string fileName = null)
    {
        Type type = Type.GetType(typeName);
        if (fileName == null)
        {
            // 使用类名作为默认文件名
            fileName = type.Name;
        }

        // 如果还没加载，先读取配置数据
        if (!dataDic.ContainsKey(fileName))
        {
            ReadConfigData(typeName);
        }
        if (!dataDic.ContainsKey(fileName))
        {
            return null;
        }

        // 获取该文件对应的数据字典
        Dictionary<string, LocalConfigDataBase> objDic = dataDic[fileName];
        if (!objDic.ContainsKey(key))
        {
            //throw new Exception("no this config");
            Debug.LogError("no this config");
            return null;
        }
        return objDic[key];
    }


    /// <summary>
    /// 获取某个配置文件中的所有数据
    /// </summary>
    /// <typeparam name="T">配置数据类型</typeparam>
    /// <param name="fileName">文件名（不包含路径和扩展名）</param>
    /// <param name="isPriKey">是否使用主键模式：true: 使用"key"或"ID"字段作为字典键，false: 使用行索引作为字典键</param>
    /// <returns>配置数据列表</returns>
    public static List<T> GetConfigDatas<T>(string fileName = null, bool isPriKey = true) where T : LocalConfigDataBase
    {
        List<T> returnList = new List<T>();
        Type setT = typeof(T);
        if (fileName == null)
        {
            // 使用类名作为默认文件名
            fileName = setT.Name;
        }

        // 如果还没加载，先读取配置数据
        if (!dataDic.ContainsKey(fileName))
        {
            ReadConfigDataForFileName<T>(fileName, isPriKey);
        }
        if (!dataDic.ContainsKey(fileName))
        {
            return returnList;
        }

        // 获取该文件对应的数据字典
        Dictionary<string, LocalConfigDataBase> objDic = dataDic[fileName];

        // 遍历字典，将所有配置对象添加到返回列表中
        foreach (KeyValuePair<string, LocalConfigDataBase> kvp in objDic)
        {
            returnList.Add((T)(kvp.Value));
        }
        return returnList;
    }
    /// <summary>
    /// 一般而言typeName和文件名一致
    /// </summary>
    /// <param name="typeName"></param>
    /// <param name="fileName"></param>
    /// <param name="hasPriKey"></param>
    public static void ReadConfigData(string typeName, string fileName = null, bool hasPriKey = true)
    {
        Type type = Type.GetType(typeName);
        if (fileName == null)
        {
            // 使用类名作为默认文件名
            fileName = type.Name;
        }
        if (dataDic.ContainsKey(fileName)) return;

        TextAsset csvFile = LocalDataManager.Instance.LoadCSVFileForAssetName(fileName);

        if (csvFile == null)
        {
            //throw new Exception($"CSV file not found: {path}");
            Debug.LogError($"CSV file not found: {fileName}");
            return;
        }

        string getString = csvFile.text;

        CsvReaderByString csr = new CsvReaderByString(getString);

        Dictionary<string, LocalConfigDataBase> objDic = new Dictionary<string, LocalConfigDataBase>();

        // 使用反射获取字段信息（第3行是字段名）
        FieldInfo[] fis = new FieldInfo[csr.ColCount];
        for (int colNum = 1; colNum < csr.ColCount + 1; colNum++)
        {
            fis[colNum - 1] = type.GetField(csr[3, colNum]);
        }

        int index = 0;
        // 从第4行开始是数据行
        for (int rowNum = 4; rowNum < csr.RowCount + 1; rowNum++)
        {
            object configObj = Activator.CreateInstance(type);
            for (int i = 0; i < fis.Length; i++)
            {
                string fieldValue = csr[rowNum, i + 1];
                object setValue = new object();

                // 根据字段类型转换数据
                switch (fis[i].FieldType.ToString())
                {
                    case "System.Int32":
                        setValue = int.Parse(fieldValue);
                        break;
                    case "System.Int64":
                        setValue = long.Parse(fieldValue);
                        break;
                    case "System.String":
                        setValue = fieldValue;
                        break;
                    case "System.Single":
                        try
                        {
                            setValue = float.Parse(fieldValue);
                        }
                        catch (System.Exception e)
                        {
                            setValue = 0.0f;
                        }

                        break;
                    default:
                        Debug.Log("error data type: " + fis[i].FieldType.ToString());
                        break;
                }
                fis[i].SetValue(configObj, setValue);

                // 如果是主键字段（key或ID），添加到字典
                if (hasPriKey && (fis[i].Name == "key" || fis[i].Name == "ID"))
                {
                    //只检测key和id的值，然后添加到objDic 中
                    objDic.Add(setValue.ToString(), (LocalConfigDataBase)configObj);
                }
            }

            if (!hasPriKey)
            {
                objDic.Add(index.ToString(), (LocalConfigDataBase)configObj);
            }
            index++;
        }
        dataDic.Add(fileName, objDic);    //可以作为参数
    }

    /// <summary>
    /// fullPath 走 IO
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="isfullPath"></param>
    /// <returns></returns>
    public static string GetString(string fileName, bool isfullPath = false)
    {
        if (string.IsNullOrEmpty(fileName)) return null;
        string getString = null;
        if (!isfullPath)
        {
            TextAsset csvFile = LocalDataManager.Instance.LoadCSVFileForAssetName(fileName);

            if (csvFile == null)
            {
                Debug.LogError($"GetString CSV file not found: {fileName}");
                return null;
            }
            getString = csvFile.text;
        }
        else
        {
            getString = LocalDataManager.Instance.LoadCSVFileForFullPath(fileName);
        }
        return getString;
    }
    public static bool ReadConfigDataForFullPath<T>(string fileName, string fullPath, bool hasPriKey = true) where T : LocalConfigDataBase
    {
        return ReadConfigDataForString<T>(fileName, GetString(fullPath, true), hasPriKey);
    }
    public static bool ReadConfigDataForFileName<T>(string fileName = null, bool hasPriKey = true) where T : LocalConfigDataBase
    {
        return ReadConfigDataForString<T>(fileName, GetString(fileName), hasPriKey);
    }
    /// <summary>
    /// 默认通过 AssetName 的方式加载；
    /// 特殊情况下，用 FullPath 加载，而且在 GetConfigData 之前，手动加载
    /// </summary>
    private static bool ReadConfigDataForString<T>(string fileName = null, string getString = null, bool hasPriKey = true) where T : LocalConfigDataBase
    {
        T obj = Activator.CreateInstance<T>();
        if (fileName == null)
        {
            // 获取子类定义的文件路径
            fileName = obj.GetFilePath();
        }
        if (string.IsNullOrEmpty(getString))
        {
            Debug.LogError($"ReadConfigDataForString CSV file not found: {fileName}");
            return false;
        }
        try
        {
            CsvReaderByString csr = new CsvReaderByString(getString);

            Dictionary<string, LocalConfigDataBase> objDic = new Dictionary<string, LocalConfigDataBase>();

            // 使用反射获取字段信息（第3行是字段名）
            FieldInfo[] fis = new FieldInfo[csr.ColCount];
            for (int colNum = 1; colNum < csr.ColCount + 1; colNum++)
            {
                fis[colNum - 1] = typeof(T).GetField(csr[3, colNum]);
            }

            int index = 0;
            // 从第4行开始是数据行
            for (int rowNum = 4; rowNum < csr.RowCount + 1; rowNum++)
            {
                T configObj = Activator.CreateInstance<T>();
                for (int i = 0; i < fis.Length; i++)
                {
                    string fieldValue = csr[rowNum, i + 1];
                    object setValue = new object();

                    // 根据字段类型转换数据
                    switch (fis[i].FieldType.ToString())
                    {
                        case "System.Int32":
                            setValue = int.Parse(fieldValue);
                            break;
                        case "System.Int64":
                            setValue = long.Parse(fieldValue);
                            break;
                        case "System.String":
                            setValue = fieldValue;
                            break;
                        case "System.Single":
                            setValue = float.Parse(fieldValue);
                            setValue = 0.0f;
                            break;
                        default:
                            Debug.Log("error data type: " + fis[i].FieldType.ToString());
                            break;
                    }
                    fis[i].SetValue(configObj, setValue);

                    // 如果是主键字段（key或ID），添加到字典
                    if (hasPriKey && (fis[i].Name == "key" || fis[i].Name == "ID"))
                    {
                        //只检测key和id的值，然后添加到objDic 中
                        objDic.Add(setValue.ToString(), configObj);
                    }
                }

                if (!hasPriKey)
                {
                    objDic.Add(index.ToString(), configObj);
                }
                index++;
            }
            dataDic.Add(fileName, objDic);    //可以作为参数
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message);
            return false;
        }

        return true;
    }
}
