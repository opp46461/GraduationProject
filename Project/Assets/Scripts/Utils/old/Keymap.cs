using UnityEngine;

/// <summary>
/// Key configuration for the entire system
/// </summary>
public static class Keymap
{
    // Start: 1(1P), 2(2P)----------3(3P), 4(4P)
    // Pause: 5(1P), 6(2P)----------7(3P), 8(4P)
    // Direction: Up Down Left Right(1P), U V W X(2P)----------R F D G(3P), I K J L(4P)

    public static KeyCode SET = KeyCode.Alpha0;
    public static KeyCode ESC = KeyCode.Escape;
    public static KeyCode Show_Crosshair = KeyCode.Alpha9;

    //1P Keys
    public static KeyCode P1_START = KeyCode.Alpha1;
    public static KeyCode COIN1 = KeyCode.Alpha5;
    public static KeyCode P1_LEFT = KeyCode.LeftArrow;
    public static KeyCode P1_RIGHT = KeyCode.RightArrow;
    public static KeyCode P1_UP = KeyCode.UpArrow;
    public static KeyCode P1_DOWN = KeyCode.DownArrow;
    public static KeyCode P1_Reload = KeyCode.G;       // Box pedal interface, KeyCode.H
    //public static KeyCode P1_Bomb = KeyCode.G;       // Bomb

    //2P Keys
    public static KeyCode P2_START = KeyCode.Alpha2;
    public static KeyCode COIN2 = KeyCode.Alpha6;
    public static KeyCode P2_UP = KeyCode.U;
    public static KeyCode P2_DOWN = KeyCode.V;
    public static KeyCode P2_LEFT = KeyCode.W;
    public static KeyCode P2_RIGHT = KeyCode.X;
    public static KeyCode P2_Reload = KeyCode.I;       // Box pedal interface, KeyCode.R
    //public static KeyCode P2_Bomb = KeyCode.G;       // Bomb

    // Mouse Keys
    public static KeyCode Tigger = KeyCode.Mouse0;
    public static KeyCode Reload = KeyCode.Mouse1;
    public static KeyCode Middle = KeyCode.Mouse2;

    // Light Gun Key Map
    public static int Gun_Tigger = 0;     // Mouse Left Button--0
    public static int Gun_Reload = 14;    // Mouse Right Button--14
    public static int Gun_Mid = 8;        // Mouse Middle Button--8

    public static int Gun_Start = 3;      // 3
    public static int Gun_Coin = 1;       // 1
    public static int Gun_Up = 4;         // 4
    public static int Gun_Down = 5;       // 5
    public static int Gun_Left = 6;       // 6
    public static int Gun_Right = 7;      // 7
    public static int Gun_Bomb = 99;      // ???

    public static int ExitGame = 9001;
    public static int Setting = 9002;
}
