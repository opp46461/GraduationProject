//此行用于编码格式变更
public enum GeoffreyChuDIYEventList
{
    Test = 10000,
    UpdateCursorPos = 10001,
    OnHotPlug = 10002,
    // 新玩家加入
    NewPlayerJoin = 10003,
    // 后台射击按钮触发事件
    OnTrigger = 10004,
    OnUseCondition = 10005,
    // 显示玩家使用条件（需要投币/触发游玩）
    ShowUseCondition = 10006,
    // 玩家枪准心激活状态更新
    NPCursorActiveUpdate = 10007,
    // 常规倒计时提示
    CountDownTips = 10008,
    // 隐藏常规倒计时提示
    HideCountDownTips = 10009,
    // 点击到某个2D物体（目前用于点击UI事件，即用2D碰撞区别其他UI元素）
    OnTrigger2D = 10010,
    // 确认为玩家射击事件下放
    OnPlayerTrigger = 10011,
    // 全局游戏数据更新
    GlobalGameDataUpdate = 10012,
    // 进入游戏
    EnterGame = 10013,
    // 中间横屏提示
    MiddleTips = 10014,
    // 游戏回合倒计时提示
    GameCountDownTips = 10015,
    // 开始游戏
    StartGame = 10016,
    // 计费模式提示激活状态更新事件
    ActiveBillingModelTips = 10017,
    // 不同游戏需要在Gaming生成/显示不同UI元素
    ShowGamingUI = 10018,
    // 游戏中射击后的HUD显示
    ShowHUD = 10019,
    // 游戏中使用该事件驱动准心检测射击目标
    ShootingTarget = 10020,
    // ShootingTarget的结果，即射击后的结果（有没有击中，击中了啥）
    ShootingTargetResult = 10021,
    // 计分
    Score = 10022,
    // 连击了
    Combo = 10023,
    // 更新分数UI
    UpdateScoreUI = 10024,
    // 单个游戏结束事件
    StageOver = 10025,
    // 转场，该事件必定是每次函数最后才emit
    Transition = 10026,
    // 禁止所有交互
    ProhibitAllInteractions = 10027,
    // 记录耗时
    KeepElapsedTime = 10028,
    // 退出游玩
    ExitPlayGame = 10029,
    // 射击输入名称UI（目前只用于输入名称界面）
    OnTriggerInputNameUI = 10030,
    // 输入名称倒计时提示
    InputNameCountDownTips = 10031,
    // 激活光标
    ActiveCursor = 10032,
    // 有GO触碰到地板
    OnTriggerEnter3D_Ground = 10033,
    // 设置当前游戏玩家血量
    SetCurrentGamePlayerBloodVolume = 10034,
    // 加减 当前游戏玩家血量
    UpdateCurrentGameBloodVolumeData = 10035,
    // 全屏特效
    UIFullScreenEffect = 10036,
    // 更新成功鼠标位置更新事件后的回调
    SucceedUpdateCursorPos = 10037,
    // CurrentGameOver
    CurrentGameOver = 10038,
    // 补弹
    ReloadAmmunition = 10039,
    // 更新子弹数
    UpdateAmmunitionUI = 10040,
    // 计时器停止
    StopTimer = 10041,
    // 玩家没血了
    PlayerBloodZero = 10042,
    // 当前玩家游戏结束
    PlayerCurrentGameOver = 10043,
    // 加减星，即命
    UpdateCurrentGameLife = 10044,
    // 停止/启动服务器输入
    ActiveServerInput = 10045,
    // Game14谁打中了靶子
    RotatingShootingTargetWhoHit = 10046,
    // 设置Game15透视镜
    SetPerspectiveMirror = 10047,
}

