using AssetBundles;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 功能：GUILayout通用工具类
/// </summary>

public class GUILayoutUtils
{
    // 绘制垂直间距
    static public void DrawPadding()
    {
        GUILayout.Space(18f);
    }

    // 绘制属性行（标签+内容）
    static public void DrawProperty(string title, string content, float totalWidth = 250f, float titleWidth = 200f)
    {
        EditorGUILayout.BeginHorizontal(GUILayout.MinWidth(totalWidth));
        // 左侧标题
        EditorGUILayout.LabelField(title, GUILayout.MaxWidth(titleWidth));
        // 右侧内容
        EditorGUILayout.LabelField(content, GUILayout.MinWidth(totalWidth - titleWidth));
        EditorGUILayout.EndHorizontal();
    }

    // 绘制输入框行（标签+输入框）
    static public string DrawInputField(string title, string content, float totalWidth = 250f, float titleWidth = 200f)
    {
        EditorGUILayout.BeginHorizontal(GUILayout.MinWidth(totalWidth));
        // 左侧标题
        EditorGUILayout.LabelField(title, GUILayout.MaxWidth(titleWidth));
        // 文本输入框并返回修改后的内容
        content = EditorGUILayout.TextField(content, GUILayout.MinWidth(totalWidth - titleWidth));
        EditorGUILayout.EndHorizontal();
        return content;
    }

    // 绘制可折叠标题栏
    static public bool DrawHeader(string text, Dictionary<string, bool> states, string key, bool forceOn, bool minimalistic, params GUILayoutOption[] options)
    {
        bool state = forceOn;
        // 处理强制展开状态
        if (forceOn)
        {
            states[key] = forceOn;
        }
        else
        {
            // 从状态字典获取展开状态
            states.TryGetValue(key, out state);
        }

        // 非极简模式增加额外间距
        if (!minimalistic)
        {
            GUILayout.Space(3f);
        }

        // 未展开状态设置背景色
        if (!forceOn && !state)
        {
            GUI.backgroundColor = new Color(0.8f, 0.8f, 0.8f);
        }
        GUILayout.BeginHorizontal();
        GUI.changed = false;

        // 极简模式渲染
        if (minimalistic)
        {
            // 添加展开/折叠箭头图标
            if (state)
            {
                // ▼ + 窄空格
                text = "\u25BC" + (char)0x200a + text;
            }
            else
            {
                // ► + 窄空格
                text = "\u25BA" + (char)0x200a + text;
            }

            GUILayout.BeginHorizontal();
            // 设置专业版/个人版不同的文字颜色
            GUI.contentColor = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.7f) : new Color(0f, 0f, 0f, 0.7f);
            // 渲染折叠开关
            state = GUILayout.Toggle(state, text, "PreToolbar2", options);
            // 恢复默认文字颜色
            GUI.contentColor = Color.white;
            GUILayout.EndHorizontal();
        }
        // 完整模式渲染
        else
        {
            // 加粗和调整字体大小
            text = "<b><size=11>" + text + "</size></b>";
            // 添加展开/折叠箭头
            if (state)
            {
                text = "\u25BC " + text;
            }
            else
            {
                text = "\u25BA " + text;
            }
            // 渲染折叠开关
            state = GUILayout.Toggle(state, text, "dragtab", options);
        }

        // 状态变化时更新字典
        if (GUI.changed)
        {
            states[key] = state;
        }

        // 非极简模式增加间距
        if (!minimalistic) GUILayout.Space(2f);
        GUILayout.EndHorizontal();
        // 恢复背景色
        GUI.backgroundColor = Color.white;

        // 未展开状态增加额外间距
        if (!forceOn && !state) GUILayout.Space(3f);
        return state;
    }

    // 绘制带缩进的子标题
    static public bool DrawSubHeader(int level, string text, Dictionary<string, bool> states, string key, string subText)
    {
        // 根据是否有子文本调整布局
        if (string.IsNullOrEmpty(subText))
        {
            EditorGUILayout.BeginHorizontal();
        }
        else
        {
            EditorGUILayout.BeginHorizontal(GUILayout.MinWidth(250f));
        }
        // 根据层级添加缩进
        EditorGUILayout.LabelField("", GUILayout.MinWidth(20 * level), GUILayout.MaxWidth(20 * level));
        // 绘制标题
        var expanded = DrawHeader(text, states, key, false, true, GUILayout.MinWidth(200f));
        // 右侧显示子文本
        EditorGUILayout.LabelField(subText, GUILayout.MinWidth(50), GUILayout.MaxWidth(50));
        EditorGUILayout.EndHorizontal();
        return expanded;
    }

    // 绘制带删除按钮的子标题
    static public bool DrawRemovableSubHeader(int level, string text, Dictionary<string, bool> states, string key, Action callback)
    {
        EditorGUILayout.BeginHorizontal(GUILayout.MinWidth(250f));
        // 根据层级添加缩进
        EditorGUILayout.LabelField("", GUILayout.MinWidth(20 * level), GUILayout.MaxWidth(20 * level));
        // 绘制标题
        var expanded = DrawHeader(text, states, key, false, true, GUILayout.MinWidth(200f));
        // 添加删除按钮
        if (GUILayout.Button("X", GUILayout.MaxWidth(18), GUILayout.MaxHeight(18)))
        {
            // 执行删除回调
            callback();
            // 从状态字典移除
            states.Remove(key);
        }
        EditorGUILayout.EndHorizontal();
        return expanded;
    }

    // 开始内容区域（带样式）
    static public void BeginContents(bool minimalistic)
    {
        if (!minimalistic)
        {
            // 完整模式：带背景框
            GUILayout.BeginHorizontal();
            EditorGUILayout.BeginHorizontal("AS TextArea", GUILayout.MinHeight(10f));
        }
        else
        {
            // 极简模式：无背景框
            EditorGUILayout.BeginHorizontal(GUILayout.MinHeight(10f));
            GUILayout.Space(10f);
        }
        GUILayout.BeginVertical();
        // 垂直间距
        GUILayout.Space(2f);
    }

    // 结束内容区域
    static public void EndContents(bool minimalistic)
    {
        // 底部间距
        GUILayout.Space(3f);
        GUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        if (!minimalistic)
        {
            // 完整模式额外布局处理
            GUILayout.Space(3f);
            GUILayout.EndHorizontal();
        }

        // 外部间距
        GUILayout.Space(3f);
    }

    // 绘制文本列表内容
    static public void DrawTextListContent(List<string> list, string prefix = null)
    {
        // 开始内容区域（完整模式）
        BeginContents(false);
        for (int i = 0; i < list.Count; i++)
        {
            // 带前缀显示文本项
            if (!string.IsNullOrEmpty(prefix))
            {
                EditorGUILayout.LabelField(prefix + list[i], GUILayout.MinWidth(150f));
            }
            else
            {
                EditorGUILayout.LabelField(list[i], GUILayout.MinWidth(150f));
            }
        }
        // 结束内容区域
        EndContents(false);
    }

    // 绘制文本数组内容
    static public void DrawTextArrayContent(string[] array, string prefix = null)
    {
        // 开始内容区域（完整模式）
        BeginContents(false);
        for (int i = 0; i < array.Length; i++)
        {
            // 带前缀显示文本项
            if (!string.IsNullOrEmpty(prefix))
            {
                EditorGUILayout.LabelField(prefix + array[i], GUILayout.MinWidth(150f));
            }
            else
            {
                EditorGUILayout.LabelField(array[i], GUILayout.MinWidth(150f));
            }
        }
        // 结束内容区域
        EndContents(false);
    }
}