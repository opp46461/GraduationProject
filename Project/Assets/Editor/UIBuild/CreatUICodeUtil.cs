using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

public class CreatUICodeUtil
{
    //创建UICtrl文件的函数
    public static void CreatUICtrlFile(GameObject selectGameObject)
    {
        string gameObjectName = selectGameObject.name;
        string className = gameObjectName + "_UICtrl";
        StreamWriter sw = null;
        
        if (File.Exists(Application.dataPath + "/Scripts/Game/UI_controllers/" + className + ".cs")) return;

        sw = new StreamWriter(Application.dataPath + "/Scripts/Game/UI_controllers/" + className + ".cs");
        sw.WriteLine("using UnityEngine;\nusing System.Collections;\nusing UnityEngine.UI;\nusing System.Collections.Generic;\n");

        sw.WriteLine("public class " + className + " : UI_Ctrl ");
        sw.WriteLine("{");

        sw.WriteLine("\t" + "public override void Awake() ");
        sw.WriteLine("\t" + "{" + "\n");
        sw.WriteLine("\t\t" + "base.Awake();" + "\n");
        sw.WriteLine("\t" + "}" + "\n");

        sw.WriteLine("\t" + "void Start() ");
        sw.WriteLine("\t" + "{" + "\n");
        sw.WriteLine("\t" + "}" + "\n");
        sw.WriteLine("}");
        sw.Flush();
        sw.Close();

        Debug.Log("Gen: " + Application.dataPath + "/Scripts/Game/UI_Controllers/" + className + ".cs");
    }
}
