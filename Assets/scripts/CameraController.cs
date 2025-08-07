using UnityEngine;
using Cinemachine;

public class CameraController : MonoBehaviour
{
    [Header("Virtual Camera References")]
    public CinemachineVirtualCamera aimCamera;      // 瞄准视角虚拟相机
    public CinemachineVirtualCamera overheadCamera; // 全局视角虚拟相机

    [Header("Game References")]
    public CueController cueController; // 球杆控制器

    [Header("Camera Settings")]
    public float transitionSpeed = 2f;  // 摄像机切换速度
    public bool debugMode = true;       // 调试模式

    [Header("Manual Control Settings")]
    public KeyCode switchViewKey = KeyCode.Tab;     // 切换视角的按键
    public bool enableManualSwitch = true;          // 是否启用手动切换

    [Header("Overhead Camera Movement")]
    public float moveSpeed = 5f;                    // 全局相机移动速度
    public float rotationSpeed = 100f;              // 全局相机旋转速度
    public bool enableOverheadMovement = true;      // 是否启用全局相机移动
    public Vector3 movementBounds = new Vector3(10f, 5f, 10f); // 移动边界

    [Header("Auto Find Settings")]
    public float cueBallCheckInterval = 1f; // 检查母球的间隔时间

    // 私有变量
    private Transform cueBall;              // 母球Transform
    private bool cueBallFound = false;      // 是否找到母球
    private float lastCueBallCheck = 0f;    // 上次检查母球的时间
    private GameState lastGameState;        // 上次的游戏状态
    private bool manualControlMode = false; // 手动控制模式
    private Vector3 initialOverheadPosition; // 全局相机初始位置
    private Vector3 initialOverheadRotation; // 全局相机初始旋转
    private Transform overheadCameraTransform; // 全局相机的Transform

    void Start()
    {
        // 初始化摄像机设置
        InitializeCameras();

        // 记录全局相机初始位置
        if (overheadCamera != null)
        {
            overheadCameraTransform = overheadCamera.transform;
            initialOverheadPosition = overheadCameraTransform.position;
            initialOverheadRotation = overheadCameraTransform.eulerAngles;
        }

        // 尝试查找母球
        FindCueBall();

        // 默认激活全局视角
        SwitchToOverheadCamera();

        lastGameState = GameState.Waiting;
    }

    void InitializeCameras()
    {
        if (aimCamera == null || overheadCamera == null)
        {
            Debug.LogError("请在Inspector中设置虚拟相机引用!");
            return;
        }

        // 设置摄像机优先级，确保同一时间只有一个激活
        aimCamera.Priority = 0;
        overheadCamera.Priority = 10; // 默认激活全局视角

        if (debugMode)
        {
            Debug.Log("虚拟相机初始化完成");
        }
    }

    void FindCueBall()
    {
        // 查找场景中所有带有ball组件的对象
        ball[] ballComponents = FindObjectsOfType<ball>();

        foreach (ball ballComponent in ballComponents)
        {
            // 检查是否是母球
            if (ballComponent.IsCueBall || ballComponent.BallNumber == 0)
            {
                cueBall = ballComponent.transform;
                cueBallFound = true;

                // 设置瞄准相机的LookAt目标为母球
                if (aimCamera != null)
                {
                    aimCamera.LookAt = cueBall;

                    if (debugMode)
                    {
                        Debug.Log($"找到母球并设置为瞄准相机目标: {ballComponent.name}");
                    }
                }
                break;
            }
        }

        if (!cueBallFound && debugMode)
        {
            Debug.LogWarning("未找到母球，将在运行时继续查找");
        }
    }

    void Update()
    {
        // 定期检查母球是否存在（适用于运行时加载的情况）
        if (!cueBallFound || cueBall == null)
        {
            if (Time.time - lastCueBallCheck > cueBallCheckInterval)
            {
                lastCueBallCheck = Time.time;
                FindCueBall();
            }

            // 如果还没有找到母球，显示警告
            if (!cueBallFound && debugMode && Time.frameCount % 300 == 0)
            {
                Debug.LogWarning("等待母球加载以配置瞄准相机...");
            }
        }

        // 处理手动控制输入
        HandleManualControls();

        // 处理全局相机移动
        HandleOverheadCameraMovement();

        // 检查游戏状态变化并切换相机（只在非手动控制模式下）
        if (cueController != null && !manualControlMode)
        {
            GameState currentState = cueController.GetCurrentState();

            // 只有当状态发生变化时才切换相机
            if (currentState != lastGameState)
            {
                HandleGameStateChange(currentState);
                lastGameState = currentState;
            }
        }
    }

    void HandleManualControls()
    {
        if (!enableManualSwitch) return;

        // 检测切换视角按键
        if (Input.GetKeyDown(switchViewKey))
        {
            ToggleCamera();
        }

        // 检测进入瞄准模式的按键（空格键）
        // if (Input.GetKeyDown(KeyCode.Space))
        // {
        //     ToggleManualControlMode();
        // }

        // 重置相机位置按键（R键）
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetOverheadCamera();
        }
    }

    void HandleOverheadCameraMovement()
    {
        // 只有在全局视角激活且启用移动时才处理
        if (!enableOverheadMovement || overheadCamera.Priority <= aimCamera.Priority || overheadCameraTransform == null)
            return;

        Vector3 movement = Vector3.zero;
        Vector3 rotation = Vector3.zero;

        // WASD 移动
        if (Input.GetKey(KeyCode.W))
            movement += overheadCameraTransform.forward;
        if (Input.GetKey(KeyCode.S))
            movement -= overheadCameraTransform.forward;
        if (Input.GetKey(KeyCode.A))
            movement -= overheadCameraTransform.right;
        if (Input.GetKey(KeyCode.D))
            movement += overheadCameraTransform.right;

        // QE 上下移动
        if (Input.GetKey(KeyCode.Q))
            movement += Vector3.down;
        if (Input.GetKey(KeyCode.E))
            movement += Vector3.up;

        // 箭头键旋转
        if (Input.GetKey(KeyCode.LeftArrow))
            rotation.y -= rotationSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.RightArrow))
            rotation.y += rotationSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.UpArrow))
            rotation.x -= rotationSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.DownArrow))
            rotation.x += rotationSpeed * Time.deltaTime;

        // 应用移动
        if (movement != Vector3.zero)
        {
            Vector3 newPosition = overheadCameraTransform.position + movement.normalized * moveSpeed * Time.deltaTime;

            // 限制移动边界
            newPosition.x = Mathf.Clamp(newPosition.x,
                initialOverheadPosition.x - movementBounds.x,
                initialOverheadPosition.x + movementBounds.x);
            newPosition.y = Mathf.Clamp(newPosition.y,
                initialOverheadPosition.y - movementBounds.y,
                initialOverheadPosition.y + movementBounds.y);
            newPosition.z = Mathf.Clamp(newPosition.z,
                initialOverheadPosition.z - movementBounds.z,
                initialOverheadPosition.z + movementBounds.z);

            overheadCameraTransform.position = newPosition;
        }

        // 应用旋转
        if (rotation != Vector3.zero)
        {
            overheadCameraTransform.Rotate(rotation);
        }
    }

    void ToggleCamera()
    {
        if (aimCamera.Priority > overheadCamera.Priority)
        {
            SwitchToOverheadCamera();
        }
        else
        {
            SwitchToAimCamera();
        }
    }

    void ToggleManualControlMode()
    {
        if (!cueBallFound || cueController == null)
        {
            if (debugMode) Debug.LogWarning("无法切换到瞄准模式：母球未找到或球杆控制器未设置");
            return;
        }

        manualControlMode = !manualControlMode;

        if (manualControlMode)
        {
            // 强制设置球杆控制器为瞄准状态
            if (cueController.GetCurrentState() == GameState.Ready)
            {
                cueController.ResetToAiming();
            }
            SwitchToAimCamera();

            if (debugMode) Debug.Log("进入手动瞄准模式 - 按空格键退出");
        }
        else
        {
            // 退出手动控制模式，设置为Ready状态
            cueController.SetToReady();
            SwitchToOverheadCamera();
            if (debugMode) Debug.Log("退出手动瞄准模式");
        }
    }
    void ResetOverheadCamera()
    {
        if (overheadCameraTransform != null)
        {
            overheadCameraTransform.position = initialOverheadPosition;
            overheadCameraTransform.eulerAngles = initialOverheadRotation;

            if (debugMode) Debug.Log("全局相机位置已重置");
        }
    }
    void HandleGameStateChange(GameState newState)
    {
        switch (newState)
        {
            case GameState.Ready:
                // Ready状态：切换到全局视角
                SwitchToOverheadCamera();
                if (debugMode) Debug.Log("相机切换到全局视角 - 可以观察球桌全貌");
                break;
                
            case GameState.Aiming:
                // 瞄准状态：切换到瞄准视角
                SwitchToAimCamera();
                if (debugMode) Debug.Log("相机切换到瞄准视角 - 可以精确瞄准");
                break;
                
            case GameState.Charging:
                // 蓄力时保持瞄准视角
                SwitchToAimCamera();
                break;
                
            case GameState.Striking:
            case GameState.Waiting:
                // 击球和等待时切换到全局视角
                SwitchToOverheadCamera();
                break;
        }
    }

    void SwitchToAimCamera()
    {
        if (aimCamera == null) return;

        // 确保母球被正确设置为LookAt目标
        if (cueBall != null && aimCamera.LookAt != cueBall)
        {
            aimCamera.LookAt = cueBall;
        }

        // 设置优先级来激活瞄准相机
        aimCamera.Priority = 20;
        overheadCamera.Priority = 10;

        if (debugMode)
        {
            Debug.Log("切换到瞄准视角");
        }
    }

    void SwitchToOverheadCamera()
    {
        if (overheadCamera == null) return;

        // 设置优先级来激活全局相机
        overheadCamera.Priority = 20;
        aimCamera.Priority = 10;

        if (debugMode)
        {
            Debug.Log("切换到全局视角");
        }
    }

    // 手动切换相机的公共方法（可用于UI按钮等）
    public void ManualSwitchToAim()
    {
        SwitchToAimCamera();
    }

    public void ManualSwitchToOverhead()
    {
        SwitchToOverheadCamera();
    }

    // 强制重新查找母球（当球被重新放置时调用）
    public void RefreshCueBall()
    {
        cueBallFound = false;
        cueBall = null;
        FindCueBall();
    }

    // 获取当前激活的相机
    public CinemachineVirtualCamera GetActiveCamera()
    {
        if (aimCamera.Priority > overheadCamera.Priority)
            return aimCamera;
        else
            return overheadCamera;
    }

    // 检查母球是否已找到
    public bool IsCueBallFound()
    {
        return cueBallFound && cueBall != null;
    }

    // 检查是否在手动控制模式
    public bool IsManualControlMode()
    {
        return manualControlMode;
    }

    // 设置相机跟随目标（可选功能）
    public void SetCameraFollow(Transform target)
    {
        if (aimCamera != null)
        {
            aimCamera.Follow = target;
        }

        if (overheadCamera != null)
        {
            overheadCamera.Follow = target;
        }
    }

    // 在屏幕上显示控制说明
    // void OnGUI()
    // {
    //     if (!debugMode) return;
        
    //     GUIStyle style = new GUIStyle();
    //     style.fontSize = 16;
    //     style.normal.textColor = Color.white;
        
    //     string controlText = "=== 台球游戏控制说明 ===\n\n";
        
    //     // 根据当前状态显示不同的控制说明
    //     if (cueController != null)
    //     {
    //         GameState currentState = cueController.GetCurrentState();
    //         controlText += $"当前状态: {GetStateDisplayName(currentState)}\n\n";
            
    //         switch (currentState)
    //         {
    //             case GameState.Ready:
    //                 controlText += "🎯 准备阶段:\n";
    //                 controlText += "• Space - 趴下瞄准\n";
    //                 controlText += "• WASD/QE - 在球桌周围走动观察\n";
    //                 controlText += "• 箭头键 - 调整观察角度\n";
    //                 break;
                    
    //             case GameState.Aiming:
    //                 controlText += "🎯 瞄准阶段:\n";
    //                 controlText += "• 鼠标移动 - 调整瞄准方向\n";
    //                 controlText += "• 左键按住 - 开始蓄力击球\n";
    //                 controlText += "• Space - 起身观察 (回到全局视角)\n";
    //                 break;
                    
    //             case GameState.Charging:
    //                 controlText += "💪 蓄力阶段:\n";
    //                 controlText += "• 继续按住左键 - 增加力度\n";
    //                 controlText += "• 松开左键 - 击球\n";
    //                 controlText += "• Space - 取消蓄力\n";
    //                 break;
                    
    //             case GameState.Waiting:
    //                 controlText += "⏳ 等待球静止...\n";
    //                 break;
    //         }
    //     }
        
    //     controlText += "\n=== 相机控制 ===\n";
    //     controlText += "• Tab - 手动切换相机视角\n";
    //     controlText += "• R - 重置全局相机位置\n";
    //     controlText += $"\n相机模式: {(manualControlMode ? "手动控制" : "自动切换")}";
        
    //     GUI.Label(new Rect(10, 10, 400, 300), controlText, style);
    // }

    // string GetStateDisplayName(GameState state)
    // {
    //     switch (state)
    //     {
    //         case GameState.Ready: return "准备观察";
    //         case GameState.Aiming: return "瞄准中";
    //         case GameState.Charging: return "蓄力中"; 
    //         case GameState.Striking: return "击球中";
    //         case GameState.Waiting: return "等待球静止";
    //         default: return "未知状态";
    //     }
    // }
}