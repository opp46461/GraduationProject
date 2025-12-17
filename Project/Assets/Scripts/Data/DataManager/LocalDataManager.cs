using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class LocalDataManager : UnitySingleton<LocalDataManager>
{
    private int difficulty = 0;

    public int Difficulty { get => difficulty; }

    private int currentGameIndex = 0;

    public int CurrentGameIndex { get => currentGameIndex; }
    GamePlayState[] gamePlayState = new GamePlayState[10];

    public override void Awake()
    {
        base.Awake();

        SimplifyEventMgr.AddListener<GlobalGameData>(10012, GlobalGameDataUpdate);
    }

    private void OnDestroy()
    {
        SimplifyEventMgr.RemoveListener<GlobalGameData>(10012, GlobalGameDataUpdate);
    }

    public void Initialize()
    {
    }

    #region Static 静态
    /// <summary>
    /// 加载数据
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="assetName"></param>
    /// <returns></returns>
    public static T LoadDataFromJson<T>(string assetName) where T : class
    {
        TextAsset text = ResManager.Instance.LoadAsset<TextAsset>(assetName, true);
        if (text == null) return null;
        T data = JsonUtility.FromJson<T>(text.text);
        return data;
    }
    #endregion

    #region TextAsset 管理 → 暂时所有本地数据是常驻内存的，后面需要优化
    public TextAsset LoadCSVFileForAssetName(string assetName)
    {
        assetName = assetName.ToLower();
        return ResManager.Instance.LoadAsset<TextAsset>(assetName);
    }
    public string LoadCSVFileForFullPath(string fullPath)
    {
        if (!System.IO.File.Exists(fullPath)) return null;
        return System.IO.File.ReadAllText(fullPath);
    }
    #endregion

    #region IO
    public string GetLocalDataOutputPath(string fileName)
    {
        string outputPath = string.Format("{0}/{1}", ResManager.GetLocalManifestDataPath("LocalData"), fileName);
        return outputPath;
    }
    /// <summary>
    /// 根据 FullPath 读表，而非 AssetName
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="fileName"></param>
    /// <returns></returns>
    public List<T> ReadCSVDatasForFullPath<T>(string fileName) where T : LocalConfigDataBase
    {
        // 暂时放到和清单一个表里
        string fullPath = GetLocalDataOutputPath(fileName);
        // 这里需要特殊处理，先加载
        //bool isRead = LocalConfigDataBase.ReadConfigDataForFullPath<RanksList>(fileName, fullPath);
        //if (!isRead) return null;
        // 后读取
        return LocalConfigDataBase.GetConfigDatas<T>(fileName);
    }

    /// <summary>
    /// 按照某一个CSV表格式进行导出
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="fileName"></param>
    /// <returns></returns>
    public bool WriteCSVDatasForFullPath<T>(string fileName, List<T> datas) where T : LocalConfigDataBase
    {
        if (datas == null || datas.Count == 0) return false;
        if (string.IsNullOrEmpty(fileName)) fileName = datas[0].GetFilePath();
        if (string.IsNullOrEmpty(fileName)) return false;

        // 导出到文件
        CsvStringWriter writer = ConfigDataExporter.ExportToCsvString(datas);
        string outputPath = GetLocalDataOutputPath(fileName);
        writer.SaveToFullPath(outputPath);

        return true;
    }
    #endregion

    #region 自定义事件
    private void GlobalGameDataUpdate(GlobalGameData data)
    {
        if (data == null) return;
        switch (data.dataName)
        {
            case "difficulty":
                DifficultyData dData = (DifficultyData)data;
                difficulty = dData.difficulty;
                break;
            case "SelectedGame":
                SelectedGameData sData = (SelectedGameData)data;
                currentGameIndex = sData.index;
                gamePlayState[currentGameIndex] = GamePlayState.Gaming;
                break;
        }
    }
    #endregion

    /// <summary>
    /// 找到第一个还没玩过的游戏，-1 即表示全玩过了
    /// </summary>
    /// <returns></returns>
    public int GetNotStartedGameFirstIndex()
    {
        for (int i = 0; i < gamePlayState.Length; i++)
        {
            if (gamePlayState[i] == GamePlayState.NotStarted)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// -1表示有错，1表示玩过了，0表示没玩过
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public int IsPlayed(int index)
    {
        if (index < 0 || index >= gamePlayState.Length) return -1;
        return gamePlayState[index] != GamePlayState.NotStarted ? 1 : 0;
    }

    public GamePlayState GetGamePlayState(int index)
    {
        if (index >= gamePlayState.Length) return GamePlayState.NotStarted;
        return gamePlayState[index];
    }

    /// <summary>
    /// 设置游戏是输了还是赢了
    /// </summary>
    /// <param name="isWin"></param>
    public void SetCurrentGamePlayState(bool isWin)
    {
        gamePlayState[currentGameIndex] = isWin ? GamePlayState.Win : GamePlayState.Lost;
    }

    public void ResetCurrentGamePlayState()
    {
        gamePlayState[currentGameIndex] = GamePlayState.NotStarted;
    }

    /// <summary>
    /// 是否全部游玩过了？
    /// </summary>
    /// <returns></returns>
    public bool IsPlayedAll()
    {
        foreach (var item in gamePlayState)
        {
            if (item == GamePlayState.NotStarted)
            {
                return false;
            }
        }

        return true;
    }

    public void ResetGamePlayState()
    {
        for (int i = 0; i < gamePlayState.Length; i++)
        {
            gamePlayState[i] = GamePlayState.NotStarted;
        }
    }
}


/// <summary>
/// 游戏游玩状态/结果
/// </summary>
public enum GamePlayState
{
    NotStarted,
    Lost,
    Win,
    Gaming,
}

public class GlobalGameData : IEventData
{
    public string dataName;
}

public class DifficultyData : GlobalGameData
{
    public int difficulty;
}

public class SelectedGameData : GlobalGameData
{
    public int index;
}